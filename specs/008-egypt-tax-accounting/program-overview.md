# DaftarX — شرح كامل للبرنامج

> منصّة محاسبة + امتثال ضريبي للسوق المصري (SMB / المكاتب المحاسبية).
> الإصدار الحالي: v1.2 "2025 Compliance Pack" — مغلَق في `2026-05-10`.

---

## 1. ما هو البرنامج؟

**DaftarX** نظام محاسبة ون-بريم (on-premise) مصمَّم خصيصاً لمتطلبات
مصلحة الضرائب المصرية. الهدف: المحاسب يفتحه أول يوم ويفصل
كل شيء بنفسه — مفيش consultant، مفيش setup wizard خاص، مفيش
Excel على الجنب.

**اللي بيميّزه عن المنافسين (Daftra / Wafeq / Edara / Odoo):**

1. **Penalty Shield** — لوحة بتقول للمحاسب بالظبط كم ج.م. غرامة
   متوقّعة عليه دلوقتي وكم ينفع يوفّر لو تصرّف اليوم.
2. **Pre-flight ETA Validator** — يحاكي 8 من فاحصات ETA محلياً قبل
   ما الفاتورة تتبعت → بيمنع ~80% من الـ rejections.
3. **مصمَّم للقانون 6 لسنة 2025** — النظام المبسّط (turnover tax + VAT
   ربع سنوي) متاح من اليوم الأول، مش feature في v3.0.
4. **شرح الأخطاء بالعربي** — `ETA Error Translator` بيترجم رسائل ETA
   التقنية إلى عمل تصحيحي واضح.
5. **Single-file portable EXE** — نسخة 65 ميجا، ضغطة دبل-كليك،
   شغّال في 30 ثانية بدون SQL ولا .NET install.

---

## 2. الجمهور المستهدف

| الفئة | الاحتياج | DaftarX يخدمها كذا |
|---|---|---|
| محاسب فرديّ مستقل | امتثال + تقارير + ETA | كل شيء في exe واحد |
| مكتب محاسبي صغير (3-10 محاسبين) | متعدد العملاء، صلاحيات | Firm Portal + role-based access |
| شركة تجارية صغيرة (≤ 15M ج.م./سنة) | فواتير + VAT + Form 41 | Law 6/2025 mode + Penalty Shield |
| شركة متوسطة (15M-100M ج.م.) | كل المعتاد + reverse-charge + WHT outbound | Standard regime + كل الميزات |

---

## 3. التكنولوجيا

| الطبقة | التقنية |
|---|---|
| Runtime | .NET 8 (LTS) |
| UI | Blazor Server (real-time SignalR) + Razor Pages للـ auth |
| ORM | EF Core 8 |
| DB (production) | SQL Server 2022 (Express ينفع) |
| DB (portable) | SQLite 3 (نفس الكود، provider مختلف) |
| Background jobs | Hangfire (SQL Server storage) أو Memory Storage |
| PDF rendering | QuestPDF (موقّع QR + ختم رقمي) |
| Cryptography | Argon2id لـ password hashing، SHA-256 لـ audit hash chain |
| MFA | TOTP (Otp.NET) |
| Logging | Serilog (console + rolling file + SQL Server sinks) |
| Localization | IStringLocalizer + .resx (ar-EG / en-US) |
| Containers | Docker compose (SQL + Web + seed) |
| Installers | WiX 5 MSI، Inno Setup wrapper، Single-file portable .exe |

---

## 4. الهيكل المعماري (Clean Architecture / DDD)

```
EgyptTax.Domain          ← Entities, Value Objects, Domain Events
   └── 53 .cs files

EgyptTax.SharedKernel    ← MoneyEgp, ArabicEnglishText, Language,
                            EgyptianTin, Clock abstraction

EgyptTax.Application     ← Use cases, ports (interfaces),
                            queries, payload builders

EgyptTax.Infrastructure  ← Adapters (EF, Hangfire jobs, mock ETA,
                            audit store, PDF renderer)

EgyptTax.Web             ← Blazor pages (53 .razor) + Razor Pages
                            (auth) + minimal APIs (health, verify,
                            eta-mock, audit verify, pdf, eInvoice)

EgyptTax.Bootstrapper    ← WiX bundle (Burn) glue
EgyptTax.Installer       ← WiX MSI source
```

