# Post-MVP Roadmap — Egypt Tax Accounting Product

> Built on top of the shipped MVP (FR-001 → FR-053). Sequenced for one team
> shipping every 2 weeks. Each module is sized so that the smallest MVP slice
> ships in 1-3 sprints; deeper investment is explicit.

---

## 0. Executive Summary

**Where we stand:** The MVP covers what most Arabic SME accounting tools do —
sales/purchase/expense, VAT, WHT, audit log, on-prem Windows install, full
Arabic. Three things already differentiate us: (1) audit log with hash chain,
(2) bilingual single-language toggle (zero English in Arabic mode), (3) firm
portal + period review-lock. No competitor surfaced in the market scan ships
all three.

**Where the market is moving:** Resolution 281/2025 dropped the e-invoicing
threshold to 250K EGP. Tier 2/3 penalties are EGP 5K-10K per late invoice.
Law 6 of 2025 created a simplified regime (quarterly VAT, 0.4-1.5%
turnover tax) most competitors haven't implemented. B2C e-receipt is a
separate stack with a 60-second submission window.

**Strategic positioning:** "Penalty Shield for Egyptian SMEs." Sell prevention
of one Tier-3 fine (10K EGP) per month as the ROI for the Professional tier.

**Top 3 risks:**
1. ETA schema/rule changes break our submission flow → mitigate with versioned
   rule catalog + nightly sync.
2. Cloud-only competitors (Wafeq, Daftra) keep poaching micro-SMEs on price →
   defend with on-prem trust narrative + accountant-firm features.
3. Bank reconciliation moat takes 8+ weeks; if we don't ship it in Q4, ERPNext
   or Wafeq will close the gap.

**Cadence (AI-augmented velocity):** Full roadmap is ~6-8 weeks of focused
shipping. Wave 1 "Penalty Shield" lands in a week. Wave 2 "Compliance Pack"
in 1-2 weeks. Wave 3 "Foundation" in 2 weeks. Wave 4 "Firm Edition" in 2-3
weeks. Calendar months would be a traditional-team estimate; with AI-paired
implementation we operate at a different unit of time.

---

## 1. Module Specification Format

Every item below uses the same six fields:

- **Pain** — concrete user pain in one sentence + a quantified cost where possible
- **Competitor parity / differentiator** — what DEXEF/Daftra/Wafeq/Odoo/SAP do today
- **Complexity** — sized for AI-paired implementation:
  - **XS** = ~2 hours (single sit-down)
  - **S** = ~½ day (4 hours)
  - **M** = ~1 day
  - **L** = 2-3 days
  - **XL** = ~1 week of focused work
  *(Traditional-team estimates would multiply these by 5-10×.)*
- **Dependencies** — what must exist first (existing FR# or earlier module)
- **MVP slice** — smallest shippable thing that delivers user value
- **Avoid** — over-engineering traps to skip in v1

---

## P0 — Release Validation

**Goal:** Ship the MVP we have with confidence. No new features here, just
hardening so the existing 53 FRs don't regress on a clean install.

### P0.1 Clean Windows VM MSI install harness

- **Pain:** The MSI install marathon was painful. Real customers run varied
  Windows configs (Win10/11 Home/Pro, missing prereqs, locked-down corporate).
  Each broken install = lost trust + 2 hours of support.
- **Competitor:** Cloud players (Daftra/Wafeq) have no install. Al-Ameen/DEXEF
  installers work but break on edge cases; users blame "the program."
- **Complexity:** M (1 day)
- **Dependencies:** Existing MSI from T247
- **MVP slice:** Packer template that builds Win11 base box → script installs
  MSI silently → asserts service running, /health/ready returns 200, browser
  smoke loads /. Run on every PR via GitHub Actions self-hosted Windows runner.
- **Avoid:** Don't build a custom VM orchestrator. Use Packer + Hyper-V or
  Vagrant. Don't try to test every Windows SKU; cover Win11 Pro + Win Server 2022.

### P0.2 Fresh DB migration / seed

- **Pain:** First-run UX. A demo install with 0 data feels broken; with the
  wrong data feels unprofessional. Demo install = first sales call.
- **Competitor:** All competitors ship a demo company. Quality varies — Daftra's
  is polished; ERPNext's is unusable.
- **Complexity:** S (½ day)
- **Dependencies:** Existing migrations (Phase 9)
- **MVP slice:** `egypttax-cli seed --demo` populates 1 company, 5 customers
  (mix of registered/non-registered taxpayers), 5 suppliers (1 non-resident),
  20 items (mix of 14% VAT / exempt / 5%), 30 sample posted invoices spanning
  3 months. Idempotent — running twice doesn't double the data.
- **Avoid:** Don't seed PII. Don't seed anything that could leak in support
  cases. Use `*@example.eg` emails, fake TINs in the test range.

### P0.3 Health endpoints

- **Pain:** Ops needs to know if the service is up. Customers behind IT
  departments need a URL their monitoring can hit.
- **Competitor:** SAP/Dynamics have it. SME tools usually don't.
- **Complexity:** XS (1-2 hours, mostly already exists per T257)
- **Dependencies:** None
- **MVP slice:** Verify three endpoints — `/health/live` (process alive),
  `/health/ready` (DB reachable + ETA mock pingable), `/health/dependencies`
  (versioned JSON of DB schema version, ETA SDK version, build SHA).
- **Avoid:** Don't expose internal metrics or PII on these. No DB query timing,
  no user counts.

### P0.4 Browser smoke checklist

- **Pain:** Before each release, someone clicks 10 critical paths manually.
  Without a checklist, regressions slip through.
- **Competitor:** N/A (internal process)
- **Complexity:** XS (1 hour to write)
- **Dependencies:** P0.2 (seeded demo data)
- **MVP slice:** `release-smoke-checklist.md` — 10 paths: login, switch
  language, create+post sales invoice, generate PDF, submit to ETA, run VAT
  report, generate Form 41, lock period, generate inspection bundle, verify
  audit chain. Each with screenshot of expected result.
- **Avoid:** Don't make this a full QA process; it's a 30-minute pre-release
  smoke. Real coverage comes from P0.5.

### P0.5 Playwright E2E suite

- **Pain:** Regressions kill trust. Manual smoke catches obvious breaks but
  misses subtle ones (e.g., Arabic RTL alignment, totals rounding).
- **Competitor:** SAP has full QA; SMEs don't.
- **Complexity:** L (2-3 days for first 20 critical scenarios)
- **Dependencies:** P0.1, P0.2 (need fresh VM + seed)
- **MVP slice:** 5 scenarios in first session — login (with TOTP), create+post sales
  invoice (assert ETA submission row + journal entry), post purchase invoice
  (assert FR-016 attachment requirement), run monthly VAT report
  (assert totals match invoice ledger), generate Form 41 (assert per-supplier
  aggregation). Add 3-5 scenarios per sprint thereafter.
- **Avoid:** Don't chase 100% coverage. Don't test what unit tests cover. Pick
  scenarios that exercise multiple modules end-to-end. Skip flaky stuff
  (timezone-sensitive tests on month boundaries — unit-test those instead).

---

## P1 — Egypt Compliance Differentiators

**Goal:** Build the "Penalty Shield" narrative — a set of features no
competitor bundles together. This is what gets us 60K EGP/seat instead of 25K.

### P1.1 Live ETA integration wizard

- **Pain:** ETA setup is the #1 onboarding blocker. 5+ steps (TIN registration,
  eSeal procurement, environment config, activity codes, test invoice) take
  the average accountant 3-5 days with no guidance.
