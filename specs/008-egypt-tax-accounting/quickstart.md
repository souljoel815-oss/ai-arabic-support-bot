# Quickstart — Egyptian Tax Accounting MVP

**Audience**: Developers setting up the project locally and running the US1-only minimum slice.
**Date**: 2026-05-07 | **Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md)

The quickest path through the codebase: install prerequisites, run database migrations, seed the smallest valid configuration, start the Blazor Server app, and execute the **US1 (P1) end-to-end demo** — issue one posted sales invoice with an Arabic+English PDF and a mock-submitted ETA eInvoice JSON. This path validates the spec's most critical user story without depending on any other story.

---

## 1. Prerequisites

| Tool | Version | Why |
| --- | --- | --- |
| Windows 10 21H2+ or Windows Server 2019+ | — | Target platform |
| .NET 8 SDK | 8.0.x (LTS) | Build + run |
| SQL Server (any edition) | 2019+ | Storage. LocalDB is acceptable for dev (`Server=(localdb)\\MSSQLLocalDB;...`); a regular Express named instance also works for dev when reached via Shared Memory (`Server=lpc:.\\SQLEXPRESS;...`) — useful when LocalDB isn't installable. The WiX installer ships SQL Server Express for production. |
| Node.js | 20.x | Required only for Playwright browser binaries; not used for app build |
| Git | 2.40+ | |
| WiX Toolset | 5.x | Building the MSI installer (only when packaging for release) |

```powershell
# Verify versions
dotnet --version
sqlcmd -? | Select-String -Pattern "Version"
```

## 2. Clone and restore

```powershell
git clone https://github.com/<org>/<repo>.git
cd <repo>
git checkout 008-egypt-tax-accounting

dotnet restore
dotnet build -c Debug
```

Expected result: clean build with no warnings escalated to errors. Project layout per [plan.md](plan.md):

```
src/
  EgyptTax.SharedKernel/
  EgyptTax.Domain/
  EgyptTax.Application/
  EgyptTax.Infrastructure/
  EgyptTax.Web/
  EgyptTax.Installer/
tests/
  EgyptTax.UnitTests/
  EgyptTax.IntegrationTests/
  EgyptTax.ContractTests/
  EgyptTax.E2ETests/
```

## 3. Configure the database connection

Create `src/EgyptTax.Web/appsettings.Development.json` (gitignored):

```json
{
  "ConnectionStrings": {
    "App": "Server=(localdb)\\MSSQLLocalDB;Database=EgyptTax_Dev;Trusted_Connection=Yes;TrustServerCertificate=Yes;",
    "Hangfire": "Server=(localdb)\\MSSQLLocalDB;Database=EgyptTax_Dev_Hangfire;Trusted_Connection=Yes;TrustServerCertificate=Yes;"
    // ALTERNATIVE — if you have a regular SQL Express instance instead of LocalDB,
    // use the Shared Memory (lpc:) protocol; this works without admin, without
    // SQL Browser, and without TCP/IP enabled on the instance:
    //   "App":      "Server=lpc:.\\SQLEXPRESS;Database=EgyptTax_Dev;Trusted_Connection=Yes;TrustServerCertificate=Yes;",
    //   "Hangfire": "Server=lpc:.\\SQLEXPRESS;Database=EgyptTax_Dev_Hangfire;Trusted_Connection=Yes;TrustServerCertificate=Yes;"
  },
  "AuditCheckpoint": {
    "Mode": "File",
    "FilePath": "C:\\EgyptTax-Dev\\audit_checkpoints\\checkpoint.json"
  },
  "Attachments": {
    "Root": "C:\\EgyptTax-Dev\\attachments"
  },
  "EtaMock": {
    "BaseUrl": "https://localhost:5443/api/v1/eta-mock"
  },
  "Ntp": {
    "Server": "pool.ntp.org",
    "AlertSkewSeconds": 60
  }
}
```

Create the audit-checkpoint and attachments directories with restricted ACLs (file mode):

```powershell
New-Item -ItemType Directory -Force "C:\EgyptTax-Dev\audit_checkpoints" | Out-Null
New-Item -ItemType Directory -Force "C:\EgyptTax-Dev\attachments" | Out-Null
icacls "C:\EgyptTax-Dev\audit_checkpoints" /grant "$env:USERDOMAIN\$env:USERNAME:(M)" /inheritance:r
```