**الـ dependency rule:** Domain لا يعرف أي شيء عن غيره. Application
يعرف Domain فقط. Infrastructure يعرف Domain + Application. Web
يعرف الكل.

---

## 5. الميزات بالتفصيل (Modules)

### 5.1 Identity & Auth (`/Pages/Auth`)

- **Login** — email + password (Argon2id hash)
- **MFA** — TOTP إلزامي للأدمن (FR-002)
- **Password change** — مفروض على أول دخول (FR-038)
- **Sessions** — server-side session row، slidable expiry، revocation عبر `SessionService`
- **Cookie auth** — `egtsess` HttpOnly + SameSite Lax
- **Auth policies** — `FullyAuthenticated` يحجب الـ pages الأساسية
- **Admin recovery CLI** — `EgyptTax.Web.exe recover-admin` لإعادة
  ضبط الباسورد لما المحاسب ينساه

### 5.2 Master Data (`/Pages/MasterData`, `/Pages/Settings`)

| الكيان | المسؤولية |
|---|---|
| `Company` (سجل واحد) | بيانات المُصدر + tax regime + fiscal year |
| `Customer` | مع `CustomerTaxProfile` (B2B Registered / B2B Unregistered / B2C) |
| `Supplier` | مع `SupplierTaxProfile` (Registered / Unregistered / Foreign) |
| `Item` | كود + اسم + VAT افتراضي + ETA item code lifecycle (P1.6) |
| `VatCategory` | Standard 14%، Reduced 5%، Zero-rated، Exempt + custom |
| `WhtCategory` | فئات الخصم (1%، 3%، 5%، إلخ) effective-dated |
| `DeductibleExpenseCategory` | فئات المصروفات + هل قابلة للخصم |
| `PaymentMethod` | نقدي، بنكي، إلخ |
| `ChartOfAccounts` | (constants حالياً، seed كامل في P3.1) |

### 5.3 Sales Invoice + ETA Submission (`/Pages/Invoices`)

**التدفّق:**
1. `SalesInvoiceEdit.razor` — operator يكتب الفاتورة
2. على Post:
   - `PostSalesInvoiceWithEtaSubmissionHandler` يحسب الـ totals، يولّد رقم الفاتورة، ينشئ JE، يولّد الـ eInvoice JSON
   - يعمل submit للـ `IEtaSubmitter` (Mock حالياً)
   - يسجل `EtaSubmission` row بحالة Pending → Submitted/Failed
3. `SalesInvoiceDetail.razor` يعرض:
   - الفاتورة + lines + totals
   - **Pre-flight Validator panel** (P1.7) — يحاكي 8 فاحصات ETA
   - **Tax Risk Score badge** (Differentiator 1)
   - **ETA Error Badge** (P1.4) — مترجَم لو فشل
   - **Correction Advisor** (P1.9) — cancel-vs-credit-note timeline
   - **ETA acknowledgement banner** (P1.3) — Pending/Acknowledged/Rejected
   - QR code seal (verifiable عبر `/api/v1/verify/{seal}`)

**Endpoints related:**
- `/invoices/{id}/pdf` — QuestPDF بيرندر فاتورة عربي/إنجليزي
- `/invoices/{id}/einvoice.json` — العرض النهائي اللي بيتبعت لـ ETA
- `/api/v1/verify/{seal}` — أي حد يقدر يتحقق من الـ QR

### 5.4 Purchase Invoice + Reverse-Charge (`/Pages/Purchases`)