- **Competitor:** Daftra has a basic wizard; Odoo requires partner deployment;
  SAP is professional services. No one owns this UX.
- **Complexity:** M (1 day)
- **Dependencies:** ETA submission infrastructure exists (FR-035/043)
- **MVP slice:** 5-step wizard — (1) TIN entry with live lookup against ETA's
  taxpayer search → auto-fill name/address, (2) eSeal certificate path or USB
  token detection, (3) activity codes picker (Arabic searchable), (4)
  environment selector (preprod/prod) with confirmation modal, (5) test
  invoice submission with success/error modal. Wizard state stored so users can
  resume after a "let me get the eSeal" interruption.
- **Avoid:** Don't try to automate certificate provisioning (regulated).
  Don't try to register the company with ETA on their behalf (regulated).

### P1.2 Digital signature / token setup checklist + monitor

- **Pain:** USB token setup breaks weekly. "السبت الصبح كل الفواتير 401
  لأن الشهادة انتهت الجمعة." Egypt Trust support is closed weekends.
- **Competitor:** Edara/Daftra show expiry on a dashboard somewhere; nobody
  alerts proactively.
- **Complexity:** S (½ day)
- **Dependencies:** Cert reading from Windows cert store (or HSM driver)
- **MVP slice:** On login, read all certs in CurrentUser\My matching ETA's CN
  pattern → cache expiry → dashboard widget always visible → email + in-app
  alerts at T-30, T-14, T-7, T-1 days. Each alert includes the renewal vendor
  contact (Egypt Trust, MCDR) and the documented turnaround time (3-5 business
  days for HSM).
- **Avoid:** Don't try to renew certs automatically (regulated process). Don't
  silently swap certs; require explicit user confirmation when a new cert is
  detected.

### P1.3 ETA submission status polling

- **Pain:** We submit; do we know it actually succeeded server-side? ETA's
  acceptance happens minutes-to-hours after the API returns 202 Accepted.
- **Competitor:** Most show a single "submitted/rejected" flag from the
  initial response and never poll for downstream state changes.
- **Complexity:** S (½ day)
- **Dependencies:** ETA submission row (FR-035), Hangfire (already in stack)
- **MVP slice:** Hangfire job every 15 min queries ETA Get Documents API for
  all submissions in `SubmittedPending` state → updates `EtaSubmission` to
  `Submitted` (long UUID issued) or `Failed` (with reason) → fires in-app +
  email notification on state change. Cap polling at the submission window
  expiry (don't poll forever).
- **Avoid:** Don't poll on every page render. Don't poll for completed
  submissions. Don't retry failed polls more than 3x.

### P1.4 ETA error translator (Arabic)

- **Pain:** "BadArgument: invoice.invoiceLines[0].totalSalesAmount" means
  nothing to a bookkeeper. Right now they screenshot the error and forward to
  the IT person.
- **Competitor:** Most pass the cryptic error through unchanged.
- **Complexity:** M (1 day for catalog of 50 errors + translation engine)
- **Dependencies:** ETA submission rows with raw error responses
- **MVP slice:** YAML catalog of known ETA error codes/patterns mapped to
  Arabic explanation + concrete fix action. UI surfaces the mapped version
  with a "تفاصيل تقنية" disclosure for the raw text. Examples:
  - `EGS code not found` → "كود الصنف رقم 12345 غير مفعّل بعد. عادة يحتاج
    14 يوم بعد الإضافة في GS1. للحل: استخدم كود GS1 احتياطي."
  - `Math validation failed: totalSalesAmount` → "قيمة البند رقم 3 لا تطابق
    (الكمية × سعر الوحدة). الفرق 0.01 ج.م. بسبب التقريب."
- **Avoid:** Don't auto-translate generically (Google Translate makes
  technical English worse). Don't translate without a fix action.

### P1.5 Received supplier e-invoices import

- **Pain:** Today, supplier sends an ETA e-invoice → buyer manually re-keys it
  into the accounting system as a Purchase Invoice. Wasted hours, transcription
  errors, mismatched amounts during VAT close.
- **Competitor:** Few do this well. Daftra mentions it but the implementation
  is basic. Big differentiator if shipped well.
- **Complexity:** L (2-3 days)
- **Dependencies:** ETA Get Received Documents API access, Purchase Invoice
  draft creation flow (exists)
- **MVP slice:** Daily Hangfire job pulls received documents from ETA → for
  each, creates a `PurchaseInvoice` in Draft state with: supplier matched by
  TIN (auto-create supplier if unknown), lines mapped to existing items by
  EGS code (or flagged for mapping), totals carried over. Bookkeeper sees a
  "ETA inbox" queue → reviews → posts.
- **Avoid:** Don't auto-post (always require human review for FR-016
  attachments + categorization). Don't try to match items by name (only by
  EGS code or SKU). Don't auto-create suppliers without warning.

### P1.6 GS1 / EGS item coding assistant

- **Pain:** New SKU requires EGS code (15 days approval) or GS1 code (24-48h
  cache). No UI tells you the status; users invoice with placeholder codes
  and get rejections.
- **Competitor:** Nobody tracks pending status. Mofawtar/eDariba describe the
  pain in blog posts but don't solve it.
- **Complexity:** M (1 day)
- **Dependencies:** Item entity, ETA Code Validator API
- **MVP slice:** When user creates an Item without code → form asks for
  GS1/EGS preference → if EGS, auto-submit request and show "Day 4 of ~15
  expected" countdown → if GS1, auto-validate against GS1 Egypt cache and
  show 24-48h status. Block invoicing the SKU until code is `Active`. Bulk
  status check Hangfire job runs daily.
- **Avoid:** Don't try to create GS1 codes (separate paid service from GS1
  Egypt). Don't try to validate EGS codes against ETA's pending queue (no
  API for that — track time elapsed instead).

### P1.7 Pre-flight ETA validator (CRITICAL — Penalty Shield core)

- **Pain:** Tier 2/3 fines = EGP 5K-10K per late or rejected invoice.
  "200 invoices in one batch with same wrong VAT category = 200 separate
  offenses." Today, validation happens server-side at ETA after submission.
- **Competitor:** SAP middleware partners (SNI, EDICOM) have strong pre-validators
  — that's their selling point at enterprise tier. SME tools have partial
  validation; none cover all 8 ETA validators locally.
