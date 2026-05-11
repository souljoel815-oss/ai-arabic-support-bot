# Post-MVP Roadmap v2 — Market Leadership Pack

> Successor to [post-mvp-roadmap.md](post-mvp-roadmap.md). Wave 1-4 of v1
> shipped the compliance + foundation layers (Penalty Shield, ETA wizard,
> Closing Cockpit, bank auto-match, multi-cashbox, opening balances,
> licensing, installer hardening, trial mode, mobile-responsive layout).
> v2 sequences the **commercial moves** + the **10 features that close
> the gap to market #1** identified in the May 2026 market analysis.
>
> Same six-field module format as v1 — see [§1 of v1](post-mvp-roadmap.md#1-module-specification-format)
> for the convention. Same complexity scale (XS / S / M / L / XL on the
> AI-paired clock, ~5-10× longer on a traditional clock).

---

## 0. Executive Summary

**Market context (May 2026):**
- Decree **281/2025** lowered the mandatory ETA e-invoicing threshold
  from EGP 500K → **EGP 250K annual revenue**. Tens of thousands of
  sole proprietors, freelancers, and micro-retailers are obligated for
  the first time before **31 March 2026**.
- New graduated penalty regime: tier-1 warning + EGP 5K → tier-3
  EGP 50K-200K **plus invoice suspension** (effective B2B shutdown).
- Law 6/2025 simplified regime (turnover tax + quarterly VAT) requires
  ETA enrolment to qualify — no escape valve.
- Daftra / Wafeq / Edara / Odoo / QuickBooks are **all cloud-only**.
  DaftarX is the only on-premise option in the SMB price band.

**Strategic position:** DaftarX is sold as **tax-compliance insurance**, not as
generic accounting software. The pitch is "pay EGP 3K/year vs. an
EGP 20K-200K penalty", not "switch from Daftra to us". The real
competitor is **Excel + paper**.

**The 10 features that close the gap to #1** (none of which any
competitor in Egypt has shipped):

| # | Feature | Phase | Existing? |
|---|---|---|---|
| 1 | Smart GS1/EGS coding assistant | G2 | No |
| 2 | Arabic AI tax assistant (chatbot) | G3 | No |
| 3 | WhatsApp invoice delivery | G2 | Nudge button only — needs PDF send |
| 4 | OCR receipt scanner (Arabic-aware) | G3 | No |
| 5 | Per-bank CSV / OFX bank import | G1 | Manual entry only |
| 6 | Tax calendar with reminders | ✅ shipped | `/compliance/calendar` |
| 7 | Accountant portal + reseller commission | G1 | Multi-company switcher exists; no commission ledger |
| 8 | Bulk invoice operations (Excel upload) | G1 | No |
| 9 | Compliance health dashboard (unified) | G2 | Cockpit + Penalty Shield exist separately |
| 10 | One-click tax-return preparation | G3 | Form 41 generator exists; VAT/income-tax don't |

**Three commercial deliverables** are blocking everything (see §G0):
1. **Pricing** — three numbers (Basic / Pro / Enterprise EGP/year)
2. **Domain** — `daftarx.com` / `.app` / `.eg`
3. **Payment processor** — Paymob (recommended) or manual InstaPay
   for v1

Without those, the **Phase 1 landing page** and **Phase 2 self-service
portal** can't ship; everything else can.

**Cadence (AI-paired velocity):** v2 is ~3-4 weeks of focused
shipping. G0 (commercial setup) gates G1 (foundation) which gates G2
(differentiation) which unlocks G3 (market leadership).

---

## G0 — Commercial Setup (gates everything else)

### G0.1 Pricing decision

- **Pain:** Without three numbers, no landing page, no checkout, no
  customer can give us money.
- **Competitor:** Daftra Basic ~EGP 6K/yr; Daftra Full ~EGP 24K/yr;
  Odoo ~EGP 5.5K/yr. QuickBooks EGP 9K-60K/yr. DaftarX should price
  **under** the Daftra Basic line to win on price + win on
  features (Penalty Shield + on-prem).
- **Complexity:** XS (decision, not engineering)
- **Dependencies:** None
- **MVP slice:** Three numbers committed to the landing-page tier
  matrix:
  - **Basic** EGP 2,000-3,000/yr — single user, single company,
    monthly VAT (Standard regime) OR turnover tax (Law 6) — picks
    one at registration
  - **Professional** EGP 5,000-7,000/yr — multi-cashbox, multi-user,
    both regimes, WhatsApp delivery
  - **Enterprise** EGP 10,000-15,000/yr — firm portal,
    multi-company, accountant commission tracking, priority support