## 4. Apply EF Core migrations

```powershell
dotnet ef database update `
  --project src\EgyptTax.Infrastructure `
  --startup-project src\EgyptTax.Web `
  --context AppDbContext

# Hangfire schema is auto-installed on first run; no separate migration required.
```

Expected: two databases created (`EgyptTax_Dev`, `EgyptTax_Dev_Hangfire`); `audit.audit_log` and `audit_meta.checkpoint` tables present in the application DB.

## 5. Seed the minimum-viable configuration (US1 only)

The seeder bootstraps exactly what US1 needs and nothing else: one company, one Administrator user (with TOTP MFA enrolled), one VAT category at 14 %, one customer, one item, and one document series for sales invoices.

```powershell
dotnet run --project src\EgyptTax.Web -- seed --profile us1-minimum
```

Output:

```
[seed] Created Company: شركة الاختبار / Test Company SAE (TIN 123456789)
[seed] Created Administrator user: admin@test.local
[seed]   MFA enrolled. Provisioning URI:
[seed]   otpauth://totp/EgyptTax:admin@test.local?secret=...&issuer=EgyptTax
[seed]   Initial password (must change at first login): TempP@ssw0rd!2026
[seed] Seeded VAT category: Standard 14 % (effective 2026-01-01)
[seed] Seeded chart of accounts: 1100, 1200, 2110, 4000 (sufficient for US1)
[seed] Seeded customer: عميل تجريبي / Test Customer LLC (B2B-Registered, TIN 987654321)
[seed] Seeded item: Consulting Hour / ساعة استشارة (default VAT = Standard 14 %)
[seed] Seeded document series: INV (sales invoices)
[seed] DONE — ready for US1 quickstart.
```

Scan the provisioning URI into your authenticator app. The TempP@ssw0rd password is forced-change on first login.

## 6. Run the Blazor Server app

```powershell
dotnet run --project src\EgyptTax.Web
```

App listens on `https://localhost:5443`. The dev cert prompt is handled by `dotnet dev-certs https --trust` if needed.

## 7. US1 end-to-end smoke (manual)

1. Open `https://localhost:5443/`. Log in as `admin@test.local` with the temp password.
2. Forced password change → enter a new password.
3. MFA prompt → enter the TOTP code from your authenticator.
4. Navigate to **Invoices → New Sales Invoice**.
5. Customer: `Test Customer LLC`. Date: today. Add one line: item = `Consulting Hour`, qty = `1`, unit price = `1000.00`. Line VAT = `Standard 14%` (default).
6. Verify computed totals: subtotal `1,000`, VAT `140`, grand total `1,140`.
7. Click **Post**. Document number assigned (e.g., `INV-2026-000001`). State → `Posted`. Audit log entry created (visible under **Audit → Latest Activity**).
8. Click **Download PDF**. Open the PDF; verify:
   - Bilingual layout (Arabic right-side block, English left).
   - Customer TIN appears (because customer is `B2B-Registered`).
   - Total in Arabic words: "ألف ومئة وأربعون جنيهاً مصرياً فقط لا غير" (or equivalent).
   - QR code (FR-044) embedded bottom-right.
9. Click **View ETA Submission**. Status: `Submitted` (mock). Inspect the JSON; it conforms to `contracts/eta-einvoice.schema.json`.
10. Open the **ETA Compliance Dashboard**. The just-posted invoice appears with a green "Submitted" badge and zero risk findings.
11. Scan the PDF's QR code with a phone, OR open `https://localhost:5443/api/v1/verify/{seal}` (substituting the QR contents). Result: `VALID`.
12. Run the audit verifier (console verb on the Web host — no separate exe project). Substitute your dev connection string — either form works:
    ```powershell
    # Form A — LocalDB (if installed)
    dotnet run --project src\EgyptTax.Web -- verify-audit `
      --connection "Server=(localdb)\MSSQLLocalDB;Database=EgyptTax_Dev;Trusted_Connection=Yes;TrustServerCertificate=Yes;" `
      --checkpoint-file "C:\EgyptTax-Dev\audit_checkpoints\checkpoint.json"

    # Form B — SQL Express via Shared Memory (no LocalDB required)
    dotnet run --project src\EgyptTax.Web -- verify-audit `
      --connection "Server=lpc:.\SQLEXPRESS;Database=EgyptTax_Dev;Trusted_Connection=Yes;TrustServerCertificate=Yes;" `
      --checkpoint-file "C:\EgyptTax-Dev\audit_checkpoints\checkpoint.json"
    ```
    Expected: `valid: true, findings: []`.