- **Complexity:** L (2 days for first cut; ongoing rule maintenance)
- **Dependencies:** ETA SDK rules (versioned), Item/Customer/Supplier data
- **MVP slice:** Replicate ETA's 8 validators locally before submission:
  Standard (schema), Code (TIN/activity/EGS), Math (line/invoice totals to
  the cent), Taxpayer (TIN active per cached lookup ≤30 days), Items
  (EGS active), Receivers (buyer registered if B2B), Issuers (signature
  cert valid), Signatures (XML/JSON signing). Rejection surfaces the exact
  field path that will fail with line number. Versioned rule catalog updates
  monthly via signed config download.
- **Avoid:** Don't fork ETA's evolving rules into hand-maintained C# code —
  drive from a versioned config so rule updates ship without a release. Don't
  skip the actual ETA submission as a backstop ("if pre-flight passed, no need
  to verify" is dangerous).

### P1.8 Penalty exposure dashboard (Penalty Shield narrative)

- **Pain:** Owner doesn't know how much in fines is accruing. Bookkeeper
  doesn't either. The first time the number surfaces is when ETA sends the
  notice.
- **Competitor:** Nobody surfaces this proactively.
- **Complexity:** S (½ day)
- **Dependencies:** EtaSubmission with timestamps, current penalty regime
  rates (versioned config)
- **MVP slice:** Real-time count of late submissions in trailing 12 months,
  current tier (warning / Tier 2 / Tier 3), projected fine if nothing is
  fixed, auto-prioritized work queue ("submit these 17 invoices first to
  avoid Tier 3 next week"). Top of dashboard. Bilingual KPI badge.
- **Avoid:** Don't be alarmist with red flags when at zero. Don't double-count
  fines that have already been settled. Don't try to predict future fines on
  unposted invoices.

### P1.9 Cancel-vs-Credit-Note decision wizard

- **Pain:** Cancel window = 7 days + buyer approval. Credit note window = 60
  days. After 60 days, no recourse. Accountants discover wrong VAT category
  90 days later during periodic return — and have no way to fix it.
- **Competitor:** Show buttons for both with no policy logic. Users guess.
- **Complexity:** S (3-4 hours)
- **Dependencies:** Sales Invoice, EtaSubmission with submission timestamp
- **MVP slice:** On any posted sales invoice page, a "تصحيح المستند" panel
  shows: today's date, days since submission, cancel deadline (T+7), credit
  deadline (T+60), recommended action with countdown ("متبقي 3 أيام للإلغاء،
  بعدها لازم إشعار خصم"). Clicking the recommended action pre-fills the
  appropriate form.
- **Avoid:** Don't try to programmatically enforce buyer approval flow for
  cancellation (out of our scope — ETA handles). Don't allow credit notes
  past T+60 even with override (record-keeping + future audit clarity).

### P1.10 ETA bulk export (PDF/Excel/ZIP)

- **Pain:** ETA portal lists invoices but won't bulk-export PDFs. A whole
  cottage industry of free Chrome extensions exists to scrape; they keep
  getting yanked or moved to paid tiers.
- **Competitor:** Most have export from their own UI; none from ETA portal
  directly.
- **Complexity:** S (½ day)
- **Dependencies:** ETA Get Documents API, existing PDF renderer
- **MVP slice:** Date range + filter UI → background job pulls from ETA Get
  Documents → for each, generates PDF (using our existing renderer) + saves
  signed JSON → packages into ZIP with filename pattern
  `{ETA_UUID}_{document_number}.pdf`. Download link emailed when ready.
- **Avoid:** Don't try to format PDFs identically to ETA portal's PDF
  (theirs is bad anyway). Don't include unsigned/draft invoices in the
  same export.

### P1.11 Books ↔ ETA reconciliation engine (THE MOAT)

- **Pain:** Audit time bomb. Invoices the ERP "thinks" are submitted may have
  been silently rejected. Invoices in ETA may have been issued by a junior
  on the portal directly and never made it back into the ledger. Surface
  symptom: VAT return doesn't match what ETA already has on file.
- **Competitor:** ERPNext is the loud exception (basic monthly reconciliation
  report, missing-UUID alerts). Everyone else falls back to Excel diffs.
- **Complexity:** XL (~1 week)
- **Dependencies:** P1.3 (status polling already maintains state), ETA Get
  Documents API, sufficient ledger history
- **MVP slice:** Nightly Hangfire job pulls full ETA invoice list for the
  active period → diffs against local ledger by (UUID, amount, counterparty
  TIN) → 3 buckets surfaced in a "Reconciliation" page:
  - **In books, not in ETA** → urgent, you owe a submission (with one-click
    "submit now")
  - **In ETA, not in books** → someone bypassed the ERP (with one-click
    "import as draft purchase invoice" if it's an inbound, or "create posting"
    if outbound)
  - **Different totals** → someone edited after submission (with diff view +
    forced human reconciliation)
- **Avoid:** Don't try to auto-fix discrepancies — surface for human triage,
  always. Don't guess at which side is "right" when totals differ. Don't
  reconcile beyond the active fiscal period (full history makes the diff
  unmanageable; we have inspection bundles for older audits).

### P1.12 Reverse-charge for non-resident services (FR-041 follow-through)

- **Pain:** Non-resident B2B services use VAT reverse-charge in the periodic
  return, NOT the e-invoice portal. Newcomers try to issue an "import"
  e-invoice, get NotFound on the supplier TIN, give up. The actual liability
  sits silently in the VAT return — easy to forget, surfaces in audit.
- **Competitor:** Most ignore the case. SAP handles via tax engine in the VAT
  return, not the invoice flow.
- **Complexity:** M (1 day)
- **Dependencies:** Supplier with `IsNonResident` flag (already on
  TaxProfile), VAT report (FR-021)
- **MVP slice:** When creating purchase invoice with non-resident supplier:
  (1) skip ETA submission entirely, (2) flag invoice as "reverse-charge,
  reportable in VAT return," (3) generate dual journal entry — DR input VAT
  recoverable / CR output VAT payable for the same amount, net zero, (4) VAT
  return picks up reverse-charge total in dedicated section.
- **Avoid:** Don't try to handle every cross-border scenario in v1 (start
  with services). Don't auto-classify "non-resident" without explicit user
  confirmation per supplier (regulatory consequences if wrong).

### P1.13 B2C E-Receipt + minimal POS

- **Pain:** Resolution 281/2025 dropped the threshold to 250K EGP turnover
  → many SMEs newly mandated. Separate platform (`pos.eta.gov.eg`), 60-second
  online window or 24-hour batch. Every printed receipt needs ETA QR.
- **Competitor:** Daftra/Edara have separate POS modules. Most SMEs treat
  B2B and B2C as one and can't reconcile.
- **Complexity:** L (3 days for minimal POS)
- **Dependencies:** Item catalog, payment methods (FR-051/052), separate
  ETA POS API integration
- **MVP slice:** New "Cashier" page — barcode scan / SKU search → cart →
  payment (cash / card / wallet) → submit to ETA POS API → print receipt
  with ETA QR (browser print, thermal printer is later). Daily Z report
  closes the cashier shift. Separate ledger entries from B2B sales for
  clean reconciliation.
- **Avoid:** Don't build a full POS (returns, holds, splits, modifiers) in
  v1. Don't support thermal printer drivers in v1 (browser print to PDF
  is fine). Don't try to integrate with a card terminal (settlement files
  are P3.3).