- **Avoid:** Don't anchor to USD; the customer thinks in EGP. Don't
  build a 6-tier matrix; 3 lines is the cognitive limit.

### G0.2 Domain + landing-page host

- **Pain:** Every day without a public landing page = a missed Google
  search. Egyptian accountants Google "برنامج فاتورة إلكترونية" daily.
- **Competitor:** All competitors have multi-page Arabic landing
  pages with download/signup CTAs.
- **Complexity:** XS (decision + ~1 hour DNS work)
- **Dependencies:** None
- **MVP slice:** Pick one of `daftarx.com` / `.app` / `.io`. (`.eg`
  needs Egyptian commercial registration — skip for v1.) Point at
  Cloudflare Pages or Netlify (free for static).
- **Avoid:** Don't over-spec the registrar — Namecheap / Cloudflare
  Registrar are both fine.

### G0.3 Payment-processor decision

- **Pain:** Can't accept money without a way to accept money.
- **Competitor:** Daftra uses Paymob + Fawry. Wafeq uses 2Checkout.
- **Complexity:** XS for **manual InstaPay** (zero integration); L
  for **Paymob** (commercial registration + webhook code).
- **Dependencies:** None for manual; commercial-registration paperwork
  for Paymob.
- **MVP slice:**
  - **v1 (this week):** Manual InstaPay → operator confirms
    payment → runs `Issue-License.ps1` → emails token. Already
    works today; just put the InstaPay number on the landing page.
  - **v2 (when monthly volume crosses ~10/mo):** Paymob webhook →
    auto-signs `license.token` → emails via Resend. Phase G1.5
    self-service portal.
- **Avoid:** Don't integrate Stripe — Egyptian cards mostly don't
  work on Stripe. Don't pay for Fawry directly until you have
  Paymob (Paymob includes Fawry).

---

## G1 — Foundation (the "ship money flow" wave)

### G1.1 Arabic landing page

- **Pain:** No way for a prospect to discover DaftarX exists.
- **Competitor:** Daftra has a polished SEO-optimised landing page
  ranking top-3 for "برنامج محاسبة مصر". Edara, Wafeq follow.