If all 12 steps pass, the US1 slice is functional. Any failure points to an issue in either the implementation or the spec's assumptions and should be triaged before proceeding to US2.

## 8. Run the test suite

```powershell
# Unit tests (~seconds)
dotnet test tests\EgyptTax.UnitTests

# Contract tests (validates eInvoice JSON, Form 41 JSON, manifest, QR seal)
dotnet test tests\EgyptTax.ContractTests

# Integration tests (uses Testcontainers; requires Docker on the dev machine)
dotnet test tests\EgyptTax.IntegrationTests

# E2E tests (Playwright; downloads browser binaries on first run)
dotnet test tests\EgyptTax.E2ETests
```

The integration suite includes the SC-006 stress test (500 concurrent posting attempts spanning a fiscal-year boundary, zero gaps / zero duplicates) and the SC-010 audit-chain tamper suite (insert / edit / delete / reorder / tail-truncation, all detected within 30 s on a 1,000,000-entry seeded chain).

## 9. Build the WiX MSI installer (release path only)

```powershell
dotnet publish src\EgyptTax.Web -c Release -r win-x64 --self-contained false -o publish\
wix build src\EgyptTax.Installer\Product.wxs -o EgyptTax-Setup.msi -arch x64
```

The MSI prompts the operator at install for: SQL connection (Windows or SQL auth), audit-checkpoint storage mode (file or table), attachments root, NTP server. It creates an `EgyptTax` Windows Service that runs the Blazor Server + Hangfire host.

## 10. Story sequencing pointers

After US1 is green, follow the story-to-entity map in [data-model.md](data-model.md) to add subsequent stories. The recommended local-development order matches the delivery phases:

- **Phase 1**: US1 → Active ETA Compliance Dashboard (FR-043) → QR seal verifier (FR-044) → Tax Risk Score MVP rules (Differentiator 1).
- **Phase 2**: US2 (purchase invoices + expenses) → US3 (approval + audit log) → US8 (Accountant-Firm Portal).
- **Phase 3**: US4 (auto journals) → VAT monthly + taxable income reports → minimal Payments & Settlement (FR-051/052/053) → US7 WHT lifecycle → US9 Tax-Inspection Bundle → Closing Cockpit (Differentiator 2).
- **Phase 4**: US6 (fixed assets + depreciation) → configurable rules UI (US5).

## 11. Common pitfalls

- **Bilingual fonts on Windows 10**: QuestPDF needs an Arabic-shaping-capable font. The installer ships Cairo (variable) and Amiri; in dev, `dotnet run` falls back to Segoe UI Arabic if those aren't on the path.
- **MFA secret lost**: in dev, re-run the seeder with `--reset-mfa admin@test.local`. In production, only the out-of-band admin recovery (FR-038) is available.
- **Audit verifier fails on file-mode checkpoint after a manual file edit**: this is the intended detection. Restore the checkpoint from backup, or rebuild it via `dotnet run --project src\EgyptTax.Web -- verify-audit --rebuild-checkpoint` (or `EgyptTax.Web.exe verify-audit --rebuild-checkpoint` after install) — the rebuild action is itself audit-logged.
- **Integration tests fail without Docker**: Testcontainers requires a Docker daemon. Either install Docker Desktop or fall back to a manually provisioned local SQL Server and set `INTEGRATION_TEST_CONN` env var.
- **Playwright first-run is slow**: it downloads browser binaries (~250 MB). Subsequent runs are fast.

## 12. Where to look next

- [plan.md](plan.md) — full Technical Context + Constitution Check + Project Structure + Complexity Tracking.
- [research.md](research.md) — every design decision (R-01..R-24) with rationale and traceability.
- [data-model.md](data-model.md) — every entity with fields, invariants, and the user story that introduces it.
- [contracts/](contracts/) — JSON Schemas + protocol specs for every external boundary (eInvoice, WHT certificate, Form 41, Inspection Bundle manifest, QR seal, audit verifier, REST API).
- [spec.md](spec.md) — the spec itself (the source of truth for everything above).