### P1.14 Inbound WHT certificate import + auto-match

- **Pain:** When customer withholds 1%/3%/5% tax from our payment, they're
  supposed to give us a certificate so we can offset against income tax.
  Today: certificates arrive as PDFs over WhatsApp, get filed in a folder,
  forgotten, lost.
- **Competitor:** Nobody does end-to-end. Microsoft Dynamics has Form 41
  but inbound certs are manual.
- **Complexity:** M (1 day)
- **Dependencies:** Sales invoices, customer entity, attachment system
- **MVP slice:** "Inbound WHT" page — upload certificate PDF + manual entry
  of (customer, amount withheld, period, certificate number) → engine matches
  against outbound sales invoices for that customer in the period (fuzzy on
  amount with ±2% tolerance) → records the WHT receivable journal entry →
  on annual income tax return, totals carry forward as tax credit.
- **Avoid:** Don't try OCR in v1 (manual entry is fine). Don't auto-match
  with confidence < 95% (force human confirmation).

### P1.15 Outbound WHT certificate generator + WhatsApp delivery

- **Pain:** Quarterly Form 41 mail-merge job. Accountants generate per-supplier
  WHT certificates in Word, then WhatsApp them one at a time. Suppliers chase
  for missing certs.
- **Competitor:** Daftra has WHT module; certificate output is PDF only,
  delivery is manual.
- **Complexity:** M (1 day; +½ day for WhatsApp Business setup)
- **Dependencies:** SupplierPaymentVoucher with WHT (FR-051), supplier entity
  with phone number
- **MVP slice:** When SPV is posted with WHT, automatically generate Arabic
  certificate PDF (template-based, similar to FR-046 Form 41 PDF) → save as
  attachment → queue for delivery. Owner approves message template once;
  per-cert sends require single click. Delivery via WhatsApp Business API
  (Meta cloud API) — message includes PDF + "تأكيد الاستلام" link that
  marks the cert as acknowledged.
- **Avoid:** Don't auto-send without owner approval at first. Don't store
  WhatsApp credentials in the customer's database (use a per-tenant
  secrets vault). Don't try to verify supplier read receipts (privacy).

---

## P2 — Onboarding and Accountant Workflow

**Goal:** Reduce time-to-first-posted-invoice from 3 days to 30 minutes.
Make accountants love us.

### P2.1 Migration assistant from Excel/legacy software

- **Pain:** Switching costs are everything for SME software. "I have 200
  customers in my old system; I'm not retyping them." Largest blocker to
  selling against incumbents.
- **Competitor:** Daftra has Excel import; Wafeq has CSV. Quality varies;
  none import COA + opening balances + open AR/AP atomically.
- **Complexity:** L (2-3 days)
- **Dependencies:** All master data entities, opening-balance JV
- **MVP slice:** Multi-sheet Excel template (customers, suppliers, items,
  COA, opening balances, open AR, open AP) → upload → preview each sheet
  with errors highlighted → "commit" runs in one transaction. Idempotent
  via per-row external ref so partial imports can be re-run.
- **Avoid:** Don't try to natively import every legacy format (DEXEF, Onyx,
  AccPro, ...). Provide one clean template; competitors export to Excel
  natively. Don't auto-balance equity; force user to enter capital/retained
  earnings explicitly.

### P2.2 Opening balances

- **Pain:** New tenant needs to enter "where we are now" without a year of
  historical entries. Without this, first month's balance sheet is wrong.
- **Competitor:** Table stakes; everyone has it. Quality varies.
- **Complexity:** M (1 day)
- **Dependencies:** COA (existing), customers/suppliers/items
- **MVP slice:** Per-account-type form — Cash (multi-cashbox/bank), AR (per
  customer with optional per-invoice breakdown), AP (per supplier ditto),
  Inventory (per SKU, qty + value), Fixed Assets (per asset already covered
  in FR-017). Posts a single "OPENING-BALANCE-{date}" JV with prefix.
- **Avoid:** Don't force per-invoice AR breakdown for migration (summary per
  customer is fine). Don't allow opening balance entry after first regular
  posting (force order: opening → operations).

### P2.3 Unpaid invoices import

- **Pain:** Migration moment — all open AR/AP must come over with full detail
  for proper aging and allocation against future receipts.
- **Competitor:** Most allow it via Excel import; none link to ETA history.
- **Complexity:** M (1 day)
- **Dependencies:** P2.1, P2.2 (sales/purchase invoice entities)
- **MVP slice:** Excel template with: invoice number, original ETA UUID
  (optional), date, customer/supplier, gross amount, currency, due date,
  WHT amount (optional). Creates "imported" invoices in `Posted` state with
  no journal entry (since opening balance covers them). They behave like
  regular invoices for allocation against future Customer Receipts /
  Supplier Payments.
- **Avoid:** Don't try to back-fill ETA submissions for historical invoices
  (regulated). Don't try to import line items (header-only is enough for
  AR/AP migration). Don't allow editing imported invoices.

### P2.4 Missing document request links

- **Pain:** Bookkeeper needs an attachment from owner ("send me the receipt
  for that hotel charge"). Current flow: bookkeeper WhatsApps owner, owner
  forgets, bookkeeper chases for a week.
- **Competitor:** Nobody does this.
- **Complexity:** M (1 day)
- **Dependencies:** Attachment system (existing), email/WhatsApp gateway,
  signed magic-link infrastructure (new)
- **MVP slice:** From any expense/purchase invoice missing FR-016 attachment,
  "اطلب من المالك" button → generates short-link with signed token →
  owner opens on phone (no login) → uploads photo → photo attaches to the
  document → bookkeeper notified. Magic link expires in 7 days or after one
  successful upload.
- **Avoid:** Don't require owner login (they'll never set up an account).
  Don't store the magic link in the database in plain text (HMAC-signed). Don't
  allow re-upload via the same link (one-shot then expire).

### P2.5 Accountant month-close task workflow

- **Pain:** Closing cockpit (FR-021 etc) is great but doesn't track per-task
  ownership or completion across team members. Today: accountant has a Word
  checklist they tick by hand.