- `PurchaseInvoice` بيشيل `SupplierTaxProfileSnapshot` (frozen at post)
- `PostPurchaseInvoiceHandler` بيفرض FR-016 (attachment + categorization)
- `PurchaseInvoiceJournalEmitter`:
  - **عادي** → DR Expense / DR Input VAT / CR AP
  - **Reverse-charge** (foreign supplier — P1.12) → DR Expense / DR Input VAT / **CR Output VAT (نفس قيمة Input)** / CR AP (subtotal فقط)
- Banner أصفر على detail page بيوضّح إن ETA متخطية + الإقرار الدوري هو القناة الصحيحة

### 5.5 Expenses + Payment Vouchers + Journal Vouchers

- `Expense` — مصروفات بدون مورد (نقل، إلخ)
- `SupplierPaymentVoucher` (SPV) — دفع للمورد، بيقدر ياخد WHT
- `CustomerReceiptVoucher` (CRV) — استلام من العميل، بيقدر يسجّل WHT inbound
- `PaymentAllocation` — ربط الـ voucher بـ invoices متعددة
- `JournalVoucher` — قيود يدوية + reversals (Admin/Accountant فقط — Bookkeeper مرفوض)

### 5.6 Reports (`/Pages/Reports`)

| تقرير | الـ FR | المسؤولية |
|---|---|---|
| Monthly VAT | FR-021 | Output VAT - Input VAT recoverable = Net Payable |
| Taxable Income | FR-023 | إجمالي الإيرادات - المصروفات القابلة للخصم |
| Trial Balance | FR-024 | كل حسابات الـ COA مع DR/CR cumulative |
| **Turnover Tax** (Day 8) | Law 6/2025 | تقدير ضريبة معدل الدوران 0.4-1.5% |

### 5.7 Withholding Tax / WHT (`/Pages/Wht`)

- `WhtDashboard` — لوحة (مُستحق / متوقّع / إقرارات سابقة)
- `WhtCategories` (في settings) — تعريف الفئات
- `Form41` quarterly — توليد الإقرار + sign بـ WhtCertificate IDs
- **Inbound WHT Import** (P1.14) — العميل خصم منك ضريبة وبعت شهادة:
  - تدخل البيانات يدوياً
  - `IInboundWhtMatcher` بيدور على فواتير مبيعات في نفس الفترة بـ implied withholding في حدود ±2%
  - تختار الفاتورة المطابقة → تسجّل WhtCertificate (Inbound)

### 5.8 Compliance (`/Pages/Compliance`)

| الصفحة | المسؤولية |
|---|---|
| **Penalty Shield** (P1.8) | لوحة لحظية للغرامات المتوقعة + قائمة العمل ذات الأولوية |
| **Compliance Calendar** (P2.6) | كل المواعيد النهائية (VAT شهري/ربعي، Form 41، ضريبة الدخل) auto-generated |
| **ETA Wizard** (P1.1) | 5 خطوات لإعداد ETA: TIN → eSeal → Activity → Environment → Test |
| **ETA Inbox** (P1.5) | فواتير المورّدين اللي وصلتك من ETA → استورد كـ Purchase Invoice |
| **ETA Dashboard** | عرض الـ submissions، فلترة بالحالة، deadlines قريبة |
| **ETA Export** (P1.10) | ZIP فيه كل الفواتير + signed JSONs لفترة محدّدة |
| **Certificates** (P1.2) | شهادات Windows cert store + expiry monitoring |
| **Audit Log** | عرض الـ append-only audit chain + verify endpoint |
| **Inspection Bundle** | حزمة كاملة للفحص الضريبي (PDFs + JSONs + checksums) |

### 5.9 Fixed Assets

- `FixedAsset` (FR-018) — كل أصل عنده cost + put-in-service date + depreciation method
- `MonthlyDepreciationJob` Hangfire — بينشئ JE شهري auto
- `FixedAssetSchedule` — جدول الإهلاك السنوي

### 5.10 Firm Portal (`/Pages/FirmPortal`)

