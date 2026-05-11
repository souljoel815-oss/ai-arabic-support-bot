# Gux.13 — Company Admin Panel + Edition System

> **Module ID:** Gux.13 (alias G1.6 — sequenced before any customer can purchase)
>
> **Priority:** P0 — without this, the company admin has no centralized control
> surface. Settings are scattered across 9+ sidebar items with no role-based
> gating.
>
> **Complexity:** L (~6 days AI-paired, including the new edition system)
>
> **Dependencies:** Gux.1 (collapsible sidebar — shipped), Gux.2 (setup items
> moved to Settings — shipped), Gux.3 (Settings tab strip — shipped),
> existing `Company` + `TaxPeriod` + `EtaCredential` entities.
>
> **Pricing — resolved 2026-05-11:** the 4-edition system in §7 was
> adopted; [`pricing.md`](pricing.md) was rewritten to match (Solo
> 3,500 / SMB 8,000 / Enterprise 17,500 / Firm 30,000 EGP/year). See
> the change log entry at the bottom of `pricing.md` for the rationale.
> Implementation can start.

---

## 1. Problem Statement

DaftarX currently has **9 separate settings pages** visible in the sidebar to
every user (post-Gux.3, two of those clusters share a tab strip but the
sidebar still shows multiple top-level Settings entries). There is no unified
admin panel where the company owner can manage all company-level
configuration from one place.

Three problems:

1. **No single source of truth.** Company data, tax config, ETA credentials,
   payment methods, and user management are spread across unrelated pages.
   The admin has to hunt for each one.
2. **No role-based access.** Every user who logs in sees every settings
   page. A cashier who should only create invoices can access ETA
   credentials, chart of accounts, and opening balances.
3. **No onboarding flow.** A new customer installs DaftarX and lands on a
   dashboard with zero data. There is no guided setup that walks them
   through company profile → tax regime → ETA credentials → first invoice.
   Drop-off risk during the 14-day trial is high.

---

## 2. Target User

The **Company Administrator** — typically the business owner or the head
accountant. This person:

- Purchased the license (or activated the trial).
- Is responsible for ETA compliance.
- Manages who else can access the system.
- Sets up the company's tax regime, bank accounts, and invoice templates.
- Is often 40-60 years old, not tech-savvy, and thinks in Arabic.

---

## 3. Admin Panel Structure — 10 Tabs

Single page at `/settings` with a horizontal tab bar (desktop) or vertical
accordion (mobile). The admin sees all 10 tabs; regular users see only the
tabs their role grants (see §5).

### Tab 1: بيانات الشركة (Company Profile)

| Field | Type | Notes |
|-------|------|-------|
| اسم الشركة (Company name AR) | text | Required. Used on invoices + ETA |
| Company name EN | text | Optional. Used on bilingual invoices |
| الرقم الضريبي (Tax ID) | text | 9-digit, validated |
| رقم السجل التجاري (Commercial reg.) | text | Optional |
| العنوان (Address) | text | Street, city, governorate |
| الهاتف (Phone) | text | Used on invoices |
| البريد الإلكتروني (Email) | email | Company email |
| لوجو الشركة (Logo) | file upload | PNG/JPG, max 2MB. Shown on invoices |
| السنة المالية (Fiscal year start) | month picker | Default: January |

**Behaviour:** Pre-filled from the existing `Company` entity. Save button at
the bottom. Changes propagate to all future invoices immediately.

---

### Tab 2: الإعدادات الضريبية (Tax Configuration)

| Field | Type | Notes |
|-------|------|-------|
| النظام الضريبي (Tax regime) | radio | Standard (VAT + income tax) OR Simplified (Law 6/2025 turnover tax) |
| فئات ضريبة القيمة المضافة (VAT categories) | table | Pre-seeded with Egyptian rates: 14%, 0%, exempt. Admin can add custom |
| ضريبة الخصم من المنبع (WHT rates) | table | Pre-seeded with standard rates per activity code |
| الفترة الضريبية (Tax period) | select | Monthly / Quarterly (Law 6 only) |
| تاريخ بداية أول فترة (First period start) | date | Auto-calculated from fiscal year |

**Behaviour:** Changing the tax regime triggers a confirmation dialog: "هل
أنت متأكد؟ سيؤثر هذا على جميع الفواتير المستقبلية." Existing posted
invoices are never modified.

---

### Tab 3: ربط منظومة ETA (ETA Integration)