- **Complexity:** M (1 day for plain HTML + CSS + Arabic copy)
- **Dependencies:** G0.1 (pricing), G0.2 (domain)
- **MVP slice:** Single-page Arabic-first (English toggle) with these
  sections in order:
  1. **Hero** — "خفّض غراماتك الضريبية بـ 50,000 ج.م. هذا العام"
     ("Cut your tax penalties by EGP 50K this year"). One-line tagline
     + screenshot of Penalty Shield + "حمّل نسخة تجريبية مجانية"
     (free 14-day trial CTA).
  2. **Five differentiators** (Penalty Shield / Pre-flight ETA /
     Law 6/2025 / Arabic errors / portable EXE) with one-line
     descriptions + icons.
  3. **Pricing** — three cards from G0.1, with "ابدأ التجربة المجانية"
     and "اتصل بالمبيعات" CTAs.
  4. **How it works** — 3 steps with annotated screenshots
     (install → import → file).
  5. **FAQ** — 8-10 Egyptian-accountant-specific questions ("هل يعمل
     بدون إنترنت؟" "هل البيانات على جهازي؟" "هل يدعم قانون 6 لسنة 2025؟").
  6. **Footer** — WhatsApp Business number, sales email, social.
- **Avoid:** No React / Vue / static-site generator — plain HTML for
  100ms load + clean SEO. No carousel. No video at first. No
  testimonials until you have real ones (fake testimonials kill
  trust in this market).

### G1.2 Bulk invoice operations (Excel upload)

- **Pain:** Distribution and retail clients issue 50-300 invoices/day.
  Typing each into the UI is the dealbreaker for them. They live in
  Excel.
- **Competitor:** SAP and Dynamics have it. SME tools mostly don't —
  Daftra has a clunky CSV import. Excel-native upload is the
  Egyptian-accountant standard.
- **Complexity:** M (1 day)
- **Dependencies:** Existing PostSalesInvoice handler (FR-009);
  trial-mode unblock so prospects can demo it.
- **MVP slice:** New page `/invoices/bulk`. Operator:
  1. Downloads `bulk-invoices-template.xlsx` (canonical column order:
     customer code, date, item code, qty, unit price, VAT category).
  2. Fills it (Excel auto-validates VAT category dropdowns via
     data-validation).
  3. Uploads. Server parses with **ClosedXML** (already in nuget for
     the inspection bundle? if not, add).
  4. Preview grid shows N rows, M validation errors highlighted in red
     with hover-translated Arabic explanations (reuses the ETA error
     translator pattern).
  5. "Post all" button: queue all N invoices on Hangfire; show progress
     bar. Each posts through the existing handler, so JE + ETA
     submission run normally.
- **Avoid:** Don't reinvent the post-handler logic — every row goes
  through the existing `PostSalesInvoiceWithEtaSubmissionHandler`.
  Don't allow free-text VAT rates; only registered categories.

### G1.3 Per-bank CSV / OFX bank import

- **Pain:** Wave 3 shipped manual statement entry. No real customer
  types 200 lines/month. P3.3 PDF parsers are blocked on real bank
  samples. CSV / OFX fills the gap **immediately** because most
  Egyptian banks now expose CSV/OFX from their portals.
- **Competitor:** Every SAP / Dynamics / QuickBooks has CSV-bank
  import. Daftra has a partial CSV importer that only handles
  CIB's format.
- **Complexity:** M (1 day for the first 3 bank formats)
- **Dependencies:** Existing `BankStatement` aggregate (Wave 3) +
  `BankAutoMatchJob` (Wave 4 P3.4).
- **MVP slice:** Extend `/payments/bank-statements/import`:
  - Add a **file picker** that accepts `.csv`, `.ofx`, `.xlsx`.
  - Auto-detect bank by header signature (CIB headers vs. NBE vs.
    QNB vs. AAIB).
  - Per-bank parser class behind a `IBankStatementParser` interface
    that returns the same `BankStatementLine[]` the manual entry form
    produces.
  - Run the existing self-check (opening + credits − debits = closing)
    and refuse to import if off.
  - Auto-match job picks up the new lines on its 10-min cron.
- **Avoid:** Don't try to support every bank on day one. Ship CIB +
  NBE + QNB (cover ~80% of SMB accounts). Don't write PDF parsers
  here — keep that as P3.3 when real samples arrive. Don't try
  ISO 20022 XML — overkill for SMB.

### G1.4 Accountant portal — commission ledger

- **Pain:** Accountants manage 30-50 clients each. They're the
  distribution channel that beat every competitor for QuickBooks +
  Xero globally. Without giving them a commission incentive,
  they have no reason to recommend DaftarX over Daftra (which
  doesn't reward them either).
- **Competitor:** QuickBooks + Xero made the accountant their primary
  sales force; nobody in Egypt has. Daftra has a "partner program" on
  paper but no operational commission tracking.
- **Complexity:** M (1 day)
- **Dependencies:** Existing Firm Portal (`/firm-portal`); G0.3 payment
  processor (to compute commission against real receipts).
- **MVP slice:**
  - New entity `AccountantReferral` (firm_id, client_company_id,
    license_id, first_payment_date, commission_rate, total_commission_egp).
  - Commission rate: **20% of first-year revenue** per referred client
    (industry standard).
  - When a customer activates a license, the activation form asks "هل
    لديك محاسب يستخدم DaftarX؟" with a dropdown of registered firms.
    Selecting one creates the referral row.
  - `/firm-portal` gets a new **Commissions** tab — list of referred
    clients, status (trial / paid / renewed), commission earned,
    commission paid (operator marks paid manually).
  - Email template "تم اعتماد عمولتك" when commission becomes due.
- **Avoid:** Don't build a payout pipeline (Paymob payouts are a
  whole compliance lift). Operator pays via InstaPay manually and
  marks paid in the UI. Don't multi-tier the commission scheme
  (10% / 15% / 20%) — one flat 20% is simpler to sell.

### G1.5 Self-service license portal (gated by G0.3 Paymob)

- **Pain:** Today the vendor manually signs every license token —
  doesn't scale past ~20 sales/month. Self-service removes the
  vendor from the loop except for support questions.
- **Competitor:** Every SaaS has this. SMB on-prem tools (DEXEF,
  Al-Ameen) require a sales call.
- **Complexity:** L (2-3 days — separate ASP.NET Core mini-site)
- **Dependencies:** G0.3 (Paymob); existing `LicenseIssueHost` CLI
  + Ed25519 signing key.
- **MVP slice:** New project `src/EgyptTax.Portal/`:
  - Public form: customer enters HWID + selects tier + customer
    name + invoice TIN (so the portal issues a B2B receipt with the
    customer's TIN, which they then need for their own books).
  - Paymob iframe checkout → on success, webhook calls into
    `LicenseIssueHost.Run(...)` → emails the token via Resend.
  - Admin dashboard (vendor-only): list of sales, refund / reissue
    actions, daily revenue, commission ledger linked to G1.4.
  - Database: own SQLite file (separate from customer installs).
- **Avoid:** Don't bake the portal into the main DaftarX app — it's
  a separate sales surface with different uptime requirements.
  Don't store HWIDs longer than needed for support (90 days).

---

## G2 — Differentiation (the "why we win" wave)

### G2.1 Smart GS1 / EGS coding assistant

- **Pain:** ETA requires every invoice line to carry a 12-digit GS1
  GPC code or EGS code. Most accountants don't know what these are.
  Wrong code = invoice rejection. This is the #1 complaint across
  every Egyptian-accountant forum.
- **Competitor:** Nobody. Not Daftra, not Wafeq, not Edara. Operators
  paste codes from PDFs.
- **Complexity:** L (2 days — UI + a 500-line code corpus + fuzzy
  matcher)
- **Dependencies:** Existing `Item` entity with `EtaItemCode` field;
  existing `EtaItemCodeCheckJob`.
- **MVP slice:** New widget in the item-edit form. Operator types
  Arabic OR English description ("لاب توب ديل" / "Dell laptop").
  Backend runs a fuzzy match against a curated **GPC-Arabic-keyword
  corpus** (bundled JSON in the assembly — ~500 GPC codes covering
  the most common Egyptian SMB inventory: electronics, food,
  textiles, services). Returns top-3 suggestions with confidence
  scores. Operator picks one with one click; the `EtaItemCode` is set.
- **Avoid:** No live ML / no LLM call for v1 — bundled corpus +
  fuzzy match (same algorithm as `BankMatchScorer.ScoreCounterparty`
  → token overlap, Levenshtein). LLM upgrade is G3.1. Don't try to
  cover every GPC code — 500 covers 95% of SMB needs; the long tail
  can be typed manually.

### G2.2 WhatsApp invoice delivery

- **Pain:** Egyptian B2B / B2C clients live on WhatsApp. Today the
  operator downloads the invoice PDF and uploads to WhatsApp Web
  manually. Daftra has a WhatsApp button (deep-link only). Nobody
  sends from the server.
- **Competitor:** QuickBooks added WhatsApp send globally in 2025
  (English only). Nobody has done it in Arabic.
- **Complexity:** M (1 day for v1 — WhatsApp Business Cloud API)
- **Dependencies:** Existing PDF renderer (T079); WhatsApp Business
  number registered on Meta.
- **MVP slice:** New action button on every posted invoice detail
  page: "إرسال عبر واتساب" / "Send via WhatsApp". Opens a modal
  with the customer's phone (prefilled from `Customer.Phone`),
  message preview (Arabic template), and "إرسال". Server POSTs to
  WhatsApp Cloud API with the PDF as a `document` attachment + the
  message body. Records send-status (Sent / Delivered / Read / Failed)
  on `InvoiceWhatsAppDispatch` table for the audit trail.
- **Avoid:** Don't use third-party brokers (Twilio adds $0.005/msg
  fee). Use Meta's direct Cloud API (free for first 1,000 msgs/mo).
  Don't require WhatsApp number verification in the customer record
  — graceful-degrade when phone is missing (button disabled with
  tooltip "أضف رقم واتساب للعميل").

### G2.3 Unified Compliance Health Dashboard

- **Pain:** Today readiness data is scattered — `/cockpit` shows
  period-lock readiness, `/penalty-shield` shows penalty exposure,
  `/eta-dashboard` shows ETA queue, `/compliance/calendar` shows
  deadlines. Operator has to visit four pages to answer "am I
  compliant?".
- **Competitor:** SAP has a compliance dashboard. SMB tools don't.
  This page becomes the **demo opener** for every sales call.
- **Complexity:** M (1 day — pulls from existing queries)
- **Dependencies:** Existing `MonthlyTaxClosingCockpitQuery`,
  `PenaltyExposureQuery`, ETA dashboard queries, compliance calendar.
- **MVP slice:** New page `/compliance/health` with a single big
  number at top — "أنت 85% متوافق" — plus four cards:
  1. **VAT readiness this month** (from cockpit query) with traffic
     light + drill link
  2. **Pending penalty exposure** (from Penalty Shield) with EGP figure
     + "what to fix"
  3. **ETA failed submissions** count + "اصلح الآن" link
  4. **Next deadline** from compliance calendar with days-remaining
  Plus a horizontal "to-do today" strip listing the top 5 actions
  ranked by EGP-impact (drafts in period, failed ETA, missing
  attachments, etc.).
- **Avoid:** No new queries — this is a UI roll-up over existing data.
  Don't aim for real-time; cache for 5 minutes.

### G2.4 Marketing copy + sales collateral

- **Pain:** A landing page (G1.1) needs honest copy. The Manus
  analysis nailed the framing — "buy insurance, not software" — but
  somebody has to write the page in Arabic.
- **Competitor:** Daftra writes good copy. Wafeq writes mediocre
  copy. DaftarX has no copy yet.
- **Complexity:** S (½ day for v1 copy)
- **Dependencies:** G1.1 (landing-page structure)
- **MVP slice:**
  - Hero headline (Arabic): "خفّض غراماتك الضريبية بـ 50,000 ج.م.
    هذا العام"
  - 5 differentiator one-liners (Penalty Shield / Pre-flight ETA /
    Law 6 / Arabic errors / Portable EXE)
  - Three pricing-card descriptions
  - 8-question FAQ
  - One **case study** placeholder ("شركة الأمل للتجارة وفّرت
    EGP 18,000 من الغرامات في الربع الأول") — even though it's a
    template, plant the framing
- **Avoid:** No fake testimonials. No fake logos. Egyptian SMBs spot
  fake social proof immediately.

---

## G3 — Market Leadership (the "nobody else can copy" wave)

### G3.1 Arabic AI tax assistant (Egyptian Arabic)

- **Pain:** Operators have tax questions in colloquial Egyptian
  Arabic ("أنا دخلي 3 مليون السنة دي، هدفع ضريبة كام تحت قانون 6؟").
  Nobody can answer well — accountants charge per call, ETA hotlines
  are slow, the official docs are formal Arabic.
- **Competitor:** QuickBooks has an AI assistant in English only.
  ChatGPT-style assistants don't know Law 6/2025. Nobody has an
  Egyptian-Arabic tax-specific assistant.
- **Complexity:** L (2-3 days for v1)
- **Dependencies:** Pricing decision on AI calls (Anthropic API ~
  $0.003/answer at current Haiku rates); Law 6/2025 + VAT corpus +
  WHT corpus indexed for RAG.
- **MVP slice:** New floating chat widget on every page. Operator
  types Egyptian Arabic. Server:
  1. Retrieves top-5 spec snippets (Law 6 / VAT law 67/2016 / ETA
     resolutions) via embedding search over a bundled corpus.
  2. Calls Anthropic Claude Haiku 4.5 with `system=` containing the
     retrieved snippets + the system prompt "أنت محاسب مصري متخصص
     في الضرائب. أجب بالعربية المصرية البسيطة. اقتبس المادة القانونية
     عند كل إجابة."
  3. Streams answer to the chat panel.
  4. Logs every Q+A to `tax_assistant_log` for audit + future
     fine-tuning. Operator can rate "مفيد / غير دقيق".
- **Avoid:** No live ETA-API integration ("submit this invoice for me")
  in v1 — read-only Q+A. No multi-turn agents — single-turn Q+A. Hard
  rate-limit per customer (50 questions/day) to cap cost. Disable
  entirely for installs not on the Pro tier (charge for it indirectly).

### G3.2 OCR receipt scanner (Arabic-aware)

- **Pain:** Operators photograph dozens of paper receipts daily and
  type the data into the expense form. 30 seconds per receipt × 50/day
  = 25 minutes lost.
- **Competitor:** QuickBooks / Xero have OCR for English receipts.
  Arabic-Egyptian receipts (mixed Arabic + Latin numerals, hand-written
  shop names, faded thermal paper) are an unsolved space — nobody has
  trained a model on them.
- **Complexity:** XL (~1 week — train + tune a per-shop template
  library)
- **Dependencies:** Existing `Expense` aggregate (FR-014) + attachment
  pipeline.
- **MVP slice:** Mobile-first capture flow:
  1. On phone, operator taps "🧾 صوّر إيصال" on `/expenses/new`.
  2. Browser opens camera. After capture, image POSTs to server.
  3. Server runs Tesseract 5 (Arabic + English language packs) → gets
     raw text + bounding boxes.
  4. Regex-based extractor pulls: total (largest EGP-shaped number),
     date (DD/MM/YYYY or Arabic-numeral date), supplier name (line
     above the total in 60% of templates).
  5. Pre-fills the expense form; operator confirms + posts.
- **Avoid:** Don't train a custom OCR model in v1 — Tesseract + regex
  hits ~75% accuracy on Egyptian receipts which is enough to save
  time. Don't aim for VAT-line extraction — most paper receipts
  don't break it out anyway. Custom-model training is v2 (probably
  Layoutsetup-style on a corpus of 5k labeled receipts).

### G3.3 One-click tax-return preparation

- **Pain:** Operators spend 1-2 days each quarter assembling the VAT
  return (VAT report → fill the regulator's online form → cross-check
  → submit). Annual income-tax is worse — full week of work.
- **Competitor:** SAP/Dynamics have it. SMB tools don't. Form 41
  generator already exists in DaftarX for WHT — extending to VAT +
  income-tax is the natural follow-up.
- **Complexity:** L (2 days for VAT; another L for income-tax)
- **Dependencies:** Existing VAT monthly report + Form 41 generator;
  Closing Cockpit period-lock gate (so the return can't be generated
  on a period with unposted drafts).
- **MVP slice:** New page `/wht/vat-return/new`:
  1. Pick month (must be a Locked period — gate borrowed from
     Form 41 generator).
  2. Page shows pre-filled return preview: output VAT, input VAT,
     non-recoverable VAT, net payable. Each cell links to the source
     report row (drill-through audit).
  3. Operator reviews + clicks "Generate". Returns a PDF in the ETA
     format + a JSON payload ready for the regulator's bulk-submit
     endpoint (currently mocked — wire to real endpoint when ETA
     publishes it).
  4. State: `Generated` → `Submitted` (operator marks manually after
     submitting through the regulator portal) → `Acknowledged` (after
     receipt).
- **Avoid:** Don't auto-submit to the regulator portal — that's a
  legal liability without explicit operator review. Don't try
  income-tax in v1 (it requires assembling 12 months + adjustments +
  fixed-asset depreciation — split into G3.4).

### G3.4 One-click income-tax return (depends on G3.3)

- **Pain:** Annual income-tax return is a 5-7-day manual job.
- **Complexity:** L (2-3 days after G3.3)
- **MVP slice:** Same shape as G3.3 but pulls 12 months instead of 1,
  includes depreciation schedule, non-deductible expense adjustments.
- **Avoid:** Don't try Law 6 turnover-tax return in this module —
  it's a different shape (a single % of revenue). Build that as
  G3.5 — even simpler.

---

## G4 — Defense + Quality of Life (optional, opportunistic)

These are not strategy-doc items but make sense to slot into idle
windows between G-waves.

### G4.1 Auto-update mechanism

- **Pain:** Customers stuck on old versions are a support tax.
- **Complexity:** M (1 day)
- **MVP slice:** App checks `https://daftarx.com/latest.json` every
  24h. If newer version available, banner offers "تحديث الآن".
  Downloads the MSI to `%TEMP%`, prompts to launch, exits. Standard
  Squirrel-style flow.
- **Avoid:** No silent self-update — Egyptian operators want to know
  what changed before installing.

### G4.2 Optional cloud backup

- **Pain:** Hard drive dies = customer loses their books. "My data
  is on my machine" is also "my data dies with my machine".
- **Complexity:** M
- **MVP slice:** Add-on subscription (EGP 50/mo). Nightly compressed
  + encrypted SQLite/SQL backup → Backblaze B2. Customer holds the
  key — vendor literally can't read backups. One-click restore.
- **Avoid:** Don't host the backup yourself. B2 is $0.005/GB/mo.

### G4.3 Referral codes (one-click viral loop)

- **Pain:** Friends of customers don't know DaftarX exists.
- **Complexity:** S (½ day)
- **MVP slice:** Every customer gets a unique referral code. They
  share it. New customer signs up with that code → both get 30 days
  free. Just a coupon table + a discount math hook on G1.5 checkout.

---

## Sequencing summary

| Wave | Calendar (AI-paired) | Items | Gate |
|---|---|---|---|
| **G0** | 1 day (decision day) | Pricing + domain + payment processor | None |
| **G1** | 5-7 days | Landing page, bulk invoice, CSV bank import, accountant commission, self-service portal | G0 |
| **G2** | 4-5 days | GS1 assistant, WhatsApp send, unified compliance dashboard, marketing copy | G1 |
| **G3** | 8-10 days | Arabic AI assistant, OCR receipts, tax-return automation | G2 |
| **G4** | 2-3 days, opportunistic | Auto-update, cloud backup, referral codes | any time |

**Total v2 effort:** ~3-4 weeks of focused shipping. Real bottleneck is **G0** — without pricing/domain/payment, the funnel can't start.

---

## What we explicitly are NOT building in v2

- Full ERP modules (inventory, MRP, payroll, POS) — out of scope by
  design.
- Multi-tenant cloud hosting — the moat is on-premise.
- Mobile native app — the responsive web layout (Phase 3.3, already
  shipped) is enough. iOS / Android apps are post-v2.
- IFRS / multi-currency / consolidation — DaftarX is Egyptian SMB,
  not multinational.
- Inventory + COGS — large refactor; not blocking compliance value.
- POS / E-Receipt — separate stack with a 60s ETA window; comes
  after v2 once Arabic AI + accountant portal stabilise.

---

## KPIs from the market analysis

For each G-wave we should ship telemetry against these:

| Horizon | KPI | Target |
|---|---|---|
| First 3 months post-launch | Registered accountants | 50 |
| First 3 months | Paid licenses | 200 |
| First 3 months | Trial → paid conversion | ≥30% |
| First 6 months | MRR | EGP 250K |
| First 6 months | Retention (annual) | ≥85% |
| First year | Annual revenue | EGP 15-25M |
| First year | SMB tax-software market share | 10-15% |

Telemetry implementation: a single anonymous-by-default `usage`
endpoint on the customer install that POSTs version + week-bucket
counts of (invoices posted, ETA submissions, returns generated)
weekly. Opt-out toggle in settings. No PII, no document content.

---

## References

This roadmap synthesises the market scan in "DaftarX — Market
Analysis & Roadmap to #1" (Manus AI, May 2026). Where v1 (this repo's
existing `post-mvp-roadmap.md`) was engineering-driven, v2 is
market-driven and assumes v1 P0-P3 has landed.

