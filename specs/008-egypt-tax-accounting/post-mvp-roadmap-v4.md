# Post-MVP Roadmap v4 — Table Stakes + Polish

> Successor to [post-mvp-roadmap-v3.md](post-mvp-roadmap-v3.md). v3
> shipped §10 (12-week sequence) + §11 N-phase (10 Odoo-parity
> modules). The May 2026 Manus AI gap analysis confirmed that the
> remaining gaps are **table stakes**, not feature parity — a
> handful of accountant-essential reports + small UX polish across
> the modules already shipped. v4 closes those gaps in 3 weeks
> instead of inventing new modules.
>
> Same six-field format as v1/v2/v3 — see
> [§1 of v1](post-mvp-roadmap.md#1-module-specification-format).
> Same complexity scale (XS / S / M / L / XL on the AI-paired
> clock, ~5-10× longer on a traditional team clock).

---

## 0. Executive Summary

**Where we are (May 2026, post-v3-N-phase):**
- v3 §10 shipped end-to-end (D-phase, L-phase, M-phase). Manus
  AI verification: 22/22 PASS.
- v3 §11 N-phase shipped 10 Odoo-parity modules: CRM, REST API,
  cost centers, lot tracking, reorder rules, multi-currency,
  eSignature, projects, POS, plus N.1 bank-rec which was already
  in P3.4. Manus AI N-phase smoke test: 10/10 PASS + 3 cosmetic
  fixes (resolved in `d5f9547`).
- ~50 commits since the v3 baseline. Branch
  `008-egypt-tax-accounting`, ahead of `ai-arabic-support-bot`
  (the original parent branch) by everything in this session.

**The bottleneck has flipped again.** It's no longer "what
features close deals" — that question got 10 answers in v3 §11.
It's now **"what makes accountants stop saying it's not a real
accounting system"** — a small set of reports + polish on already-
shipped modules.

**v4 strategy:** ship Phase A (5 table-stakes table-stakes
features, ~10 days) + Phase B (5 polish/extension items, ~13
days). Total ~3 weeks of focused work. Phase C carries over the
v3 §12 v4 trigger list unchanged — those still wait for a real
customer ask.

**The Manus AI v4 spec (May 14 2026)** identified 5 features as
"Phase A — Table Stakes". Three of those landed in v3 §11
(multi-currency, cost centers, reorder rules); two genuine gaps
remain (General Ledger + Trial Balance, plus stock-count and
expense-receipt-link as the audit also flagged).

---

## 1. Acknowledging the Manus AI v4 Spec

The Manus AI gap analysis (May 14 2026) flagged 5 "Phase A" gaps.
Cross-checking against what v3 §11 actually shipped:

| Manus AI claim | Reality |
|---|---|
| ❌ Multi-currency | ✅ Shipped (`11819db` — currencies + exchange rates) |
| ❌ Cost centers | ✅ Shipped (`fc45fd1` — entity + Expense tagging + report) |
| ❌ Lot tracking | ✅ Shipped (`da0e7b8` — per-batch + expiry alerts) |
| ❌ Reordering rules | ✅ Shipped (`ddea67b` — rules + suggestions page) |
| ❌ POS module | ✅ Shipped (`9edcceb` — single-page checkout) |
| ❌ Project management | ✅ Shipped (`2c6cd47` — projects + tasks) |
| ❌ eSignature | ✅ Shipped (`e14a412` — magic-link signing) |
| ❌ General Ledger report | ✅ **Real gap** |
| ❌ Trial Balance report | ⚠️ Page exists per v3 audit; needs verification |
| ❌ Inventory adjustments (stock count) | ✅ **Real gap** |
| ❌ Receipt attachment to Expense | ✅ **Real gap** (M.1 prefill works; raw image not attached) |
| ❌ QR codes on invoices | ✅ **Real gap** |

**Net:** 4 genuine new gaps from the audit + a handful of polish
items. The rest of the report's "Phase B / C" recommendations
either landed in v3 §11 already or are explicitly deferred to v5
trigger conditions (carried over from v3 §12 unchanged).

---

## 2. v4 Phase A — Table Stakes (~10 days)

The 5 features whose absence makes accountants say "this isn't a
real accounting system." Each is small, well-scoped, and
high-ROI per day of effort.

### A.1 — General Ledger report ✅ SHIPPED

- **Pain:** Every accountant pulls a GL at month-end to drill
  from a single account into every transaction that hit it. v3
  has the underlying journal-entry data + the Trial Balance
  report; the GL is "Trial Balance with the line drilldown
  expanded per account, filterable by date range." Without it,
  accountants export everything to Excel and complain.
- **Competitor parity:** Odoo + Wafeq + Edara + every desktop
  accounting app ships GL. Pure parity.
- **Complexity:** S (~3 days)
- **Dependencies:** existing `JournalVoucher` + `JournalLine`
  entities. Existing `ChartOfAccounts` + `Account` entities.
- **MVP slice:**
  - `/reports/general-ledger` page
  - Filters: date range, account picker (multi-select), state
    (Posted only by default; option to include Draft)
  - Output: per-account section header + chronological line
    list (date, source doc + link, debit, credit, running
    balance) + opening + closing balance
  - CSV export via the existing `CsvExporter`
- **Avoid:** PDF rendering for v1 (operator can browser-print).
  Multi-period comparison columns. Drill-into-attachment links.

### A.2 — Trial Balance verification

- **Pain:** Per the v3 audit (May 13), `/reports/trial-balance`
  was listed as "Not re-verified — was working in earlier
  checks". With the schema changes from cost centers + projects,
  the page may have drifted. Verify it loads + sums correctly +
  exports to CSV.
- **Competitor parity:** N/A — already shipped.
- **Complexity:** XS (~½ day verification + any fix)
- **Dependencies:** existing Trial Balance page.
- **MVP slice:**
  - Open page, post a few invoices/expenses, confirm totals match
  - Confirm CSV export works
  - If broken: fix the drift (likely an EF query on a renamed/
    new column)
- **Avoid:** Re-implementing if it works. New columns. Period
  comparison.

### A.3 — Inventory adjustments (stock count) ✅ SHIPPED

- **Pain:** Every warehouse does a periodic physical count and
  needs to enter "actual count = X, system shows Y, adjust the
  difference and reason." v3 has receive-stock + sale-decrement
  but no "set absolute count" entry path. Operators currently
  add/subtract via receive-stock with synthetic reasons — fragile
  + audit-unfriendly.
- **Competitor parity:** Odoo Inventory ships full stock
  adjustments + count session UI. Wafeq has lightweight version.
- **Complexity:** M (~4 days)
- **Dependencies:** existing `Item` + `StockMovement` + L4
  per-location tracking (#5 + L4 phases).
- **MVP slice:**
  - New `StockAdjustment` entity: per-(item, location) "actual
    count" + difference + reason + counted-by-user + counted-at
  - `/stock-adjustments` page: pick location → table of items
    at that location → operator types actual count per row →
    Submit creates `StockAdjustment` rows + decrements/increments
    `Item.QuantityOnHand` + `ItemStockByLocation` + writes
    `StockMovement` rows with `kind = Adjustment`
  - Reason picker: shrinkage / damage / recount / other
- **Avoid:** Multi-day count sessions (Odoo's "Count Session"
  with mid-count freezes). Cycle-counting schedules. Variance
  approval workflow — operator just commits.

### A.4 — Receipt image → Expense attachment ✅ SHIPPED

- **Pain:** M.1 (Arabic OCR via Claude Vision) shipped. It
  pre-fills the Expense form with extracted fields. But the
  ORIGINAL receipt image isn't attached to the resulting Expense
  record — operator has to manually re-upload via the existing
  attachment widget. Defeats half the OCR's value (the audit
  trail "did the receipt actually say 850?" can't be answered
  from inside the app).
- **Competitor parity:** Odoo Expenses links the receipt image
  to the expense automatically when created via OCR.
- **Complexity:** S (~2 days)
- **Dependencies:** existing M.1 `OcrReceiptHandler`, existing
  `Attachment` entity + `UploadAttachmentHandler`.
- **MVP slice:**
  - When `/scan-receipt` posts to `/expenses/new` with prefill,
    also stash the original image bytes in the session
  - When the operator submits the expense form, take the stashed
    bytes + create an `Attachment` row pointing at the new
    Expense.Id with `DocumentType.Expense`
  - Display the thumbnail on the expense detail (works already —
    the existing attachments widget renders any attached image)
- **Avoid:** Multi-receipt batch attachment. Cross-document
  attachments (one receipt linked to two expenses). Server-side
  thumbnail generation — let the browser scale.

### A.5 — QR code on invoice PDF

- **Pain:** ETA already requires the document-verification seal
  QR per FR-044. But the QR currently encodes only the seal
  payload; many customers want a QR that also encodes a "pay
  now" link or a "view invoice online" portal URL. Easy
  professional-looking polish.
- **Competitor parity:** Odoo + every modern invoicing tool
  emits a payment QR. Egyptian invoices increasingly carry
  Fawry/InstaPay QR codes that customers scan to pay.
- **Complexity:** XS (~1 day)
- **Dependencies:** existing `QuestPdfInvoiceRenderer` (already
  uses QRCoder for the FR-044 seal). L5 customer portal magic-
  link infrastructure.
- **MVP slice:**
  - Render a SECOND QR on the invoice PDF (next to the seal QR)
    encoding `{baseUrl}/portal/{customerToken}#invoice-{id}`
  - The portal page already shows that customer's invoices;
    deep-linking to the right one is just an anchor scroll
  - Caption beneath the QR: "امسح للوصول لكشف الحساب /
    Scan to view your account"
- **Avoid:** Paymob payment-link QR — that's L6, externally
  blocked. Multiple QRs (keep visual clarity). Per-invoice
  custom URL.

**Phase A total: ~10 days. Order: A.2 (verify) → A.5 (1 day
quick win) → A.4 → A.1 → A.3.**

---

## 3. v4 Phase B — Polish on Already-Shipped Modules (~13 days)

Things that exist but feel half-finished. Each closes a real ask
without spinning up a new entity family.

### B.1 — CRM CSV import + pipeline value report ✅ SHIPPED

- **Pain:** Sales reps with 50+ legacy leads in Excel can't
  bulk-import (D2.5 import handles customers + items only). And
  managers want a per-stage value forecast (we already compute
  it on the kanban headers; need a dedicated report view with
  weighted-by-probability totals).
- **Complexity:** S (~2 days import + 2 days report)
- **MVP slice:**
  - Add Lead handler to `DataImportHandlers.cs` + new section
    on `/data-import`. Required cols: Name, Phone OR Email.
    Optional: Company, Source, Stage, ExpectedValueEgp,
    ExpectedCloseDate
  - `/reports/sales-pipeline`: per-stage count + raw value +
    weighted (multiply by stage probability — operator-set on
    /settings or hard-coded New=10%/Qualified=30%/
    ProposalSent=60%/Negotiation=80%/Won=100%/Lost=0%)
- **Avoid:** Probability customisation per pipeline (over-scoped).
  Cohort analysis. Time-to-close analytics.

### B.2 — Automated invoice follow-up sequences (extend L8) ✅ SHIPPED

- **Pain:** L8 sends a single reminder when an invoice is
  N-days-overdue. Real DSO reduction needs a schedule:
  T+7 (gentle), T+14 (firm), T+30 (final notice). Each with
  different tone. v3 has the SMTP path + the cooldown gate;
  what's missing is the multi-step state machine.
- **Complexity:** M (~4 days)
- **Dependencies:** existing L8 `PaymentReminderJob` +
  `PaymentReminderDispatch`.
- **MVP slice:**
  - Add `ReminderTier` enum (Gentle / Firm / FinalNotice) +
    column on `PaymentReminderDispatch`
  - Update job to: for each customer with overdue invoices,
    determine the highest tier they're due for based on
    days-overdue thresholds (configurable per-tenant, defaults
    7 / 14 / 30) AND no dispatch of that tier in the last
    30 days
  - 3 email templates (Ar+En) bundled in the job, each with
    distinct subject + body tone
  - `/settings/notifications` extends the existing reminder row
    with three "days overdue" inputs + tier-specific copy
    (textarea per tier)
- **Avoid:** Per-customer custom tones. SMS escalation. Manual
  "send this tier now" override (operator can email manually
  using the existing L1 send). Phone-call reminders.

### B.3 — REST API: rate limiting + webhooks + write endpoints ✅ SHIPPED

- **Pain:** N.3 shipped read-only GET endpoints + Bearer auth.
  The Manus AI v4 spec rightly flags rate-limiting as a security
  issue (a leaked key can be hammered without consequence) +
  webhooks as the missing real-time integration story + write
  endpoints (especially POST invoice/draft for Shopify-style
  push) as the gap that blocks integration partners.
- **Complexity:** L (~5 days)
- **Dependencies:** N.3 ApiKey infrastructure.
- **MVP slice:**
  - Per-key rate limit: 60 requests / minute, returns 429 with
    Retry-After header. In-memory counter (single-instance
    portable; replace with Redis only if we ever scale-out)
  - `Webhook` entity: name + URL + secret (HMAC-signed) +
    event-mask. Operator manages on `/settings/webhooks`
  - Outbound dispatcher: on `invoice.posted` /
    `payment.received` events (raised inline by the existing
    handlers), POST to each registered webhook with a JSON
    payload + `X-Daftarx-Signature` header. Best-effort: log
    failures, retry x3 with exponential backoff
  - `POST /api/v1/invoices/draft`: same auth gate as the GETs;
    creates a SalesInvoice in Draft state. Operator reviews +
    posts via the regular page or via a future post endpoint
- **Avoid:** Full CRUD on all entities (build per integration-
  partner ask). OAuth flow. Streaming endpoints. GraphQL.

### B.4 — Cash Flow statement ✅ SHIPPED

- **Pain:** Medium-size companies need a Cash Flow report (cash
  in / cash out / net change) for board reporting. v3 has the
  underlying CRV + SPV data + bank statement imports. The
  report is straightforward aggregation.
- **Complexity:** M (~4 days)
- **MVP slice:**
  - `/reports/cash-flow` page with month/quarter/year picker
  - Three sections (Operating / Investing / Financing) per
    standard P&L convention; v1 lumps everything into Operating
    until a customer needs the deeper split
  - Per-row: Customer Receipts (CRVs) + Supplier Payments (SPVs
    × -1) + Other Cash Movements (manual JV with cash account)
  - Net change line + opening + closing cash balance from
    Cash Account
- **Avoid:** Indirect method (start from net income + adjust).
  Cash-flow forecasting. Multi-currency revaluation impact —
  v1 stays in EGP only.

**Phase B total: ~13 days. Order: B.1 → B.4 → B.2 → B.3.**

---

## 4. v4 Phase C — Carryover from v3 §12 (no new triggers)

The v3 §12 v4-trigger list is **unchanged in v4**. Each item
still waits for a real customer ask before any code is written:

| Trigger | What lands | Effort |
|---|---|---|
| Customer asks for **multi-currency on invoices** | Per-invoice currency picker + revaluation | XL ~3w |
| Customer asks for **per-line cost center tagging** | Tag column on JE lines + sales/purchase invoice lines | L ~2w |
| Retail customer with cash register + offline need | POS extensions: offline mode, receipt printer, barcode | XL ~4w |
| Pharmacy/electronics with serial-tracked units | Per-serial tracking layer on top of lots | L ~2w |
| Distribution customer asks for auto-PO | Hangfire job: convert reorder suggestions to draft POs | M ~1w |
| Consulting firm with timesheets + project P&L | Timesheet entry + project profitability via invoice tagging | XL ~3w |
| Deal blocked on cryptographic e-signature | DocuSign / qualified-signature integration | L ~2w |
| Bank-feed auto-import (CIB/NBE/QNB) | OFX/CAMT import from bank export portal | M ~1w |
| Multiple invoice PDF templates ✅ SHIPPED (3 hard-coded variants: Classic/Modern/Minimal) | Template designer or 3 hard-coded variants | M ~5d |
| Purchase Orders (full PO → Receive → Bill flow) | PO entity + RfQ + receive workflow + 3-way matching | XL ~3w |

The discipline: **don't pre-build any of these**. The Manus AI
v4 report ranked Purchase Orders as "Phase B HIGH"; my read is
it's a 10-day commitment for a workflow we have zero
customer-developed evidence for. Wait for the first procurement-
heavy customer to ask, build with their actual vendor list in
front of you.

---

## 5. Anti-Roadmap — Carryover + Strategic Reaffirmation

The anti-roadmap from v3 §2 stands. The Manus AI v4 spec
explicitly endorses this discipline:

> **"Do not try to be Odoo."** Own the Egyptian niche, close
> table stakes, defer the rest.

Items that remain explicitly NOT on any v4 / v5 path:

| Feature | Why not (unchanged from v3 §2) |
|---|---|
| Full MRP / Manufacturing | 6+ months; segment doesn't need it |
| Dashboard Designer (Studio-lite) | Decade-long Odoo investment |
| Native iOS/Android apps | PWA covers it |
| Egyptian Payroll core | Regulatory; only build for 100K EGP prepay |
| eCommerce site builder | Shopify exists |
| Subscriptions module (full) | L3 covers 80% |
| Document management | Google Drive exists |
| Helpdesk / ticketing | Out of scope |
| Field service management | Out of scope |
| Email marketing | Mailchimp exists |
| VoIP integration | WhatsApp wins in Egypt |

**New for v4:** the Manus AI report flagged "Predictive lead
scoring" as a CRM v2 LOW. Adding that to the anti-roadmap —
predictive ML is the kind of thing that looks impressive in a
demo but wastes 2 weeks of build for marginal accuracy on small
datasets. Defer indefinitely.

---

## 6. Sequencing — 3-week v4 plan

Realistic 3-week schedule for one developer (operator + me) on
the AI-paired clock:

| Week | Focus | Deliverable |
|---|---|---|
| 1 | A.2, A.5, A.4, A.1 | Trial Balance verify + QR + receipt-attach + General Ledger |
| 2 | A.3, B.1 | Stock count + CRM CSV import + pipeline report |
| 3 | B.2, B.3 (start) | Reminder sequences + start API rate-limit/webhooks |
| 4 (slip) | B.3 (finish), B.4 | API polish + Cash Flow statement |

**Done state (week 4):** every Manus AI Phase A + B item shipped.
v4 release notes can lead with: *"Closed every gap our auditors
flagged. Trial balance + general ledger that passes the
accountant test. Per-tier dunning, real API surface, stock-
count workflow."*

---

## 7. v5 Trigger Conditions

Same shape as v3 §5/§8/§12. Write v5 only when ONE of these
fires:

1. **5 paying customers for ≥ 30 days.** v5 reacts to their
   tickets — not to any analyst's gap report.
2. **A specific deal of 100K+ EGP/year ARR** blocked by a
   v3+v4-out-of-scope feature. That deal's needs become v5
   priority 1.
3. **Egyptian tax-law change** mandating new behaviour (ETA
   threshold drop, new penalty regime, e-receipt mandate
   activation, payroll-related compliance shift).
4. **One of the v3 §12 + v4 §4 trigger conditions fires** for
   a specific named customer.
5. **3+ trial customers ask for the same single feature** that's
   not on any phased plan — emergent-pattern signal.

If none of those is true after v4 ships, the right move is
**outreach + onboarding**, not v5 features. The build queue is
done; the customer queue isn't.

---

## 8. Differentiator Reaffirmation

The Manus AI v4 report's most important contribution is its
explicit list of features where DaftarX is AHEAD of Odoo in
Egypt. These stay the headline pitch:

| Differentiator | DaftarX | Odoo |
|---|---|---|
| ETA e-invoicing native | ✅ One-click submit | ❌ 3rd-party module / custom dev |
| Penalty Shield (Resolution 281/2025) | ✅ Unique | ❌ Doesn't exist |
| Arabic-first RTL UX | ✅ Designed RTL-first | ⚠️ Translated feel |
| Offline desktop app | ✅ Works without internet | ⚠️ POS only |
| WHT lifecycle (Egyptian) | ✅ Full FR-047 workflow | ⚠️ Generic, needs setup |
| Compliance Health Score | ✅ Single dashboard | ❌ No equivalent |
| AI receipt scan (Arabic) | ✅ Claude Vision native | ⚠️ Generic OCR |
| Compliance Calendar (EG deadlines) | ✅ Pre-configured | ❌ Manual setup |
| Setup Wizard (Egyptian company) | ✅ 4-step guided | ⚠️ Overwhelming generic |
| Magic-link customer portal | ✅ No-friction (L5) | ❌ OTP/account required |
| Magic-link signing | ✅ One-click sign (#8) | ❌ Requires Odoo Sign module |
| Penalty Shield demo moment | ✅ Demo-killer | ❌ Doesn't exist |

The winning sales pitch stays:

> "We have everything you need for Egyptian tax compliance + the
> 5 modules you actually use daily — and it works offline, in
> Arabic, without a consultant to set it up."