| Field | Type | Notes |
|-------|------|-------|
| البيئة (Environment) | toggle | Sandbox / Production |
| Client ID | text | From ETA portal |
| Client Secret | password | Masked, show/hide toggle |
| Token PIN | password | For USB token signing |
| حالة الاتصال (Connection status) | indicator | Green (connected) / Orange (token offline) / Red (auth failed) / Grey (not configured) |
| آخر اتصال ناجح (Last successful connection) | timestamp | Auto-updated |
| زر اختبار الاتصال (Test Connection) | button | Calls ETA auth endpoint, shows result |

**Behaviour:**
- Test Connection sends a real **OAuth authentication** request to ETA
  sandbox/production and shows success/failure with the exact error message
  in Arabic. **Note:** this tests Client ID + Secret authentication only —
  USB token signing is tested at invoice submission time, not here.
- Toggle between Sandbox and Production requires confirmation: "التبديل
  للبيئة الحقيقية يعني أن الفواتير سترسل لمصلحة الضرائب فعلياً."
- If the USB token is not connected, the status indicator should show an
  **orange** "Token غير متصل" state (distinct from the red auth-failure
  state).
- The existing ETA Wizard (`/eta-wizard`) becomes a first-time setup flow
  that redirects here after completion.

---

### Tab 4: إعدادات الفواتير (Invoice Settings)

| Field | Type | Notes |
|-------|------|-------|
| قالب الفاتورة (Invoice template) | select + preview | 3 built-in templates: Classic, Modern, Compact |
| لغة الفاتورة (Invoice language) | select | Arabic only / Arabic + English (bilingual) |
| بادئة رقم الفاتورة (Invoice prefix) | text | e.g., "INV-" or "فت-" |
| الترقيم التالي (Next number) | number | Auto-incremented, admin can override (see validation below) |
| شروط الدفع الافتراضية (Default payment terms) | select | Immediate / 15 days / 30 days / 60 days / Custom |
| ملاحظات أسفل الفاتورة (Footer notes) | textarea | Appears on every invoice. e.g., bank account for transfer |
| إظهار QR code | toggle | QR code with ETA verification URL |
| إظهار اللوجو | toggle | Show/hide company logo on invoice |

**Behaviour:** Live preview panel on the right side (desktop) shows a
sample invoice updating in real-time as the admin changes settings.

**Next Number Override Validation:**
- The new number **must be greater than** the highest invoice number already
  used (posted or draft). Attempting to set a lower number shows: "لا يمكن
  استخدام رقم أقل من آخر فاتورة (رقم X). اختر رقماً أكبر."
- If the new number creates a gap (e.g., jumping from 150 to 200), show a
  warning: "تغيير الترقيم سيخلق فجوة في أرقام الفواتير (من X إلى Y). هل
  أنت متأكد؟"
- **Period-lock guard (FR-037):** if the tax period containing the most
  recent invoice is **Locked**, the Next Number field is read-only. Show:
  "لا يمكن تغيير الترقيم — الفترة الضريبية الحالية مقفولة." The admin must
  unlock the period first (which itself requires confirmation).
- Every change to the next number is recorded in the audit log with the old
  and new values.

---

### Tab 5: البريد الإلكتروني (Email Settings)

| Setting | Type | Notes |
|---------|------|-------|
| طريقة الإرسال (Send method) | radio | **Option A:** Open in mail client (Outlook/Thunderbird) — default, zero config. **Option B:** Send directly from DaftarX (SMTP) |
| **SMTP Settings (Option B only):** | | |
| SMTP Server | text | Pre-fill suggestions: smtp.gmail.com / smtp.office365.com / smtp.yahoo.com |
| Port | number | Default 587 (TLS) |
| اسم المستخدم (Username) | text | Usually the email address |
| كلمة المرور (Password) | password | App password for Gmail |
| البريد المرسل منه (From address) | email | Company email |
| اسم المرسل (From name) | text | Company name |
| زر اختبار (Test) | button | Sends a test email to the admin's address |

**Behaviour:**
- Option A (default): No configuration needed. When user clicks "Send by
  email" on any invoice, DaftarX generates the PDF, opens the default mail
  client via MAPI with To/Subject/Body pre-filled and PDF attached.
- Option B: DaftarX sends the email directly. Requires one-time SMTP setup.
  Pre-fill buttons for Gmail/Outlook/Yahoo auto-populate server + port.
- Test Email button sends a real test email and shows success/failure.

---

### Tab 6: إدارة المستخدمين (User Management)

| Column | Notes |
|--------|-------|
| الاسم (Name) | Display name |
| البريد (Email) | Login credential |
| الدور (Role) | Admin / Accountant / Cashier / View-only |
| الحالة (Status) | Active / Disabled |
| آخر دخول (Last login) | Timestamp |
| إجراءات (Actions) | Edit / Disable / Reset password / Delete |

**Role permissions matrix:**