- **Competitor:** Mostly checklist PDFs from accounting firms; no tool support.
- **Complexity:** M (1 day)
- **Dependencies:** Closing cockpit (existing), users, new tasks entity
- **MVP slice:** Per-period (e.g., "إقفال مارس 2026") templated checklist of
  ~15 tasks (reconcile cash, reconcile bank, post depreciation, run VAT
  report, file Form 10, reconcile inventory, etc.) → each task has assignee,
  due date, status (pending/in-progress/blocked/done), comment thread, and a
  link to the relevant page. Task templates are versioned (so adding a task
  doesn't retroactively appear on closed periods).
- **Avoid:** Don't build a generic project tracker; keep it month-close
  specific. Don't allow custom tasks in v1 (template only). Don't try to
  integrate with external task tools (Asana, etc.) in v1.

### P2.6 Compliance calendar

- **Pain:** VAT due 28th of next month, Form 41 quarterly, payroll monthly,
  income tax annual, social insurance Form 2 monthly. Easy to miss → fines.
- **Competitor:** Nobody surfaces this proactively in the product.
- **Complexity:** M (1 day)
- **Dependencies:** Company profile (FY start), users, notification system
- **MVP slice:** Calendar view — auto-generated obligations based on company
  profile (regime: Law 6 simplified vs standard, fiscal year), each with
  due date, status (upcoming / overdue / filed). Email + in-app alerts at
  T-7, T-3, T-1 days. Status updates by clicking "تم التقديم" with proof
  attachment (ETA acknowledgement PDF).
- **Avoid:** Don't try to integrate with Outlook/Google Calendar in v1
  (export iCal is fine). Don't auto-file anything (regulatory risk). Don't
  show obligations the company isn't subject to (e.g., payroll if no
  employees registered).

---

## P3 — Payments and Reconciliation

**Goal:** Eliminate the Excel bank reconciliation grind. This is where Q3
revenue comes from.

### P3.1 Multi-cashbox / bank accounts

- **Pain:** Today everything routes through one Cash account (1100). Real
  businesses have multiple cashboxes per branch + multiple bank accounts
  (CIB, NBE, USD, EGP).
- **Competitor:** Table stakes for everyone above SME entry.
- **Complexity:** M (1 day)
- **Dependencies:** COA (existing), payment vouchers (existing FR-051/052)
- **MVP slice:** Expand COA — bank accounts as 1100.{n} with metadata (bank
  name, account number, currency, branch). Per-branch cashboxes as 1100.B{n}.
  Voucher creation requires picking specific cash/bank account. Trial balance
  + GL drill-down work per account. Customer Receipt/Supplier Payment forms
  show "Account" dropdown.
- **Avoid:** Don't try real-time bank balance display (no API). Show
  last-imported statement balance + computed unposted activity. Don't allow
  deletion of cashboxes/banks with history (deactivate only).

### P3.2 Bank statement import (PDF + manual)

- **Pain:** PDF→Excel via Kashfbank or similar paid service is the current
  state. Egyptian banks don't offer reliable CSV export. Bookkeeper does this
  monthly at best, often quarterly → late detection of fraud / errors.
- **Competitor:** Nobody does it well in Arabic. Wafeq/Daftra import CSV
  if you produce it; nobody parses Egyptian bank PDFs.
- **Complexity:** L (3 days for first 3 banks)
- **Dependencies:** P3.1 (cashbox/bank accounts), document storage, OCR
  service (commercial — Azure Form Recognizer, AWS Textract, or open-source
  Tesseract)
- **MVP slice:** Upload PDF for top-3 banks (CIB, NBE, QNB) → bank-specific
  parser extracts transactions → preview table → bookkeeper marks "import" →
  saves as `BankStatement` entity (lines NOT yet matched to invoices; see
  P3.4). Bank balance shown vs computed ledger balance with diff highlighted.
- **Avoid:** Don't try to support every bank in v1 (add 1-2 per release).
  Don't pretend OCR is 100% accurate (always show preview). Don't auto-create
  journal entries from imported statement lines (matching first, journals
  later).

### P3.3 Paymob / Fawry / mobile wallet settlement import

- **Pain:** PSP settlement reports are CSV but format differs per provider
  per release. Reconciling Paymob receipts to sales invoices is currently
  manual Excel work.
- **Competitor:** Some plug-ins exist for individual providers. None bundle.
- **Complexity:** M (½ day per provider)
- **Dependencies:** P3.1
- **MVP slice:** Paymob first (largest in Egypt) → CSV upload → match to
  sales invoices by external reference → preview matched/unmatched →
  commit creates Customer Receipt vouchers per matched line. Add Fawry
  next sprint.
- **Avoid:** Don't try real-time webhook integration in v1 (settlement-day
  CSV import is fine and matches accountant workflow). Don't support every
  PSP — focus on top 3 by volume.

### P3.4 Auto-match invoices to payments (the moat)

- **Pain:** Manual ticking is a week of work per month. Bookkeepers literally
  print statement, print AR ledger, draw lines.
- **Competitor:** Nobody does Egyptian Arabic OCR/matching well. ERPNext has
  basic English matching; nothing handles mixed Arabic/English narrations
  + partial cheque numbers + combined deposits.
- **Complexity:** XL (~1 week for engine + ongoing tuning)
- **Dependencies:** P3.2, P3.3, sales/purchase invoices, customer/supplier
  master with phone/account aliases
- **MVP slice:** Fuzzy match by amount (exact) + date window (±7 days) +
  counterparty (name fuzzy match against customer/supplier name + phone
  + bank account aliases). Confidence score 0-100. Auto-match at ≥95%
  (creates voucher draft). Queue 70-95% for human approve. Skip <70%. Target
  70% auto-match rate; tune monthly based on user feedback loop.
- **Avoid:** Don't optimize for 100% — show confidence + let human approve.
  Don't try to handle every weird edge case in v1 (e.g., one deposit
  spanning multiple invoices — surface, let human split). Don't ship before
  3-firm beta with real data.

### P3.5 Unmatched transaction queue

- **Pain:** Bookkeeper needs a single inbox to triage statement lines that
  didn't auto-match.
- **Competitor:** Generic. Most bank-recon tools have it.
- **Complexity:** S (3 hours if P3.4 exists)
- **Dependencies:** P3.4
- **MVP slice:** List view with filters (account, date range, amount range,
  status). Bulk actions: "mark as bank fee → expense account 5xxx",
  "mark as transfer between accounts", "create new customer receipt", "ignore
  (with reason)".
- **Avoid:** Don't make it a full inbox/notification surface. Just a triage
  queue. Don't allow bulk-deletion (only bulk-categorization).

---

## P4 — Retail and Inventory

**Goal:** Capture the retail SME segment that Resolution 281/2025 just
mandated into e-invoicing. Sell as add-on, don't bloat the core product.

### P4.1 E-Receipt / cash-business mode

> Already covered in P1.13 (B2C E-Receipt). Implementation lives in this
> wave if customer mix says "we have many service-only SMEs first, retail
> later"; otherwise build with P1.

### P4.2 Cashier shifts

- **Pain:** Daily Z report needs shift definition. Without it, can't
  reconcile cash drawer end-of-day.
- **Competitor:** Daftra/DEXEF have it. SAP has it. Wafeq doesn't really.
- **Complexity:** M (1 day)
- **Dependencies:** Multi-user (existing), cashbox accounts (P3.1)
- **MVP slice:** Per-cashier shift record — open with starting cash,
  close with ending cash + variance (auto-computed from sales). Variance
  posts to "كسر الصندوق" (drawer variance) account. Block second shift
  open by same cashier without closing first.
