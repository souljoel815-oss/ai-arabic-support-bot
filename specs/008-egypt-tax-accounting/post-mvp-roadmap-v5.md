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
| "Asset depreciation deferred" | Full `FixedAsset` aggregate (Draft → InService → Disposed/WrittenOff), `MonthlyDepreciationJob`, `JournalVoucher.CreateAutoDepreciation`, pages at `/fixed-assets`, `/fixed-assets/new`, `/fixed-assets/{id}/schedule` | Phase 8 / FR-017, migration `20260508040250_FixedAssets` |

**Action: do NOT re-build any of these.** Time saved: ~5 weeks
(was ~3 — the Asset depreciation correction added 2 more weeks
that would otherwise have been re-built).

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

### B.5 — REST API write endpoints (customers / leads / expenses)

- **Pain:** v4 B.3 shipped `POST /api/v1/invoices/draft` —
  enough to demo the integration story but a single endpoint
  blocks any real partner ship. Shopify integrators want
  `POST /api/v1/customers` to push a new buyer; Mailchimp /
  marketing tools want `POST /api/v1/leads`; mobile receipt-
  capture apps want `POST /api/v1/expenses`. Each is a 401-
  closing demo for that integration partner.
- **Competitor parity:** Odoo XML-RPC / REST has full CRUD on
  every model out of the box (different ergonomics; same surface
  area).
- **Complexity:** M (~1 week)
- **Dependencies:** Existing `ApiKeyService`,
  `ApiKeyRateLimiter`, `WebhookDispatcher` (so creates fan out
  to webhooks too — `customer.created`, `lead.created`,
  `expense.created`).
- **MVP slice:**
  - `POST /api/v1/customers` — JSON body
    (`code`, `name_ar`, `name_en`, `tin?`, `phone?`,
    `email?`, address fields). Returns 201 + the created row.
    Reuses `CustomerImportHandler`'s validation.
  - `POST /api/v1/leads` — JSON body (`name`, `phone` OR
    `email`, optional `company`, `source`, `expected_value_egp`,
    `expected_close_date`). Reuses `LeadImportHandler`'s
    validation.
  - `POST /api/v1/expenses` — JSON body (`document_date`,
    `category_id`, `amount_egp`, `deductible_flag?`,
    `description_ar`, `description_en`). Creates Draft only;
    operator posts via the regular page (FR-027 keeps the post
    path interactive).
  - All three obey the existing rate-limit + auth gate.
  - Each emits the matching webhook event on success.