| Permission | Admin | Accountant | Cashier | View-only |
|-----------|:-----:|:----------:|:-------:|:---------:|
| All settings | ✓ | ✗ | ✗ | ✗ |
| Create/edit invoices | ✓ | ✓ | ✓ | ✗ |
| Post to ETA | ✓ | ✓ | ✓ | ✗ |
| View reports | ✓ | ✓ | ✗ | ✓ |
| Manage users | ✓ | ✗ | ✗ | ✗ |
| Manage customers/suppliers | ✓ | ✓ | ✓ | ✗ |
| View audit log | ✓ | ✓ | ✗ | ✓ |
| Backup/restore | ✓ | ✗ | ✗ | ✗ |
| Approve pending items | ✓ | ✓ | ✗ | ✗ |

**Behaviour:**
- "Add User" button opens a form. Admin sets name, email, temporary
  password, and role.
- Only available on **SMB** and higher editions (see §7). **Solo** edition
  shows: "خطة فردي تدعم مستخدم واحد. ترقية لخطة أعمال صغيرة لإضافة
  مستخدمين."
- Disabling a user immediately logs them out.

---

### Tab 7: الترخيص (License)

| Field | Notes |
|-------|-------|
| حالة الترخيص (License status) | Active / Trial (X days remaining) / Expired |
| نوع الخطة (Plan) | One of the editions defined in §7 |
| تاريخ الانتهاء (Expiry date) | Date |
| معرف الجهاز (Device ID) | Hardware fingerprint (read-only) |
| مفتاح الترخيص (License key) | Masked, copy button |
| زر التجديد (Renew) | Opens renewal flow or contact sales |
| زر التفعيل (Activate) | For entering a new license key |
| سجل التراخيص (License history) | Table: date, action (activated/renewed/expired), key |

**Behaviour:**
- Trial mode: Shows a yellow banner "متبقي X يوم من الفترة التجريبية" with
  a "Buy Now" CTA.
- Expired: Shows a red banner. All features locked except viewing existing
  data and this settings page.
- Activate button: Paste license key → validate → show success/failure
  with clear Arabic message.
- Renew button: Opens the self-service portal (G1.5) or shows InstaPay
  instructions (pre-G1.5).

---

### Tab 8: النسخ الاحتياطي (Backup)