- **Avoid:** Don't try multi-cashier-per-shift in v1. Don't support
  hand-overs mid-shift. Don't try to handle drawer count per denomination
  (just total).

### P4.3 Daily Z report

- **Pain:** End-of-day reconciliation. Without it, owner can't trust the till.
- **Competitor:** Standard for POS-equipped tools.
- **Complexity:** S (3-4 hours)
- **Dependencies:** P4.2, P1.13 (e-receipts), sales invoices
- **MVP slice:** Per-shift summary report — total sales by tender (cash /
  card / wallet), VAT collected, refunds, current drawer balance, ETA
  submission status (all submitted? any pending?). PDF download.
- **Avoid:** Don't try to print to thermal printer in v1 (PDF + browser
  print is enough). Don't try X reports (mid-shift snapshots) in v1.

### P4.4 Inventory lite

- **Pain:** SMEs need to know "how much do I have left." Today: Excel.
- **Competitor:** DEXEF/Onyx have full inventory. We compete by being
  simpler — "lite" is a positioning.
- **Complexity:** L (2 days)
- **Dependencies:** Items (existing), sales/purchase invoices
- **MVP slice:** `StockOnHand` per (SKU, warehouse) → auto-decrements on
  sales invoice post → auto-increments on purchase invoice post. Read-only
  view with filters. Stock-take page allows manual adjustment with reason
  code (post a "جرد" JV automatically).
- **Avoid:** Don't build advanced features (lots, serial numbers, ABC
  analysis, MRP, BOM) in lite. Keep one warehouse per SME by default;
  multi-warehouse is opt-in flag.

### P4.5 Weighted-average COGS

- **Pain:** Without it, you can't compute gross margin. Tax-wise, COGS is
  a real expense that affects taxable income.
- **Competitor:** Standard.
- **Complexity:** M (1 day)
- **Dependencies:** P4.4, items
- **MVP slice:** Each purchase posts qty + value into moving-average per SKU
  per warehouse. Each sale posts a JV: DR COGS / CR Inventory at
  current-moving-average × qty sold. Sales invoice line shows COGS for
  margin display (manager view only, hidden from cashier).
- **Avoid:** Don't offer FIFO/LIFO in v1 (most Egyptian SMEs use weighted
  average). Don't try to revalue inventory at year-end (lower of cost or
  NRV) in v1.

### P4.6 Batches / expiry (optional)

- **Pain:** Pharma, food retail, cosmetics need batch + expiry tracking.
  Most accounting tools don't surface it.
- **Competitor:** ERP-tier feature. SME tools don't have it.
- **Complexity:** L (2-3 days)
- **Dependencies:** P4.4, P4.5
- **MVP slice:** Per-batch tracking with expiry warnings (T-90, T-60,
  T-30 alerts). FEFO (first-expired-first-out) deduction on sale.
- **Avoid:** Don't build this unless we're targeting pharma/food
  vertical specifically. Significant complexity for ~10% of customers.

---

## P5 — HR / Payroll

**Goal:** Capture the 70% of Egyptian SMEs running payroll in Excel.
Defensible because of 2026 regulatory changes (NOSI changes, EAF retroactive,
unified ETA+NOSI filing in Q1 2026).

### P5.1 Payroll lite

- **Pain:** SME owner runs payroll in Excel monthly. Errors → underpayment
  disputes; overpayment → tax exposure.
- **Competitor:** Wafeq has payroll. Local: Microtech AccPro, Pioneers HR.
  None handle 2026 changes well yet.
- **Complexity:** XL (~1 week)
- **Dependencies:** New `Employee` entity, tax brackets table, COA
  expansion (salary expense, accruals)
- **MVP slice:** ≤10 employees, monthly payroll run, gross-to-net engine
  with 2026 income tax brackets, pension contribution, social insurance
  (employee 11% / employer 18.75%), capped at insurable wage 16,700.
  Monthly run produces: per-employee payslip, summary GL JV, bank
  transfer file (ACH-formatted).
- **Avoid:** Don't try to handle every allowance type in v1. Start with
  base + transport + variable bonus. Don't build leave management or
  performance reviews (not accounting). Don't try to handle expat
  payroll (different rules).

### P5.2 Egyptian income tax / social insurance forms

- **Pain:** NOSI Form 1, Form 2, Form 6 all have specific format. April
  2026 EAF revision retroactive to Jan 2026. Insurable wage cap changes
  yearly.
- **Competitor:** Local payroll tools handle but with manual updates per
  reg change; first to ship 2026 wins.
- **Complexity:** L (3 days; ongoing reg-update maintenance)
- **Dependencies:** P5.1, versioned regulatory config
- **MVP slice:** 2026 brackets implementation, monthly NOSI Form 1
  generation (insurable wage, contributions per employee), quarterly
  payroll tax form generation. Versioned brackets so historical recompute
  works correctly.
- **Avoid:** Don't try to integrate with NOSI portal API (none exists
  publicly for SMEs). Don't auto-file (regulatory risk). Don't try to
  cover every special case (military, disability, etc.) in v1.

### P5.3 Payslips

- **Pain:** Currently handed out as Word docs or paper. Employee can't verify
  later.
- **Complexity:** S (½ day)
- **Dependencies:** P5.1, P5.2
- **MVP slice:** Bilingual PDF payslip (Arabic primary), QR code linking to
  a verification page (accessible without login via signed URL similar to
  invoice verify), email or WhatsApp delivery.
- **Avoid:** Don't try ESS (employee self-service portal) in v1. Don't
  store decrypted PDFs (regenerate from data on each access).

### P5.4 Payroll journal entries

- **Pain:** Currently a manual "summary entry" per month. Hides which
  employees cost what.
- **Complexity:** S (3-4 hours)
- **Dependencies:** P5.1, P5.2, COA expansion
- **MVP slice:** Per-employee detail or summary JV per month (configurable).
  Accruals for end-of-service benefit (mukafa'a) computed monthly.
- **Avoid:** Don't try cost-center allocation in v1 (everyone goes to
  generic salary expense). Don't try project-time-tracking integration.

---

## Cross-Cutting Themes

### CT.1 — "Penalty Shield" Marketing Bundle

- Bundle items: P1.1 (wizard), P1.2 (cert monitor), P1.4 (error translator),
  P1.7 (pre-flight validator), P1.8 (penalty dashboard), P1.9 (cancel/credit
  wizard), P1.10 (bulk export).
- Position as the v1.1 release headline.
- Marketing message: "وفّر غرامة Tier 3 واحدة شهرياً (10,000 ج.م.) =
  المنتج دفع نفسه."
- All Penalty Shield items are Professional tier (not Starter).

### CT.2 — WhatsApp Business horizontal layer

- Build once in Q2, reused by P1.15 (WHT certs), P2.4 (missing doc requests),
  P3.4 (collection follow-ups), P5.3 (payslips).
