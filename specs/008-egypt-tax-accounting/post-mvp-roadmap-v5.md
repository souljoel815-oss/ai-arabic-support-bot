# Post-MVP Roadmap — v5 (Odoo Parity Sweep)

**Trigger:** Manus AI gap-analysis report dated 2026-05-15
("DaftarX vs Odoo — Comprehensive Gap Analysis"). Captures
8-module-by-module comparison against Odoo 18 CE; flags 14
table-stakes gaps + 32 deferable items + 6 areas where DaftarX
already leads.

**Status when v5 begins:** v4 Phase A + B fully shipped, three
Phase C items shipped (C.2 per-line cost centers, C.8 OFX bank
import, C.9 PDF templates). Server is running clean on :5050;
zero runtime exceptions across the 12 v4 commits.

---

## 1. Critical review of Manus's claims

Before adopting Manus's roadmap wholesale, this section
fact-checks the gaps it flagged. Manus has historically reported
features as "missing" that were actually shipped (the v4 plan
caught 7 such cases). The v5 plan starts by separating real gaps
from stale ones.

### 1.1 Already-shipped (Manus marked as gap)

These are NOT real gaps — Manus was working from incomplete data:

| Manus's claim | Reality | Evidence |
|---|---|---|
| "Expense approval workflow missing" | `ApprovalRequest` aggregate handles `DocumentType.Expense` polymorphically; Submit button is on `/expenses/{id}` already | `src/EgyptTax.Domain/Workflow/ApprovalRequest.cs`, `ExpenseDetail.razor` |
| "Customer Statement / Partner Ledger missing" | Customer-side ledger exists at `/customers/{id}/statement` | `src/EgyptTax.Web/Pages/MasterData/CustomerStatement.razor` |
| "Recurring billing missing" | Recurring invoice templates ship via `RecurringInvoiceTemplate` + `/recurring-invoices` | Migration `20260513155404_RecurringInvoiceTemplates` |
| "Lot tracking missing" | `ItemLot` aggregate + `/items/{id}/lots` page | v3 §11 #5 |
| "ETA item codes missing" | Full lifecycle (`None → PendingGs1/PendingEgs → Active/Failed`) on `Item` | P1.6 |

**Action: do NOT re-build any of these.** Time saved: ~3 weeks.

### 1.2 Half-shipped (some scope remaining)

These are partial gaps — the data path or one side of the
feature exists; the other side genuinely needs building:

| Item | What ships | What's missing |
|---|---|---|
| Partner Ledger | Customer side (`CustomerStatement`) | Supplier statement / `/suppliers/{id}/statement` |
| Multi-currency | `Currency` + `ExchangeRate` entities (N #7) | Per-invoice currency override + FX snapshot at post |
| POS | Single-page checkout (`/pos`, commit `9edcceb`) | Split payment, opening/closing cash, offline mode |
| Sales | Quotation → SalesInvoice pipeline | Reusable quotation templates, customer-specific pricelists |
| Project | Task board (`/projects`, commit `2c6cd47`) | Sub-tasks, priority, Gantt, timesheets |
| CRM | Lead pipeline + activities | Email integration, Sales Teams, marketing attribution |

### 1.3 Genuinely missing (Manus correct)

| Item | Effort | Notes |
|---|---|---|
| Stock valuation report (FIFO / weighted-average) | M ~3d | Reads `Item` + `StockMovement` |
| Quotation templates | S ~3d | Reusable line sets |
| Task priority enum | XS ~1d | Add column + sort |
| Sub-tasks | M ~3d | `ProjectTask.ParentTaskId` + UI |
| Split payment in POS | S ~2d | Allow N payment lines per sale |
| Opening/closing cash control in POS | M ~3d | New `PosSession` entity |
| Supplier statement | S ~2d | Mirror `CustomerStatement` |
| Sales Teams | M ~1w | New `SalesTeam` + `User.SalesTeamId` |
| CRM email integration | L ~2w | Receive + send threading on Lead |
| Pricelists (customer-specific) | L ~1w | New `Pricelist` + line-resolve hook |
| Expense reports (batch submit) | S ~3d | Group expenses → single approval |

**Real Manus signal: ~8 weeks of work** (was 8 in their report;
my correction shaved 3 weeks of false claims and added 0 weeks
of new ones — net the same). The **v5 plan keeps the spirit of
Phase 1 + Phase 2** but corrects the contents.

---

## 2. v5 Phase A — Quick Wins (~2 weeks)

The "demo-impact per day" winners. Each ships independently;
shipped in priority order so an interrupted plan still leaves
visible improvements.

### A.1 — Supplier statement page

- **Pain:** Customer statement exists at
  `/customers/{id}/statement` (FIFO-allocated receipts, aged
  buckets, reminder timeline). The supplier side has nothing —
  asking "what do I owe ABC Suppliers right now and which bills
  are open?" requires opening each PurchaseInvoice individually.
- **Competitor parity:** Odoo's "Vendor Ledger" report.
- **Complexity:** S (~2 days)
- **Dependencies:** Existing `CustomerStatement.razor` (mirror),
  `PurchaseInvoice` aggregate, `SupplierPaymentVoucher` aggregate.
- **MVP slice:**
  - `/suppliers/{id}/statement` page
  - FIFO-allocate `SupplierPaymentVoucher.GrossPaymentAmount`
    against open `PurchaseInvoice` rows ordered by `DateReceived`
  - Per-bill table: doc number, date, gross, paid, open, age
  - Aged-buckets KPIs (current / 1-30 / 31-60 / 61-90 / 90+)
  - CSV export via `CsvExporter`
- **Avoid:** Reminder dispatching to suppliers (they bill us; we
  don't dun them). PDF rendering. Multi-currency on supplier side.

### A.2 — Stock valuation report

- **Pain:** Accountants need to know "what's on the shelf
  worth?" at month-end for the Balance Sheet's inventory line.
  Currently the only stock figure is `Item.QuantityOnHand` × no
  cost — no inventory-cost asset value. Trial Balance line for
  inventory is hand-entered.
- **Competitor parity:** Odoo Inventory's Stock Valuation report
  (FIFO / AVCO / Standard).
- **Complexity:** M (~3 days)
- **Dependencies:** `Item.QuantityOnHand`, `StockMovement`
  history, `PurchaseInvoiceLine.UnitPrice` for cost.
- **MVP slice:**
  - `/reports/stock-valuation` page
  - Per-item: code, name, on-hand qty, weighted-average cost
    (sum of inbound `StockMovement.Quantity × UnitPrice` ÷ sum
    of inbound qty), value = qty × cost
  - Grand total + count of items with no cost data
  - CSV export
- **Avoid:** FIFO valuation (adds layer accounting; weighted-avg
  is the SME-standard in Egypt). LIFO. Per-warehouse split.
  Real-time recosting on every movement.

### A.3 — Task priority + sub-tasks

- **Pain:** v3 §11 #9 shipped a flat task board with three
  states. Real project boards need at least priority + parent-
  child relationships; without them anything beyond a 5-task
  project becomes a wall of equal-weight cards.
- **Competitor parity:** Odoo Project's priority stars + sub-task
  drill-down.
- **Complexity:** S (~3 days, both items)
- **Dependencies:** Existing `ProjectTask` entity.
- **MVP slice:**
  - `TaskPriority` enum (Low / Normal / High / Urgent)
  - `ProjectTask.Priority` + `ProjectTask.ParentTaskId` columns
    (nullable parent), EF migration
  - Sort kanban columns by priority desc within state
  - On `/projects/{id}` detail page: nested sub-task render under
    each parent (one level deep, no recursive nesting)
- **Avoid:** Multi-level sub-task trees. Priority-driven SLA
  rules. Auto-escalation jobs.

### A.4 — Quotation templates

- **Pain:** The same agency that quotes "logo design + 3
  revisions + brand guide" 50 times a year retypes those three
  lines every time. Operators want to save common line sets +
  apply with one click.
- **Competitor parity:** Odoo Sales' quotation templates.
- **Complexity:** S (~3 days)
- **Dependencies:** Existing `Quotation` + `QuotationLine`
  aggregates.
- **MVP slice:**
  - New `QuotationTemplate` entity: name + description +
    `TemplateLine[]` (item, qty, unit price, vat category)
  - `/settings/quotation-templates` management page
  - On `/quotations/new`: dropdown "Apply template…" prepends
    template lines to the new quotation
- **Avoid:** Per-customer template defaults. Conditional lines.
  Variable substitution in description text.

### A.5 — Split payment in POS

- **Pain:** `/pos` accepts a single payment per sale. Real retail
  often splits "200 EGP cash + 350 EGP card" — operator currently
  has to fake one payment method.
- **Competitor parity:** Every POS ships split payment.
- **Complexity:** S (~2 days)
- **Dependencies:** Existing `/pos` page + `PaymentMethod` table.
- **MVP slice:**
  - On `/pos` cart: payment section becomes a list of (method,
    amount) rows with "+ Add payment" button
  - Validation: sum of amounts == cart grand total before Submit
  - Generated `CustomerReceiptVoucher` carries one payment row
    per UI row (existing voucher already supports `PaymentMethod`
    per-row internally)
- **Avoid:** Partial payment (deferred balance). Multi-currency
  payment. Tip calculation.

### A.6 — Opening/closing cash control in POS

- **Pain:** No accountability for the cash drawer — operator
  starts a shift, drops sales in, ends shift, and there's no
  recorded "started with X, expected to end with Y, actually
  counted Z, variance = ±N." Owner can't reconcile.
- **Competitor parity:** Odoo POS sessions.
- **Complexity:** M (~3 days)
- **Dependencies:** `/pos` page, `CashAccount`.
- **MVP slice:**
  - New `PosSession` entity: opened/closed timestamps, opener
    user, opening cash, expected closing (derived from sales),
    actual counted closing, variance, notes
  - `/pos` requires an Open Session before sales; first click
    shows "Enter opening cash" modal
  - "Close session" button: shows expected total, operator types
    actual, variance auto-computed; on confirm, locks the session
  - `/reports/pos-sessions` lists all sessions with variances
- **Avoid:** Cash deposits / withdrawals mid-session. Multi-user
  same-session. Session-time-window enforcement.

**Phase A total: ~2.3 weeks. Order: A.1 → A.5 → A.4 → A.3 →
A.2 → A.6 (visible-impact first; A.6 last because it's the most
behaviour-changing).**

---

## 3. v5 Phase B — Deal Closers (~5 weeks)

Features prospects ask about in every demo. Each is bigger than
Phase A but ships in well-scoped MVPs.

### B.1 — Sales Teams (multi-salesperson)

- **Pain:** v3 ships `User.IsSalesRep` + per-user
  `CommissionRate`, but there's no team grouping. A company with
  two sales reps wants one Sales Team with both reps + a target;
  the manager wants per-team-per-period revenue rollups (not just
  per-individual rep).
- **Competitor parity:** Odoo CRM + Sales' Sales Teams.
- **Complexity:** M (~1 week)
- **Dependencies:** Existing `User`, `Lead.AssignedToUserId`,
  `SalesInvoice.CreatedByUserId`, `SalesByRep` report.
- **MVP slice:**
  - New `SalesTeam` entity: name, lead-user (manager), monthly
    revenue target
  - `User.SalesTeamId` (nullable FK)
  - `/settings/sales-teams` management page (CRUD)
  - Extend `/reports/sales-by-rep`: add a "By team" toggle that
    groups rows + shows team totals + target attainment %
- **Avoid:** Per-team commission rate (use existing per-user).
  Rolling 12-month targets. Quota planning workflow.

### B.2 — Pricelists (customer-specific pricing)

- **Pain:** Today every `Item` has a single price. Real B2B
  pricing is "customer X gets 15% off list" or "customer Y gets
  fixed price 850 on item Z." Operators currently type the
  custom price on every line manually.
- **Competitor parity:** Odoo's Pricelists module.
- **Complexity:** L (~1 week)
- **Dependencies:** `Item`, `Customer`, `SalesInvoiceEdit`.
- **MVP slice:**
  - New `Pricelist` entity: name + `PricelistRule[]` rows
    (`item_id` (nullable for "applies to all"), `customer_id`
    (nullable for "applies to all"), `discount_percent` OR
    `fixed_price_egp`)
  - `Customer.DefaultPricelistId` (nullable FK)
  - `/settings/pricelists` CRUD page
  - On `SalesInvoiceEdit` line add: resolve price via
    `Customer.DefaultPricelistId → Pricelist.Rules → first match`
    (falls back to `Item.UnitPrice` when no rule matches)
  - Surface the applied rule as a small "(via Pricelist X)" hint
    so the operator knows why the price differs
- **Avoid:** Volume-based tiers. Date-bound validity windows.
  Pricelist-on-pricelist inheritance. Currency-specific rules.

### B.3 — CRM email integration (send + receive on Lead)

- **Pain:** Today the only thing recorded against a Lead is
  manually-typed `LeadActivity` notes. Real sales reps live in
  email — they want to see the back-and-forth thread on the lead
  page and reply without leaving the app.
- **Competitor parity:** Odoo's "chatter" panel; HubSpot's email
  threading.
- **Complexity:** L (~2 weeks)
- **Dependencies:** Existing `Lead`, SMTP infrastructure
  (`SmtpPasswordProtector`, `SendInvoiceByEmailHandler`).
- **MVP slice — phase 1 (send only):**
  - On `/leads/{id}` detail: "Send email" composer (subject +
    body; To prefilled with `Lead.Email`)
  - Sent emails persisted as `LeadActivity` rows with
    `kind = EmailSent` + body excerpt + timestamp
- **MVP slice — phase 2 (receive — deferred):**
  - IMAP polling job pulls new mail addressed to a configured
    inbox
  - Match each incoming message by `In-Reply-To` header → Lead
    email; create `LeadActivity` with `kind = EmailReceived`
- **v5 ships phase 1 only.** Phase 2 (receive) waits until a
  customer actually asks — many SMBs are happy with send-only
  threading because they read mail in their main inbox anyway.
- **Avoid:** Full mailbox sync. Calendar invite parsing.
  Attachment sync. Multi-user inboxes.

### B.4 — Expense reports (batch submit)

- **Pain:** Per-expense Submit-for-approval works but is tedious
  for an employee submitting a week of travel — they have to
  Submit each expense individually and the manager Approve each
  individually. Real reimbursement is "here's my week-of-
  conference receipts, approve all together."
- **Complexity:** S (~3 days)
- **Dependencies:** Existing `Expense` + `ApprovalRequest`.
- **MVP slice:**
  - New `ExpenseReport` entity: name, submitted-by user,
    submitted-at, status, total amount; many-to-one back from
    `Expense.ExpenseReportId`
  - `/expense-reports/new` page: pick a date range, multi-select
    open expenses, name the report, hit Submit
  - Single `ApprovalRequest` against the ExpenseReport (not per
    Expense); approver sees the bundle
  - Approval cascades: approving the report approves every child
    Expense in one transaction
- **Avoid:** Mileage / per-diem auto-fill. Manager hierarchy
  routing. Reimbursement payment generation.

**Phase B total: ~5 weeks. Order: B.4 (cheapest) → B.1 → B.2 →
B.3.**

---

## 4. v5 Phase C — Carryover from v4 §C (unchanged)

These wait for a real customer ask. Manus reaffirmed each in
their "Phase 3 — Strategic" list, which matches v4's discipline:

| Trigger | What lands | Effort |
|---|---|---|
| Customer with foreign suppliers | Per-invoice currency + FX snapshot at post + revaluation | XL ~3w |
| Retail customer with bad internet | POS offline mode (PWA service worker + sync queue) | L ~2w |
| Pharmacy/electronics customer | Per-serial tracking layer on top of lots | L ~2w |
| Distribution customer with procurement | Full Purchase Orders (RFQ → PO → Receive → Bill, 3-way matching) | XL ~3w |
| Consulting firm | Timesheets + project P&L + Gantt | XL ~4w |
| Customer with significant fixed assets | Asset depreciation schedules | M ~2w |
| Marketing-heavy customer | UTM / campaign / source attribution on Lead | S ~3d |
| Customer with high lead volume | AI lead scoring / probability | XL ~2w |

The v4 anti-roadmap discipline holds: **don't pre-build any of
these**. The Manus v5 report ranks Purchase Orders as #13 in
Phase 3 (deferred), which finally aligns with the v4 read.

---

## 5. Anti-Roadmap — Carryover + Strategic Reaffirmation

Items that remain explicitly NOT on any v5 / v6 path. Manus's v5
report explicitly endorses this discipline ("**do not try to be
Odoo**"):

| Feature | Why not (carried from v4) |
|---|---|
| Full MRP / Manufacturing | 6+ months; segment doesn't need it |
| Dashboard Designer (Studio-lite) | Decade-long Odoo investment |
| Native iOS/Android apps | PWA covers it |
| Egyptian Payroll core | Regulatory; only build for 100K EGP prepay |
| eCommerce site builder | Shopify exists |
| Subscriptions module (full) | L3 + recurring invoices cover 80% |
| Document management | Google Drive exists |
| Helpdesk / ticketing | Out of scope |
| Field service management | Out of scope |
| Email marketing | Mailchimp exists |
| VoIP integration | WhatsApp wins in Egypt |
| Restaurant mode (POS tables) | Carried from v4 |
| Self-service kiosk | Carried from v4 |
| Loyalty programs | L3 (referrals) covers basic case |
| Customer display (POS) | Hardware-dependent; defer |

**New for v5 (from Manus's deferred list):**
- **Lead mining / IAP enrichment** (Odoo's paid feature) —
  speculative ROI on Egyptian datasets
- **Multi-warehouse routes / putaway rules** — only matters at
  >10-warehouse scale, beyond MVP segment
- **AI lead scoring** — already on the v4 anti-roadmap; Manus
  also flags as defer

---

## 6. Sequencing — 3-week v5 plan

Realistic schedule for one developer (operator + AI-paired):

| Week | Focus | Deliverable |
|---|---|---|
| 1 | A.1, A.5, A.4, A.3 | Supplier statement + POS split payment + quotation templates + task priority/sub-tasks |
| 2 | A.2, A.6, B.4 | Stock valuation + POS sessions + expense reports |
| 3 | B.1, B.2 | Sales Teams + Pricelists |
| 4 (slip) | B.3 | CRM send-email |

**Done state (week 4):** every Manus AI Phase 1 + Phase 2 item
either shipped (the genuine ones) or struck through (the stale
ones). v5 release notes can lead with: *"Closed every gap our
Odoo-comparison auditor flagged. POS sessions, supplier ledger,
sales teams, customer-specific pricing — all the demo-blockers
now answered with a 'yes, here it is'."*

---

## 7. v6 Trigger Conditions

Same shape as v3 §5/§8/§12 + v4 §7. Write v6 only when ONE of
these fires:

1. **5 paying customers for ≥ 30 days.** v6 reacts to their
   tickets — not to any analyst's gap report.
2. **A specific deal of 100K+ EGP/year ARR** blocked by a
   v5-out-of-scope feature. That deal's needs become v6
   priority 1.
3. **Egyptian tax-law change** mandating new behaviour (ETA
   threshold drop, new penalty regime, e-receipt mandate
   activation, payroll-related compliance shift).
4. **One of the v4 §4 + v5 §4 trigger conditions fires** for a
   specific named customer.
5. **3+ trial customers ask for the same single feature** that's
   not on any phased plan — emergent-pattern signal.

---

## 8. Honest assessment vs Manus's report

Where I agree with Manus (fully):
- Egyptian tax compliance is the moat; double down on it
- "Do not try to be Odoo" is the right strategy
- Phase 3 items (multi-currency, full POs, timesheets, POS
  offline, serial tracking, asset depreciation, AI lead scoring)
  should wait for customer pull
- Sales Teams + Pricelists + CRM email are the demo-killer
  table-stakes gaps

Where I disagree with Manus (corrections):
- Expense approval is NOT missing — `ApprovalRequest` aggregate
  is polymorphic and works for `DocumentType.Expense` today
- Customer-side Partner Ledger is NOT missing —
  `CustomerStatement.razor` ships at `/customers/{id}/statement`
- Recurring billing is NOT missing — `RecurringInvoiceTemplate`
  ships at `/recurring-invoices`
- "AI" features (lead scoring, AI-OCR, AI prediction) shouldn't
  be ranked as a competitive disadvantage — Odoo's AI features
  are mostly mediocre + we ship Claude-Vision OCR which is
  genuinely better than Odoo's

Where Manus surfaces real new value (not in the v4 plan):
- **Stock valuation report** (A.2) — accountant-facing, low
  effort, high value at month-end. Worth shipping.
- **POS sessions / cash control** (A.6) — surprisingly absent
  from v4; clear retail blocker.
- **Quotation templates** (A.4) — we surface "save as template"
  for nothing today; cheap to add.
- **Sub-tasks** (A.3) — Project board needs this beyond toy
  scope.

Net: ~7 weeks of useful work, not 8. The plan above ships in
3-4 weeks of paired-with-AI development.