| Setting | Notes |
|---------|-------|
| النسخ التلقائي (Auto-backup) | Toggle on/off |
| التكرار (Frequency) | Daily / Weekly / On every closing |
| مكان الحفظ (Save location) | Folder picker — default: `%USERPROFILE%\DaftarX\Backups\` |
| آخر نسخة (Last backup) | Timestamp + file size |
| زر نسخ الآن (Backup Now) | Manual trigger |
| زر استعادة (Restore) | File picker → confirmation → restore |
| سجل النسخ (Backup history) | Table: date, size, status (success/failed) |

**Behaviour:**
- Auto-backup creates a compressed `.dxbak` file (renamed ZIP). The backup
  strategy is **provider-aware**:
  - **SQL Server Express:** runs `BACKUP DATABASE ... TO DISK` (T-SQL) to
    produce a `.bak` file, then zips it with the attachments folder.
  - **SQLite (SQLCipher):** copies the encrypted `.db` file directly (using
    the SQLite Online Backup API to avoid locking), then zips it with the
    attachments folder.
  - The `.dxbak` file includes a `manifest.json` indicating which provider
    was used, so Restore knows which path to take.
- Backup Now shows a progress bar and confirms with file path.
- Restore requires double confirmation: "هذا سيستبدل جميع البيانات
  الحالية. هل أنت متأكد؟" → "اكتب 'استعادة' للتأكيد."
- **Cross-provider restore is not supported.** If the manifest says
  SQL Server but the current instance runs SQLite (or vice versa), show:
  "هذه النسخة الاحتياطية من نوع مختلف (SQL Server). لا يمكن استعادتها
  على هذا الجهاز (SQLite). استخدم نفس نوع قاعدة البيانات."
- Keep last 30 backups by default. Older ones auto-deleted (configurable).
- Cloud backup (G4.2) will add a third option in Save location: "Cloud
  (encrypted)".

---

### Tab 9: الإشعارات (Notifications)

| Setting | Notes |
|---------|-------|
| تنبيه المواعيد الضريبية (Tax deadline alerts) | Toggle + days before (default: 7, 3, 1) |
| تنبيه انتهاء الترخيص (License expiry alert) | Toggle + days before (default: 30, 7, 1) |
| تنبيه فشل إرسال ETA (ETA submission failure) | Toggle (default: on) |
| تنبيه اعتمادات معلقة (Pending approvals) | Toggle (default: on) |
| تنبيه النسخ الاحتياطي (Backup reminder) | Toggle + if no backup in X days (default: 7) |
| تنبيه انتهاء شهادة ETA (ETA certificate expiry) | Toggle + days before (default: 30, 7) |
| طريقة التنبيه (Notification method) | In-app (always) + Email (optional, requires Tab 5 SMTP) |

**Behaviour:**
- In-app notifications appear in the bell icon (Gux.10 — shipped).
- Email notifications only work if SMTP is configured in Tab 5.
- All toggles default to ON for new installations.
- The ETA certificate expiry alert checks the USB token certificate's
  NotAfter date (if available) and warns before it expires.

---

### Tab 10: حول ومساعدة (About & Support)

| Item | Notes |
|------|-------|
| إصدار البرنامج (App version) | e.g., 1.2.0 |
| إصدار قاعدة البيانات (DB version) | Schema version |
| معلومات النظام (System info) | OS, .NET version, DB provider (SQL Server Express / SQLite), DB version |
| زر تقرير التشخيص (Diagnostic Report) | Generates the existing DIAGNOSTIC-REPORT and saves/copies |
| زر التحقق من التحديثات (Check for Updates) | Calls `latest.json` (G4.1) |
| زر تصدير البيانات (Export Data) | Exports all company data (invoices, customers, suppliers, reports) as a ZIP file (PDPL 151/2020 compliance) |
| رابط دليل المستخدم (User Guide) | Opens USER-GUIDE in browser |
| رابط التواصل (Contact Support) | WhatsApp link + email |
| الرخصة القانونية (Legal) | EULA + third-party licenses |
| إعادة تشغيل معالج الإعداد (Re-run Setup Wizard) | Link to re-launch the first-time wizard |

**Behaviour:** Visible to all users (not admin-only). The diagnostic
report button is critical for support — when a customer reports a bug,
support says "اضغط على تقرير التشخيص وابعتلنا الملف."

The "System info" field should show the **actual DB provider** detected at
runtime (e.g., "SQL Server Express 16.0" or "SQLite 3.46 (SQLCipher)") —
not a hardcoded string.

---

## 4. First-Time Setup Wizard (Onboarding)

When DaftarX launches for the first time (fresh install or trial
activation), instead of dropping the user on an empty dashboard, a 4-step
wizard appears:

| Step | Tab it configures | What the user does |
|------|-------------------|-------------------|
| 1. مرحباً بك في DaftarX | Tab 1 | Enter company name, tax ID, logo |
| 2. النظام الضريبي | Tab 2 | Choose Standard or Simplified (Law 6). Pre-fill VAT categories |
| 3. ربط ETA | Tab 3 | Enter Client ID + Secret + PIN. Test connection. Option to skip ("أربط لاحقاً") |
| 4. أول فاتورة | — | Guided walkthrough: create one sample invoice (marked as draft, not posted) |

**Behaviour:**
- Each step has a "التالي" (Next) and "رجوع" (Back) button.
- Step 3 has a Skip option — many users won't have ETA credentials on day one.
- Step 4 creates a real draft invoice that the user can delete later. The
  point is to show them the workflow.
- After completion: "تم الإعداد! يمكنك تعديل أي إعداد لاحقاً من صفحة
  الإعدادات."
- The wizard only appears once. A "Run setup wizard again" link exists in
  Tab 10 (About).

---

## 5. Access Control

| Tab | Admin | Accountant | Cashier | View-only |
|-----|:-----:|:----------:|:-------:|:---------:|
| 1. Company Profile | ✓ edit | 👁 view | ✗ | ✗ |
| 2. Tax Config | ✓ edit | 👁 view | ✗ | ✗ |
| 3. ETA Integration | ✓ edit | ✗ | ✗ | ✗ |
| 4. Invoice Settings | ✓ edit | ✓ edit | ✗ | ✗ |
| 5. Email Settings | ✓ edit | ✗ | ✗ | ✗ |
| 6. User Management | ✓ edit | ✗ | ✗ | ✗ |
| 7. License | ✓ edit | 👁 view | ✗ | ✗ |
| 8. Backup | ✓ edit | ✗ | ✗ | ✗ |
| 9. Notifications | ✓ edit | ✓ edit (own) | ✓ edit (own) | ✗ |
| 10. About & Support | 👁 view | 👁 view | 👁 view | 👁 view |

Non-admin users who navigate to `/settings` see only the tabs they have
access to. Tabs they cannot see do not appear in the tab bar. **Server-side
enforcement is mandatory** — UI hiding is not enough.

---

## 6. Technical Implementation Notes

### Database provider awareness

DaftarX is **provider-aware** — the same binary runs against SQL Server
Express (MSI install) or SQLite/SQLCipher (portable/Docker/dev). The admin
panel must respect this:

| Context | DB | Detection |
|---------|-----|-----------|
| MSI install on Windows | SQL Server Express (`.\SQLEXPRESS`) | Connection-string shape detection in `Program.cs:545` |
| Portable / Docker / dev | SQLite (SQLCipher-encrypted) | Fallback to `%LOCALAPPDATA%\DaftarX\daftarx.db` |

Key implications for Gux.13:

- **Tab 8 (Backup):** must use provider-specific backup strategy (see
  Tab 8 spec above).
- **Tab 10 (About):** must show the actual detected provider, not a
  hardcoded string.
- **New entities** (`InvoiceSettings`, `SmtpSettings`, etc.) must work with
  both providers — use `AppDbContext.OnModelCreating` patterns already
  established (strip SQL Server annotations when SQLite detected).

### Existing entities to reuse

| Entity | Location | Maps to Tab |
|--------|----------|-------------|
| `Company` | Domain.MasterData | Tab 1 |
| `TaxPeriod` + `VatCategory` | Domain | Tab 2 |
| `EtaCredential` | Domain/Eta | Tab 3 |
| `InvoiceSettings` (new) | Domain | Tab 4 |
| `SmtpSettings` (new) | Domain | Tab 5 |
| `User` + roles | Identity | Tab 6 |
| `LicensePayload` | Web/Licensing | Tab 7 |
| `BackupConfig` (new) | Infrastructure | Tab 8 |
| `NotificationPrefs` (new) | Domain | Tab 9 |

### New entities required

```
InvoiceSettings
├── TemplateId (string)
├── Language (enum: ArabicOnly, Bilingual)
├── Prefix (string)
├── NextNumber (int)
├── DefaultPaymentTermsDays (int)
├── FooterNotes (string)
├── ShowQrCode (bool)
└── ShowLogo (bool)