Existing implementation references in this repo (for delta calculation):
- Penalty Shield → [src/EgyptTax.Web/Pages/Compliance/PenaltyShield.razor](../../src/EgyptTax.Web/Pages/Compliance/PenaltyShield.razor)
- Closing Cockpit → [src/EgyptTax.Web/Pages/Compliance/ClosingCockpit.razor](../../src/EgyptTax.Web/Pages/Compliance/ClosingCockpit.razor)
- Firm Portal → [src/EgyptTax.Web/Pages/FirmPortal/](../../src/EgyptTax.Web/Pages/FirmPortal/)
- ETA wizard / dashboard / inbox → [src/EgyptTax.Web/Pages/Compliance/Eta*](../../src/EgyptTax.Web/Pages/Compliance/)
- Form 41 generator → [src/EgyptTax.Web/Pages/Wht/Form41GeneratorPage.razor](../../src/EgyptTax.Web/Pages/Wht/Form41GeneratorPage.razor)
- Bank statement import (manual) → [src/EgyptTax.Web/Pages/Payments/BankStatementImport.razor](../../src/EgyptTax.Web/Pages/Payments/BankStatementImport.razor)
- Trial mode + license gate → [src/EgyptTax.Web/Licensing/LicenseGate.cs](../../src/EgyptTax.Web/Licensing/LicenseGate.cs)
- Issue-License vendor script → [Issue-License.ps1](../../Issue-License.ps1)