- `CompanySwitcher` — للمكاتب اللي بتدير عميلين
- `AccountantReviewMode` — قفل فترة للمراجعة + توقيع إلكتروني
- `InviteAccountantFirmUser` — دعوة محاسب جديد للمكتب

---

## 6. Background Jobs (Hangfire)

| Job ID | الجدول | الوظيفة |
|---|---|---|
| `audit-checkpoint` | كل دقيقة | كتابة checkpoint للـ audit chain لو 1k entry أو 15 دقيقة |
| `compliance-calendar-refresh` | كل 5 دقائق | توليد الـ obligations للسنة الحالية والقادمة |
| `eta-item-code-check` | كل دقيقة | فحص حالة GS1/EGS code requests |
| `eta-received-inbox` | كل دقيقتين | سحب فواتير المورّدين من ETA |
| `eta-status-polling` | كل دقيقة | متابعة حالة الـ submissions الـ Submitted |
| `eta-submission-retry` | كل 15 دقيقة | إعادة محاولة الـ Failed ضمن نافذة الإرسال |
| `ntp-health-check` | كل 6 ساعات | فحص فرق الساعة مع NTP server |
| `supplier-tin-revalidation` | يومي 03:00 | revalidation للـ TINs مع registry |

> **ملاحظة:** الفترات في Production أبطأ بكثير (مثلاً eta-status-polling كل 15 دقيقة). الفترات السريعة هنا للـ demo.

---

## 7. الأمان والـ Audit Trail

### 7.1 Audit Chain (FR-028)

كل عملية مهمة بتُكتب في `audit_log` كـ event مع:
- `index` (monotonic)
- `prev_hash` (آخر hash)
- `this_hash = SHA-256(payload + prev_hash)`

النتيجة: chain غير قابل للتعديل بدون اكتشاف. الـ `verify-audit` CLI أو
الـ `/api/v1/audit/verify` endpoint بيفحص الـ chain بالكامل.

### 7.2 Tax Period Locks (FR-037)

`SqlTaxPeriodLockGuard` بيمنع أي post على فترة مقفولة. الـ guard
محقون في كل الـ post handlers.

### 7.3 Document State Machine

```
Draft → Submitted (للموافقة) → Approved (موقّع من مسؤول) → Posted
                              ↘ Rejected
                              
Posted → (immutable, تصحيح بـ credit-note فقط — P1.9)
```

### 7.4 Data Protection

- Passwords: Argon2id (3 iterations, 64MB memory, 4 parallelism)
- MFA secrets: encrypted with `IDataProtectionProvider` (DPAPI on Windows)
- Audit log: append-only، لا UPDATE ولا DELETE
- Sessions: server-side, revocable

---

## 8. خيارات النشر (Deployment)

### 8.1 Single-file Portable EXE (الأسرع)

```powershell
dotnet publish src/EgyptTax.Web/EgyptTax.Web.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o D:\daftarx-portable\build
```

**الناتج:** `EgyptTax.Web.exe` بحجم 65 MB. العميل ينزّله، يضغط دبل-كليك،
الـ console window بتطبع باسورد admin مولّدة عشوائياً + المتصفح بيفتح
لوحده.

**المزايا:**
- مفيش SQL install، مفيش .NET install
- DB ملف SQLite بيتعمل جنب الـ exe
- نسخة احتياطية = نسخ الـ folder
- `first-run-credentials.txt` بيتحفظ جنب الـ exe (recoverable)

**العيوب:**
- مستخدم واحد (SQLite بيقفل DB-level على writes)
- مفيش Windows Service (لازم console window يفضل مفتوح)

### 8.2 Docker Compose (التطوير + production متوسط الحجم)

```bash
cd c:/Users/ezzfa/speckit-co
docker compose up -d --build
```

**الـ stack فيه:**
- `daftarx-sql` — SQL Server 2022 Express (named volume للـ data)
- `daftarx-db-init` — one-shot يعمل DB الـ EgyptTax + EgyptTax_Hangfire
- `daftarx-seed` — one-shot migrations + admin seed
- `daftarx-web` — Blazor Server (port 8088)

