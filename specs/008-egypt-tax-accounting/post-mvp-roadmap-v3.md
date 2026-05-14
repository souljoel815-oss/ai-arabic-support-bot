# Post-MVP Roadmap v3 — Distribution + Close-the-Deal Pack

> Successor to [post-mvp-roadmap-v2.md](post-mvp-roadmap-v2.md). v1+v2 shipped
> the compliance + foundation + market-leadership features (Penalty Shield,
> ETA wizard, Closing Cockpit, GS1/EGS suggester, Inspection Bundle, sales-rep
> workflow, route plan, sales orders, dashboards, commissions). v3 sequences
> the **commercial moves** (sales / distribution / trust-building) plus the
> **6 features that close the deals Odoo Egypt currently wins**.
>
> Same six-field module format as v1/v2 — see
> [§1 of v1](post-mvp-roadmap.md#1-module-specification-format) for the
> convention. Same complexity scale (XS / S / M / L / XL on the AI-paired
> clock, ~5-10× longer on a traditional team clock).

---

## 0. Executive Summary

**Where we are (May 2026):**
- 9 phases (A–K) shipped end-to-end. 119 files uncommitted but ready.
- Test report: 30+ pages pass, all known bugs patched.
- Real ETA is still a mock; Paymob/Fawry not integrated; one local dev install,
  no paying customers yet.

**The bottleneck has flipped.** It's no longer product depth — it's
**distribution + customer count**. With 0 customers, no roadmap matters. With
50 paying customers, the roadmap writes itself from their support tickets.

**v3 strategy:** spend 60% of the next 12 weeks on distribution + the
specific 6 features prospects ask for when comparing to Odoo Egypt, and 0%
on the seductive-but-deadly features (full MRP, dashboard designer, native
mobile apps, payroll, eCommerce site builder).

**The 6 features that close the gap to Odoo Egypt** in our actual segment:

| # | Feature | Phase | Existing? |
|---|---|---|---|
| 1 | Email invoice send (SMTP-based) | L1 | No (settings tab exists, send button missing) |
| 2 | WhatsApp invoice send | L1 | Partial — migration `InvoiceWhatsAppDispatches` exists, UI missing |
| 3 | CSV / Excel export on key pages | L2 | No |
| 4 | Recurring invoice templates | L3 | No |
| 5 | Customer self-service portal (read-only) | L4 | No |
| 6 | Paymob + Fawry real integration | L5 | No (Paymob/Fawry placeholders only) |

**Plus 4 trust / distribution items that must ship before any of the above:**

| # | Item | Phase | Existing? |
|---|---|---|---|
| 0 | Commit + push 119 uncommitted files | D0 | No (sitting in working tree) |
| 1 | 3-minute demo video | D1 | No |
| 2 | Landing page with FAQ + Trial CTA | D2 | No (only trycloudflare URL) |
| 3 | Real ETA submission (or honest sandbox label) | D3 | Mock only |

---

## 1. Module Specification Format

Same six fields as v1/v2:

- **Pain** — concrete user pain + quantified cost where possible
- **Competitor parity / differentiator** — what Odoo / Daftra / Wafeq /
  Edara / QuickBooks Egypt do today
- **Complexity** — XS (~2 hrs) / S (~½ day) / M (~1 day) / L (2-3 days) /
  XL (~1 week of focused work). Multiply ×5-10 for traditional-team clocks.
- **Dependencies** — earlier modules / FR refs that must exist first
- **MVP slice** — smallest shippable thing that delivers user value
- **Avoid** — over-engineering traps to skip

---

## D — Distribution / Trust (Weeks 1-2, ship before any new feature)

**Goal:** make DaftarX pitchable to a stranger. The product is ready;
distribution is the gap.

### D0 — Git commit + push everything

- **Pain:** 119 files modified since `8eaee56`. A disk failure / accidental
  `git reset --hard` today wipes phases A through K. Real risk, zero cost
  to fix.
- **Complexity:** XS (~1 hr)
- **Dependencies:** none
- **MVP slice:** group commits by phase (A+B rep workflow, C-D inventory +
  credit limit, E PWA, F sales orders, G dashboard, H+I routes/offline,
  J+K rep dashboard + commissions, post-test bug fixes). Push to GitHub
  on a feature branch off `008-egypt-tax-accounting`.
- **Avoid:** one giant commit. Reviewers will give up. Group by phase
  so blame works.

### D1 — 3-minute demo video

- **Pain:** A document converts ~5%. A 3-minute video showing the product
  in action converts much higher. Currently prospects see only a
  trycloudflare URL — no idea what they're looking at.
- **Competitor:** Odoo has slick 90-second demo videos for every module.
  Daftra has a YouTube channel. We have nothing.
- **Complexity:** M (1 day to record, edit, host)
- **Dependencies:** seed data with realistic Egyptian company + 5-10
  customers + 20 items + a few posted invoices (D0.5 — extend the seed
  CLI)
- **MVP slice:** OBS screen recording, Arabic narration, four acts:
  1. **0:00-0:30** Penalty Shield with a real "you'd owe 5,000 EGP if you
     don't fix X" scenario — the moat
  2. **0:30-1:30** Invoice creation → ETA submission → Inspection Bundle
     one-click ZIP — proves you handle the boring stuff
  3. **1:30-2:30** Sales rep on mobile: route plan → confirm order →
     office converts → credit limit blocks the next sale
  4. **2:30-3:00** Sidebar zoom-out: 7 sections vs Odoo's 82 — "this is
     everything you need, nothing you don't"
- **Avoid:** narration in English. Stock music. Cuts longer than 5 seconds.
  Showing bugs / loading states.

### D2 — Landing page + FAQ

- **Pain:** trycloudflare URL is not a brand. No way for a prospect to
  bookmark, share, or sign up for trial themselves.
- **Competitor:** Every competitor has a polished landing page in Arabic
  with feature pages, pricing, FAQ, demo CTA.
- **Complexity:** L (2-3 days for a no-code build)
- **Dependencies:** domain + DNS (currently blocked — externally-blocked
  per v2)
- **MVP slice:** one page, sections:
  1. Hero: "محاسبتك المصرية كاملة في برنامج واحد — بدون اشتراك شهري"
  2. 3 differentiators with screenshots: Penalty Shield, Inspection
     Bundle, Sales rep workflow
  3. "Try free for 14 days" → downloads the .exe + portable .db
  4. Pricing table (Solo 3,500 / SMB 8,000 / Enterprise 17,500 / Firm 30,000)
  5. FAQ section addressing every objection from v3 §3 (Odoo
     objection-handling)
- **Avoid:** custom CMS. Use Framer / Webflow / a static HTML deploy on
  Cloudflare Pages.

### D3 — Real ETA submission OR sandbox label

- **Pain:** The whole pitch is "we handle ETA correctly". But the current
  build submits to a mock endpoint. If a customer tries to send a real
  invoice they get nothing. Trust killer the moment anyone audits this.
- **Competitor:** Every Egyptian competitor has live ETA submission.
  This is table stakes, not a differentiator.
- **Complexity:** XL (~1 week real, OR XS — 1 hour to add a "Sandbox
  Mode" label to the ETA dashboard)
- **Dependencies:** ETA developer account, test TIN, signing certificate
- **MVP slice (1 hr version):** add a prominent yellow banner to the ETA
  dashboard: "ETA Sandbox Mode — submissions are not sent to the live
  regulator. Switch to live mode with your activation credentials."
- **MVP slice (1 week version):** real submission against ETA test
  environment, signed with USB token, regulator UUID returned and
  stored, status polling job updates `EtaSubmission.RegulatorStatus`
- **Avoid:** trying to handle ALL ETA edge cases in v1. Aim for happy
  path + one retry. Hardening lands incrementally as real customers
  surface failures.

---

## L — Lite features that close Odoo objections (Weeks 3-10)

**Goal:** answer the top 6 "Odoo has X, you don't" objections without
turning into Odoo. Order by ROI, not by personal interest.

### L1 — Email + WhatsApp invoice send

- **Pain:** Egyptian buyer says "ابعتلي الفاتورة على الإيميل / واتساب".
  Rep currently has no answer — they have to download the PDF and email
  it from their personal Gmail. Friction = lost professionalism signal.
- **Competitor:** Odoo / Daftra / Wafeq all have one-click email send.
  WhatsApp send is missing from most (the closest competitor with it is
  Edara.app, partial implementation).
- **Complexity:** L (2-3 days for both, paired)
- **Dependencies:** SMTP settings tab exists (Email Settings under
  Admin Panel). `InvoiceWhatsAppDispatches` migration exists.
- **MVP slice (email):** "Send to customer" button on invoice detail.
  Uses customer.Email if set, otherwise prompts. Body = templated
  Arabic+English with PDF attachment. Records a `EmailDispatch` row
  for audit.
- **MVP slice (whatsapp):** "Send via WhatsApp" button opens
  `https://wa.me/{customer.phone}?text={url-encoded-message}` with a
  link to download the PDF (signed temporary URL, 7-day expiry). No
  in-app WhatsApp Business API integration — that's L+.
- **Avoid:** building an in-app email client. Building drip campaigns.
  Drag-and-drop email template editor.

### L2 — CSV / Excel export on key pages

- **Pain:** "هل أقدر آخد بياناتي لو سبت البرنامج؟" — universal Egyptian
  SMB objection. Buyers fear lock-in. Without an answer, deal dies.
- **Competitor:** Odoo exports everything as CSV/XLSX with one click.
  Daftra has CSV export. Wafeq has limited export. We currently have
  zero.
- **Complexity:** M (1 day)
- **Dependencies:** none
- **MVP slice:** one shared `ExportToCsv<T>` component (icon button in
  the page toolbar) wired to:
  1. `/customers` — full customer list with TIN + balance
  2. `/invoices` — invoice list with state + totals
  3. `/items` — item master + current stock
  4. `/customers/{id}/statement` — full ledger as a sheet
  5. `/audit-log` — for legal disclosure / inspection prep
- **Avoid:** trying to export every page. "Top 5 pages where buyers
  ask" beats "every page". Excel formatting (just CSV). PDF report
  generation (separate feature).

### L3 — Recurring invoice templates

- **Pain:** Subscription / service businesses (gyms, schools, ISPs,
  maintenance contracts, monthly retainer accountants) are stuck with
  QuickBooks for this single feature. They generate ~30 identical
  invoices per month and copy-paste each one. Lost vertical.
- **Competitor:** Odoo Subscriptions module, Daftra "متكرر", Wafeq
  recurring invoices. We have nothing.
- **Complexity:** L (2-3 days)
- **Dependencies:** none. New `RecurringInvoiceTemplate` entity +
  Hangfire job that fires daily and generates due invoices.
- **MVP slice:**
  - New entity: template with CustomerId, lines, interval (monthly /
    quarterly / annual), next-run date, end-date or never
  - `/recurring-invoices` page to manage templates
  - Daily Hangfire job: find templates due today, generate Draft
    invoices, advance next-run date
  - Toast on dashboard: "5 recurring invoices generated today — review
    before posting"
- **Avoid:** prorated billing, mid-cycle changes, dunning, automatic
  posting (always generate as Draft — let the operator review).

### L4 — Multi-location inventory (lite)

- **Pain:** SMBs with a shop + a back-storage room ("الفرع + المخزن
  الكبير") track stock per location informally. They can't use a system
  with one global counter — it doesn't match reality. We lose to Odoo
  the moment they ask "do you support multi-warehouse".
- **Competitor:** Odoo full multi-warehouse with putaway strategies.
  We need 10% of that — just per-location counters and transfers.
- **Complexity:** XL (~1 week)
- **Dependencies:** Phase D inventory shipped
- **MVP slice:**
  - New entity: `StockLocation` (name, code, default-flag)
  - Refactor `Item.QuantityOnHand` → `ItemStockByLocation` (item ×
    location → quantity)
  - "Transfer stock" action: move N units of item X from loc A → loc B,
    creates two balanced StockMovement rows
  - Receive stock now requires location selector
  - Sale decrements default location (configurable per item)
- **Avoid:** putaway rules, removal strategies (FIFO/FEFO), bin
  locations within a warehouse, multi-step transfer with in-transit
  state. None of these match what an Egyptian SMB actually needs.

### L5 — Customer self-service portal (read-only)

- **Pain:** Customers WhatsApp the rep "ابعتلي كشف حساب آخر شهرين"
  five times a week. Manual lookup + PDF generation + send. Rep loses
  2-3 hours/week per active customer.
- **Competitor:** Odoo Customer Portal, QuickBooks customer portal,
  Wafeq partial. None polished for Egyptian Arabic-first UX.
- **Complexity:** XL (~1 week)
- **Dependencies:** customer email or phone (for OTP login). FR-040
  customer tax profile.
- **MVP slice:**
  - Customer-facing route: `/portal` with phone + OTP login (SMS via
    Vodafone API or just emailed OTP)
  - Three screens: My statement (read-only ledger), My invoices
    (download PDF), My contact info (read-only)
  - No write actions — pure read
  - Customer can be invited from the Customers page: "Send portal
    link" button → SMS or WhatsApp with login link
- **Avoid:** customer-initiated payments (that's L6). Tickets / chat
  support. Profile editing. Letting customers create their own
  invoices.

### L6 — Paymob + Fawry real integration

- **Pain:** Egyptian B2B buyers increasingly pay online (Paymob accepts
  cards + wallets, Fawry handles cash-at-point-of-sale). Without this,
  every receipt is manual: customer pays at a Fawry kiosk → texts the
  receipt number → rep manually creates a CustomerReceiptVoucher. With
  it: webhook fires, receipt auto-created, customer sees paid status
  in portal.
- **Competitor:** Daftra has Paymob built in. Wafeq partial. Odoo has
  global PSPs but Egyptian wiring is via 3rd-party modules. We can
  win here with a polished local-first integration.
- **Complexity:** XL (~1 week per gateway; ship Paymob first, Fawry
  second). Real work involves webhook signature verification,
  idempotent receipt creation, refund flow, chargeback handling,
  settlement reconciliation.
- **Dependencies:** Paymob merchant account, Fawry merchant account
  (externally-blocked per v2). API keys.
- **MVP slice:**
  - "Pay this invoice" button on the customer portal → opens Paymob
    iframe / Fawry redirect
  - Webhook endpoint at `/api/v1/paymob/webhook` verifies HMAC, looks
    up the order, creates a posted CustomerReceiptVoucher
  - Reconciliation report: matching settlement reports from Paymob
    against our receipts
- **Avoid:** building a generic PSP framework. Wire Paymob directly,
  copy-paste for Fawry. Multi-currency PSPs (Stripe, etc.) — out of
  segment.

---

## 2. Anti-Roadmap — Do NOT Build (in v3 or v4)

Each of these is seductive but will kill the company if pursued before 100
paying customers. Revisit only when a real customer pays for it.

| Feature | Why not |
|---|---|
| **Full MRP / Manufacturing** | 6+ months to build something credible. Odoo's sweet spot. Your customer segment doesn't need it. |
| **Dashboard Designer (Studio-lite)** | Customers asking for "let me customize" usually mean "show me metric X". Solution: ship 3-5 more pre-built dashboard variants. Studio took Odoo a decade to make usable. |
| **Native iOS / Android apps** | PWA works on mobile (Phase E). Only build native if real users say PWA is blocking deals — and even then, 6-8 weeks per platform. |
| **Egyptian Payroll + HR core** | Regulatory burden (taxes, social insurance, end-of-service). 8-12 weeks of work. Only build if customer offers to pay 100K EGP for it upfront. |
| **eCommerce site builder** | Shopify exists. Integrate via API if a customer asks; don't build your own. |
| **Full multi-company / inter-company transactions** | Firm Portal MVP is enough. Real multi-company accounting is 6+ weeks and only matters for accounting firms with 20+ clients. |
| **Translation to a third language** | Arabic + English covers >99% of the target market. Adding French/Turkish/Hindi fragments your support and confuses positioning. |
| **Drag-and-drop kanban for invoices / tasks / anything** | Pretty, low ROI for the target buyer (accountant), high engineering cost. |

---

## 3. Anti-Roadmap — Premature Optimization Traps

Engineering hygiene items that the AI-paired clock will be tempted to
prioritise, but real bottleneck is sales:

| Trap | Defer until |
|---|---|
| **Full test coverage for phases C-K** | 5 paying customers + first bug report you couldn't quickly diagnose |
| **Performance audit / dashboard caching** | First customer with a 10K+ invoice DB complains about slowness |
| **Security audit on new pages** | First customer with a security-conscious IT department |
| **Auto-update mechanism in portable mode** | First time you need to ship an urgent bug fix to all installed customers |
| **Telemetry / error reporting** | 10 paying customers + 1 hard-to-reproduce crash report |
| **Refactor away the SQLite-decimal Sum pattern more broadly** | When a real query crashes in production. So far only 5 sites needed fixing. |

These are real engineering needs. They're not on the v3 plan because the
business risk of NOT having them is currently lower than the business
risk of NOT having paying customers.

---

## 4. Sequencing

Realistic 12-week plan for one developer (you + me) bootstrapping with
trial revenue:

| Week | Focus | Deliverable |
|---|---|---|
| 1 | D0, D2 | All work in git + push. Landing page draft. |
| 2 | D1, D3 | Demo video + sandbox label OR start real ETA |
| 3 | Outreach | 30 prospect emails (target: disgruntled QuickBooks Egypt / Sage / Edara.app / Wafeq users). Onboarding-flow polish. |
| 4 | L1 | Email + WhatsApp invoice send |
| 5 | L2 | CSV / Excel export on 5 key pages |
| 6 | L3 | Recurring invoice templates |
| 7-8 | Outreach + L4 | First 1-3 trial customers. Multi-location inventory (only if a customer asks). |
| 9-10 | L5 | Customer portal (read-only) |
| 11-12 | L6 part 1 | Paymob real integration (Fawry slips to v4) |

**Done state (week 12):** 3-5 paying customers, real ETA + Paymob,
landing page + demo, all the deal-killer features. Anything that's
NOT in the v3 plan stays out until you have these customers + their
specific asks.

---

## 5. What Triggers v4

Write v4 when ONE of these is true:

1. **5 paying customers** for at least 30 days. v4 reacts to their
   support tickets, not to my analysis or yours.
2. **A specific deal of 50K+ EGP/year ARR** is being blocked by a
   missing feature outside the v3 scope. That deal's needs become v4
   priority 1.
3. **A change in Egyptian tax law** mandates new behaviour (e.g. ETA
   threshold drops again, new penalty regime, e-receipt mandate
   activates). v4 starts with compliance.

If none of those is true after 12 weeks, the right move is to **extend
v3** with more sales + onboarding work, not to plan v4 features.

---

## 6. Competitive Intel Cross-Check (May 2026)

A late-May competitive analysis (Manus AI, 12 competitors, 9 axes) was
captured as a separate reference doc at
[competitive-intelligence-2026.md](competitive-intelligence-2026.md).
This section reconciles its findings against the v3 plan: what v3
already covers, what it under-prioritised, and what it missed.

### 6.1 What the matrix validates in v3

The matrix confirms — independently — every gap v3 already targets:

| v3 Phase | Matrix confirmation |
|---|---|
| L1 — Email + WhatsApp invoice send | All 6 competitors with Arabic UX have email send; Wafeq is the only one with WhatsApp send. We now match the leader. |
| L2 — CSV / Excel export | All 8 competitors export; Wafeq + QuickBooks have full Excel/PDF, others CSV-only. v3's CSV-with-BOM matches the floor, not the ceiling. |
| L3 — Recurring invoices | All 7 sales-axis competitors ship this. We just shipped it. |
| L4 — Multi-location inventory | All 6 inventory-axis competitors ship per-location stock. Without it we lose import/export + multi-shop SMBs. v3 keeps this scoped (per-location counters + transfers, no putaway). |
| L5 — Customer portal | Only Daftra ships this. Gap is real but smaller than expected; one differentiated portal beats no portal. |
| L6 — Paymob + Fawry | 5 of 6 Egyptian-relevant competitors have payment-gateway integration. Largest deal-killer in the list. |
| D3 — Real ETA submission | Confirms ETA is *the* moat: Wafeq supports only Saudi ZATCA (not Egyptian ETA), QuickBooks/Xero/FreshBooks have zero ETA. Sandbox label is honest interim; live ETA must follow. |

### 6.2 What the matrix surfaces that v3 missed

Three load-bearing gaps the v3 plan does not cover:

| Gap | v3 status | Action |
|---|---|---|
| **Cost centers** | Not in v3 | Add to v4 trigger list (project-costed businesses can't adopt without it) |
| **Bank reconciliation** | Not in v3 | Add to v4 trigger list (every cloud competitor ships it) |
| **Multi-currency** | Not in v3 | Add to v4 trigger list (any import/export buyer rejects without it) |
| **Quotations** | Not in v3 | Small (S, ~½ day). Add to L-phase tail (L7) — it's a deal-shape mismatch with B2B buyers who expect to negotiate before invoicing. |
| **Auto payment reminders** | Not in v3 | Small (S, ~½ day) on top of L1 SMTP plumbing. Add to L-phase tail (L8). |
| **Arabic-native AI** | Not in v3 | **Biggest miss** — added as M-phase below. |

### 6.3 The strategic miss — Arabic-native AI

The matrix's biggest finding: **no Arabic accounting software ships
AI features**. Wafeq has one (Arabic OCR for receipts); nobody else
has any. Global players (QuickBooks Intuit Assist, Xero JAX, Sage
Copilot) ship English-only AI — useless for Egyptian SMB accountants
who do their data entry in Arabic.

Gartner (2024) reports AI saves accountants 5.4 hrs/week and
automates 80%+ of tax-return prep. The market is moving. The first
Arabic-native AI accounting tool wins the segment of accountants who
have already started using ChatGPT in their workflow informally.

**This is the demo moment that closes the deal:** accountant pastes
5 photos of paper receipts → Claude reads them, fills in date,
vendor, amount, category, VAT. 5 seconds vs 90 seconds manually.
None of the 12 surveyed competitors can do this in Arabic.

v3 §0 says "the bottleneck is distribution + customer count, not
feature depth." That thesis still holds — distribution work in
weeks 1-3 stays as planned. But Arabic AI is one feature where
shipping **before** customer #1 reshapes every subsequent sales
conversation, because every demo gains a unique moment.

The M-phase below is sized to slot between L3 and L4 in the
sequencing without bumping any L-phase item.

---

## M — Arabic-native AI (Weeks 7-8, slots between L3 and L4)

**Goal:** introduce the only feature class no Arabic competitor has.
Two modules, both leaning on Claude (already in our toolbelt, no
new vendor onboarding).

### M.1 — Arabic OCR for receipts / paper invoices

- **Pain:** SMB accountants spend hours per week typing in paper
  receipts from couriers, suppliers, fuel stations, restaurants. Each
  receipt: date, vendor, amount, VAT, category — 90 seconds of
  attention. 30-40 receipts per day = 1 hour of pure transcription.
- **Competitor parity / differentiator:** Wafeq has Arabic OCR
  (their only AI feature). No Egyptian-native competitor (Edara,
  DEXEF, Daftra, Hunt ERP, Sahl) has it. Differentiator vs all
  Egyptian options; parity with Wafeq + better integration with
  Egyptian tax structure (VAT 14%, WHT, Schedule tax).
- **Complexity:** L (2-3 days)
- **Dependencies:** Anthropic API key (already wired for the AI
  Arabic support bot from the parent branch `ai-arabic-support-bot`).
  `Item` master for the auto-categorisation suggestion.
- **MVP slice:**
  - "Scan receipt" button on the Purchases page → file picker (image
    or PDF, up to 5 files at a time)
  - Each file → POST to a server endpoint that pipes it to Claude
    Vision with an Arabic-language prompt: *"اقرأ الإيصال واستخرج
    التاريخ، اسم المورد، المبلغ الإجمالي، ضريبة القيمة المضافة،
    والفئة المقترحة (سفر / وقود / مكتب / مطعم / اتصالات / أخرى)."*
  - Returns JSON: `{date, vendor, total, vat, category}`
  - Pre-fills a Purchase Invoice draft for the user to confirm in
    one click
  - Records a `ReceiptScan` row with original file + extracted JSON
    for audit
- **Avoid:** training a custom OCR model. Building a queue / batch
  processor (do it inline). Auto-posting (always Draft — accountant
  reviews). Multi-page invoice extraction (single-page receipts in
  v1).

### M.2 — Arabic NL queries via Claude chat

- **Pain:** Accountants ask "كام ضريبتي الشهر دي؟" or "مين العملاء
  اللي عليهم فلوس أكتر من ٣٠ يوم؟" then click through 4-5 pages to
  find out. Same data, same answer, every time. Friction = adoption
  gap; the senior accountant uses the app, the junior keeps Excel.
- **Competitor:** None. QuickBooks Intuit Assist + Xero JAX exist
  in English only. No Arabic accounting tool has NL queries.
  Pure greenfield differentiator.
- **Complexity:** L (2-3 days)
- **Dependencies:** M.1 not strictly required, but ship M.1 first
  so the chat sidebar has a destination users already trust.
  Read-only access to the ledger / customer / inventory tables.
- **MVP slice:**
  - Floating chat sidebar (Claude logo, "اسأل دفترك") on every page
  - Question goes to a server endpoint with a system prompt that
    explains the schema + the user's role + Arabic conventions
  - Claude returns either (a) plain Arabic text answer for "how
    much / how many" questions, or (b) a structured query: which
    page + filters to deep-link to
  - 10 canonical questions pre-seeded as suggestion chips:
    "كام ضريبتي؟" / "مين أكبر عميل؟" / "إيه المخزون اللي خلص؟" /
    "كام فاتورة لسه مش متدفعة؟" / etc.
  - All Claude queries logged in `AiChatLog` (audit + cost
    tracking + future fine-tune corpus)
- **Avoid:** letting Claude execute writes (read-only in v1).
  Open-ended chat ("tell me a joke") — keep prompt tight to
  accounting questions. Voice input (typed Arabic only in v1).
  Multi-turn dialogue with memory beyond the current page session.

### M.3 — (Deferred to v4) Auto-categorisation of transactions

- **Pain:** Transaction categorisation is the second-biggest time
  sink after data entry. Same vendor → same category 95% of the
  time, but accountants still click the dropdown for every line.
- **Defer rationale:** depends on M.1 + M.2 shipping + 2 weeks of
  real usage data so the suggestions are accurate. Premature
  without that data — bad suggestions train users to ignore the
  feature.

---

## 7. Updated 12-week sequencing (replaces §4)

The matrix-driven additions (M.1, M.2, L7 quotations, L8 reminders)
slot in without bumping the distribution-first thesis. Quotations
+ reminders are small enough to ride alongside L1/L2 work.

| Week | Focus | Deliverable |
|---|---|---|
| 1 | D0, D2 | All work in git + push. Landing page draft. |
| 2 | D1, D3 | Demo video + sandbox label OR start real ETA |
| 3 | Outreach | 30 prospect emails. Onboarding-flow polish. |
| 4 | L1, L8 | Email + WhatsApp invoice send + auto payment reminders |
| 5 | L2, L7 | CSV export + Quotations |
| 6 | L3 | Recurring invoice templates |
| **7** | **M.1** | **Arabic OCR for receipts** |
| **8** | **M.2** | **Arabic NL queries via Claude chat** |
| 9 | Outreach + L4 (only if asked) | First 1-3 trial customers. Multi-location only if a customer asks. |
| 10 | L5 | Customer portal (read-only) |
| 11-12 | L6 part 1 | Paymob real integration (Fawry slips to v4) |

**Why M slots at weeks 7-8, not weeks 1-2:** distribution work
still comes first. By week 7, D0-D3 + L1-L3 are shipped, the
landing page is live, and M.1/M.2 become the killer **demo
moment** for the outreach push that starts week 3 and intensifies
through weeks 9-12.

---

## 8. Updated v4 triggers (replaces §5)

Write v4 when ANY of these is true:

1. **5 paying customers** for at least 30 days (unchanged).
2. **A specific deal of 50K+ EGP/year ARR** blocked by a v3-out-of-scope
   feature — that deal's needs become v4 priority 1 (unchanged).
3. **A change in Egyptian tax law** mandates new behaviour (unchanged).
4. **(New)** A trial customer asks for **cost centers**,
   **bank reconciliation**, or **multi-currency**. Each one
   excludes a whole segment today; the first ask validates that
   segment is reachable for us.
5. **(New)** M.1 + M.2 ship + 2 weeks of real usage data exist.
   M.3 (auto-categorisation) becomes the first v4 module.

If none of those is true after 12 weeks, the right move is still to
**extend v3** with more sales + onboarding work.

---

## 9. Late-May Critical Review (response to user feedback, 2026-05-13)

User raised 6 weaknesses + 4 missing items against §1-§8. Each is
addressed below with **agreed / partial / pushback**, then the
resulting edit is captured. Section 10 gives the final superseding
sequencing table.

### 9.1 Six concerns

**1 — Quotations under-prioritised as L7 tail item.** Agreed. In
Egyptian B2B no deal starts without عرض سعر; without quotations
there is no top-of-funnel. Promoting from L7 (Week 5 ride-along) to
**L1.5 (Week 4)**, paired with L1 send because once you can send an
invoice you should be able to send a quote.

**2 — Outreach detail (Week 3) too vague.** Agreed. "30 prospect
emails" with no ICP / channels / message ladder / funnel is a plan
that fails silently. Expanded into a real distribution sub-roadmap
in §9.3.

**3 — Perpetual-license-only revenue is fragile.** Agreed with
refinement. The "بدون اشتراك شهري" message is a real wedge against
Wafeq/Daftra/Edara and should stay. But pure perpetual = one-time
revenue, no funding for the continuous ETA-spec work the regulator
forces on us yearly (Resolution 281/2025, Law 6/2025 already this
year). **Edit:** add **"صيانة سنوية اختيارية"** at 30% of license
price (e.g. Solo = 1,050 EGP/year). Includes ETA spec updates +
security patches + support + the metered M-phase Anthropic API
costs. Pitched as optional; in practice the ETA-update component
makes it functionally mandatory the first time the regulator
changes the format. Update D2 landing-page pricing accordingly.

**4 — 3-min demo video too long.** Partial pushback. The 3-min
version is the conversion asset for the landing page and the
sales-call asset where 3 min is short, not long. Don't shorten it.
**But** add **D1.b — 60-sec social cut** for Facebook /
TikTok / Instagram outreach: only the Penalty Shield hook + "جرّب
مجاناً" CTA (Act 1 of the 3-min version, recut). ~30 min extra
editing on top of D1.

**5 — Onboarding experience missing entirely.** Agreed — biggest
miss. The plan covers acquisition but not what happens in the
first 5 minutes after install, which is when 60% of trial users
decide whether to keep the app open. **Edit:** new module
**D0.5 — First-Run Onboarding Wizard** (slots before D1 so the
demo video can show the wizard in Act 1). MVP slice:
- 4-step wizard on first launch: company name + TIN, activity
  type, fiscal year start, accountant or owner persona
- "Sample data?" toggle — pre-loads 5 customers, 20 items, 8
  posted invoices, one Penalty Shield trigger so the new user
  sees the moat in the first 2 minutes
- Guided tour: first invoice → ETA submission → Penalty Shield
  resolution

**6 — L4 "only if a customer asks" is dangerous.** Agreed. SMBs
with a shop + a back-storage room are a majority segment, not a
niche. Customers who hit single-location stock on day one don't
ask — they just leave silently. **Edit:** L4 in §10 is
**unconditional Week 9**, not conditional.

### 9.2 Four missing items

| Missing | Severity | New module / action |
|---|---|---|
| Data migration tool (Excel / QuickBooks / Edara import) | High — switching cost is the #1 blocker | **D2.5 — CSV import wizard** for customers + items + opening balances. ~2 days. |
| Backup & restore for portable SQLite | High — hard-drive failure = total data loss | **D2.5b — Backup module:** one-click backup to USB + scheduled daily auto-backup with 7-day retention. ~1 day. |
| Arabic localization audit | Medium — every English string seen by an Egyptian buyer dents trust | **D0.5b — Localization sweep:** review pass before D1 demo recording for stray English in error messages, validation, date/number formatting. ~½ day. |
| Pricing tier validation | Medium — tiers + perpetual-vs-maintenance split are assumptions | **D2.6 — Pricing A/B test:** ship two landing-page variants (perpetual-only vs perpetual+maintenance) + measure 14-day-trial signup rate over 4 weeks before locking pricing. |

### 9.3 Week 3 Outreach — concrete sub-roadmap (replaces "30 emails")

**ICP, in order of accessibility:**
- **Tier 1:** Solo accountant freelancer billing 5-15 SMB clients.
  Pain: ETA compliance for clients without paying Edara per client.
- **Tier 2:** Small accounting firm (3-10 staff, 20-100 SMB
  clients). Pain: per-seat cost of Edara/Wafeq, no Egyptian-tax
  depth in QuickBooks Egypt.
- **Tier 3:** Single SMB (5-50 employees) doing accounting in-house
  with Excel. Pain: ETA enforcement deadline + no internal
  accountant hire budget.

**Channels (priority order, week 3 starts at #1):**
1. **Facebook groups** — "محاسبين مصر" (~80K members), "محاسبين شغل
   حر" (~15K), "محاسبين القاهرة" (~12K). Helpful Penalty Shield
   content first, not sales pitch. Answer ETA compliance questions
   organically.
2. **LinkedIn** — direct outreach with "Accountant" + "Egypt"
   filter. Personalized message referencing prospect's company.
3. **WhatsApp peer groups** — needs warm intro from a Tier 1
   customer (chicken-and-egg, unlocks at week 6+).
4. **ETA developer / accountant forums** — answer technical
   questions; signature links to landing page.

**Message ladder (no AI/pricing in opener):**
1. **Hook:** Penalty Shield framing — "هل تعلم إنك ممكن تخسر
   5,000 جنيه لو غلطت في توقيت تقديم 10 الإقرار الضريبي؟"
2. **Second touch:** link to D1.b 60-sec demo.
3. **Third touch:** free 14-day trial with onboarding hand-hold
   (call or WhatsApp video).

**Conversion-funnel target (week 3-12 cumulative):**
- 200 Facebook group touches → 60 landing-page visits
- 60 visits → 15 trial downloads
- 15 trials → 5 active users by week 4 → **3 paying customers by week 12**

**Hard gate at week 6:** if the funnel is below 50% of these
numbers, **stop building features** and execute the Plan B
decision tree in §9.4. "Stop and think" is avoidance — Plan B is
pre-committed so the response doesn't depend on willpower in the
moment.

### 9.4 Plan B — pre-committed pivots if week-6 gate trips

The week-6 gate is meaningless without a pre-committed response.
Diagnose first (which funnel step is failing), then pivot
cheapest-first. Each pivot is a 2-week experiment with one
measurable success signal — if it doesn't move the broken step,
escalate to the next.

**Step 1 — Diagnose. Where is the funnel breaking?**

Look at the 200 → 60 → 15 → 5 → 3 funnel and find the worst
conversion ratio relative to target. The leak tells you which
pivot to try.

| Symptom (vs target) | Diagnosis | Try first |
|---|---|---|
| <100 group touches in 3 weeks | Channel reach failing — algo isn't surfacing posts, or you're posting in low-activity hours | **Pivot A — Channel** |
| Group touches OK, <30 landing visits | Hook isn't earning the click | **Pivot D — Message** |
| Visits OK, <8 trial downloads | Landing page sells, pricing or trial friction kills | **Pivot C — Pricing/Trial** |
| Downloads OK, <3 active by week 4 | Onboarding broken (D0.5 didn't land the aha moment) | **Onboarding fix** — not a real pivot, fix the wizard |
| Active OK, 0 paying conversations | Real value gap — they like the trial but won't pay | **Pivot B — ICP** (current segment doesn't have budget) |

If you can't diagnose because data is too thin (e.g. 5 group
touches and 0 visits), the diagnosis itself is the work — extend
Week 6 gate by 1 week and double outreach volume to gather signal.

**Step 2 — Pivot, cheapest first.**

#### Pivot A — Channel (cheapest, ~3 days)

- **When:** group reach is the leak
- **Action:** add 2 paid channels alongside (don't replace) the
  free ones:
  - Facebook boosted post: 500 EGP on the best-performing organic
    post, narrowly targeted (Egypt + Accountant interest +
    age 25-50)
  - LinkedIn Sales Navigator trial (free 30 days): 50 personalised
    DMs to Tier-1 ICP
- **Success signal at 2 weeks:** ≥30 landing visits attributable
  to paid channels
- **Cost:** ~3 days build/setup + 1,500 EGP cash
- **Escalate to** Pivot D if visits still <30

#### Pivot C — Pricing / trial friction (cheap, ~3 days)

- **When:** visits are OK but trials are not
- **Action:** ship two changes simultaneously:
  - Add a **Free tier** (50 invoices/month, no ETA submission, no
    PWA, no Penalty Shield). Pure acquisition channel — drives
    them to upgrade once they get value.
  - Extend trial from 14 → 30 days
- **Success signal at 2 weeks:** trial-download rate doubles
- **Cost:** ~3 days build + landing-page rev
- **Escalate to** Pivot D if rate still doesn't move (means the
  page itself isn't selling, not the price)

#### Pivot D — Message (medium, ~1 week)

- **When:** A and C didn't move the leak, OR the leak was
  visits-from-touches from the start
- **Action:** A/B test 4 hooks on the landing page, one per week
  for 4 weeks. Already-built D2.6 infrastructure handles this:
  1. **Penalty Shield** (current) — fear/compliance
  2. **"وفّر 5.4 ساعة في الأسبوع بالذكاء الاصطناعي العربي"** —
     productivity (leans on M-phase if shipped)
  3. **"نفس Edara بنص السعر، ومن غير اشتراك شهري"** — direct
     price comparison
  4. **"كل حاجة في برنامج واحد، يشتغل أوفلاين"** — simplicity
     + offline (PWA + portable mode)
- **Success signal at 4 weeks:** one variant >2x baseline visit→
  trial rate
- **Cost:** ~1 week (rewrite landing copy + record alt 60-sec
  videos for top 2 candidates)
- **Escalate to** Pivot B if no variant beats baseline by 1.5x

#### Pivot B-lite — ICP validation (1 week, mandatory gate before B-full)

Apply the same MVP discipline to the pivot itself: don't spend 3
weeks rebuilding for a new ICP before confirming that ICP cares.

- **When:** D didn't move the dial AND active trials don't convert
  to paying
- **Action:** 5 direct **phone calls** (not email, not Facebook DM
  — actual voice) to accounting-firm owners. The 10-minute
  discovery call:
  - "بتديروا كام عميل دلوقتي؟" — tier-2 fit check
  - "إيه أكبر pain في إدارة ETA لكل العملاء؟" — validates whether
    our moat (ETA depth + Penalty Shield) maps to their pain
  - "بتدفعوا كام للأداة الحالية بتاعتكم؟" — price ceiling
    discovery
  - "لو حليتلكم المشكلة دي بـ X جنيه/سنة، تجربوا؟" — commitment
    test, not a hypothetical
- **Contact-list source — prepare DURING Pivot D's 4-week signal
  window so it's ready the moment B-lite triggers:**
  warm network → دليل مصلحة الضرائب public accountant registry →
  LinkedIn ("Accounting" + "Egypt" + 3-50 employees)
- **Success signal:** ≥3 of 5 say "yes, this is real for me,
  send me a trial". Anything less = signal isn't strong enough
  to justify the 3-week rebuild.
- **Cost:** ~1 week (assumes contact list pre-built; otherwise
  +1 week for outreach)
- **Escalate to** Pivot B-full if 3+ of 5 validate; **escalate
  to v4 emergency** if 0-1 of 5 are interested — means
  accounting firms aren't the answer either, and the product/
  market-fit problem is real.

#### Pivot B-full — ICP rebuild (3 weeks, only after B-lite validates)

- **When:** B-lite gave 3+ qualified "yes" signals
- **Action:** drop Tier 1 (solo freelancers) and rebuild for
  **Tier 2 (small accounting firms, 3-10 staff)**:
  - Higher ARR per deal (10-20K EGP/year vs 3.5K)
  - Longer sales cycle (call + demo + procurement) but real
    budget
  - Different message: not "save you 5.4 hrs/week", instead
    **"manage 50 SMB clients' ETA from one dashboard, no
    per-client license"** — collapses Edara's per-tenant pricing
  - Requires building Firm Portal MVP earlier (was post-v3) +
    new demo recorded for the firm-owner persona
- **Success signal at 3 weeks:** 3 first-call demos booked with
  the same firm owners B-lite validated
- **Cost:** ~3 weeks (Firm Portal MVP + new demo + new outreach
  copy)
- **Escalate to** v4 emergency if 0 demos booked despite B-lite
  validation — means promised interest didn't convert to
  scheduled time, which is itself a strong signal.

**Total worst-case pivot cycle if everything fails:**
A (2.5w) → C (2.5w) → D (5w with overlapping prep for B-lite
during the 4-week signal window) → B-lite (1w) → B-full (3w) or
v4 emergency. Building the next pivot's prep work during the
previous pivot's signal window — not after it ends — keeps the
total cycle ≤ 12 weeks instead of 16.

**What's deliberately NOT a Pivot:** building more features.
"Maybe they need L5/L6 first" is the failure mode v3 was built
to prevent. The pivot tree exhausts distribution levers before
admitting the build queue is wrong.

---

## 10. Final 12-week sequencing (replaces §4 and §7)

| Week | Focus | Deliverable |
|---|---|---|
| 1 | D0, D0.5, D0.5b, D2 | Git push. Onboarding wizard + localization sweep. Landing-page draft. |
| 2 | D1, D1.b, D2.5, D2.5b, D2.6, D3 | 3-min + 60-sec videos. CSV import + backup module. Pricing A/B live. Sandbox label. |
| 3 | Outreach (per §9.3) | First 200 Facebook touches. Tier-1 ICP outreach starts. |
| 4 | L1, **L1.5 (Quotations)**, L8 | Email/WhatsApp send + Quotations + auto reminders |
| 5 | L2 | CSV / Excel export (L7 absorbed into L1.5) |
| 6 | L3 + **week-6 hard gate** | Recurring invoices + funnel checkpoint. If <50% target → pivot to interviews. |
| 7 | M.1 | Arabic OCR for receipts |
| 8 | M.2 | Arabic NL queries via Claude chat |
| 9 | **L4 (unconditional)** | Multi-location inventory |
| 10 | L5 | Customer portal (read-only) |
| 11-12 | L6 | Paymob real integration (Fawry → v4) |

Two things changed structurally vs §7:

- **Quotations promoted to Week 4** (was L7 ride-along in Week 5)
- **L4 multi-location is unconditional Week 9** (was conditional)
- **Onboarding + migration + backup added to Weeks 1-2** (were
  silently absent)
- **Week-6 funnel checkpoint** as a hard gate, not commentary

§4 and §7 are kept above as the audit trail for what changed and
why; §10 is the operational plan.

---

## 11. Odoo Gap Analysis (May 2026, post-v3)

After v3 §10 shipped end-to-end (commits 3e3a047 → 88b59d8) and the
Manus AI verification report came back at 20/22 PASS, the natural
next question is: *what does Odoo still have that we don't, and
which of those gaps actually close deals in our segment?*

### 11.1 Real gaps that close deals — ranked by ROI for Egyptian SMB

The 10 missing modules below are present in Odoo (and in most
cases in Daftra / Wafeq / Edara too). Each is scored on whether
it currently blocks a real deal in the SMB segment.

| # | Module | Odoo | DaftarX | Why it matters in Egypt | Phase |
|---|---|---|---|---|---|
| 1 | **Bank reconciliation auto-match** | Full + AI suggestions | Skeleton (`/payments/bank-statements` UI exists, matching engine is empty) | Every accountant burns ~4 hrs/month on CIB/NBE/QNB statement matching. Top mentioned pain in customer-development calls. | **N.1** |
| 2 | **Multi-currency** | Full + daily exchange rates | EGP-only | Every import/export business needs it. Wafeq + Edara ship it. Hard segment exclusion today. | N.5 (v4 trigger) |
| 3 | **Cost centers / analytical accounting** | Full | ❌ | Construction + consulting firms tag every JE to a project. Hard segment exclusion. | N.5 (v4 trigger) |
| 4 | **POS (Point of Sale)** | Full module + offline mode | ❌ | Every retail shop with a register. Huge segment in Egypt — but a different sales motion. | N.4 (separate vertical) |
| 5 | **CRM leads/opportunities pipeline** | Full pipeline + activities | Quotations only (no top-of-funnel) | B2B sales teams need lead → opportunity → quote → invoice. Quotations alone are missing 50% of the funnel. | **N.2** |
| 6 | **REST API + Webhooks** | XML-RPC + REST + 800+ marketplace | ❌ | Any integration story (Shopify, Zapier, external CRM, custom dashboards) blocked without it. | N.3 |
| 7 | **Lot / Serial number tracking** | Full + expiry date | ❌ | Pharmacies + electronics + food. Legally required for some sub-segments (pharma traceability). | v4 trigger |
| 8 | **Reorder rules** (auto-PO when stock low) | Full | Low-stock alert only | Retail shops that auto-replenish from suppliers. Saves data entry; medium volume. | v4 trigger |
| 9 | **Project / Task management** | Full module + timesheets | ❌ | Consulting offices + agencies. Different sales motion (project-billed work). | v4 trigger |
| 10 | **eSignature** | Odoo Sign | ❌ | Contract + quotation signing. Nice to have, not blocking. | v4 trigger |

### 11.2 What Odoo has that we deliberately skip

Reinforces §2's anti-roadmap. These are NOT gaps — they're
intentional choices for the segment + the AI-paired build clock:

| Feature | Why we skip |
|---|---|
| Full MRP / Manufacturing | Odoo's sweet spot, 6+ months of depth, segment doesn't need it |
| Dashboard Designer (Studio) | Took Odoo a decade to make usable; ship more pre-built variants instead |
| Native iOS/Android apps | PWA covers 99% of mobile use cases (Phase E shipped) |
| Egyptian Payroll core | 8-12 weeks of regulatory work; only build if a customer prepays 100K EGP |
| eCommerce site builder | Shopify exists; integrate via API (item 6 above), don't build |
| Subscriptions module | Recurring invoices (L3) cover 80%; full subscriptions adds proration + dunning + customer-self-service plan changes — defer until 5 paying customers ask |
| Document management | Google Drive exists; integrate, don't replicate |
| Helpdesk / ticketing | Out of accounting-app scope |
| Field service management | Out of scope |
| Email marketing | Mailchimp exists |
| Inter-company / consolidation | 6+ weeks; only matters at multi-company firm scale |

### 11.3 Recommendation

Three of the gaps above are top-of-funnel-blocking enough to
warrant a focused N-phase before any v4 work:

🥇 **N.1 Bank reconciliation auto-match** — highest ROI, every
   accountant feels the pain monthly
🥈 **N.2 CRM leads/opportunities** — converts DaftarX from
   "accounting tool" to "business OS"; raises ARPU
🥉 **N.3 Open REST API + webhooks** — unblocks every integration
   story including a future POS adapter

The other 7 items move to v4 trigger conditions (§5/§8) — build
them when a real customer asks, not on speculation.

---

## N — Next-tier modules (post-v3, pre-v4)

**Goal:** ship the 3 highest-ROI Odoo-parity items so the next 6
months of demos don't keep losing on these specific points.
Sequencing: N.1 → N.2 → N.3 in priority order. Total estimated
effort: ~6 weeks (N.1: 3w, N.2: 1.5w, N.3: 1.5w).

### N.1 — Bank reconciliation auto-match engine

- **Pain:** Every accountant manually matches CIB/NBE/QNB
  statement lines to invoices/receipts/SPVs once a month, ~4 hrs
  per session. The page skeleton exists (`/payments/bank-
  statements` import + `/payments/unmatched` queue) but the
  matching engine is empty — every line lands in the manual queue.
- **Competitor parity / differentiator:** Odoo + Daftra + Wafeq
  + Edara all ship auto-match. We're the only Egyptian-tax tool
  that surfaces a bank-statement page WITHOUT a matching engine
  behind it. Closing this is parity, not differentiation.
- **Complexity:** L (~3 weeks)
- **Dependencies:** existing `BankStatementParserRegistry` (CIB
  / NBE / QNB parsers shipped). `CustomerReceiptVoucher` +
  `SupplierPaymentVoucher` entities exist. `Expense` exists.
- **MVP slice:**
  - Match-rule engine: amount-exact + date-window-7d + reference-
    text-fuzzy. Per-line confidence score 0-100.
  - For confidence ≥ 85 → auto-allocate; for 50-85 → "suggested"
    queue; <50 → manual queue (existing flow)
  - One-click accept/reject on the suggested queue
  - Learn-from-corrections: if operator overrides a rule, store
    the (pattern → target) mapping for next month's run
- **Avoid:** ML-based matcher in v1 — rule-based is good enough
  for 80%+ accuracy and debuggable. Multi-currency
  reconciliation. Foreign bank statement formats.

### N.2 — CRM leads + opportunities pipeline

- **Pain:** Sales reps managing prospects in Excel / WhatsApp
  groups before they become customers. The quotation flow (L1.5)
  starts from "I have a customer record" — there's no top-of-
  funnel for "I met someone at a conference, need to follow up
  in 2 weeks". Reps ask "where do I track leads?" — answer is
  "you don't, sorry".
- **Competitor parity / differentiator:** Odoo CRM is full-
  featured. Daftra has it. Wafeq doesn't (their gap). Edara has
  partial. Building this puts us at parity with Odoo on the B2B
  sales motion + ahead of Wafeq.
- **Complexity:** L (~1.5 weeks)
- **Dependencies:** existing `Customer` + `Quotation` entities.
  Sales-rep workflow + commissions (Phase J/K).
- **MVP slice:**
  - New entities: `Lead` (name, contact, source, stage,
    assigned-to, expected-close-date, expected-value,
    next-action) + `LeadActivity` (note/call/meeting log)
  - Stages: New → Qualified → Proposal Sent → Negotiation →
    Won / Lost (5 fixed; Studio-style customisation deferred)
  - `/leads` kanban board grouped by stage (drag to move)
  - "Convert to customer + quotation" action on Won leads —
    creates Customer + new Quotation pre-filled
  - Lead activities surface on the rep's My Dashboard
- **Avoid:** email integration (Outlook plugin etc) — operator
  pastes manually for v1. Custom stages. Lead scoring. Multiple
  pipelines. Permission rules beyond admin/rep visibility.

### N.3 — Open REST API + webhooks

- **Pain:** Any integration story (Shopify orders sync, Zapier
  automation, external dashboards, future POS adapter) blocked
  without an API. Customers asking "can you push invoices to my
  Shopify?" today — answer is "no, sorry".
- **Competitor parity / differentiator:** Odoo has XML-RPC + REST
  + 800+ marketplace integrations. QuickBooks ships 650+. Xero
  ships 600+. We have zero. Even minimal API + webhook surface
  unblocks the long tail.
- **Complexity:** L (~1.5 weeks)
- **Dependencies:** existing entities. Need a token-auth scheme
  separate from cookie auth.
- **MVP slice:**
  - `ApiKey` entity (name, hashed key prefix, scopes, created-by,
    last-used)
  - `/settings/api-keys` admin page to mint + revoke
  - `Authorization: Bearer dx_xxx` header validation middleware
  - Read-only endpoints first: GET /api/v1/customers,
    /api/v1/items, /api/v1/invoices (with pagination + filters)
  - Write endpoints: POST /api/v1/customers, POST /api/v1/items,
    POST /api/v1/invoices/draft (no posting from API in v1)
  - Webhooks: outbound POST when invoice.posted, payment.received
    — operator configures URL + selects events on
    `/settings/webhooks`
  - OpenAPI spec auto-generated from minimal-API metadata
- **Avoid:** full GraphQL. OAuth flow (just API keys for v1).
  Rate limiting beyond a per-key counter. Streaming endpoints.
  Bulk-import endpoints — keep operators on the CSV import (D2.5)
  for now.

---

## 12. Updated v4 trigger list (replaces §8 add-ons)

§8 already lists the original v4 triggers. The Odoo gap analysis
in §11 adds 7 more — each promoted to a v4 trigger condition
when the matching customer ask materialises:

| Trigger | What lands | Effort |
|---|---|---|
| First customer asks for **multi-currency** | Schema migration + exchange-rate table + per-document currency override + revaluation reports | XL (~3 weeks) |
| First customer asks for **cost centers** | Tag column on JE lines + cost-center master data + analytical reports | L (~1.5 weeks) |
| First **retail customer** with cash register | Full POS module — separate route family + offline-first PWA + receipt printer integration | XL (~4 weeks) |
| First **pharmacy / electronics** customer | Lot/serial tracking on items + per-lot stock movements + expiry alerts | L (~2 weeks) |
| First **retail / distribution** asks for auto-PO | Reorder rules + PO generation job + supplier price lists | M (~1 week) |
| First **consulting / agency** customer | Project + task entities + timesheet entry + project P&L | XL (~3 weeks) |
| First **deal blocked on signature workflow** | eSignature (integrate with DocuSign or build minimal sign UI) | L (~2 weeks) |

Same rule as §5/§8: don't pre-build any of these. Wait for the
first paying customer to ask, then build with their actual data
in front of you.

