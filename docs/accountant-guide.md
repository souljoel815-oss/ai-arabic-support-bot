# Accountant Guide — Day-to-day workflows

**Audience**: Accountants, bookkeepers, and approvers using EgyptTax for day-to-day Egyptian SME accounting + tax compliance.

**Scope**: How to perform the recurring tasks the system was built for — issue invoices, record purchases, run reports, close periods, hand off to inspectors. Bilingual UI (Arabic + English) — the screen captions in this guide use English; the same labels appear in Arabic when the language toggle is set to AR.

**Not covered**: Initial installation + operator setup — see [`operator-runbook.md`](operator-runbook.md). The format of the inspector bundle — see [`inspector-bundle-format.md`](inspector-bundle-format.md).

---

## Table of contents

1. [Sign in + first-time setup](#1-sign-in--first-time-setup)
2. [Sales invoices (US1)](#2-sales-invoices-us1)
3. [Purchase invoices + expenses (US2)](#3-purchase-invoices--expenses-us2)
4. [Approval workflow (US3)](#4-approval-workflow-us3)
5. [Journal vouchers + reversals (US4)](#5-journal-vouchers--reversals-us4)
6. [Configurable rules (US5)](#6-configurable-rules-us5)
7. [Fixed assets + depreciation (US6)](#7-fixed-assets--depreciation-us6)
8. [WHT certificates + Form 41 (US7)](#8-wht-certificates--form-41-us7)
9. [Payment vouchers (Phase 9)](#9-payment-vouchers-phase-9)
10. [Closing cockpit (Differentiator 2)](#10-closing-cockpit-differentiator-2)
11. [Firm portal — multi-client review (US8)](#11-firm-portal--multi-client-review-us8)
12. [Inspection bundles (US9)](#12-inspection-bundles-us9)
13. [Reports](#13-reports)
14. [Period close — the monthly ritual](#14-period-close--the-monthly-ritual)

---

## 1. Sign in + first-time setup

### Sign in

Open the EgyptTax URL the operator gave you (typically `https://<server-name>/`). Enter your email + password. If the operator enrolled MFA on your account, you'll be prompted for the 6-digit TOTP code from your authenticator app.

**First sign-in**: you'll be required to change your password before you can do anything else. Pick something you won't reuse on another system; the password is stored as an Argon2id hash so even the system administrator can't recover it.

### Pick your language

Top-right of every page has an AR/EN toggle. Your choice survives across sessions (cookie-backed).

### Sign out

Click "Sign out" in the top nav. Your session also auto-times-out after a period of inactivity — sign back in to continue.

---

## 2. Sales invoices (US1)

### Issue a new invoice

1. **Navigate**: nav → "Sales invoices" → "+ New".
2. **Pick a customer**: dropdown lists every active customer. If the customer doesn't exist, click "Customers" in the nav, add them (TIN, name, address, default VAT category), then return to the invoice draft.
3. **Add lines**: per line, pick the item (or type a free-text description if no Item exists yet), enter quantity + unit price + VAT category. The system computes line subtotal + line VAT automatically.
4. **Optional invoice-level discount**: type a percentage or absolute EGP amount.
5. **Save Draft**: the invoice is persisted but not yet posted — you can edit, delete, or come back to it.
6. **Post**: clicks the document state from `Draft → Posted`. The system:
   - Allocates the next sequential `INV-{year}-{n}` number from the FR-011 gap-free allocator.
   - Generates a PDF (bilingual A4) with embedded QR seal.
   - Submits to ETA (mock or live, depending on installation).
   - Writes a balanced journal entry: DR Accounts Receivable / CR Sales Revenue + CR Output VAT Payable.
   - Appends to the FR-028 audit chain.

Once posted the invoice is **immutable** — you cannot edit it. To correct a posted invoice, issue a credit note (see below).

### Credit notes

Open any posted sales invoice → "Issue credit note". Pick the lines + quantities to credit (typically negate the original quantities for a full reversal). Enter the legally-required justification reason. Posting follows the same flow; the credit note carries `CN-{year}-{n}` numbering and contributes negatively to revenue.

### Verify a QR seal

Open a posted invoice → click the QR image to download. On any phone with a QR reader, scanning navigates to `/api/v1/verify/{seal}` on your installation, which returns `VALID` (with the document grand total + customer + date) or `TAMPERED` (with the specific mismatched field).

The seal is offline-verifiable — it carries the document's hash + a signature so a tax inspector can verify provenance without internet access.

### View ETA submission status

Nav → "ETA Dashboard". Three columns:
- **Pending**: submissions queued for retry.
- **Failed**: submissions where ETA returned an error — click for the error detail + retry button.
- **Submitted (last 7 days)**: recent successful submissions.

The dashboard auto-refreshes via SignalR; you don't need to F5.

---

## 3. Purchase invoices + expenses (US2)

### Record a purchase invoice

1. Nav → "Purchase invoices" → "+ New".
2. Pick the supplier (add via "Suppliers" if needed; supplier TIN must be valid).
3. Add lines — each line carries:
   - The item or expense category.
   - Quantity + unit price + VAT category.
   - **Deductible flag**: per-line. Default comes from the supplier + category but you can override per-line; the override is recorded in the audit log so an inspector can trace any "marked deductible against type-default" decision.
4. **Attach receipts** (FR-016): if any line is marked deductible, you MUST attach a receipt PDF / JPEG / PNG before posting. The Post button is disabled until at least one attachment is on file.
5. Save Draft → Post. Posts as `PI-{year}-{n}`; emits a balanced JE.

### Record a stand-alone expense

Same flow as a purchase invoice but without a supplier (use the "Expenses" nav). Use this for petty-cash receipts and similar. The deductible flag + attachment requirement work identically.

### Recoverable input VAT (FR-020)

The system automatically classifies input VAT as recoverable / non-recoverable based on the supplier's tax-profile snapshot **at posting time**:

- Supplier marked `RegisteredTaxpayer` → input VAT recoverable, DR Input VAT Recoverable.
- Otherwise → input VAT not recoverable, the gross amount goes to the expense account.

The snapshot is frozen at post time so a future change to the supplier's profile does not retroactively change the journal effect (that would break audit trails).

---

## 4. Approval workflow (US3)

Documents that require approval (configurable per-document-type — sales invoices, purchase invoices, expenses, journal vouchers, fixed-asset capitalizations) follow a **Submit → Approve → Post** flow instead of direct posting:

1. Bookkeeper saves a Draft.
2. Bookkeeper clicks **Submit for approval**. State flips to `Submitted`.
3. Approver opens "My approvals" in the nav. The queue shows every Submitted document needing their attention.
4. Approver clicks **Approve** or **Reject (with reason)**.
5. On Approve, the document state is `Approved`. The bookkeeper (or any operator with post permission) can now Post it.
6. On Reject, the document returns to Draft with the rejection reason recorded; the bookkeeper edits + re-submits.

**Separation of duties (FR-004)**: the approver MUST be a different user than the submitter — the system enforces this. A bookkeeper cannot self-approve.

The approval queue is auto-refreshed via SignalR.

---

## 5. Journal vouchers + reversals (US4)

### Manual adjusting voucher (FR-031)

For corrections that don't fit the standard document model (e.g. reclassifying an account at month-end, recording a year-end adjustment):

1. Nav → "Journals" → "+ New voucher".
2. Pick the voucher date.
3. Enter free-text description (the legally-required justification).
4. Add lines — each line is `DR amount` OR `CR amount` (not both) + an account from the chart of accounts.
5. The total DR MUST equal total CR — the system blocks Post until balanced.
6. Save Draft → Submit (if approval required) → Approve → Post.

### Reversal voucher

To unwind a previously-posted journal entry: open the source journal → "Issue reversal". The system pre-fills a new voucher with every line's amount negated. Edit the date + description, then post.

The reversal contains a back-pointer to the original voucher; the original voucher's detail page surfaces a "reversed by JV-{year}-{n}" badge so the audit trail is visible at a glance.

---

## 6. Configurable rules (US5)

### VAT categories (FR-022)

Nav → "VAT categories". Each row carries a code (e.g. `Standard`, `Reduced`, `Zero`, `Exempt`), a rate %, and an effective-from / optional effective-to date range. The system uses the **document date** of each invoice to pick the applicable rate — historical invoices keep their original rate even after you add a newer rate.

To change a rate:
- **DON'T** edit the existing row — that would retroactively change historical invoices.
- **DO** add a NEW row with the same code + the new rate + the effective-from date when the new rate kicks in. Cap the older row's effective-to date strictly before the new effective-from. The list page flags overlapping windows in red so you catch operator errors.

### WHT categories (FR-045)

Same model as VAT — see "WHT categories" in the nav. Code + rate + effective-from-dated; "applies to" picker (Suppliers / Customers / Both) determines whether the category appears on supplier-payment vs customer-receipt screens.

### Deductible expense categories (FR-015)

Nav → "Expense categories (settings)". Each category has a default deductible flag + default account. **Editing the defaults does NOT retroactively affect already-posted expense rows** — only new expenses pick up the new default. This is by design: the per-row deductible flag is frozen at post time.

You can also Deactivate a category — it remains usable on existing posted rows but won't appear on new-expense forms.

### Chart of accounts / Fiscal year / Payment methods

Read-only settings pages. The MVP ships with a fixed CoA + a single fiscal year start month + Cash + BankTransfer. Cheque + Card payment methods are deferred to a near-term batch.

---

## 7. Fixed assets + depreciation (US6)

### Capitalize an asset

1. Nav → "Fixed assets" → "+ New".
2. Enter asset name, capitalization date, capitalization amount, salvage value, useful life in years, depreciation method (straight-line is the MVP default).
3. Save Draft → Approve (if approval required) → Capitalize.
4. The system computes the depreciation schedule + records the asset.

### Place in service

Click "Put in service" on a capitalized asset. The asset's depreciation period starts; the monthly Hangfire job emits depreciation expense automatically.

### Monthly depreciation (FR-017)

The Hangfire `MonthlyDepreciationJob` runs at the start of each month + emits one journal entry per asset:

- DR Depreciation Expense (`5100`)
- CR Accumulated Depreciation (`1290`)

You can view the schedule + lifetime depreciation per asset on the Fixed Asset Detail page. The trial balance + taxable income reports automatically include the depreciation effect.

---

## 8. WHT certificates + Form 41 (US7)

### Withhold tax on a supplier payment (outbound certificate)

When you post a supplier payment that's subject to withholding (services payments per Egyptian tax law):

1. Open the Supplier Payment Voucher edit page.
2. Pick the supplier + payment date + gross amount.
3. Allocate to one or more outstanding purchase invoices.
4. Pick the WHT category (e.g. `Services 5%`). The system computes the withholding amount + net cash to pay the supplier.
5. Post. The system:
   - Splits the payment into 3 JE lines: DR AP / CR Cash (net) / CR WHT Payable.
   - Generates an outbound `WhtCertificate` row + bilingual PDF you hand to the supplier.

### Record a customer-issued certificate (inbound)

When a customer pays you net of withholding + provides their certificate number:

1. Open the Customer Receipt Voucher edit page.
2. Pick the customer + receipt date + gross amount.
3. Allocate to one or more outstanding sales invoices.
4. Enter the customer's WHT certificate number + amount.
5. Post. The system:
   - 3-line JE: DR Cash (net) / DR WHT Receivable / CR AR.
   - Records the inbound certificate with the customer's certificate number preserved verbatim.

### Quarterly Form 41 (FR-046)

Nav → "WHT" → "Form 41". Pick year + quarter, click Generate.

The system aggregates every outbound WHT certificate dated in the quarter, reconciles the total against the WHT Payable account balance, and produces:
- A JSON file matching the regulator's schema.
- A bilingual PDF.

If the reconciliation matches, you can click "Mark filed" — the filing becomes immutable + every certificate it includes is locked to this filing (it cannot be moved to a different quarter's filing).

If the reconciliation **doesn't** match (the certificates total ≠ the GL accrual), the filing is flagged "DISCREPANCY" + Mark Filed is disabled. Investigate the gap before filing — typically a missing certificate or an out-of-sequence accrual.

### WHT dashboard (FR-047)

Nav → "WHT" landing page surfaces three bands:
- **Owed** (total accrued but not yet filed + cert count + oldest unfiled date).
- **Expected** (customer-WHT receivable total + cert count).
- **Filings** with status badges (Filed / Unfiled / Overdue) + days-overdue + estimated penalty.

Overdue rows are red so you can't miss them.

---

## 9. Payment vouchers (Phase 9)

### Supplier payment voucher

Nav → "Supplier payments" → "+ New". Two-step flow:
1. Header — supplier + payment date + method (Cash / Bank Transfer) + reference + gross amount.
2. Outstanding-invoice picker — every Posted purchase invoice with `open_balance > 0` is listed; allocate amounts per-invoice. Running "remaining to allocate" total surfaces over-allocation immediately.
3. Optional WHT split (see [§8](#8-wht-certificates--form-41-us7)).
4. Post. JE: DR AP / CR Cash (with the optional WHT split when applicable).

### Customer receipt voucher

Same pattern; the symmetric receipts side. Outstanding **sales** invoices instead of purchases. JE: DR Cash / CR AR.

### Payment allocation view

Nav → "Payments" → "Payment allocations" gives a per-invoice rollup of every voucher allocation against that invoice (`AllocatedToDate`, `OpenBalance`, voucher history). Useful when an invoice has been settled in installments.

---

## 10. Closing cockpit (Differentiator 2)

Nav → "Closing cockpit". Year + month picker shows the FR-026 month-close readiness for that period:

- **VAT readiness** %: clean posts ÷ total posts in period.
- **Period-lock checklist** with ✓/✗ per item — what must clear before you can lock.
- **Drafts in period** count — drafts dated in this month that need to post before close.
- **Failed ETA submissions** count + grand-total — these need to clear before filing.
- **Missing-document buckets** (deductible posts without attachments, posted sales invoices without ETA submissions, etc) — collapsible blocks with "Open" links per example row.
- **Non-recoverable input VAT** sum — FR-020 information; not a blocker but useful for variance against the VAT report.

The cockpit is your "did I miss anything?" landing page for month-end. Refresh it as many times as you like — results are cached for 30 seconds for performance, with auto-invalidation when any tax-impacting state change lands.

---

## 11. Firm portal — multi-client review (US8)

### Acceptance handshake (firm reps)

When a client invites you as an external accountant, you'll receive an invitation email with a temporary password. Sign in to the client's installation, change your password, and from that point you're a recognized firm-user there. Every audit-log entry written by you is tagged with both your user-id AND your firm name.

### Switch between client installations

Nav → "Firm portal" → "Switch installation". The list shows every installation you've remembered. Click "Switch" to land on that installation; click "Remember this installation" on a fresh one to add it to your pool. The pool lives in localStorage on your browser only — there is no central vendor hub.

### Lock-for-review (FR-050)

When a client's bookkeeper signals "I'm done editing this month, please review", they raise a soft lock-for-review on the period. While the lock is active:

- Bookkeepers cannot edit primary documents whose document date falls in the locked month.
- You (the firm rep) CAN post adjusting journal vouchers via FR-031.
- Each adjusting action you take is counted against the lock.

When you're done reviewing, click "Release". The release event records how many adjusting actions you posted during the lock — your "review activity report".

This is **distinct** from the FR-037 hard period close, which happens later after the VAT return is filed. Lock-for-review is the soft pre-close handoff.

---

## 12. Inspection bundles (US9)

When the tax authority requests an inspection:

1. Nav → "Inspection bundle".
2. Pick the period start + end (typically a quarter or a fiscal year).
3. **Allow drafts in period?** Default OFF — the bundle build will refuse if any drafts dated in the period exist; you must post or void them first. If you check ON, the drafts are listed in the manifest's `excludedDraftIds` field as an explicit acknowledgement that they were excluded.
4. Click "Generate bundle".
5. The system collects every posted document, every attachment, the audit-chain extract for the period, computes per-file SHA-256, and builds the ZIP. For long periods this can take a few minutes — for very large bundles the generation runs in the background via Hangfire and you can watch the progress live.
6. Click the resulting filename to download. Hand the ZIP to the inspector along with a copy of the [`inspector-bundle-format.md`](inspector-bundle-format.md) spec.

**The inspector verifies the bundle on a clean Windows machine** using the bundled `verify-bundle.ps1` script — no EgyptTax installation needed on their side.

---

## 13. Reports

### VAT monthly report (FR-021)

Nav → "VAT report" → pick year + month. Output: output VAT, recoverable input VAT, net payable. Drill-down rows by document, sorted by date then number.

### Taxable income report (FR-023)

Nav → "Taxable income". Pick a date range (fiscal year, calendar year, quarter, or custom).

Headline: **Revenue − Deductible Expenses = Taxable Income**.

The screen also shows:
- **Management P&L** (Revenue − Deductible − NonDeductible) — the internal view.
- **Non-deductible adjustments** that are added back when computing taxable income (US2 acceptance scenario 2).
- The arithmetic identity `TaxableIncome == ManagementPL + NonDeductibleAdjustments` is explicit on the page so you can sanity-check the math.

Per-row drill-down to source detail pages.

### Trial balance (FR-024)

Nav → "Trial balance" → pick as-of date. DR / CR totals per account; total DR MUST equal total CR (FR-009 invariant — if it doesn't, you have a system bug; report immediately to the operator).

---

## 14. Period close — the monthly ritual

The recommended end-of-month sequence:

1. **Open the closing cockpit** (Nav → "Closing cockpit") for the closing month.
2. **Fix every missing-document bucket** — drill into each, post or attach as needed.
3. **Clear failed ETA submissions** — retry from the ETA dashboard; investigate persistent failures with the operator.
4. **Post all drafts in the period** — drafts dated in the month MUST be Posted (or explicitly Voided) before close.
5. **Resolve approval queue** — every pending approval should be Approved or Rejected.
6. **Run the VAT monthly report** + cross-check the totals against your bank reconciliation.
7. **Lock the period** (Nav → "Tax periods" → click the month → "Lock" with a reason). Once locked, no new documents can be posted with a document date in that month. An Administrator can Reopen if needed; Reopen is audit-logged.
8. **(Quarter-end only)** Generate Form 41 → Mark filed when reconciliation matches.

For the firm-rep workflow: after the bookkeeper raises the lock-for-review (step 4–5), the firm rep does steps 6–7 themselves + then releases the soft lock.

---

**Guide last reviewed: 2026-05-08. Cross-references the canonical specs in [`specs/008-egypt-tax-accounting/`](../specs/008-egypt-tax-accounting/) — when in doubt, the spec wins.**
