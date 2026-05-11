# Gux.13 — Company Admin Panel + Edition System

> **Module ID:** Gux.13 (alias G1.6 — sequenced before any customer can purchase)
>
> **Priority:** P0 — without this, the company admin has no centralized control
> surface. Settings are scattered across 9+ sidebar items with no role-based
> gating.
>
> **Complexity:** L (~5.75 days AI-paired, including the new edition system)
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
| حالة الاتصال (Connection status) | indicator | Green (connected) / Red (failed) / Grey (not configured) |
| آخر اتصال ناجح (Last successful connection) | timestamp | Auto-updated |
| زر اختبار الاتصال (Test Connection) | button | Calls ETA auth endpoint, shows result |

**Behaviour:**
- Test Connection sends a real auth request to ETA sandbox/production and
  shows success/failure with the exact error message in Arabic.
- Toggle between Sandbox and Production requires confirmation: "التبديل
  للبيئة الحقيقية يعني أن الفواتير سترسل لمصلحة الضرائب فعلياً."
- The existing ETA Wizard (`/eta-wizard`) becomes a first-time setup flow
  that redirects here after completion.

---

### Tab 4: إعدادات الفواتير (Invoice Settings)

| Field | Type | Notes |
|-------|------|-------|
| قالب الفاتورة (Invoice template) | select + preview | 3 built-in templates: Classic, Modern, Compact |
| لغة الفاتورة (Invoice language) | select | Arabic only / Arabic + English (bilingual) |
| بادئة رقم الفاتورة (Invoice prefix) | text | e.g., "INV-" or "فت-" |
| الترقيم التالي (Next number) | number | Auto-incremented, admin can override |
| شروط الدفع الافتراضية (Default payment terms) | select | Immediate / 15 days / 30 days / 60 days / Custom |
| ملاحظات أسفل الفاتورة (Footer notes) | textarea | Appears on every invoice. e.g., bank account for transfer |
| إظهار QR code | toggle | QR code with ETA verification URL |
| إظهار اللوجو | toggle | Show/hide company logo on invoice |

**Behaviour:** Live preview panel on the right side (desktop) shows a
sample invoice updating in real-time as the admin changes settings.

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
- Only available on Pro and higher plans (see §7). Basic plan shows:
  "الخطة الأساسية تدعم مستخدم واحد. ترقية لخطة المهنية لإضافة مستخدمين."
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
- Auto-backup creates a compressed `.dxbak` file (renamed ZIP containing
  the SQLite DB + attachments).
- Backup Now shows a progress bar and confirms with file path.
- Restore requires double confirmation: "هذا سيستبدل جميع البيانات
  الحالية. هل أنت متأكد؟" → "اكتب 'استعادة' للتأكيد."
- Keep last 30 backups by default. Older ones auto-deleted (configurable).
- Cloud backup (G4.2) will add a third option in Save location: "Cloud
  (encrypted)".

---

### Tab 9: الإشعارات (Notifications)

| Setting | Notes |
|---------|-------|
| تنبيه المواعيد الضريبية (Tax deadline alerts) | Toggle + days before (default: 7, 3, 1) |
| تنبيه انتهاء الترخيص (License expiry alert) | Toggle + days before (default: 30, 14, 7, 1) |
| تنبيه فشل إرسال ETA (ETA submission failure) | Toggle (default: on) |
| تنبيه اعتمادات معلقة (Pending approvals) | Toggle (default: on) |
| تنبيه النسخ الاحتياطي (Backup reminder) | Toggle + if no backup in X days (default: 7) |
| طريقة التنبيه (Notification method) | In-app (always) + Email (optional, requires Tab 5 SMTP) |

**Behaviour:**
- In-app notifications appear in the bell icon (Gux.10 — shipped).
- Email notifications only work if SMTP is configured in Tab 5.
- All toggles default to ON for new installations.

---

### Tab 10: حول ومساعدة (About & Support)

| Item | Notes |
|------|-------|
| إصدار البرنامج (App version) | e.g., 1.2.0 |
| إصدار قاعدة البيانات (DB version) | Schema version |
| معلومات النظام (System info) | OS, .NET version, SQL LocalDB version |
| زر تقرير التشخيص (Diagnostic Report) | Generates the existing DIAGNOSTIC-REPORT and saves/copies |
| زر التحقق من التحديثات (Check for Updates) | Calls `latest.json` (G4.1) |
| رابط دليل المستخدم (User Guide) | Opens USER-GUIDE in browser |
| رابط التواصل (Contact Support) | WhatsApp link + email |
| الرخصة القانونية (Legal) | EULA + third-party licenses |