- Use Meta Cloud API (no Twilio markup).
- Per-tenant credentials stored in encrypted secrets vault (one Meta WABA
  per company). Customer onboards their own number to avoid template
  approval bottleneck.
- Approval workflow: owner approves message templates; per-message sends
  by bookkeeper require explicit click in v1 (no full auto-send until
  v2).

### CT.3 — Multi-Tenant Firm Edition

- Bundle items: P1.11 (ETA reconciliation), existing FR-049/050 (firm
  portal + review-lock), P3.4 (bank matching), Q4 cross-client
  dashboards.
- Premium tier for accounting firms managing 10+ client companies.
- Pricing: 120K EGP base + 8K per client company.
- Ships v2.0 (Q4 2026).

### CT.4 — Versioned regulatory rule catalog

- All rules that change with regulation (ETA validators, VAT rates, WHT
  rates, payroll brackets, NOSI insurable wage, EAF formula) live in
  signed YAML/JSON config.
- Monthly sync via download + signature verification (no auto-execute).
- Audit log records when rules changed and which version was active at
  posting time. Critical for FR-040/041 snapshot semantics.

### CT.5 — Egyptian-quirk handling

- Two fiscal years coexist (Jan-Dec private, Jul-Jun state-owned) — already
  configurable per company; verify in P0 testing.
- Hijri date display alongside Gregorian on documents (optional toggle, like
  legal contracts often need).
- Ramadan working hours mode for compliance calendar (shifts T-7/T-3
  alerts to morning).
- WhatsApp-as-file-system assumption baked into P2.4, P3.4, P5.3.

---

## What We Explicitly Defer / Skip

| Item | Reason | Reconsider when |
|---|---|---|
| Cloud SaaS edition | On-prem is our trust differentiator | Never in this product — separate SKU only |
| Mobile native app | Web mobile-friendly is enough | Field sales become a customer segment |
| Multi-country (KSA/UAE) | Wafeq owns this; deepen Egypt instead | After 200 paying Egyptian customers |
| Manufacturing / MRP / BOM | Not our segment; Onyx Pro owns | Never (separate product if pursued) |
| Project management | Accountants don't need; not our ICP | Never |
| Multi-currency revaluation engine | Basic FX is enough for FY1 | FY2 if 10+ customers ask |
| DMS beyond attachments | Accountants don't pay for this | Never |
| Auto-cert renewal | Regulated; vendor's job | Never |
| AI invoice categorization | Accountants want control, not AI | After P3.4 ships and people trust matching |
| ESS portal | Out of scope for accounting product | After P5 + 50 paying customers |
| Card-terminal integration | Settlement files (P3.3) are enough for SMEs | Never in this product |
| Open-banking real-time API | Egypt has no PSD2 equivalent | When CBE mandates one |
| Tax filing bot | Regulatory risk too high | Never |

---

## Pricing Tier Map (Updated)

| Capability | Starter (25K + 5K/yr) | Professional (60K + 12K/yr) | Firm Edition (120K + 24K/yr + 8K/client) |
|---|---|---|---|
| Sales / Purchase / Expense | ✅ | ✅ | ✅ |
| ETA submission + retry | ✅ | ✅ | ✅ |
| VAT report (Form 10) | ✅ | ✅ | ✅ |
| Audit log + chain | ✅ | ✅ | ✅ |
| Bilingual single-language | ✅ | ✅ | ✅ |
| Up to 3 users | ✅ | | |
| Up to 10 users | | ✅ | |
| Unlimited users | | | ✅ |
| **Penalty Shield (CT.1)** | | ✅ | ✅ |
| Form 41 + WHT certificates | | ✅ | ✅ |
| Document 360 + Inspection | | ✅ | ✅ |
| Migration assistant (P2.1) | | ✅ | ✅ |
| Closing cockpit + tasks (P2.5) | | ✅ | ✅ |
| Compliance calendar (P2.6) | | ✅ | ✅ |
| Bank statement import (P3.2) | | ✅ | ✅ |
| **ETA reconciliation engine (P1.11)** | | | ✅ |
| **Bank matching engine (P3.4)** | | | ✅ |
| Multi-firm portal (FR-049/050) | | | ✅ |
| WhatsApp Business (CT.2) | | | ✅ |
| **Add-on: E-Receipt / POS** | +15K | +10K | +10K |
| **Add-on: Payroll lite (≤10 emp)** | +12K | +8K | +8K |
| **Add-on: Payroll lite (≤50 emp)** | +25K | +18K | +18K |

---

## Realistic Cadence (AI-Augmented Velocity)

> Sized in days of focused work, not calendar months. Each "wave" is a
> coherent release; releases between waves are fine if you want to ship
> incrementally. A "day" assumes one productive sit-down, not a full
> 8-hour work day.

### Wave 1 — "Penalty Shield" (v1.1) — ~5-7 days total

| Day | Items |
|---|---|
| 1 | P0.1 VM install harness + P0.2 fresh seed + P0.3 health + P0.4 smoke checklist |
| 2 | P0.5 first 5 Playwright scenarios |
| 3 | P1.7 pre-flight validator (day 1 of 2) + start ETA rule catalog |
| 4 | P1.7 pre-flight validator (day 2) + P1.4 error translator |
| 5 | P1.8 penalty dashboard + P1.9 cancel/credit wizard |
| 6 | P1.2 cert monitor + P1.10 bulk export |
| 7 | P2.1 migration assistant (adoption blocker — ships with v1.1) |

**Release:** v1.1 "Penalty Shield." First paid release. Pricing tiers
launched.

### Wave 2 — "2025 Compliance Pack" (v1.2) — ~7-10 days total

| Day | Items | Status |
|---|---|---|
| 1 | P1.1 ETA wizard + P1.3 status polling | ✅ shipped |
| 2-4 | P1.5 received supplier e-invoices (3-day item) | ✅ shipped (PI auto-conversion deferred) |
| 5 | P1.6 GS1/EGS tracker + P1.12 reverse-charge | ✅ shipped |
| 6 | P1.14 inbound WHT + start CT.2 WhatsApp horizontal layer | ✅ P1.14 shipped, CT.2 deferred |
| 7 | P1.15 outbound WHT certs + WhatsApp delivery (uses CT.2) | ⏳ deferred — needs WhatsApp Business creds |
| 8 | Law 6 of 2025 mode (turnover tax, quarterly VAT) | ✅ shipped |
| 9 | P2.5 month-close workflow + P2.6 compliance calendar | ✅ P2.6 shipped, P2.5 deferred |
| 10 | Polish, Playwright additions, Wave 2 release | ✅ closeout sweep clean |

**Release:** v1.2. Captures Resolution 281/2025 Wave 9/10 mandate.

**Wave 2 closeout (2026-05-10):**
- 7 of the 10 days landed substantively. Two items deferred (P1.15 needs
  WhatsApp Business credentials; P2.5 is a workflow tool that pairs better
  with multi-user firm-portal work in Wave 4).
