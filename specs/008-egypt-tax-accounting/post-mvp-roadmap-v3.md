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