**Behaviour:** Visible to all users (not admin-only). The diagnostic
report button is critical for support — when a customer reports a bug,
support says "اضغط على تقرير التشخيص وابعتلنا الملف."

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
└── EmailNotificationsEnabled (bool)
```

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
| **Suggested price (EGP/yr)** | 3,000 - 4,000 | 7,000 - 9,000 | 15,000 - 20,000 | 25,000 - 35,000 |

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

The suggested prices in §7.1 are based on competitor pricing research (May
2026, 1 USD ≈ 53 EGP):

| Competitor | Cheapest Plan | Mid Plan | Top Plan |
|-----------|--------------|----------|----------|
| Wafeq | 9,660 EGP/yr (Starter, 2 users) | 13,860 EGP/yr (Plus, 5 users) | 23,892 EGP/yr (Premium) |
| Daftra | 12,720 EGP/yr (Basic, $240/yr) | 26,235 EGP/yr (Advanced, $495/yr) | 31,800 EGP/yr (Complete, $600/yr) |
| Optify Flex | ~5,000 EGP/yr (Starter) | ~10,000 EGP/yr (Business) | ~18,000 EGP/yr (Premium) |
| eDariba | ~3,600 EGP/yr (basic e-invoice only) | — | — |

All competitors above are cloud-only SaaS. DaftarX is on-premise (data on
the customer's machine, annual license). This justifies a different
positioning:

- **Solo at 3,000-4,000 EGP/yr:** Cheaper than Wafeq Starter (9,660) and
  Daftra Basic (12,720), but not suspiciously cheap. The message: "أقل من
  350 جنيه في الشهر — أرخص من غرامة تأخير واحدة. وبياناتك على جهازك."
- **SMB at 7,000-9,000 EGP/yr:** Competes with Wafeq Starter (9,660) and
  undercuts Daftra Basic (12,720), but includes features they charge extra
  for (bank import, bulk upload, 3 users, audit log).
- **Enterprise at 15,000-20,000 EGP/yr:** Competes with Daftra Advanced
  (26,235) and Wafeq Premium (23,892). DaftarX wins on: on-premise +
  Arabic-first + compliance focus + multi-company. Significantly cheaper.
- **Firm at 25,000-35,000 EGP/yr:** No direct competitor. Daftra has no
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
   machine.
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

---

## 10. Estimated Effort

| Component | Effort (AI-paired) |
|-----------|-------------------|
| Tab 1-3 (refactor existing pages into tabs) | 0.5 day |
| Tab 4 (Invoice Settings — new) | 0.5 day |
| Tab 5 (Email — MAPI + SMTP) | 0.5 day |
| Tab 6 (User Management + roles) | 1 day |
| Tab 7 (License — refactor existing) | 0.25 day |
| Tab 8 (Backup — new) | 0.5 day |
| Tab 9 (Notifications — new) | 0.25 day |
| Tab 10 (About — refactor existing) | 0.25 day |
| First-time wizard | 0.5 day |
| Role-based access enforcement | 0.5 day |
| Edition system (LicenseGate + feature flags + upgrade prompts) | 1 day |
| Edition-aware UI (show/hide/lock features per edition) | 0.5 day |
| `Issue-License.ps1` update (encode edition in token) | 0.25 day |
| **Total** | **~5.75 days** |

---

## Pricing decision needed

The edition system in §7 is **not compatible with the 3-tier pricing
already locked** in [`pricing.md`](pricing.md). One of these has to give
before implementation can start:

### Option A — Adopt the 4-edition system (this doc wins)

Re-open `pricing.md` and restructure to:
- **Solo** (1 user / 1 company / 3,000-4,000 EGP)
- **SMB** (3 users / 1 company / 7,000-9,000 EGP)
- **Enterprise** (unlimited users / 3 companies / 15,000-20,000 EGP)
- **Firm** (unlimited / unlimited / 25,000-35,000 EGP)

Rationale for this option: the new spec is more granular (separates
"company that just hit you with ETA" from "real SMB with 3 employees" from
"firm running clients"). Better revenue ladder. Closer to competitor pricing
than the 3-tier scheme. Resolves the "Solo tier below Basic?" question that
was deferred in `pricing.md`.

Cost: rewrites the pricing decision committed in `55fc87d`.

### Option B — Keep the 3-tier pricing (pricing.md wins)

Adapt §7 of this spec to use the 3 existing tiers (Basic / Pro /
Enterprise). Drop the Firm-specific tier; merge Firm Portal features into
Enterprise (as the existing decision says).

Cost: less granular ladder. Firm Portal usage doesn't have its own
revenue tier. Solo persona (the newly-mandated micro-business) still gets
served by Basic at 2,500 EGP, but with fewer features.

### Option C — Three tiers + Add-ons

Keep Basic / Pro / Enterprise from `pricing.md`, but allow per-feature
add-ons (e.g., "Firm Portal +5,000 EGP/year" on top of Enterprise). Maps
to the spec's existing "Cloud backup add-on" pattern in §7.2.

Cost: more complex pricing page; harder to position in a hero copy line.

**Resolved 2026-05-11:** Option A adopted. `pricing.md` was rewritten
to use the 4-edition scheme (Solo / SMB / Enterprise / Firm) at
3,500 / 8,000 / 17,500 / 30,000 EGP/year. `LicenseGate` implementation
proceeds against that ladder.