- Final route sweep: 17/17 routes return expected codes
  (200 public / 302 auth-redirect). Zero warnings, zero exceptions in the
  Wave 2 deploy logs.
- 8 Hangfire recurring jobs registered:
  `audit-checkpoint`, `compliance-calendar-refresh`,
  `eta-item-code-check`, `eta-received-inbox`, `eta-status-polling`,
  `eta-submission-retry`, `ntp-health-check`, `supplier-tin-revalidation`.
- 4 EF migrations added in Wave 2:
  `EtaSubmissionAcknowledgement`, `EtaReceivedInbox`,
  `ItemEtaCodeLifecycle`, `CompanyTaxRegime`, `ComplianceObligations`.
- Mock-first design: every ETA / WhatsApp / WHT registry contact has a
  mock implementation behind a stable interface (`IEtaStatusQuery`,
  `IEtaTaxpayerLookup`, `IEtaActivityCodeCatalog`,
  `IEtaReceivedDocumentSource`, `IEtaItemCodeRegistry`,
  `IInboundWhtMatcher`). Production deployment swaps the registration
  line — no caller changes.

### Wave 3 — "Foundation" (v1.3) — ~10-12 days total

| Day | Items | Status |
|---|---|---|
| 1 | P3.1 multi-cashbox/bank + P3.5 unmatched queue scaffold | ✅ shipped |
| 2-4 | P3.2 PDF statement import (CIB, NBE, QNB) | ✅ entity + manual entry shipped; PDF parser deferred to Wave 4 |
| 5-7 | P1.13 / P4.1 E-Receipt + minimal POS | ⏳ deferred — needs ETA POS API access |
| 8 | P4.2 cashier shifts + P4.3 daily Z report | ⏳ deferred — depends on P4.1 |
| 9-10 | P4.4 inventory lite + P4.5 weighted-avg COGS | ⏳ deferred — separate refactor of Item/Sales |
| 11 | P2.2 opening balances + P2.3 unpaid invoices import | ✅ P2.2 shipped; P2.3 deferred to migration assistant push |
| 12 | P2.4 missing doc request links + Wave 3 release | ✅ shipped |

**Release:** v1.3. Captures retail/cash-business segment + finishes
onboarding story.

**Wave 3 closeout (2026-05-11):**
- Foundational data: `CashAccount` (multi-cashbox/bank) + `BankStatement` /
  `BankStatementLine` (manual entry today; PDF parser hooks behind a
  pluggable interface for Wave 4) + opening-balance JE generator with
  Opening Balance Equity (3000) offset.
- New pages: `/settings/cash-accounts`, `/settings/opening-balances`,
  `/payments/bank-statements`, `/payments/bank-statements/import`,
  `/payments/unmatched`. WhatsApp nudge button mounted on
  PurchaseInvoiceDetail.
- 2 EF migrations added: `CashAccounts`, `BankStatements`.
- Voucher-side: `SupplierPaymentVoucher` + `CustomerReceiptVoucher` now
  carry `CashAccountId`; emitters look up the actual account code so
  the trial balance shows balances per cashbox/bank instead of one
  rolled-up `1100`.
- Deferred to Wave 4 with explicit reasons:
  - **P1.13/P4.1 E-Receipt + POS** — needs separate ETA POS API endpoint
    and credentials. Demo value is low without a real cashier flow.
  - **P3.4 Auto-match engine** — XL effort + needs 3-firm beta with real
    bank PDFs to tune the fuzzy matching. Becomes a Wave 4 moat.
  - **P4.4/P4.5 Inventory lite + COGS** — touches sales-line emit,
    purchase-line emit, and item master simultaneously. Needs its own
    refactor push.
  - **P2.3 Unpaid invoices import** — pairs with the migration assistant
    (P2.1) which is its own L item.

### Wave 4 — "Firm Edition" (v2.0) — ~12-15 days total

| Day | Items |
|---|---|
| 1-5 | **P1.11 ETA reconciliation engine (the moat)** |
| 6 | P3.3 PSP imports (Paymob, Fawry) |
| 7-9 | **P3.4 bank matching engine (the moat)** |
| 10-12 | P5.1 payroll lite + P5.2 Egyptian tax forms |
| 13 | P5.3 payslips + P5.4 payroll JEs |
| 14 | Firm Edition packaging — cross-client dashboard, multi-tenant |
| 15 | v2.0 release prep, billing/seats |

**Release:** v2.0 Firm Edition. New SKU, new pricing tier.

### Total cumulative

**~6-8 weeks of focused shipping** to go from where we are now to v2.0
Firm Edition with all moats in place.

If we ship one wave per real-world week (assuming part-time investment +
real-life interruptions), the calendar timeline becomes:

- Wave 1 → end of Week 1
- Wave 2 → end of Week 3
- Wave 3 → end of Week 5
- Wave 4 → end of Week 8

Not 12 calendar months. Not 4 quarters. **~8 weeks.**

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| ETA schema/rule change breaks submission | High | High | CT.4 versioned rule catalog; subscription to ETA SDK release notes |
| Wafeq/Daftra ship competing pre-flight validator | Medium | Medium | Ship P1.7 in Q1; ours covers all 8 validators (theirs partial) |
| Bank PDF format changes silently break parsers | High | Medium | Per-bank version detection; fallback to manual entry |
| WhatsApp Business policy changes block messaging | Medium | High | Email fallback for all notifications; require explicit owner approval |
| 2026 payroll reg changes faster than we ship | Medium | Medium | Version brackets in CT.4; monthly sync |
| Customer success demands Cloud SaaS | High | Medium | Deflect with on-prem trust narrative; monitor lost-deal reasons |
| Egyptian bank PDFs vary too much for OCR to reach 70% match | Medium | High | Beta with 3 firms before P3.4 release; budget for Azure/AWS OCR services |
| Audit log hash chain too slow at >1M entries | Low | Medium | Add periodic checkpoint commits; benchmark in P0.5 |

---

## What I Would Build First If I Had Two Days

If forced to ship in 2 days to start generating differentiation, I'd
combine:

1. **P1.8 Penalty exposure dashboard** (½ day — the narrative — sells
   the product)
2. **P1.2 Cert monitor + alerts** (½ day — the magical moment — every
   accountant has been burned)
3. **P1.9 Cancel/credit wizard** (3-4 hours — the "ah-ha, finally"
   feature)
4. **P0.4 + P0.5 smoke + 5 Playwright scenarios** (1 hour + half a
   day — so we can ship without breaking things)

That bundle = ~2 productive days, demonstrates the "Penalty Shield"
positioning, and sets up the rest of P1 to land within the week.

---

## Document Status

- **Version:** 1.0 (initial)
- **Last updated:** 2026-05-09
- **Owner:** Product (review with engineering for sprint capacity)
- **Next review:** End of Q1 2026 release cycle
- **Source research:**
  - Egyptian SME accounting software market scan (3 agents, May 2026)
  - ETA SDK + 2025-2026 regulatory commentary
  - Daftra/Wafeq/DEXEF/Odoo/SAP feature comparison