**الـ data:** attachments + logs + inspection-bundles على `D:\daftarx-data\`
(bind mounts) — SQL data في named volume على ext4 (SQLPAL مش بيشتغل
كويس على bind mount NTFS).

**المتصفح:** http://localhost:8088

### 8.3 WiX 5 MSI (Production الفعلي — على VPS Windows)

`EgyptTax.Installer/Product.wxs` بيبني MSI كامل:
- Self-contained .NET publish (~190 MB)
- Custom action: Apply migrations + seed
- Custom action: Setup HTTPS (PowerShell بيولّد cert + يبني Kestrel)
- Windows Service installation (`EgyptTax`)
- Firewall rule (port 443)
- Workstation tools (trust cert script)

`installer-inno/daftarx-setup.iss` Inno Setup wrapper بيشغّل:
1. SQL Server Express install (لو مش موجود)
2. Custom wizard (admin email/password/internal domain)
3. الـ MSI

> ⚠️ في bug حالياً: الـ MSI بيرجع 1603 على بعض الـ VPS — جاري التشخيص.
> الـ portable EXE هو البديل الموصى به للوقت الحالي.

---

## 9. الحالة الحالية (2026-05-10)

### MVP — مغلَق ✅
كل الـ FR-001 إلى FR-052 + كل الـ Differentiators 1+2 + كل البنية
التحتية (Audit chain, Tax periods, Approval workflow, Firm portal).

### Wave 1 "Penalty Shield" v1.1 — مغلَق ✅
- P0.4 Smoke checklist
- P1.2 Cert monitor
- P1.4 ETA error translator
- P1.7 Pre-flight validator (الـ moat الأساسي)
- P1.8 Penalty exposure dashboard
- P1.9 Cancel/credit wizard
- P1.10 ETA bulk export

### Wave 2 "2025 Compliance Pack" v1.2 — مغلَق ✅
- P1.1 ETA Wizard
- P1.3 ETA Status Polling
- P1.5 Received supplier e-invoices
- P1.6 GS1/EGS coding tracker
- P1.12 Reverse-charge VAT
- P1.14 Inbound WHT certificate import
- Law 6/2025 mode
- P2.6 Compliance Calendar

### Wave 3 "Foundation" v1.3 — جاي ⏳
- Multi-cashbox/bank
- PDF statement import (CIB, NBE, QNB)
- B2C E-Receipt + minimal POS
- Cashier shifts + Z report
- Inventory lite
- Opening balances import

### Wave 4 "Firm Edition" v2.0 — جاي ⏳
- ETA reconciliation engine (الـ moat الكبير)
- PSP imports (Paymob, Fawry)
- Bank matching engine
- Payroll lite + Form 1 + payslips
- Multi-tenant pricing

---

## 10. الإحصائيات

| المقياس | العدد |
|---|---|
| .NET projects | 7 |
| Domain entities (.cs) | 53 |
| Razor pages | 53 |
| EF migrations | 34 |
| Hangfire recurring jobs | 8 |
| Domain modules | 11 (Accounting, Audit, Documents, Eta, Expenses, Identity, Invoices, MasterData, Numbering, Periods, Purchases, Tax, Workflow, Compliance) |
| Languages supported | 2 (ar-EG، en-US) |
| Tax regimes | 2 (Standard، Law 6/2025) |
| Deployment options | 3 (Portable EXE، Docker Compose، WiX MSI) |

---

## 11. أين تنظر في الكود

### إذا كنت تريد فهم...

| السؤال | ابدأ من |
|---|---|
| ما هو الـ contract اللي ETA يطلبه؟ | `specs/008-egypt-tax-accounting/contracts/api/openapi.yaml` |
| كيف يحسب الـ Penalty Shield؟ | `src/EgyptTax.Application/Compliance/PenaltyShield/` |
| كيف يولّد الـ JE من فاتورة؟ | `src/EgyptTax.Infrastructure/Accounting/SalesInvoiceJournalEmitter.cs` |
| كيف يعمل الـ Pre-flight validator؟ | `src/EgyptTax.Application/Compliance/PreFlight/PreFlightValidator.cs` |
| كيف يبدأ المتصفح أوتوماتيك في الـ portable mode؟ | `src/EgyptTax.Web/Tools/PortableFirstRun.cs` |
| ما هي الـ approval workflow rules؟ | `src/EgyptTax.Domain/Workflow/ApprovalRequest.cs` |
| كيف يتم signing للـ audit chain؟ | `src/EgyptTax.Domain/Audit/AuditChainHasher.cs` |
| كيف يتم تحويل الـ schemas لـ SQLite؟ | `src/EgyptTax.Infrastructure/Persistence/AppDbContext.cs` (`OnModelCreating`) |

### بنية المشروع

```
.
├── .specify/                       ← Spec Kit metadata + constitution
├── specs/008-egypt-tax-accounting/ ← Spec, plan, research, contracts, roadmap
├── src/
│   ├── EgyptTax.Domain/            ← Entities, value objects, domain logic
│   ├── EgyptTax.SharedKernel/      ← Money, ArabicEnglishText, Clock
│   ├── EgyptTax.Application/       ← Use cases, ports, queries
│   ├── EgyptTax.Infrastructure/    ← EF, jobs, mocks, PDF, audit
│   ├── EgyptTax.Web/               ← Blazor + Razor Pages + APIs
│   ├── EgyptTax.Bootstrapper/      ← WiX bundle
│   └── EgyptTax.Installer/         ← WiX MSI
├── tests/                          ← Unit + integration + Playwright
├── installer-inno/                 ← Inno Setup wizard
├── Dockerfile                      ← Multi-stage Linux build
└── docker-compose.yml              ← Dev stack (SQL + web)
```

---

## 12. أسئلة شائعة

**س: هل البرنامج جاهز للاستخدام عند العميل؟**
ج: الـ portable EXE نعم — مع الانتباه إلى أن MFA معطَّل في الـ portable
mode للسهولة. للـ deployment الجدّي على VPS الـ MSI يحتاج fix للـ 1603.

**س: هل يدعم العملة غير ج.م.؟**
ج: لا. كل الـ MoneyEgp قيمها بالـ EGP. التوسّع لعملات أخرى مؤجَّل
لـ Wave 5.

**س: هل يتكامل مع real ETA APIs أم mock؟**
ج: جميع الـ ETA integrations حالياً Mock-first behind clean interfaces.
لما تظهر credentials production، تغيير سطر تسجيل DI واحد لكل integration
وكل الـ caller code يبقى زي ما هو.

**س: هل يدعم multi-tenancy؟**
ج: حالياً single-tenant (شركة واحدة per install). Multi-tenant يجي مع
Wave 4 "Firm Edition" v2.0.

**س: هل يخزّن البيانات في cloud؟**
ج: لا. كل البيانات local — DB + attachments + logs. ده by design
(الـ on-prem trust narrative في السوق المصري).

**س: ماذا لو ETA غيّرت rules؟**
ج: الـ rule constants في كود واحد (مثلاً `PenaltyRegime`،
`TurnoverTaxBracketCalculator`، `EtaErrorTranslator` catalog).
update واحد + republish.

---

## 13. الـ Constitution

البرنامج بيلتزم بـ 5 مبادئ من `.specify/memory/constitution.md`:

1. **Spec-First Development** (NON-NEGOTIABLE) — كل feature يبدأ بـ spec
2. **Plan Before Code** — design artifacts قبل الـ implementation
3. **Test-First Discipline** (NON-NEGOTIABLE) — TDD حيثما أمكن
4. **Simplicity & YAGNI** — مفيش over-engineering
5. **Incremental, Independently Testable Delivery** — كل push شغّال لوحده

---

> **آخر تحديث:** 2026-05-10
> **الإصدار:** v1.2 "2025 Compliance Pack"
> **المسؤول:** فريق DaftarX