- **Avoid:** Update endpoints (PUT/PATCH) — the integration
  patterns we know about today are all create-only. Bulk
  endpoints (Shopify pushes one customer per webhook). Validation
  config (use the import handlers' rules verbatim).

**Phase B total: ~6 weeks. Order: B.4 (cheapest) → B.5 → B.1 →
B.2 → B.3.**

---

## 3.5. v5 Phase D — Promoted from §C (operator request 2026-05-15)

The operator explicitly promoted three Phase C items into the
active build queue, overriding the v4 / v5 default discipline of
"wait for customer pull." One of the three (asset depreciation)
turned out to already be shipped — corrected in §1.1 above. The
remaining two are real new work:

### D.1 — Serial number tracking (L ~2w)

- **Pain:** Lots cover the common case (a batch of 100 paracetamol
  packs share one expiry + one supplier). But pharmacies with
  dispense-tracking and electronics retailers selling laptops with
  warranty coverage need per-unit identity — "which exact serial
  did customer X buy on date Y?" — and that's beyond what `ItemLot`
  models.
- **Competitor parity:** Odoo Inventory's "Track by Serial Number"
  product flag.
- **Complexity:** L (~2 weeks)
- **Dependencies:** `Item.TracksLots` (mirror flag pattern),
  `ItemLot` (parent if serial belongs to a lot), `StockMovement`
  audit trail.
- **MVP slice:**
  - New `ItemSerial` entity per (item_id, serial_number,
    lot_id?, status, current_location_id?, current_customer_id?)
  - `ItemSerialStatus` enum: InStock / Reserved / Sold /
    Returned / Damaged / WrittenOff
  - `Item.TracksSerials` opt-in flag (sibling to TracksLots)
  - `/items/{id}/serials` management page — CRUD + status filter
  - On `SalesInvoiceEdit`: when line's item has TracksSerials,
    surface a "Serials" sub-grid below the line — operator types
    or pastes serial numbers (one per line); validation rejects
    serials not InStock or quantity-mismatched
  - `SalesInvoiceLine.SerialIds` (JSON column on the line); on
    post, each listed serial transitions to Sold + records the
    customer + invoice id
  - On stock receive: when receiving an item with TracksSerials,
    operator must list the new serials being received (one
    `ItemSerial` row created per serial, all InStock)
- **Avoid:** Barcode-scanner integration (waits for hardware
  spec — Phase C carryover). Bulk serial generation (e.g., "give
  me 1000 sequential serials"). Serial range entry. Cross-item
  serial uniqueness (we scope to per-item; same serial on two
  different SKUs is a valid distinction in practice). Mobile
  capture flow.
- **Risk + flag:** Wraps the sales-post path for the affected
  items — feature flag `SerialTrackingEnabled` (default false on
  fresh installs; opt-in per item via `Item.TracksSerials`)
  ensures shops not using serials see zero behaviour change.

### D.2 — Timesheets + Project P&L + Gantt (XL ~4w, three sub-items)

- **Pain:** v3 §11 #9 shipped a basic project task board.
  Consulting firms — the segment most likely to push hard on
  project features — need three things on top of that: log hours
  per task per day (timesheets), see the per-project bottom line
  (revenue from tagged invoices minus labor cost minus tagged
  expenses + purchases = P&L), and visualise the schedule
  (Gantt). Without these, "we use DaftarX for projects" doesn't
  hold up against any consultancy ERP.
- **Competitor parity:** Odoo Project + Timesheets + Gantt view
  combined.
- **Complexity:** XL (~4 weeks total)
- **Dependencies:** Existing `Project` + `ProjectTask` aggregates;
  C.2 per-line cost-center tagging (a59b996) for the financial
  rollup; A.3 sub-tasks (Phase A) for the Gantt nesting.

#### D.2.1 — Timesheets (~1.5w)

- New `TimesheetEntry` per (user_id, date, project_id, task_id?,
  hours, billable_flag, hourly_rate_egp, note)
- `/timesheets/me` weekly entry page: 7-day grid with project
  rows; user types hours per cell, marks billable, picks rate
- `/timesheets/team` manager view: filter by user / project /
  week; total hours + total billable amount per row
- Validation: hours per day per user ≤ 16 (catch typos);
  hourly_rate inherits from `User` profile field (new col)
- Skip: timer mode (start/stop on a task), approval workflow on
  timesheets (defer to v6), per-customer rate cards

#### D.2.2 — Project P&L (~1w)

- `/projects/{id}/pnl` page: rolls up
  - **Revenue:** sales-invoice lines tagged to the project's
    cost-center (`Project.LinkedCostCenterId` → new nullable
    column linking project to cost-center)
  - **Labor cost:** sum of TimesheetEntry (hours × rate) for the
    project, regardless of cost-center tag
  - **Direct costs:** purchase-invoice lines + expenses tagged
    to the project's cost-center
  - **Net margin:** revenue − labor − direct costs
- Same per-period filter as `/reports/cost-centers`
- CSV export
- Skip: WIP accounting (defer to v6 if a customer asks); per-
  task drill-down (project-level only); recovery rate (% of
  hours billed)

#### D.2.3 — Gantt chart (~1.5w)

- `/projects/{id}/gantt` page: read-only timeline view
- New `ProjectTask.StartDate` + `ProjectTask.DueDate` columns
  (both nullable; tasks without dates skipped from the chart)
- Layout: x-axis = day columns (zoom levels: week / month /
  quarter); y-axis = tasks (one row per task), sub-tasks (from
  A.3) indent under their parent
- Each task renders as a colored bar spanning start → due, color
  by status (Todo gray / InProgress blue / Done green)
- Pure HTML/CSS via positioned divs — no heavy chart library; we
  trade interactivity for zero new JS dependency
- Skip: drag-to-reschedule (defer); inter-task dependencies
  (defer); critical-path highlighting; baseline tracking

- **Risk + flag:** Project P&L wires into the cost-centers report
  data path; if the linkage produces wrong margins on a live
  project, operator wants escape hatch. Feature flag
  `ProjectPnlEnabled` (default true) lets the operator hide the
  P&L tab without removing the timesheet data. Timesheets +
  Gantt are additive (no behaviour change to existing flows) so
  no flag needed.

**Phase D total: ~6 weeks. Order: D.1 → D.2.1 → D.2.3 → D.2.2.**

---

## 3.6. v5 Design Polish — operator-pulled subset (~2 weeks)

The operator surfaced two design docs (2026-05-15): a full design
system (Manus AI) targeting React/TSX naming conventions, and a
navigation redesign reducing the sidebar from "30+ links" to 8
top-level groups. Honest reading on the codebase:

* The design tokens / colors / spacing / RTL rules / motion specs
  are framework-agnostic and apply directly to the Blazor app.
* The "30+ links" strawman is unfair — the live sidebar has **83
  links across 6 collapsible groups** (verified). The proposal's
  real win is consolidating to 8 top-level groups + adding the
  smart behaviors (Cmd+K palette, contextual notifications,
  recent/favorites).
* Several flagged "gaps" already ship: RTL works, CSS tokens
  exist, sidebar IS already grouped + collapsible, status colors
  drive components.

The operator picked the **high-ROI subset** (~2 weeks) over the
full 6-week redesign:

### DP.1 — Toast notification system (2d)

- **Pain:** Inline `flash-success` / `flash-error` divs scattered
  across 100+ pages; no consistent feedback channel for async
  events (background jobs finishing, webhook fires).
- **MVP slice:** Singleton `ToastService` that pages inject;
  fixed bottom-right (LTR) / bottom-left (RTL) container in
  `MainLayout.razor` that subscribes + renders 1..N toasts; auto-
  dismiss after 5s; success / error / warn / info variants
  matching the design system's color tokens.
- **Avoid:** Action buttons in toasts. Toast queueing past 5
  visible. Animations longer than 300ms.

### DP.2 — Sidebar consolidation (83→~22 links in 8 groups, 2-3d)

- **Pain:** Current sidebar lists 83 links — most users only need
  6-8 of them. Cognitive overload on first login.
- **MVP slice:** Reorganise `MainLayout.razor` into the 8 groups
  per the navigation redesign doc:
  - الرئيسية (Dashboard)
  - المبيعات (Sales: invoices + quotations + receipts + leads)
  - المشتريات (Purchases: bills + supplier-payments + expenses)
  - المخزون (Inventory: items + adjustments + reorder)
  - جهات الاتصال (Contacts: customers + suppliers)
  - نقطة البيع (POS — direct link, no sub-items)
  - التقارير (Reports: P&L, BS, GL, Cash Flow, Aging, Cost Centers)
  - الإعدادات (Settings: company, CoA, tax, currencies, API
    keys, bank import, users, webhooks, quotation templates)
- Pages move only at the *navigation tree* level — no URLs
  change, so existing bookmarks / saved links keep working.
- **Avoid:** Renaming any pages. Restructuring the URL space.
  Deleting any pages from the app — just hiding less-used ones
  inside their group.

### DP.3 — Cmd+K command palette (3d)

- **Pain:** Power users want to jump straight to any page +
  trigger any common action without sidebar-hunting.
- **MVP slice:** Global keyboard shortcut (Ctrl+K / ⌘K) opens a
  centered modal with a search input + filtered list of:
  - Every page route + its label (Ar+En)
  - Common quick-create actions ("+ فاتورة جديدة", "+ مصروف
    جديد", "+ عميل جديد")
  - Recent customer / item lookups (top 10 from a
    `RecentNavigations` cookie)
- Type-to-filter matches across both languages; Enter navigates
  / Esc closes.
- **Avoid:** Fuzzy matching beyond simple prefix-contains
  (lib-free; we don't need a Levenshtein dependency). AI-powered
  search. Action history beyond cookie.

### DP.4 — Mobile bottom tab bar (2d)

- **Pain:** PWA installs but the desktop sidebar consumes a
  third of a phone screen. No proper mobile nav.
- **MVP slice:** Below 768px width, hide the sidebar and render
  a fixed 5-tab bottom bar: Dashboard / Sales / [Quick-create
  FAB] / Reports / Settings. The "More" overflow opens a sheet
  with the remaining 4 groups.
- **Avoid:** Per-tab badges (defer until counters exist).
  Animated transitions between tabs (the page swap is enough).

### DP.5 — Dark theme (Leil) + theme toggle (~1w)

- **Pain:** Single light theme; operator-requested dark mode
  for late-night accountants (a real Egyptian SMB pattern).
- **MVP slice:** Add a `[data-theme="leil"]` selector that
  overrides every CSS variable in `site.css`'s `:root` block
  with the Leil palette from §2.2 of the design system. Theme
  toggle in the user menu (top bar) writes the preference to a
  cookie + applies on subsequent renders. Visual QA pass across
  the top 20 pages to fix any inline-color leaks (e.g., hard-
  coded `#000` in templates instead of `var(--text)`).
- **Avoid:** Dhahabi (gold) + HighContrast themes — defer until
  customer pull. Auto-switching by OS preference (operator
  picks explicitly). Per-page theme overrides.

**Design Polish total: ~2 weeks. Order: DP.1 → DP.2 → DP.3 → DP.4 → DP.5.**

After DP.5 ships, **resume v5 Phase A2** (A.2 stock valuation +
A.6 POS sessions) per §6 sequencing, then Phase B + D as planned.

---

## 3.7. v5 UI Rebuild — operator-pulled (~9.5 weeks total)

**Status update 2026-05-15 (afternoon):** The original §3.7 below
(6-sprint plan kicked off after DP.5) was paused after the
operator pushed back on the first attempt. Operator went away,
worked with Manus to produce a much more detailed spec, and
returned with **5 high-fidelity mockups + a full screen-by-screen
Manus design document** dropped on 2026-05-15.

**Authoritative spec lives in [`ui-redesign-spec-2026-05-15.md`](./ui-redesign-spec-2026-05-15.md).**
The summary: replace the single-level 30-item sidebar with a
3-tier "Mission Control" navigation:
- **64px Module Sidebar** — 7 module icons (Dashboard / Sales /
  Purchases / Inventory / Accounting / Contacts / Settings)
- **240px Sub-Nav Panel** — context-sensitive per active module,
  groups by named section (e.g. Accounting → Tax / Closing / Reports)
- **Command Palette** — universal fuzzy search (already shipped
  via DP.3; polish in this rebuild)

**Implementation discipline (operator-confirmed, 2026-05-15):**
the Manus spec proposes React/TypeScript/Vite/shadcn — that's
incompatible with our Blazor Server stack and would require a
months-long rewrite of all ~145 `.razor` pages. Operator picked
"adopt design intent in Blazor" — the IA + visual + UX patterns
transfer 1:1 to `.razor`; the file-structure paths the spec gives
as `client/src/components/...tsx` map to
`src/EgyptTax.Web/Shared/AppShell/...razor`.

**Sprint plan (Blazor adaptation, supersedes the 6-sprint plan
written below for the prior operator pass):**

| Sprint | Deliverable | Effort |
|---|---|---|
| 1 | `AppShellLayout.razor` + `ModuleSidebar.razor` + `SubNavPanel.razor` + Dashboard rebuild matching mockup #5. Built side-by-side with existing `MainLayout`; only Dashboard switched over so the other 144 pages keep working unchanged | ~1 week |
| 2 | Migrate Sales pages (lists + details set the template) | ~2 weeks |
| 3 | Migrate Purchases pages | ~1.5 weeks |
| 4 | Migrate Inventory pages | ~1 week |
| 5 | Migrate Accounting pages + every report | ~2 weeks |
| 6 | Migrate Contacts + CRM Pipeline (Kanban) | ~1 week |
| 7 | Migrate Settings + polish (mobile responsive, empty states, loading states) | ~1 week |

Total: **~9.5 weeks**. Sprint 1 is the high-risk sprint (gets the
shell right, gets one screen pixel-matched to the mockup so the
operator can sign off the visual direction); the rest is mechanical
migration of one screen pattern at a time.

The 6-sprint plan written below is **superseded** but kept for
historical context (it documents what the operator pushed back on
+ what we learned about not band-aiding CSS on top of legacy).

---

### Original 6-sprint plan (superseded — kept for context)

**Trigger:** 2026-05-15 operator feedback (morning). After DP.1–DP.5 shipped,
the operator surfaced six high-fidelity Arabic mockups (Dashboard
light + dark, Invoice editor, POS, CRM kanban, sidebar
Before/After) and explicit critique of DP.2: *"شلت اللينكات
رميتها كلها في settings — انا استفدت اي"*. The §3.6 Design
Polish was a band-aid; the mockups call for an actual UI rebuild.

The operator picked a 6-sprint plan, all six executed in
sequence (no Phase A2/B/D work in between).

### Sprint 1 — Sidebar v2 (~2d)

What the mockup specifies (last image):
- DaftarX gold logo at top
- Full-width **search box** to filter nav items live
- ⭐ **المفضلة** section with operator-pinned items (gold pin
  icon next to each)
- 8 groups, each expandable; active group highlighted **gold**
  (`--accent-gold` #D4A017) with chevron-up
- Sub-items revealed on expand, connected by a dotted vertical
  border on the inline-end side
- 3–6 sub-items per group max — NOT the 22-item dumping ground
  DP.2 left in Settings

What ships:
- Sidebar search input (filters visible items as user types)
- Favorites: localStorage list of `{url, label}`; pin/unpin from
  hover ⋮ menu on any nav item; favorites render at top under
  "المفضلة" with the gold pin icon
- 8 groups properly organized:
  - **الرئيسية** — direct link
  - **المبيعات** (5): invoices, quotations, leads, sales
    orders, customer receipts
  - **المشتريات** (4): purchase invoices, expenses, supplier
    payments, AI scan-receipt
  - **المخزون** (5): items, stock count, reorder suggestions,
    stock transfer, locations
  - **جهات الاتصال** (3): customers, suppliers, data import
  - **نقطة البيع** — direct link
  - **التقارير والامتثال** (organized with sub-headers,
    not 18-item dump): GL / TB / Cash Flow / Cost Centers /
    Sales reports / VAT / Income tax / WHT / Penalty Shield /
    ETA dashboard
  - **الإعدادات** (organized): Company / Tax / Accounts /
    Advanced (templates, webhooks) / Audit
- Visual: gold-active styling, dotted connector lines on sub-
  items, modern type weight

### Sprint 2 — Dashboard rebuild (~3d)

What the mockup specifies (images 4 + 5):
- 4 large KPI cards at top: Total Sales, Expenses, Net Profit,
  Overdue Invoices
- Each card: colored circular icon (green up-arrow / red wallet
  / green credit-card / red triangle), big bold number with EGP
  suffix, trend % with up/down arrow ("12.5% up from previous
  period")
- Monthly Sales & Expenses **bar chart** (6 months back,
  blue=sales / gray=expenses)
- **Recent Activity** timeline on right: per-row status pill
  (Paid green, Partial blue, Overdue red), date + time, company
  name, INV-XXXX-XXXX number link
- Dark mode variant: same layout with glow on card borders

What ships:
- Replace `Index.razor` with the new layout
- 4 KPI cards using existing data (fetch real numbers from
  posted invoices + expenses + AR)
- Bar chart via Chart.js (one new ~30KB JS dep — accepted)
- Recent Activity = last 10 posted invoices with their state +
  outstanding balance derived from receipts

### Sprint 3 — POS rebuild (~3d)

What the mockup specifies (image 2):
- Category tabs at top (الكل / إلكترونيات / أثاث / مستلزمات)
- Product grid with **image thumbnails** (placeholder + photo
  upload later); name + price below each tile
- Cart pane on right: per-row qty controls (+ / − / ×), price,
  subtotal
- Big totals block (subtotal + VAT 14% + total)
- 3 huge payment buttons:
  - **نقدي** (green, with banknote icon)
  - **بطاقة** (blue, with card icon)
  - **تقسيم** (orange, with split icon — opens DP.5's split
    payment we already have)

What ships:
- Add `Item.ImageUrl` (nullable) + categories navigation
- Replace `/pos` with the new tile-grid + colored payment
  trio; preserves the v5 A.5 split-payment + AR-settling logic
  from `b8ba5f1` underneath

### Sprint 4 — Invoice editor rebuild (~3d)

What the mockup specifies (image 1):
- Breadcrumb (الفواتير > فاتورة جديدة)
- 4 fields in one row: Customer (with dropdown + search),
  invoice number (auto), invoice date, due date — date pickers
  with calendar icons
- Customer dropdown opens a search field + list with C-001 codes
  and "+ عميل جديد" inline link
- Lines table: trash icon per row, item dropdown, qty, unit
  price, VAT dropdown, total
- Totals card on the right (subtotal, VAT 14%, **bold total**)
- 3 buttons at bottom: حفظ كمسودة (gray), إرسال للعميل (blue),
  إرسال للضرائب (orange/gold)

What ships:
- Rewrite `SalesInvoiceEdit.razor` to match the layout
- Customer dropdown with inline search + create-new shortcut
- Cleaner line editor with date-picker + VAT dropdown styling
- Three-button footer with new color treatment

### Sprint 5 — CRM kanban polish (~2d)

What the mockup specifies (image 3):
- 5-column kanban: جديد / مؤهل / عرض سعر / تفاوض / مكسب
- Each column header shows total deal value: "إجمالي: 180,000 ج.م"
- Cards show: company name, contact name, deal value, probability
  badge (75% / 60% / 50% / 100%), status dot color (red/yellow/
  green), "آخر نشاط منذ X يوم"
- "+ فرصة جديدة" button (gold) at top-right
- Filter chips: تصفية / منتجات/خدمات / المسؤولين / تاريخ متوقع /
  المزيد

What ships:
- Update `Leads.razor` (the kanban) to match column header style
  + per-stage totals + activity-staleness indicator + status dot
- Filter chips at top

### Sprint 6 — Component library + visual QA (~2d)

- Status pills (Paid green / Partial blue / Overdue red /
  Cancelled gray / Draft) — single component used across all
  list pages
- Button variants standardized (primary blue / success green /
  warning orange / danger red / ghost gray)
- Card component (border-radius 12px, padding 24px, shadow
  pattern from §6.3 of the design doc)
- Visual sweep across the top 15 pages to apply the new
  components

**UI Rebuild total: ~3 weeks. After Sprint 6, resume v5 Phase A2.**

---

## 3.8. v5 Phase E — Accountant-workflow gaps (Odoo course pass)

Source: Manus AI's *Odoo 18 Accounting Full Course* gap analysis,
2026-05-15. The analyst watched 21 of 76 lectures and listed 12
"missing" workflows. **Before adding to this plan I verified each
claim against the codebase** — same discipline as §1.1 — because
the prior Manus review had a 33% stale-claim rate and adding
unverified work would burn weeks shipping things that already
ship.

### 3.8.1 Critical review — what Manus got wrong this round

| Manus claim | Status | Evidence |
|---|---|---|
| GL + Trial Balance reports missing | **Stale** | `Pages/Reports/GeneralLedger.razor` + `TrialBalance.razor` already render with date filters + drill-down; backed by `IGeneralLedgerReportQuery` + `ITrialBalanceReportQuery`. |
| Inventory scrap workflow absent | **Partial** | `StockAdjustment` entity ships with reasons (Shrinkage / Damage / Recount / Other) and emits `StockMovement.Adjustment` rows. What's missing is the JE side — the adjustment doesn't currently book to a Loss-of-Inventory expense account. Scope is *wire JE emission*, not build the workflow. |
| COA + Opening-Balance import missing | **Partial** | `OpeningBalances.razor` already lets the operator type opening balances for cash accounts and posts a balanced JE to "Opening Balance Equity (3000)". What's missing is *bulk CSV upload* + *full GL accounts* (today only cash). Scope shrinks to "extend the existing page," not "build from scratch." |
| Customer Statement / Partner Ledger missing | **Partial** | `CustomerStatement.razor` ships at `/customers/{id}/statement` and shows the aging buckets. Manus calls this missing because Odoo's "Partner Ledger" shows full ledger lines (every invoice + receipt with running balance), not just the buckets. Scope = add a "Detailed view" toggle that lists the underlying transactions. |

Net of the 12 claims: **9 genuinely missing, 3 partial (smaller
than Manus estimated), 0 fully shipped**. Better hit-rate than
the v4 Manus pass (where 4/N items were already done).

### 3.8.2 Verified gaps — verbatim from the course pass

These are real. Sized + sequenced honestly (not Manus's
estimates, which assume from-scratch builds).

#### E.1 — Customer advances as current liability (HIGH)

- **Pain:** Egyptian construction / custom-manufacturing /
  professional-services SMBs collect deposits up-front. Today
  the operator either books the deposit as a `CustomerReceiptVoucher`
  (which lands as a credit balance with no AR invoice to clear
  against, polluting the customer ledger) or — worse — fakes a
  draft invoice and posts it early (premature revenue
  recognition; IFRS / EAS violation).
- **Complexity:** M (~5 days)
- **Dependencies:** New `CustomerAdvance` aggregate;
  `Quotation` / `SalesOrder` (already shipped).
- **MVP slice:**
  - From a confirmed Quotation: "Request down payment" → enter
    amount → creates a `CustomerAdvance` record + a draft invoice
    that books to a new `2310 Customer Advances` liability account
    (added to the EG seed COA).
  - When the final sales invoice posts, the held advance
    auto-applies as an AR offset (DR Customer Advances liability /
    CR AR), surfacing the net balance owed.
  - Customer Statement shows the advance as a credit row.
- **Avoid:** Multi-currency advances. Partial draw-downs across
  multiple invoices (one advance → one final invoice for v5).

#### E.2 — Group payments (multi-invoice receipt) (HIGH)

- **Pain:** Today every `CustomerReceiptVoucher` is one wire
  transfer for one customer. When a customer pays 6 outstanding
  invoices with one cheque, the operator has to either (a) create
  6 separate vouchers (slow + bank reconciliation breaks because
  the bank statement has one line) or (b) create one voucher
  with the total + manually allocate it across the 6 invoices in
  the existing per-voucher allocation grid. The grid handles
  this — but the *entry path* is via "create voucher → search
  invoices one by one." The Odoo flow inverts: from the invoice
  list, multi-select 6 invoices → "Pay" → one voucher pre-loaded.
- **Complexity:** S (~3 days, NOT 4d Manus estimated — the
  underlying `PaymentAllocations` already handles N-to-1)
- **Dependencies:** `CustomerReceiptVoucher.Allocations`
  (already polymorphic).
- **MVP slice:**
  - On `/invoices`, add a checkbox column on rows where
    `Status = Posted AND OutstandingBalance > 0`.
  - "Pay selected (N)" button at the toolbar pre-fills the
    `/payments/customer-receipts/new` page with the sum +
    individual allocation lines.
- **Avoid:** Cross-customer grouped receipts (each receipt is
  still per-customer; mixing customers on one cheque is rare and
  would complicate the AR ledger). Auto-detect of which invoices
  the cheque amount matches (operator picks).

#### E.3 — Purchase credit notes from a bill (MEDIUM)

- **Pain:** `SalesInvoice.CreditNoteOfInvoiceId` exists; the
  purchase side has no equivalent. When a supplier sends a credit
  note, the operator has to manually post a `JournalVoucher` —
  no audit-traceable link back to the original `PurchaseInvoice`.
- **Complexity:** S (~3 days)
- **Dependencies:** Existing `PurchaseInvoice`.
- **MVP slice:**
  - Add `PurchaseInvoice.CreditNoteOfPurchaseInvoiceId` (mirrors
    sales side).
  - "Issue purchase credit note" action on a posted bill →
    pre-fills lines from the original (operator deletes
    non-returned items, adjusts qty), posts as a separate
    `PurchaseInvoice` with `IsCreditNote = true`.
  - JE: DR Accounts Payable / CR Stock Interim (mirroring
    `IssueCreditNoteCommand` on the sales side).

#### E.4 — Inventory adjustment posts to expense account (MEDIUM)

- **Pain:** `StockAdjustment` writes the inventory delta but
  doesn't post the matching JE. Today the inventory shrinks on
  the books only (`StockMovement.Adjustment`); the financial
  ledger doesn't reflect the loss until someone manually posts a
  matching JV. So `Inventory account != ledger inventory` until
  reconciled.
- **Complexity:** S (~2 days, NOT 4d Manus estimated — the
  workflow exists; only the JE emit is missing)
- **Dependencies:** `StockAdjustment` (shipped),
  `JournalVoucher` (shipped).
- **MVP slice:**
  - When a `StockAdjustment` posts with reason ∈
    {Damage, Shrinkage}, auto-emit a JV: DR `6140 Inventory Loss`
    / CR `1410 Inventory Asset`, valued at the item's
    weighted-avg cost (already computed for `/reports/stock-valuation`).
  - For reason = Recount with positive delta: DR Inventory /
    CR `4900 Inventory Gain`. Reason = Other: prompt for an
    operator-supplied account.
- **Avoid:** Per-location loss accounts (one global account is
  enough). Variance categorization beyond the 4 reasons.

#### E.5 — Cash transfers between accounts (MEDIUM)

- **Pain:** Bank-to-bank or bank-to-cash transfers require a
  manual `JournalVoucher`. Common daily operation that should be
  one click.
- **Complexity:** S (~2 days)
- **Dependencies:** `CashAccount` (shipped), `JournalVoucher`.
- **MVP slice:**
  - `/cash-accounts/transfer` page: source account, destination
    account, amount, date, optional memo.
  - On submit: emit a balanced JV (DR destination cash account /
    CR source cash account); both cash-account ledgers reflect
    the transfer.
  - Transfer history view filtered to JVs tagged
    `TransferBetweenCashAccounts = true`.
- **Avoid:** FX on the transfer (single-currency for v5).

#### E.6 — COA + opening-balance bulk import (MEDIUM)

- **Pain:** `OpeningBalances.razor` only handles cash accounts.
  Migrating an existing business needs to import the full COA
  trial balance.
- **Complexity:** S (~3 days, NOT 4d Manus estimated — the seed
  COA + the OpeningBalances page already exist; scope is
  extending them)
- **Dependencies:** `OpeningBalances.razor`, `JournalVoucher`.
- **MVP slice:**
  - Extend `OpeningBalances.razor` to list all accounts (not
    just cash) with debit/credit input columns.
  - Add a "Download template" button → CSV with one row per
    seeded account; "Upload" parses + previews → operator
    reviews → posts a single balanced JV stamped with the
    opening date.
  - Validation: if debits ≠ credits, surface the variance line
    auto-routed to "Suspense — opening balance" so the operator
    can fix without losing the partial work.
- **Avoid:** Custom-account creation via the import (Egyptian
  COA stays curated; operator adds custom accounts via the
  master-data page first).

#### E.7 — Partner Ledger detail toggle (MEDIUM)

- **Pain:** `CustomerStatement.razor` shows aging buckets (0–30,
  31–60, 61–90, 90+) but no underlying transactions. Operator
  drilling into a dispute has to leave the statement, run GL with
  a customer filter, and cross-reference manually.
- **Complexity:** S (~2 days)
- **Dependencies:** `CustomerStatement.razor`, `IGeneralLedgerReportQuery`.
- **MVP slice:**
  - Add a "Show transactions" toggle on the existing statement
    page.
  - When on, render a chronological list (date, doc#, type,
    debit, credit, running balance) underneath the bucket
    summary.
  - Add a parallel `/suppliers/{id}/statement` page for the
    payable side (today only customer-side ships).

#### E.8 — Early settlement discount terms (MEDIUM)

- **Pain:** `InvoiceSettings.DefaultPaymentTermsDays` is a
  single-number field (Net N). No conditional-discount terms
  like "2/10 Net 30." Egyptian wholesalers often offer/take
  these to manage cash flow.
- **Complexity:** M (~5 days — Manus's estimate is right here)
- **Dependencies:** `CustomerReceiptVoucher`, `InvoiceSettings`.
- **MVP slice:**
  - New `PaymentTerm` master entity: name, net days, discount %,
    discount window days. Replaces the single-number setting
    (existing setting becomes the default `PaymentTerm`).
  - On invoice creation, operator picks a `PaymentTerm`
    (defaults to company default).
  - On `CustomerReceiptVoucher` save, if `today <= invoice_date
    + discount_window` and amount = subtotal × (1 − disc%), the
    voucher auto-routes the discount to a new
    `4910 Discount Given` account.
- **Avoid:** Sliding-scale terms (2/10, 1/15, Net 30 — operator
  picks one of two terms). Auto-suggestion of which discount
  the customer earned (operator picks).

#### E.9 — Owner withdrawals (Drawings) (LOW)

- **Pain:** Owner-operated SMBs (vast majority of EG market)
  routinely take cash out of the business for personal use.
  Today this requires a manual JV with a free-text equity
  account.
- **Complexity:** XS (~1 day)
- **Dependencies:** Equity COA (shipped).
- **MVP slice:**
  - Add `3210 Owner Drawings` to the seed COA.
  - `/cash-accounts/{id}/drawings` action: amount, date, optional
    memo → posts JV (DR Drawings / CR Cash).
  - Drawings YTD shows on the equity section of the Balance
    Sheet.

#### E.10 — Recurring journal entry templates (LOW)

- **Pain:** Month-end accruals (rent, utilities, salaries
  pending payroll cut-off) repeat every period. Today the
  operator types the same multi-line JV every month.
- **Complexity:** S (~3 days)
- **Dependencies:** `JournalVoucher`, `JournalVoucher.ReversesJournalVoucherId`
  (shipped).
- **MVP slice:**
  - New `JournalTemplate` entity: name, schedule (monthly /
    quarterly), auto-reverse flag, line definitions.
  - On a date, operator clicks "Generate from template" → posts
    a JV from the template lines stamped with current period.
  - If `auto-reverse = true`, schedule the reversal JV for the
    1st of the next month (uses existing `Hangfire` recurring
    job).
- **Avoid:** Auto-firing the templates without operator click —
  every JV in the system today requires interactive confirmation
  per FR-027; we keep that discipline.

#### E.11 — Prepaid / deferred expense amortization (LOW)

- **Pain:** Annual insurance, advance rent, prepaid
  subscriptions all need monthly recognition. Today the operator
  manually JV's each month.
- **Complexity:** L (~2 weeks)
- **Dependencies:** `FixedAsset` (similar shape — shipped),
  `Hangfire` recurring job.
- **MVP slice:**
  - New `PrepaidExpense` aggregate: amount, recognition account,
    start month, period count.
  - On post, books DR Prepaid Expense Asset / CR Cash (or AP).
  - Hangfire monthly sweep posts the unwind JV (DR Expense /
    CR Prepaid Expense Asset) for `period_amount = total / N`.
  - Detail page shows the schedule + remaining balance.
- **Avoid:** Custom amortization curves (straight-line only).
  Mid-life adjustment / write-down (issue a credit note to the
  source bill instead).

#### E.12 — Manufacturing / BOM (DEFERRED — confirmed)

Manus correctly flags this as deep + deferred. Verified absent
from the codebase. Stays in the **Anti-Roadmap** (§5) — won't
build until 3+ trial customers are manufacturers.

### 3.8.3 Phase E sequencing + total

| Order | Item | Effort | Bucket |
|---|---|---|---|
| 1 | E.4 Inventory adjustment JE | 2d | Quick win — extends shipped feature |
| 2 | E.2 Group payments | 3d | High op-frequency, low complexity |
| 3 | E.5 Cash transfers | 2d | Daily-use convenience |
| 4 | E.9 Owner drawings | 1d | Cheapest of the bunch |
| 5 | E.7 Partner ledger detail | 2d | Read-side; ships on top of GL |
| 6 | E.3 Purchase credit notes | 3d | Mirror of shipped sales path |
| 7 | E.6 COA bulk-import extension | 3d | Migration unblocker |
| 8 | E.1 Customer advances | 5d | Biggest single deal-closer |
| 9 | E.8 Early settlement discounts | 5d | Wholesale-segment differentiator |
| 10 | E.10 Recurring JV templates | 3d | Month-end cycle quality-of-life |
| 11 | E.11 Prepaid amortization | 10d | Most complex; ship last |

**Phase E total: ~39 working days (~7.5 weeks for one dev).**

### 3.8.4 Where Phase E slots into the v5 sequencing

Add as **after Phase B, before Phase D** in §6 sequencing.
Rationale: Phase E is accountant-facing (depth) while Phase D is
operations-facing (serial tracking + Gantt). The next operator
demo will probably be to an accountant evaluating the system,
not a foreman; Phase E directly addresses what they'll grade us
on. This pushes the 11-week plan to **~18 weeks** if shipped in
full, or operator can cherry-pick the top 5 (E.4, E.2, E.5,
E.9, E.7 = ~10 days = 2 weeks of work) and push the rest to v6.

---

## 4. v5 Phase C — Carryover from v4 §C

These wait for a real customer ask. Three items previously on
this list have been promoted to **Phase D** (operator override,
2026-05-15) — see §3.5 above. One item (asset depreciation) was
removed because it turned out to already be shipped — see §1.1.
The remainder hold the v4 / v5 discipline:

| Trigger | What lands | Effort |
|---|---|---|
| Customer with foreign suppliers | Per-invoice currency + FX snapshot at post + revaluation | XL ~3w |
| Retail customer with bad internet | POS offline mode (PWA service worker + sync queue) | L ~2w |
| Distribution customer with procurement | Full Purchase Orders (RFQ → PO → Receive → Bill, 3-way matching) | XL ~3w |
| Marketing-heavy customer | UTM / campaign / source attribution on Lead | S ~3d |
| Customer with high lead volume | AI lead scoring / probability | XL ~2w |

The v4 anti-roadmap discipline holds for everything left:
**don't pre-build any of these.**

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

## 6. Sequencing — 11-week v5 plan with explicit testing buffer

Realistic schedule for one developer (operator + AI-paired). The
**½-day testing buffer** after each phase is non-negotiable: it's
where the operator drives the new pages in a real browser,
catches the cosmetic / UX issues that build-time + smoke tests
miss, and signs off before the next phase starts. Phase D adds
~6 weeks of dev time on top of the original 5-week plan; Phase E
(if shipped in full) adds another ~7.5 weeks. The table below
shows the original 11-week sequencing; Phase E lands **between
B3 test pass and D.1** if the operator opts in (see §3.8.4).

| Week | Focus | Deliverable |
|---|---|---|
| 1 | A.1, A.5, A.4, A.3 | Supplier statement + POS split payment + quotation templates + task priority/sub-tasks |
| 1 (½d) | **Phase A1 test pass** | Operator drives the four pages above; reports any cosmetic / UX gaps; I fix on the spot |
| 2 | A.2, A.6 | Stock valuation + POS sessions / cash control |
| 2 (½d) | **Phase A2 test pass** | Same — focus on POS-session lifecycle + variance math |
| 3 | B.4, B.5 | Expense reports + API write endpoints (customers/leads/expenses) |
| 3 (½d) | **Phase B1 test pass** | curl + Postman against the new endpoints; verify rate-limit + webhook fan-out |
| 4 | B.1, B.2 | Sales Teams + Pricelists |
| 4 (½d) | **Phase B2 test pass** | Verify per-team revenue rollups + pricelist resolution on a real invoice |
| 5 | B.3 | CRM send-email composer |
| 5 (½d) | **Phase B3 test pass** | Send a real email; confirm thread shows in lead activity log |
| (opt) | E.4, E.2, E.5, E.9, E.7 | Phase E "minimum bundle" — inventory adj JE + group payments + cash transfers + drawings + partner ledger detail (~10 days) |
| (opt) | E.3, E.6, E.1, E.8, E.10, E.11 | Phase E full — purchase credit notes + COA bulk import + customer advances + early settlement discounts + recurring JV + prepaid amortization (~30 days) |
| (opt ½d) | **Phase E test pass** | Operator-driven; focus on the JE side of every new entry-point (E.4 + E.5 + E.9 each emit a JV — verify postings) |
| 6-7 | D.1 | Serial number tracking (entity, /items/{id}/serials, sales-line sub-grid, opt-in flag) |
| 7 (½d) | **Phase D1 test pass** | Toggle TracksSerials on one item; receive 5 serials; sell 2; verify status transitions + statement |
| 8-9 | D.2.1 + D.2.3 | Timesheets weekly grid + Gantt read-only chart |
| 9 (½d) | **Phase D2a test pass** | Log a week of hours for two users; visually verify Gantt against task dates |
| 10 | D.2.2 | Project P&L page + Project.LinkedCostCenterId migration |
| 10 (½d) | **Phase D2b test pass** | Tag a project to a cost-center; verify revenue + labor + direct cost rollup |
| 11 (slip buffer) | catchup | Reserved for spillover from any of the above |

**Total calendar time:** ~11 weeks (10 dev weeks + 8 × ½-day
test passes + 1 slip-buffer week). Phase D nearly doubled the
plan; if any of those test passes surface architectural rework,
budget for an extra 2 weeks.

**What "test pass" looks like:**
1. Operator boots the server fresh from the latest commit
2. Walks the new pages with at least 2 customers / 2 items in the
   data set (not the empty state)
3. Files a single dated message: ✅ what works, ⚠️ what's
   cosmetic, ❌ what's broken
4. I fix the ❌ list inline; the ⚠️ list goes into a follow-up
   commit at the end of the next phase (don't rabbit-hole on
   polish during the test pass)

**Done state (end of week 5):** every Manus AI Phase 1 + Phase 2
item either shipped (the genuine ones) or struck through (the
stale ones), each with a recorded operator sign-off. v5 release
notes can lead with: *"Closed every gap our Odoo-comparison
auditor flagged. POS sessions, supplier ledger, sales teams,
customer-specific pricing, write API — all demo-blockers
answered with a 'yes, here it is'."*

---

## 6.5. Rollback strategy

Every v5 ship should be reversible without an emergency
deploy. Three layers of protection:

### 6.5.1 Branch-per-phase development

Each Phase A / Phase B item lands on a short-lived feature
branch named `v5/<item-id>-<short-slug>` (e.g.
`v5/a1-supplier-statement`, `v5/b5-api-write-endpoints`).
Phase boundaries become obvious merge points; if the operator's
test pass surfaces a blocker, the branch can be closed without
polluting `main`.

The PR for each phase merges into a `v5-staging` integration
branch first (so the operator can test the cumulative effect),
then `v5-staging` rolls into `main` after the test pass signs
off. Any single feature can be reverted via `git revert <phase-
merge-sha>` without unwinding the rest of the phase.

### 6.5.2 Runtime feature flags for risky items

Five v5 items are risky enough to need a runtime opt-out (a
single boolean on `Company` or in `appsettings`) so the operator
can disable the new behaviour without redeploying:

| Item | Flag | Why |
|---|---|---|
| A.6 POS sessions / cash control | `RequirePosSession` (default true) | A retail store opening Saturday morning to a broken session-open modal would lose the day's sales — flag lets the operator fall back to v4-style direct sales |
| B.2 Pricelists | `PricelistsEnabled` (default true) | If the resolution rule produces wrong prices on a live invoice, the operator can flip the flag back off and lines fall back to `Item.UnitPrice` |
| B.5 API write endpoints | `ApiWritesEnabled` (default true) | A leaked key + bad integration could create thousands of garbage rows; the flag is the panic button before key revocation kicks in |
| D.1 Serial tracking | `SerialTrackingEnabled` (default false) | New tracking layer on top of stock movements; opt-in per-item via `Item.TracksSerials` so shops not using serials see zero behaviour change |
| D.2.2 Project P&L | `ProjectPnlEnabled` (default true) | Hides the P&L tab without removing timesheet data if margin numbers come out wrong on a real project |

The other v5 items (A.1 supplier statement, A.2 stock valuation,
A.3 priorities + sub-tasks, A.4 quotation templates, A.5 split
payment, B.1 Sales Teams, B.3 CRM email send, B.4 expense
reports, D.2.1 timesheets, D.2.3 Gantt) are read-side or
additive enough that a feature flag adds more confusion than
safety — branch revert covers them.

### 6.5.3 Migration rollback

Every v5 phase that touches the schema gets:
1. A fresh EF migration (one per shipped item; never mixed)
2. The migration's `Down()` method left intact (don't `--idempotent`
   strip it) so `dotnet ef database update <previous-id> -p
   src/EgyptTax.Infrastructure -s src/EgyptTax.Web` reverses
   cleanly
3. A note in the commit message naming the previous migration
   id, e.g.: *"reverts via `dotnet ef database update
   20260514085513_LineCostCenterTags`"*

Reversing a migration is the **last-resort** rollback (for
production it loses any data the new column captured). Branch
revert + feature flag handle the common cases; migration
rollback is reserved for "the schema change itself caused the
bug" scenarios.

### 6.5.4 Backup discipline (already shipped, reaffirmed)

The backup-reminder banner (Gux notifications tab,
`BackupReminderEnabled`) keeps the operator nudged. Before
merging any v5 phase into `main`, the operator confirms a fresh
backup of `daftarx.db` exists. This is the absolute floor — if
all three rollback layers above fail, restoring the backup is
the get-out-of-jail-free card.

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

### 8.1 Second Manus pass — Odoo course gap analysis (2026-05-15)

A second Manus report landed in May tagged "12 gaps from the
Odoo Accounting Full Course." Same critical-review discipline
applied (verified each claim against the codebase before
adding) — see §3.8.1 for the per-claim outcome.

Result: **9 confirmed gaps, 3 partial (smaller scope than
Manus estimated because the underlying infrastructure already
ships), 0 fully shipped surprises.** All 9 verified items
became Phase E (§3.8). The 3 partial items got a *reduced*
scope baked into Phase E (E.4 inventory adjustment is 2 days
not 4 because `StockAdjustment` already exists; E.6 COA import
is 3 days not 4 because `OpeningBalances.razor` already exists;
E.7 Partner Ledger is a toggle on `CustomerStatement.razor`,
not a from-scratch report).

Better hit-rate than the first Manus pass (where 4 of N items
turned out to already ship) — likely because watching the Odoo
course gave the analyst more concrete behaviors to map against
ours, not just feature names.
