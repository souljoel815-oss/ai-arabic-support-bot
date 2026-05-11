# DaftarX — Full User Guide

> Every feature, every workflow, end-to-end. Aimed at the operator
> (accountant / bookkeeper) and the installer/admin. Read it once,
> keep it bookmarked.

**Version covered:** v1.2 "2025 Compliance Pack" + Wave 4 (Bank
auto-match + Closing-readiness gate). **Last updated:** 2026-05-11.

---

## Table of contents

1. [What DaftarX is (and isn't)](#1-what-daftarx-is)
2. [System requirements](#2-system-requirements)
3. [Install](#3-install)
4. [First-run setup](#4-first-run-setup)
5. [The home dashboard](#5-the-home-dashboard)
6. [Master-data setup (in order)](#6-master-data-setup-in-order)
7. [Sales workflow](#7-sales-workflow)
8. [Purchases & expenses](#8-purchases--expenses)
9. [Payments (supplier + customer vouchers)](#9-payments)
10. [Banking & bank-recon (P3.2 + P3.4)](#10-banking--bank-recon)
11. [ETA — Egypt Tax Authority integration](#11-eta)
12. [WHT — withholding tax](#12-wht)
13. [Fixed assets + monthly depreciation](#13-fixed-assets)
14. [Period closing — the Closing Cockpit (P3.6 + P2.5)](#14-period-closing)
15. [Reports](#15-reports)
16. [Penalty Shield & Compliance Calendar](#16-penalty-shield--compliance-calendar)
17. [Audit log, Document 360 & Inspection bundle](#17-audit-log)
18. [Approvals & Firm Portal (multi-company)](#18-approvals--firm-portal)
19. [Settings reference](#19-settings-reference)
20. [Operator / admin tasks](#20-operator--admin-tasks)
21. [Licensing — vendor + customer side](#21-licensing)
22. [Troubleshooting & FAQ](#22-troubleshooting--faq)
23. [Roles & permissions](#23-roles--permissions)
24. [Glossary](#24-glossary)

---

## 1. What DaftarX is

DaftarX is an **on-premise** accounting + tax-compliance app built
specifically for Egyptian SMBs and accounting firms. It runs as a
single Windows service on a customer's own machine, talks to a local
SQL Server (Express is fine) or a bundled SQLite file, and exposes a
Blazor Server UI at `http://localhost:8088`.

**The five differentiators** (vs Daftra / Wafeq / Edara / Odoo):

1. **Penalty Shield** — at any moment, tells the operator how much
   EGP they'd owe in penalties today and how much they'd save if
   they fixed each item now.
2. **Pre-flight ETA validator** — runs 8 of the regulator's
   validations locally before submission, killing ~80% of
   rejections.
3. **Law 6/2025 ready** — turnover-tax + quarterly-VAT simplified
   regime is available on day one.
4. **Arabic error translator** — translates ETA's English/code
   errors into Arabic corrective actions.
5. **Portable EXE** — a 65 MB single-file build runs on a fresh PC
   in 30 seconds with no SQL or .NET install.

**What it is not:** a SaaS, a POS, an inventory MRP system, or a
payroll engine. Those are out of scope by design.

---

## 2. System requirements

| Mode | OS | RAM | Disk | DB |
|---|---|---|---|---|
| **MSI install (recommended)** | Windows 10 22H2+ / Windows 11 / Server 2019+ | 4 GB | 1 GB free | SQL Server Express (bundled offline) |
| **Portable EXE** | Windows 10 22H2+ | 2 GB | 200 MB free | SQLite (bundled, no install) |
| **Docker compose** | Any Linux/Windows with Docker | 4 GB | 1 GB free | SQL Server 2022 container |

Browsers: Chrome / Edge / Firefox current. Internet **not required**
at install time (SQL Express is bundled in the MSI). Outbound HTTPS
to ETA endpoints is required only when you submit invoices.

---

## 3. Install

### 3.1 Production install (MSI)

1. Run `DaftarX-Setup.exe` as Administrator (double-click → "Yes" at
   UAC).
2. The wrapper silently installs SQL Server Express LocalDB if it's
   not already present, then runs the MSI.
3. Accept the install path (`C:\Program Files\DaftarX\`).
4. The installer creates a Windows service named `EgyptTax`, opens
   firewall port 8088, runs EF migrations, seeds the database, and
   grants `NT AUTHORITY\NETWORK SERVICE` `db_owner` on the
   `EgyptTax` database.
5. When done, browse to `http://localhost:8088`.

If anything goes wrong, run `Diagnose-DaftarX.cmd` from the install
folder — it writes `daftarx-diagnostic.txt` next to itself. Send
that file to support.

### 3.2 Portable EXE

1. Copy `DaftarX-Portable.exe` to any folder with write access (e.g.,
   `D:\DaftarX\`).
2. Double-click. On first run it creates `data.db` (SQLite) next to
   the EXE, applies migrations, seeds the database, and opens a
   browser.
3. Bind URL defaults to `http://localhost:8088`. Override with
   `--urls http://+:9000` if 8088 is taken.
4. To uninstall: stop the EXE and delete the folder. Nothing is
   left behind.

### 3.3 Docker compose

1. From the repo root: `docker compose up -d`.
2. SQL Server 2022 container exposes 1433; web container exposes
   8088. Data is mounted from `./data/` on the host (override the
   path via the `DAFTARX_DATA_DIR` env var).
3. Stop with `docker compose down` (data persists in the volume).

---

## 4. First-run setup

### 4.1 License activation

Every install is hardware-bound. On first run the boot-time
`LicenseGate` prints a **Hardware ID (HWID)** in the activation
banner — a string like `017F-0D1A-1BA1-C968`.

The customer sends that HWID to sales. Sales runs
[`Issue-License.cmd`](Issue-License.cmd) (or the PowerShell
equivalent) to generate `license.token`, then sends the token back
to the customer.

Customer places the file at:

```
C:\ProgramData\DaftarX\license\license.token
```

…and restarts the EgyptTax service (`net stop EgyptTax && net start EgyptTax`).
The activation banner disappears.

See [§21 Licensing](#21-licensing) for the vendor-side flow.

### 4.2 Default login

After activation, browse to `http://localhost:8088/login` and sign in:

| Username | Password |
|---|---|
| `admin` | `Admin@2026!` |

You'll be asked to change the password and enrol an authenticator
(MFA is **optional** in v1.2 — you can skip enrolment and still
sign in). Use Google Authenticator / Microsoft Authenticator /
Authy to scan the QR code on `/mfa/enroll`.

### 4.3 Pick your tax regime

Go to **Settings → Company profile** (`/settings/company`). Fill:

- **Legal name** (Arabic + English) — appears on every PDF
- **Tax registration number (TIN)** — used by the ETA validator
- **Tax regime** — Standard (monthly VAT) or **Law 6/2025**
  (simplified: turnover tax + quarterly VAT). The choice is
  retroactive — switching after posting documents will recompute
  reports but not journal entries.
- **VAT registration date** — drives the VAT-recoverability cutoff

Save. The home dashboard immediately recalculates against the new
regime.

---

## 5. The home dashboard

URL: `/`. Visible after sign-in.

Five KPI cards at the top:

1. **Current period status** — Open / Locked. Shows when locked
   and by whom.
2. **VAT exposure** — output VAT, input VAT, payable (signed).
3. **ETA queue** — pending submissions, failed, critical
   (rejected with a regulator code).
4. **Penalty exposure** — Penalty Shield's current EGP figure +
   the savings if you fix everything today. Click to drill down.
5. **Risk** — drafts in period + posts missing attachments +
   approvals waiting on you.

Below the KPIs:

- **Activity feed** — last 30 audit events (posted invoices,
  approvals, ETA acknowledgements, lock/reopen).
- **Quick actions** — direct links to "new sales invoice", "new
  expense", "open closing cockpit", "run VAT report".

Everything on the dashboard is read-only; nothing posts.

---

## 6. Master-data setup (in order)

Recommended order — earlier rows are referenced by later ones:

| # | Page | URL | Why first? |
|---|---|---|---|
| 1 | Chart of accounts | `/settings/chart-of-accounts` | Used by every journal entry |
| 2 | VAT categories | `/settings/vat-categories` | Used by sales/purchase lines |
| 3 | WHT categories | `/settings/wht-categories` | Used by supplier payments + customer receipts |
| 4 | Payment methods | `/settings/payment-methods` | Used by vouchers |
| 5 | Cash accounts | `/settings/cash-accounts` | Used by vouchers + bank statements |
| 6 | Deductible-expense categories | `/settings/expense-categories` | Used by expenses |
| 7 | Fiscal year | `/settings/fiscal-year` | Used by income-tax periods |
| 8 | Items | `/items` | Used by sales/purchase invoices |
| 9 | Customers | `/customers` | Used by sales |
| 10 | Suppliers | `/suppliers` | Used by purchases + payments |
| 11 | Opening balances | `/settings/opening-balances` | Run **after** all the above |

### 6.1 Chart of accounts

Pre-seeded with the standard Egyptian SMB chart (4-digit account
codes). You can:

- Add accounts (e.g., `5310 Office supplies — Cairo branch`).
- Mark accounts as inactive — they hide from the JV form but stay
  in historical reports.
- **Cannot** delete an account once a journal entry references it.

Each account has a **type** (Asset / Liability / Equity / Revenue /
Expense) — drives the trial balance sign.

### 6.2 VAT categories

Standard rates pre-seeded:

| Code | Rate | Use case |
|---|---|---|
| `VAT-14` | 14% | Standard goods & services |
| `VAT-0` | 0% | Zero-rated (export, etc.) |
| `EXEMPT` | — | Exempt items (FR-020 non-recoverable) |
| `RC-14` | 14% | Reverse-charge imports |

The category links to (a) the VAT payable account on output and
(b) the VAT receivable account on input. **Don't delete** seeded
rows; create new ones for special cases.

### 6.3 WHT categories

WHT is **outbound** (you withhold from a supplier) or **inbound**
(a customer withholds from you). Each category sets:

- **Rate** (e.g., 5% for services per Law 28/2008)
- **Direction** (outbound / inbound)
- **Form-41 box** (which line of Form 41 it feeds)

Seeded with the seven standard Egyptian rates. The
`SupplierPaymentVoucher` post-handler reads the supplier's default
category to compute the split at post time.

### 6.4 Payment methods

Five seeded: Cash, BankTransfer, Cheque, CreditCard, MobileWallet.
Used as a tag on vouchers — they don't drive accounting (which
cashbox is hit comes from `CashAccountId`, see next).

### 6.5 Cash accounts (P3.1)

A cashbox **or** a bank account. Each has:

- **Code + display name** (Arabic + English)
- **Account type** — Cash / BankAccount
- **GL account** — links to a row in the chart of accounts (e.g.,
  `1110 Cash on hand — Main` or `1130 CIB current account`)
- **Default flag** — exactly one Cash + one BankAccount can be
  default. Vouchers without an explicit selection fall back to
  the default.

Adding a cash account is **mandatory** before posting any voucher.

### 6.6 Deductible-expense categories

Drives input-VAT recoverability and the taxable-income report.
Each row has:

- **Name** (Arabic + English)
- **Deductible?** — bool; non-deductible categories (e.g.,
  Entertainment) zero-out for income-tax purposes
- **Default expense GL account**
- **Default input-VAT category**

### 6.7 Fiscal year

Default: 1 January – 31 December. Override if the company runs on a
non-calendar fiscal (e.g., 1 July – 30 June). Drives the
income-tax periods table.

### 6.8 Items

Each item:

- **Code** + bilingual name + base unit (Each / Box / Litre / etc.)
- **Default VAT category**
- **ETA item code** — the 12-digit GS1 GPC code the regulator
  expects. Without one, the pre-flight ETA validator flags the
  invoice line and refuses to submit.
- **Item-code lifecycle** — Pending / Approved / Expired. The
  daily `EtaItemCodeCheckJob` checks Pending items against the
  ETA registry.

### 6.9 Customers

- **Code** + bilingual name + **postal address** (governorate /
  region / street / building / postal code — required for ETA's
  `address` shape)
- **Phone, email**
- **Tax profile**:
  - `B2BRegistered` — TIN + full structured address; output VAT
    applies normally; e-invoice has receiver TIN
  - `B2BUnregistered` — no TIN; output VAT applies; e-invoice
    sends the buyer's national ID if provided
  - `B2C` — natural person, name + phone optional, ETA receiver
    block is collapsed

Status: Active / Inactive (soft delete only — hard delete refused
when a posted document references the customer).

### 6.10 Suppliers

Symmetrical to customers, plus:

- **Tax profile**:
  - `RegisteredTaxpayer` — input VAT is recoverable
  - `Unregistered` — input VAT is **non-recoverable** (lands in
    expense, FR-020)
  - `ForeignSupplier` — reverse charge applies

The daily `SupplierTinRevalidationJob` re-checks every supplier's
TIN against the ETA registry. The "last-revalidated" timestamp
shows on the supplier row.

### 6.11 Opening balances (P2.2)

Captures the journal balances on the **DaftarX go-live date** so
the trial balance is correct from day one. Page `/settings/opening-balances`:

1. Set the **opening date** (typically your last filed period's
   end-of-day).
2. Enter each GL account's debit/credit. The form auto-balances
   the suspense row.
3. Click **Lock opening balances**. A single auto-generated JV is
   emitted with date = opening date. Future edits require an
   Administrator to reopen.

You **cannot** post any transaction dated **before** the opening
balances date.

---

## 7. Sales workflow

### 7.1 New sales invoice

URL: `/invoices/new`.

1. Pick customer, document date, payment terms.
2. Add lines: item / qty / unit price / discount % / VAT category.
3. Optional: invoice-level discount (% or fixed amount).
4. Save draft. URL becomes `/invoices/{id}/edit`.

The invoice totals are computed live: subtotal, line discounts,
invoice discount, VAT, grand total. **Drafts are editable; posted
invoices are not.**

### 7.2 Post the invoice

Click **Post** on the draft. The post handler runs (in order):

1. **License sentry** check.
2. **Tax-period lock guard** — refuses if the document date falls
   inside a locked VAT period.
3. **Pre-flight ETA validator** — runs 8 of the regulator's checks
   (missing TIN, missing ETA item code, etc.). Each finding is
   either `Blocker`, `MustFixBeforeFiling`, or `Informational`.
   Blockers refuse the post; the others allow it but show on the
   risk badge.
4. **Numbering** — allocates the next sequential `DocumentNumber`
   from the sales counter.
5. **JE emission** — emits `DR AR / CR Sales / CR VAT Payable` (or
   the WHT-split variant if the customer is set to withhold).
6. **ETA submission** — queued via Hangfire; the
   `EtaSubmissionRetryJob` posts to the regulator.

Outcome: draft moves to `Posted`. The detail page (`/invoices/{id}`)
shows the lines, totals, JE, and ETA status (Pending → Submitted →
Acknowledged → Rejected).

### 7.3 ETA status lifecycle

| State | Meaning | Next step |
|---|---|---|
| `PendingSubmission` | Queued, not sent yet | Wait for the retry job (≤1 min in demo, configurable) |
| `Submitted` | Sent to regulator, awaiting ack | Wait for the polling job (every 1 min) |
| `PendingAcknowledgement` | Regulator received it | Wait |
| `Acknowledged` | Regulator issued a long UUID | Done — citable on the customer's e-invoice |
| `Failed` (transport) | Network / 500 from regulator | Auto-retried 5× with backoff |
| `Failed` (rejected) | Regulator returned an error code | Use **ETA Error Translator** on `/eta-dashboard` to read the Arabic correction; usually requires void + re-issue |

### 7.4 Credit note

URL: `/invoices/{id}/credit-note/new`. Generates a credit note that
references the original invoice. Posts a reversal JE; if the
customer's payments allocated to the original are now unallocated,
they surface on `/payments/unmatched`.

### 7.5 Sales invoice list

URL: `/invoices`. Filter by state, customer, date range. Bulk
export to CSV via the export icon.

---

## 8. Purchases & expenses

Two surfaces because the journal effects differ:

- **Purchase invoice** (`/purchase-invoices/new`) — supplier
  invoice that lands AP + may include input VAT to recover. Used
  when the supplier issued a tax invoice (so the JE is `DR
  Expense + DR VAT Receivable / CR AP`).
- **Expense** (`/expenses/new`) — operating cost without a tax
  invoice (e.g., a phone bill in cash). JE is `DR Expense / CR
  Cash`. No input VAT line.

### 8.1 Purchase invoice

1. Pick supplier, document date, supplier invoice number.
2. Add lines (item or free-text + amount + VAT category).
3. Attach the supplier's PDF/JPEG. **Attachments are mandatory**
   for deductible posts — the post handler refuses otherwise.
4. Post.

Post-time guards:

- License sentry
- Period lock (FR-037)
- Attachment presence (FR-051)
- Allocation cap (the sum of payment allocations to this invoice
  can't exceed the gross — FR-053)

### 8.2 Expense

1. Pick deductible category (drives the GL + VAT).
2. Enter amount, payment method, cashbox.
3. Attach receipt.
4. Post.

If category is non-deductible, the expense lands as an entry but
won't reduce taxable income.

### 8.3 Expense detail page

URL: `/expenses/{id}`. Shows the JE, attachments, audit trail.
Click **void** to reverse (Administrator only; produces an offset
JV).

---

## 9. Payments

Two voucher types:

- **Supplier payment voucher (SPV)** — you pay a supplier
- **Customer receipt voucher (CRV)** — a customer pays you

Both follow the same lifecycle.

### 9.1 New supplier payment voucher

URL: `/payments/supplier-payments/new`.

1. Pick supplier + payment date + payment method + cashbox
   (`CashAccountId`).
2. Enter the gross payment amount.
3. Add allocations — pick one or more outstanding purchase
   invoices and how much to allocate. The form caps each
   allocation at the invoice's open balance and the voucher's
   gross.
4. Save draft.
5. Post.

Post-time:

- License sentry, period guard.
- **WHT split** — if the supplier has a default WHT category, the
  handler subtracts the WHT amount, generates a WHT certificate,
  and emits the JE: `DR AP (gross) / CR Cash (net) + CR WHT
  Payable (wht)`.
- **JE** — uses the selected cash account.

### 9.2 New customer receipt voucher

URL: `/payments/customer-receipts/new`. Symmetrical to the SPV:
allocates the receipt to outstanding sales invoices; if the
customer withheld, captures the WHT receivable.

### 9.3 Payment allocation view

URL: `/payments/allocations/{InvoiceType}/{InvoiceId}`. For any
posted invoice, shows the allocation history (which vouchers
touched it + how much). Read-only — to change, void the voucher.

---

## 10. Banking & bank-recon

### 10.1 Bank statement list

URL: `/payments/bank-statements`. One row per imported statement.
Click a row to drill into lines.

### 10.2 Import a bank statement

URL: `/payments/bank-statements/import`.

1. Pick the cash account (must be type `BankAccount`).
2. Period start + end.
3. Opening + closing balance.
4. Paste lines (CSV) **or** type them manually:
   - Transaction date, description, debit, credit, running
     balance, bank reference (optional).
5. Self-check: total debits + credits + opening must equal the
   closing balance. The form refuses to save if it doesn't.
6. Save.

> **P3.3** (PDF parsers per bank) is on the roadmap. Until then,
> import via CSV exported from the bank's portal.

### 10.3 Unmatched queue + auto-match (P3.4)

URL: `/payments/unmatched`. Two tabs:

- **Engine suggestions** — lines the
  `BankAutoMatchJob` scored at 70–94% confidence; they're
  pending review. The scorer adds points for amount proximity
  (50), date proximity (25), and counterparty fuzzy match (25).
  - **Approve** — promotes the suggestion to a real match (JE
    side: nothing extra; the voucher is already posted).
  - **Reject** — returns the line to the manual queue so the
    operator can match by hand.
- **Manual queue** — Unmatched lines with no suggestion. Shows
  candidate vouchers within ±2% of the line amount; click one
  to match.

The job runs **every 10 minutes**. At ≥95% confidence it
auto-matches without asking (system user). At <70% it leaves the
line Unmatched.

### 10.4 Ignoring a line

A line you'll never match (interbank transfer, bank fee already
booked) — click **Ignore** in either tab. It stays on the
statement for audit but disappears from the unmatched queue.

---

## 11. ETA

### 11.1 First-time setup wizard

URL: `/eta-wizard`. Five steps:

1. **TIN** — type the company's TIN. The taxpayer-lookup service
   validates it against the registry (or mock in demo).
2. **eSeal certificate** — choose USB token (default) or
   file-based PFX; type the thumbprint or PFX path. Production
   submissions need a valid eSeal.
3. **Activity codes** — search + multi-select your registered
   activity codes (drives the e-invoice's `activityCode` field).
4. **Environment** — Preprod (sandbox) or Prod.
5. **Test invoice** — submits a sample invoice. If it returns an
   Acknowledged UUID, setup is complete.

You can re-run the wizard at any time (e.g., when your eSeal
expires).

### 11.2 ETA dashboard

URL: `/eta-dashboard`. Three panels:

- **Failed submissions** — grouped by error code, with the
  Arabic translation + suggested fix.
- **Pending submissions** — what's queued.
- **Acknowledged** — paginated list of completed submissions.

Click any row to drill into the submission detail (the raw JSON
sent, the regulator's response, the retry history).

### 11.3 ETA inbox

URL: `/eta-inbox`. Documents the regulator sent **to you**
(supplier-issued invoices, credit notes addressed to your TIN).
You can convert each into a draft Purchase Invoice with one click;
the `EtaReceivedInboxJob` polls hourly.

### 11.4 ETA export (P1.14)

URL: `/eta-export`. Generates the regulator's annual
"taxpayer-data" CSV bundle for inspection requests. Pick year +
month range; the export includes every posted sales + purchase
invoice with its ETA UUID. Bundles into a single ZIP.

### 11.5 Certificates

URL: `/certificates`. Inventory of eSeal certificates currently
registered. Shows expiry date + warns ≥30 days before. Add /
remove via the form.

---

## 12. WHT

### 12.1 WHT dashboard

URL: `/wht`. Three columns:

- **Pending outbound certificates** — WHT you withheld this
  quarter, not yet declared on Form 41.
- **Pending inbound certificates** — WHT a customer withheld from
  you (captured on the CRV).
- **Filed Form 41s** — past filings.

### 12.2 Form 41 list

URL: `/wht/form41`. One row per filing.

### 12.3 Generate a Form 41

URL: `/wht/form41/new`. Pick the quarter; the generator pulls
every WHT certificate posted in that quarter and groups them by
WHT category (= Form-41 box). Review the totals, fill in the
declaring person's name, click **Generate**. The result is a
print-ready Form 41 PDF; the underlying state moves to `Filed`.

### 12.4 Inbound WHT import

URL: `/wht/inbound`. Bulk upload of customer-issued WHT
certificates (CSV / Excel). Each row is matched to its CRV by
voucher number; matched rows update the certificate ID + amount.

---

## 13. Fixed assets

### 13.1 Fixed asset list

URL: `/fixed-assets`. Each asset:

- Code, name, acquisition date, cost
- Depreciation method (straight-line by default)
- Useful life (months)
- Status: Draft / InService / Disposed

### 13.2 New / edit asset

URL: `/fixed-assets/new` or `/fixed-assets/{id}/edit`. Form. Save
draft. Click **Place in service** to move to InService — locks
cost + life.

### 13.3 Depreciation schedule

URL: `/fixed-assets/{id}/schedule`. Read-only — shows the full
month-by-month schedule.

### 13.4 Monthly depreciation job

A Hangfire cron job runs on the 1st of every month. For each
InService asset, it emits a balanced auto-JV: `DR Depreciation
Expense + CR Accumulated Depreciation`. Idempotent — re-runs of
the same period are no-ops.

### 13.5 Dispose an asset

On the detail page, click **Dispose** + supply the disposal date.
Emits the reversing JE; status moves to Disposed.

---

## 14. Period closing

### 14.1 The Closing Cockpit (`/cockpit`)

The single landing surface for "am I ready to lock May?". Sections:

1. **Period status** — Open / Locked + lock metadata
2. **VAT readiness** — % of posts with no blocking findings
3. **Period-lock checklist** — every required check + ✓/✗
4. **Drafts in period** — count + drill-down list
5. **Failed ETA submissions** — count + total EGP
6. **Missing documents** — buckets (deductible posts without
   attachments, posted invoices without ETA, etc.)
7. **Non-recoverable input VAT** — informational total (FR-020)
8. **Reports for this period** — direct links to VAT-monthly,
   taxable-income, trial-balance reports

### 14.2 Lock the period (P3.6 + P2.5)

At the bottom of the cockpit: **Lock this period** section. The
button is enabled only when the gate decides the period is safe:

- **Hard blockers** (drafts, failed ETA) → button disabled, error
  card lists each item.
- **Soft blockers** (missing documents) → button still allowed
  but a **Force-lock reason** input becomes required.
- **No blockers** → optional reason field, button enabled.

Click **Lock period**. Behind the scenes the handler:

1. Re-evaluates the gate against fresh DB state (catches the
   race where someone else posted a draft in another tab).
2. If allowed, sets the period status to Locked, audit-logs
   `tax_period.locked` (or `tax_period.locked.forced` if the
   force-lock reason was filled — the audit payload includes the
   reason + soft-blocker counts).

### 14.3 Reopen a period

URL: `/settings/tax-periods`. Find the Locked row → click
**Reopen** → supply a reason. Audit-logs `tax_period.reopened`.
Administrator role only.

### 14.4 Backdate enforcement

Once a period is Locked, the period-lock guard refuses any post
whose document date falls inside it (FR-037 / SC-004). The error
shown is the lock metadata (locked-at + locked-by).

---

## 15. Reports

All reports support: pick period → run → CSV export.

### 15.1 VAT monthly

URL: `/reports/vat-monthly`. Standard regime; produces the
regulator's monthly VAT-return shape: output VAT by category,
input VAT, non-recoverable input VAT, net payable. Tip: cross-
check against the cockpit's VAT-readiness panel.

### 15.2 Taxable income

URL: `/reports/taxable-income`. Year-to-date P&L computation with
non-deductible expenses zeroed out. Forms the basis of the annual
income-tax return.

### 15.3 Trial balance

URL: `/reports/trial-balance`. Standard trial balance with
opening, period movements, closing.

### 15.4 Turnover-tax report (Law 6/2025)

URL: `/reports/turnover-tax`. Only visible when the company's tax
regime is set to Law 6/2025. Computes turnover-tax brackets per
the simplified-regime rules.

### 15.5 Journal ledger

URL: `/journals` (list) and `/journals/{id}` (detail). Every
auto-generated and manual JV is here. Filter by account, date,
source-document kind.

---

## 16. Penalty Shield & Compliance Calendar

### 16.1 Penalty Shield

URL: `/penalty-shield`. Computes the current penalty exposure for:

- Late VAT filings
- Late income-tax filings
- Late WHT filings
- Unsubmitted ETA invoices over their submission window

Each row shows: **penalty now**, **penalty in 30 days**, **what
to do** (e.g., "Submit the ETA invoice today to avoid 1% / day").

### 16.2 Compliance calendar

URL: `/compliance/calendar`. Materialised view of every upcoming
filing obligation for the current + next year. The
`ComplianceCalendarRefreshJob` runs daily and inserts missing
obligations.

---

## 17. Audit log, Document 360 & Inspection bundle

### 17.1 Audit log viewer

URL: `/audit-log`. The full FR-028 audit trail — every event has
a SHA-256 hash that chains to the previous event. Filter by
kind, actor, date. Each row shows: timestamp, actor, kind,
JSON payload, hash.

The `AuditCheckpointJob` runs daily and stamps a checkpoint —
any future tampering breaks the chain and the next checkpoint
fails verification.

### 17.2 Document 360

URL: `/document360/{Kind}/{Id}`. One unified detail view for any
document type (SalesInvoice, PurchaseInvoice, Expense, JV,
SupplierPaymentVoucher, CustomerReceiptVoucher, FixedAsset).
Shows:

- Header + lines
- Journal entry rows
- Attachments (with hash for integrity)
- Allocations / payments
- ETA submission (if applicable)
- Audit trail filtered to this document

### 17.3 Inspection bundle

URL: `/inspection-bundle`. Generates a single ZIP containing
everything an ETA inspector would ask for: every posted invoice
PDF + every JV + audit log + opening balances + trial balance +
VAT report for a year range. Pick the year, click **Generate**.

---

## 18. Approvals & Firm Portal

### 18.1 My approval queue

URL: `/approvals`. Documents waiting on your approval. The
approval policy (per DocumentType) decides who approves:

- Sales invoice above EGP X → require a senior accountant
- Credit note → require an Administrator
- Purchase invoice above EGP Y → require approval

Click **Approve** or **Reject** with a reason. The audit log
records the decision.

### 18.2 Firm Portal — multi-company

URL: `/firm-portal`. Visible when the user has access to more
than one company.

1. **`/firm-portal/switch`** — pick the company you're working
   on. The session's `CompanyId` claim is rewritten; all
   subsequent queries scope to that company.
2. **`/firm-portal`** — "Accountant review mode" landing — a
   summary across all companies the firm services (this month's
   open drafts, failed ETA, period-lock readiness).

---

## 19. Settings reference

| Page | URL | What's there |
|---|---|---|
| Company profile | `/settings/company` | Legal name, TIN, tax regime, fiscal year, eSeal |
| Chart of accounts | `/settings/chart-of-accounts` | GL accounts |
| VAT categories | `/settings/vat-categories` | Rates + GL mapping |
| WHT categories | `/settings/wht-categories` | Rates + Form-41 box |
| Payment methods | `/settings/payment-methods` | Cash / Cheque / etc. |
| Cash accounts | `/settings/cash-accounts` | Cashboxes & bank accounts |
| Expense categories | `/settings/expense-categories` | Deductible-expense library |
| Fiscal year | `/settings/fiscal-year` | Income-tax year shape |
| Opening balances | `/settings/opening-balances` | Go-live JV |
| Tax periods | `/settings/tax-periods` | Lock / reopen periods |

---

## 20. Operator / admin tasks

### 20.1 Backup

DaftarX runs in **append-only journal** mode — every JV row is
immutable once inserted. Backup the DB **and** the
`%PROGRAMDATA%\DaftarX\` folder.

**SQL Server install:**
```powershell
sqlcmd -S .\SQLEXPRESS -E -Q "BACKUP DATABASE EgyptTax TO DISK = 'D:\Backups\EgyptTax-$(Get-Date -Format yyyyMMdd).bak' WITH COMPRESSION, INIT;"
```

**Portable EXE:** copy `data.db` next to the EXE.

Schedule the backup with Windows Task Scheduler — daily at 01:00 is
typical.

### 20.2 Restore

Stop the service, restore the `.bak`, restart. The migration runner
on next boot is a no-op if the DB version matches.

### 20.3 Diagnose a broken install

Double-click `Diagnose-DaftarX.cmd` from the install folder. Auto-
elevates, writes `daftarx-diagnostic.txt`. Contains: service
status, last 200 lines of the service log, SQL connection check,
firewall rule presence, license-gate state, last 10 audit events.

Email or Telegram the txt file to support.

### 20.4 Uninstall

1. Stop the service: `net stop EgyptTax`.
2. Run `Uninstall-DaftarX.cmd` from the install folder (or use
   Apps & Features). Removes the program files, firewall rule,
   service entry.
3. The DB and `%PROGRAMDATA%\DaftarX\` folder are **not** touched
   — delete them manually if you really want a clean slate.

### 20.5 Service control

Common commands (run as Administrator):

```powershell
net stop  EgyptTax
net start EgyptTax
Restart-Service EgyptTax
sc.exe query EgyptTax
```

The service writes to the Windows Event Viewer under "Application"
+ source `EgyptTax`. Tail the log:

```powershell
Get-EventLog -LogName Application -Source EgyptTax -Newest 50
```

### 20.6 Common config overrides

`appsettings.Production.json` at the install root takes precedence
over the built-in defaults:

```json
{
  "ConnectionStrings": {
    "EgyptTax": "Server=.\\SQLEXPRESS;Database=EgyptTax;Trusted_Connection=true;TrustServerCertificate=true;",
    "EgyptTax_Hangfire": "Server=.\\SQLEXPRESS;Database=EgyptTax_Hangfire;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://+:8088" } } },
  "Eta": { "Environment": "Production" }
}
```

Restart the service after every change.

---

## 21. Licensing

### 21.1 Customer-side

Already covered in [§4.1](#41-license-activation). One sentence:
copy the vendor-supplied `license.token` to
`%PROGRAMDATA%\DaftarX\license\` and restart the service.

### 21.2 Vendor-side — issue a license

From the repo root, use [`Issue-License.cmd`](Issue-License.cmd)
(double-click for interactive prompts) or PowerShell:

```powershell
.\Issue-License.ps1 -Hwid 017F-0D1A-1BA1-C968 -Customer "Hope Co" -Expires 2027-12-31
```

The script:

1. Validates the HWID shape.
2. Runs `dotnet run -- license-issue` with absolute paths so
   `vendor-keys.json` is found regardless of cwd.
3. Drops `license.token` + a customer-facing `README.txt` into
   `licenses\<HWID>\`.
4. The folder is gitignored.

### 21.3 Rotate the signing keypair

Run `dotnet run --project src/EgyptTax.Web -- license-keygen --out
vendor-keys.json`. Then patch the new public key into
`src/EgyptTax.Web/Licensing/LicensePublicKey.cs` and re-publish the
installer. **Every existing customer's license becomes invalid**
— rotation is an emergency action (e.g., key compromise).

### 21.4 Activation flow internals

For the curious: the flow is in
[`LicenseGate.cs`](src/EgyptTax.Web/Licensing/LicenseGate.cs):

1. Hardware ID derived from CPU + motherboard + disk volume serial
   via WMI (cached in registry once computed).
2. Verifier checks the Ed25519 signature on `license.token`
   against the public key baked into the binary.
3. If valid, a 256-bit master key is generated and split via
   Shamir's Secret Sharing (2-of-3): one share to DPAPI-encrypted
   file, one to registry, one HWID-derived. Reconstructed every
   boot.
4. Master key encrypts `license.activated` (AES-256-GCM); on
   subsequent boots the gate reads that instead of re-verifying
   the token.

---

## 22. Troubleshooting & FAQ

### `http://localhost:8088` returns `ERR_CONNECTION_REFUSED`

The service isn't running. Open an elevated PowerShell:

```powershell
sc.exe query EgyptTax
net start EgyptTax
```

If `net start` fails with `Error 1067`, run `Diagnose-DaftarX.cmd`
and send the output to support — usually a config / DB-connection
issue.

### Activation banner won't go away

1. Confirm the file exists at `C:\ProgramData\DaftarX\license\license.token`.
2. Confirm the HWID inside the JSON matches the HWID the banner
   shows. If it doesn't, the customer copied someone else's
   license — request a new one for the current HWID.
3. Confirm the expiry hasn't lapsed.
4. Check `license-gate-crash.log` next to the token for a stack.

### Login says "invalid credentials" but I'm sure they're right

If you reset SQL Express's `sa` password recently, the service
account may be locked out. Run `grant-sql-permissions.ps1` from
the install folder as Administrator.

### ETA submission stuck on `Pending` for hours

Check `/eta-dashboard`. If the retry job is healthy, the regulator
is probably down — the retry will land when their endpoint comes
back. If you see a stack trace in the submission's "last error",
the eSeal cert is likely expired — re-run the ETA wizard.

### "Cannot lock period — N draft document(s)…"

The Closing Cockpit gate refused. Open `/cockpit`, scroll to
"Drafts in period", post or void each, then retry. See
[§14.2](#142-lock-the-period-p36--p25).

### "Login failed for user 'NT AUTHORITY\NETWORK SERVICE'"

The service can't read the DB. Run
`grant-sql-permissions.ps1` as Administrator. Restart the service.

### The bank-statement totals don't add up

The form refuses to save when (opening + credits − debits ≠
closing). Most often a typo — re-check the CSV. The "auto-balance"
checkbox lets you accept the calculated closing instead of typing it.

### I posted an invoice to the wrong customer

Posted invoices are immutable (FR-027). Issue a credit note that
zeroes the original (`/invoices/{id}/credit-note/new`), then post
a fresh invoice to the correct customer.

### Force-locking a period — who can?

Administrator role only (DocumentType "TaxPeriod" approval
policy). The reason becomes part of the audit trail forever — be
specific ("Attachments are in the mail; filing on June 12
deadline" beats "force lock").

---

## 23. Roles & permissions

| Role | Can do | Cannot do |
|---|---|---|
| **Administrator** | Everything | — |
| **SeniorAccountant** | Post + approve, edit master data, run reports, lock period | Reopen a locked period, force-lock with soft blockers, change tax regime |
| **Bookkeeper** | Create + edit drafts, post (if below approval threshold) | Approve large documents, edit posted documents, lock/reopen periods |
| **Auditor** | Read-only across everything + audit log + inspection bundle | Any write operation |
| **FirmManager** | Switch companies, view Firm Portal | — (a senior role above SeniorAccountant scoped to the firm) |

Role-by-page enforcement uses ASP.NET Core's `[Authorize(Policy =
…)]`. The policies are defined in
[`Program.cs`](src/EgyptTax.Web/Program.cs).

---

## 24. Glossary

- **ETA** — Egyptian Tax Authority (eta.gov.eg). The e-invoicing
  regulator.
- **TIN** — Tax Identification Number.
- **VAT** — Value Added Tax (14% standard rate in Egypt).
- **WHT** — Withholding Tax (Law 28/2008).
- **JV / JE** — Journal Voucher / Journal Entry. The accounting
  building block.
- **AR / AP** — Accounts Receivable / Payable. GL parent accounts.
- **SPV / CRV** — Supplier Payment Voucher / Customer Receipt
  Voucher.
- **FR-### / SC-### / R-##** — spec reference codes (Functional
  Requirement / Success Criterion / Research note) in
  [specs/008-egypt-tax-accounting/](specs/008-egypt-tax-accounting/).
- **HWID** — Hardware ID. Bound to one machine for licensing.
- **eSeal** — the company's signing certificate registered with
  ETA, used to sign every submission.
- **Form 41** — quarterly WHT return filed with ETA.
- **Penalty Shield** — DaftarX's forward-looking penalty
  estimator.
- **Closing Cockpit** — the single readiness-and-lock surface at
  `/cockpit`.
- **Auto-match** — the Hangfire job that ties bank-statement
  lines to vouchers without operator action when confidence ≥95%.
- **Pre-flight validator** — local replica of 8 ETA validations
  that runs before submission to kill avoidable rejections.

---

## Appendix A — Page index (by URL)

| URL | Page |
|---|---|
| `/` | Home dashboard |
| `/login` | Login |
| `/logout` | Logout |
| `/password/change` | Change password |
| `/mfa/enroll` | Enrol MFA |
| `/invoices` | Sales invoice list |
| `/invoices/new` | New sales invoice |
| `/invoices/{id}/edit` | Edit draft sales invoice |
| `/invoices/{id}` | Sales invoice detail |
| `/invoices/{id}/credit-note/new` | Issue credit note |
| `/purchase-invoices` | Purchase invoice list |
| `/purchase-invoices/new` | New purchase invoice |
| `/purchase-invoices/{id}/edit` | Edit draft purchase invoice |
| `/purchase-invoices/{id}` | Purchase invoice detail |
| `/expenses` | Expense list |
| `/expenses/new` | New expense |
| `/expenses/{id}/edit` | Edit draft expense |
| `/expenses/{id}` | Expense detail |
| `/payments/supplier-payments/new` | New supplier payment voucher |
| `/payments/customer-receipts/new` | New customer receipt voucher |
| `/payments/allocations/{type}/{id}` | Payment allocation view |
| `/payments/bank-statements` | Bank statement list |
| `/payments/bank-statements/import` | Import bank statement |
| `/payments/unmatched` | Unmatched queue (engine + manual) |
| `/eta-wizard` | ETA first-time setup |
| `/eta-dashboard` | ETA submissions dashboard |
| `/eta-inbox` | ETA inbox (regulator → you) |
| `/eta-export` | ETA annual export bundle |
| `/certificates` | eSeal certificates |
| `/wht` | WHT dashboard |
| `/wht/form41` | Form 41 list |
| `/wht/form41/new` | Generate a Form 41 |
| `/wht/inbound` | Inbound WHT import |
| `/fixed-assets` | Fixed asset list |
| `/fixed-assets/new` | New fixed asset |
| `/fixed-assets/{id}/edit` | Edit fixed asset |
| `/fixed-assets/{id}/schedule` | Depreciation schedule |
| `/cockpit` | Closing Cockpit |
| `/penalty-shield` | Penalty Shield |
| `/compliance/calendar` | Compliance calendar |
| `/reports/vat-monthly` | VAT-monthly report |
| `/reports/taxable-income` | Taxable-income report |
| `/reports/trial-balance` | Trial balance |
| `/reports/turnover-tax` | Turnover-tax report (Law 6/2025) |
| `/journals` | Journal list |
| `/journals/{id}` | Journal detail |
| `/audit-log` | Audit log viewer |
| `/document360/{kind}/{id}` | Document 360 view |
| `/inspection-bundle` | Inspection bundle generator |
| `/approvals` | My approval queue |
| `/firm-portal` | Firm Portal landing (review mode) |
| `/firm-portal/switch` | Company switcher |
| `/customers` | Customers |
| `/suppliers` | Suppliers |
| `/items` | Items |
| `/expense-categories` | Master-data expense categories |
| `/settings/company` | Company profile |
| `/settings/chart-of-accounts` | Chart of accounts |
| `/settings/vat-categories` | VAT categories |
| `/settings/wht-categories` | WHT categories |
| `/settings/payment-methods` | Payment methods |
| `/settings/cash-accounts` | Cash accounts (cashboxes + bank) |
| `/settings/expense-categories` | Deductible-expense categories |
| `/settings/fiscal-year` | Fiscal year |
| `/settings/opening-balances` | Opening balances |
| `/settings/tax-periods` | Tax periods (lock/reopen list) |

---

## Appendix B — Background jobs (Hangfire)

| Job | Cron | What it does |
|---|---|---|
| `eta-submission-retry` | `*/1 * * * *` | Sends queued ETA submissions; retries failed ones with backoff |
| `eta-status-poll` | `*/1 * * * *` | Polls regulator for ack on submitted invoices |
| `eta-received-inbox` | `0 * * * *` | Pulls regulator-delivered documents into the ETA inbox |
| `eta-item-code-check` | `* * * * *` | Validates Pending item codes against ETA registry |
| `compliance-calendar-refresh` | `*/5 * * * *` | Materialises upcoming filing obligations |
| `supplier-tin-revalidation` | `0 3 * * *` | Re-checks supplier TINs daily |
| `monthly-depreciation` | `0 2 1 * *` | Emits monthly DR Dep Exp / CR Acc Dep for InService assets |
| `bank-auto-match` | `*/10 * * * *` | P3.4 — scores unmatched bank lines, suggests / auto-matches |
| `ntp-health-check` | `0 */6 * * *` | Verifies system clock is within tolerance for ETA timestamping |
| `audit-checkpoint` | `0 4 * * *` | Computes the daily SHA-256 audit checkpoint |
| `inspection-bundle` | manual | Run on demand from `/inspection-bundle` |

Most are visible on Hangfire's built-in dashboard at
`http://localhost:8088/jobs` (Administrator-only).

---

## Appendix C — Files & folders

| Path | Purpose |
|---|---|
| `C:\Program Files\DaftarX\` | Install root (MSI) |
| `C:\ProgramData\DaftarX\` | Per-machine data (license, logs) |
| `C:\ProgramData\DaftarX\license\license.token` | Customer's signed license |
| `C:\ProgramData\DaftarX\license\license.activated` | Encrypted activated-state cache |
| `C:\ProgramData\DaftarX\logs\` | Rolling Serilog files |
| `%APPDATA%\DaftarX\share.bin` | Shamir share #1 (DPAPI-encrypted) |
| `HKCU\Software\DaftarX\Activation\ShareData` | Shamir share #2 (registry) |
| `vendor-keys.json` (repo root, gitignored) | Vendor's Ed25519 private signing key |
| `licenses/` (repo root, gitignored) | Issued customer licenses |
| `data.db` (portable EXE only) | SQLite database |

---

*End of guide. Suggestions / corrections welcome — open an issue
or ping support.*