SmtpSettings
├── SendMethod (enum: MailClient, DirectSmtp)
├── Server (string)
├── Port (int)
├── Username (string)
├── EncryptedPassword (string)  -- DataProtection-encrypted at rest
├── FromAddress (string)
├── FromName (string)
└── UseTls (bool)

BackupConfig
├── AutoBackupEnabled (bool)
├── Frequency (enum: Daily, Weekly, OnClosing)
├── SavePath (string)
└── RetentionCount (int, default 30)

NotificationPrefs
├── TaxDeadlineEnabled (bool)
├── TaxDeadlineDaysBefore (int[])
├── LicenseExpiryEnabled (bool)
├── LicenseExpiryDaysBefore (int[])
├── EtaFailureEnabled (bool)
├── PendingApprovalsEnabled (bool)
├── BackupReminderEnabled (bool)
├── BackupReminderDays (int)
├── EtaCertExpiryEnabled (bool)       -- v2: added per Manus review
├── EtaCertExpiryDaysBefore (int[])   -- v2: added per Manus review
└── EmailNotificationsEnabled (bool)
```

### Data migration for existing installs

When upgrading from a pre-Gux.13 version:

1. Existing `Company`, `TaxPeriod`, `EtaCredential` data must appear in
   Tabs 1-3 without re-entry.
2. New entities (`InvoiceSettings`, `SmtpSettings`, `BackupConfig`,
   `NotificationPrefs`) are created with sensible defaults on first access
   if they don't exist yet (lazy initialization pattern).
3. Old routes (`/settings/company`, `/settings/tax-periods`, etc.) redirect
   to `/settings` with the correct tab pre-selected via query parameter
   (e.g., `/settings?tab=company-profile`).

### UI approach

- **Desktop:** Single page with horizontal tab bar at the top. Selected
  tab content below. No page navigation — tab switching is instant (all
  tabs loaded, CSS-hidden).
- **Mobile:** Vertical accordion. Each tab title is a collapsible header.
  Only one open at a time.
- **Framework:** Blazor component `AdminPanel.razor` with child
  components per tab: `CompanyProfileTab.razor`, `TaxConfigTab.razor`, etc.
- **Route:** `/settings` (replaces the current scattered settings pages).
  Old routes (`/settings/company`, `/settings/tax-periods`, etc.) redirect
  to `/settings` with the correct tab pre-selected.

---

## 7. Product Editions (إصدارات البرنامج)

> **Locked pricing:** the 4-edition system below was adopted on
> 2026-05-11. Final prices in [`pricing.md`](pricing.md):
> Solo 3,500 / SMB 8,000 / Enterprise 17,500 / Firm 30,000 EGP/year.

DaftarX ships as a single installer, but the license key unlocks one of
four editions. The edition determines which features are available, how
many users can log in, and how many companies the install can manage. The
Admin Panel (Tab 7 — License) shows the current edition and offers an
upgrade path.

### 7.1 Edition Overview

| | فردي (Solo) | أعمال صغيرة (SMB) | مؤسسات (Enterprise) | مكاتب محاسبة (Firm) |
|---|---|---|---|---|
| **Target** | فريلانسر، محل صغير، طبيب، مهندس حر | شركة 2-15 موظف، تاجر، مقاول | شركة 15+ موظف، مصنع، سلسلة فروع | مكتب محاسبة يدير 10-100 عميل |
| **Users** | 1 | 3 | Unlimited | Unlimited |
| **Companies** | 1 | 1 | 3 | Unlimited |
| **Tax regime** | Standard OR Simplified | Both | Both | Both |
| **Price (EGP/yr)** | 3,500 | 8,000 | 17,500 | 30,000 |

### 7.2 Feature Matrix

| Feature | Solo | SMB | Enterprise | Firm |
|---------|:---:|:---:|:----------:|:----:|
| **Core** | | | | |
| Sales invoices + ETA submission | ✓ | ✓ | ✓ | ✓ |
| Purchase invoices | ✓ | ✓ | ✓ | ✓ |
| Expenses | ✓ | ✓ | ✓ | ✓ |
| Monthly VAT report (Form 10) | ✓ | ✓ | ✓ | ✓ |
| WHT report (Form 41) | ✓ | ✓ | ✓ | ✓ |
| Penalty Shield | ✓ | ✓ | ✓ | ✓ |
| Tax Calendar + reminders | ✓ | ✓ | ✓ | ✓ |
| ETA error translator (Arabic) | ✓ | ✓ | ✓ | ✓ |
| Offline mode | ✓ | ✓ | ✓ | ✓ |
| Local backup (manual) | ✓ | ✓ | ✓ | ✓ |
| Email invoice (mail client) | ✓ | ✓ | ✓ | ✓ |
| **Operations** | | | | |
| Bank statement import (CSV) | ✗ | ✓ | ✓ | ✓ |
| Bulk invoice upload (Excel) | ✗ | ✓ | ✓ | ✓ |
| Multi-cashbox | ✗ | ✓ | ✓ | ✓ |
| Closing Cockpit | ✗ | ✓ | ✓ | ✓ |
| Trial Balance | ✗ | ✓ | ✓ | ✓ |
| Chart of Accounts (custom) | ✗ | ✓ | ✓ | ✓ |
| **Advanced** | | | | |
| Multi-user + roles | ✗ | ✓ (3 users) | ✓ (unlimited) | ✓ (unlimited) |
| Email invoice (SMTP direct) | ✗ | ✓ | ✓ | ✓ |
| Auto-backup (scheduled) | ✗ | ✓ | ✓ | ✓ |
| Audit log | ✗ | ✓ | ✓ | ✓ |
| Invoice templates (3 designs) | ✗ | ✓ | ✓ | ✓ |
| Bilingual invoices (AR+EN) | ✗ | ✓ | ✓ | ✓ |
| **Enterprise** | | | | |
| Multi-company (3) | ✗ | ✗ | ✓ | ✓ (unlimited) |
| Income tax return prep | ✗ | ✗ | ✓ | ✓ |
| WHT certificate management | ✗ | ✗ | ✓ | ✓ |
| Compliance Health dashboard | ✗ | ✗ | ✓ | ✓ |
| Priority support (WhatsApp) | ✗ | ✗ | ✓ | ✓ |
| **Firm Portal** | | | | |
| Company switcher | ✗ | ✗ | ✗ | ✓ |
| Accountant commission ledger | ✗ | ✗ | ✗ | ✓ |
| Client onboarding wizard | ✗ | ✗ | ✗ | ✓ |
| Bulk operations across companies | ✗ | ✗ | ✗ | ✓ |
| Referral code generation | ✓ | ✓ | ✓ | ✓ |
| **Future (G2-G3)** | | | | |
| GS1 smart coding assistant | ✗ | ✓ | ✓ | ✓ |
| WhatsApp invoice delivery | ✗ | ✓ | ✓ | ✓ |
| Arabic AI tax assistant | ✗ | ✗ | ✓ | ✓ |
| OCR receipt scanner | ✗ | ✓ | ✓ | ✓ |
| Cloud backup (encrypted) | ✗ | Add-on | ✓ | ✓ |
| Auto-update | ✓ | ✓ | ✓ | ✓ |

### 7.3 Edition Enforcement (Technical)

The license key encodes the edition as a claim inside the signed JWT-style
payload (the existing `LicensePayload` type already has Hwid + ExpiresAt;
add Edition + MaxUsers + MaxCompanies + Features):

```
LicensePayload (extended)
├── Edition (enum: Solo, SMB, Enterprise, Firm)
├── MaxUsers (int)
├── MaxCompanies (int)
├── Features[] (string[] — feature flags from §7.2)
├── ExpiresAt (DateTime)
└── DeviceFingerprint (string — already there as Hwid)
```

At runtime, a `LicenseGate` service (extension of the existing one) checks
the current edition before enabling any gated feature:

```csharp
// Example: gating bulk invoice upload
public class BulkSalesInvoicePostHandler
{
    public async Task HandleAsync(BulkUploadCommand cmd)
    {
        _licenseGate.Require(Feature.BulkInvoice); // throws if Solo edition
        // ... proceed
    }
}
```

The `LicenseGate.Require()` method:

1. Reads the current `LicensePayload` from the in-memory cache.
2. Checks if the requested feature is in the `Features[]` array.
3. If not, throws a `LicenseRestrictionException` with a user-friendly
   Arabic message: "هذه الميزة متاحة في خطة أعمال صغيرة فأعلى. ترقية الآن؟"
4. The UI catches this exception and shows an upgrade prompt with a
   comparison of what the user gets.

### 7.4 Upgrade Flow

When a user hits a gated feature:

1. A modal appears: "هذه الميزة غير متاحة في خطتك الحالية"
2. Shows a mini comparison: current edition vs. the minimum edition that
   includes the feature.
3. Two buttons:
   - "ترقية الآن" → opens the self-service portal (G1.5) or shows InstaPay
     instructions
   - "لاحقاً" → dismisses the modal

The upgrade replaces the license key. No reinstall needed. The app reads
the new key, validates it, and immediately unlocks the new features.

### 7.5 Trial Mode Behaviour

During the 14-day trial, **all Enterprise features are unlocked** (except
Firm Portal). This lets the prospect experience the full product before
deciding which edition to buy. After trial expiry:

- If no license key is entered → all features locked except data viewing +
  Settings (Tab 7).
- The dashboard shows: "انتهت الفترة التجريبية. بياناتك محفوظة. أدخل
  مفتاح الترخيص للاستمرار."
- No data is deleted. Ever.

### 7.6 Pricing Strategy Notes (input to G0 decision)

The prices in §7.1 are based on competitor pricing research (May 2026,
1 USD ≈ 53 EGP):

| Competitor | Cheapest Plan | Mid Plan | Top Plan |
|-----------|--------------|----------|----------|
| Wafeq | 9,660 EGP/yr (Starter, 2 users) | 13,860 EGP/yr (Plus, 5 users) | 23,892 EGP/yr (Premium) |
| Daftra | 12,720 EGP/yr (Basic, $240/yr) | 26,235 EGP/yr (Advanced, $495/yr) | 31,800 EGP/yr (Complete, $600/yr) |
| Optify Flex | ~5,000 EGP/yr (Starter) | ~10,000 EGP/yr (Business) | ~18,000 EGP/yr (Premium) |
| eDariba | ~3,600 EGP/yr (basic e-invoice only) | — | — |

All competitors above are cloud-only SaaS. DaftarX is on-premise (data on
the customer's machine, annual license). This justifies a different
positioning:

- **Solo at 3,500 EGP/yr:** Cheaper than Wafeq Starter (9,660) and Daftra
  Basic (12,720), but not suspiciously cheap. The message: "أقل من 300
  جنيه في الشهر — أرخص من غرامة تأخير واحدة. وبياناتك على جهازك."
- **SMB at 8,000 EGP/yr:** Competes with Wafeq Starter (9,660) and
  undercuts Daftra Basic (12,720), but includes features they charge extra
  for (bank import, bulk upload, 3 users, audit log).
- **Enterprise at 17,500 EGP/yr:** Competes with Daftra Advanced (26,235)
  and Wafeq Premium (23,892). DaftarX wins on: on-premise + Arabic-first +
  compliance focus + multi-company. Significantly cheaper.
- **Firm at 30,000 EGP/yr:** No direct competitor. Daftra has no
  accountant portal. Wafeq has basic "Accountant Perks" but nothing close
  to a full firm management system. New market segment.

**Key pricing principle:** DaftarX should be 40-60% cheaper than
Daftra/Wafeq at every tier, because on-premise has lower marginal cost (no
cloud hosting per customer). But not 80% cheaper — that signals "cheap =
low quality."

---

## 8. Sequencing

This module should be built **before G1.5 (self-service portal)** and
**before any customer purchases a license**. The recommended position in
the roadmap:

| Order | Module | Rationale |
|-------|--------|-----------|
| ... | Gux.1-12 (shipped) | Sidebar + dashboard redesign |
| **→** | **Gux.13 Company Admin Panel** | **Admin needs control before customers arrive** |
| ... | G2.x (shipped) | Differentiation features land into a clean admin surface |
| ... | G1.5 Self-service portal | License tab (Tab 7) is ready to link to the portal |

---

## 9. Acceptance Criteria

1. Admin can complete all 10 tabs without leaving `/settings`.
2. First-time wizard runs on fresh install and configures Tabs 1-3.
3. Non-admin users cannot see or access admin-only tabs.
4. Test Connection (ETA) and Test Email (SMTP) return clear Arabic
   success/failure messages.
5. Backup creates a valid `.dxbak` file that can be restored on a different
   machine **of the same DB provider type**.
6. All settings persist across app restarts.
7. Changing tax regime shows a confirmation dialog and does not modify
   existing posted invoices.
8. Role permissions matrix is enforced server-side (not just UI-hidden).
9. Edition-gated features show an upgrade prompt (not a crash or blank
   page) when accessed on a lower edition.
10. Trial mode unlocks all Enterprise features for 14 days.
11. Upgrading edition via new license key takes effect immediately without
    reinstall.
12. `LicenseGate.Require()` is enforced server-side for all gated features.
13. Invoice number override rejects values ≤ highest used number, and is
    read-only when the active period is Locked.
14. Tab 10 shows the actual detected DB provider at runtime (not a
    hardcoded string).
15. New entities are lazy-initialized with defaults for existing installs
    on first access.

---

## 10. Estimated Effort

| Component | Effort (AI-paired) |
|-----------|-------------------|
| Tab 1-3 (refactor existing pages into tabs) | 0.5 day |
| Tab 4 (Invoice Settings — new, incl. number validation + period-lock guard) | 0.5 day |
| Tab 5 (Email — MAPI + SMTP) | 0.5 day |
| Tab 6 (User Management + roles) | 1 day |
| Tab 7 (License — refactor existing) | 0.25 day |
| Tab 8 (Backup — new, provider-aware + manifest) | 0.75 day |
| Tab 9 (Notifications — new, incl. ETA cert expiry) | 0.25 day |
| Tab 10 (About — refactor existing, add data export + provider info) | 0.25 day |
| First-time wizard | 0.5 day |
| Role-based access enforcement | 0.5 day |
| Edition system (LicenseGate + feature flags + upgrade prompts) | 1 day |
| Edition-aware UI (show/hide/lock features per edition) | 0.5 day |
| `Issue-License.ps1` update (encode edition in token) | 0.25 day |
| **Total** | **~6 days** |

---

## 11. Known Bug — Dev Config Skew

`appsettings.Development.json` defines a connection string named `"App"`,
but `Program.cs:541-545` reads `"EgyptTax"`. They don't match, so every
`dotnet run` in dev falls through to the portable SQLite default — even if
the developer intended to hit SQL Server Express.

**Fix (choose one):**

- **Option A:** Rename `"App"` → `"EgyptTax"` in
  `appsettings.Development.json` so dev matches the MSI production path
  (SQL Server Express).
- **Option B:** Leave it as-is and document that dev always uses SQLite.
  This is actually convenient for new contributors who don't have SQL
  Server installed.

**Recommendation:** Option B (document it). Most developers and CI will
prefer the zero-dependency SQLite path. Add a comment in
`appsettings.Development.json` explaining the intentional fallthrough.

---

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-05-11 | Initial spec | Claude (AI) |
| 2026-05-11 | Pricing resolved: Option A adopted (4 editions) | User + Claude |
| 2026-05-11 | **v2 review fixes:** (1) Tab 6 "Pro/Basic" → "SMB/Solo", (2) Tab 8 SQLite → provider-aware backup, (3) Tab 4 invoice number validation added, (4) Tab 3 ETA clarified OAuth-only test, (5) Tab 9 ETA cert expiry alert added, (6) Tab 10 data export + runtime DB provider added, (7) §6 dual-DB architecture documented, (8) §9 acceptance criteria expanded 12→15, (9) §10 effort adjusted +0.25 day, (10) §11 dev-config skew documented, (11) Tab 8 cross-provider restore guard added | Manus AI (review) |
| 2026-05-11 | **v3 final fixes:** (1) Tab 4 added FR-037 period-lock guard for invoice number override, (2) §10 license tooling clarified as `Issue-License.ps1` (not Python/web portal — those are separate systems), (3) Referral codes confirmed unlimited across all editions (no cap), (4) Danger Zone deferred to post-v1, (5) Complexity updated to ~6 days | User + Manus AI |
