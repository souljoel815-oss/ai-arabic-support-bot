# Competitive Intelligence — May 2026

> Source: Manus AI competitive analysis, May 2026. 12 competitors
> reviewed: Egyptian-native (Edara, DEXEF, Sahl, Hunt ERP), Arab cloud
> (Daftra, Wafeq), global (QuickBooks, Xero, FreshBooks, Zoho Books,
> Odoo, Wave). Source doc preserves the full Arabic narrative; this
> file extracts the matrix + the strategic conclusions in a format
> that cross-references against [post-mvp-roadmap-v3.md](post-mvp-roadmap-v3.md).
>
> Legend: ✅ = full feature, ⚠️ = partial / limited, ❌ = not available.

---

## 1. 9-axis feature matrix

### 1.1 Core accounting

| Feature | DaftarX | Edara | Daftra | Wafeq | DEXEF | QuickBooks | Xero | Odoo |
|---|---|---|---|---|---|---|---|---|
| Chart of accounts | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Automatic JE | ✅ | ✅ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Trial balance | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Income statement | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Tax balance sheet | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Cash flow statement | ⚠️ | ✅ | ⚠️ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Cost centers** | ❌ | ✅ | ❌ | ✅ | ✅ | ⚠️ | ✅ | ✅ |
| Fixed assets + depreciation | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Bank reconciliation** | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Multi-currency** | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| Period close | ⚠️ | ✅ | ❌ | ✅ | ❌ | ✅ | ✅ | ✅ |
| Audit log | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |

**Gaps marked load-bearing:** Cost centers, Bank reconciliation,
Multi-currency. 6 of 7 named competitors ship all three. Without
them, DaftarX is excluded from import/export businesses + project-
costed businesses.

---

### 1.2 E-invoicing + Egyptian tax — DaftarX's MOAT

| Feature | DaftarX | Edara | Daftra | DEXEF | Hunt ERP | QuickBooks | Wafeq |
|---|---|---|---|---|---|---|---|
| ETA B2B e-invoice | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| ETA B2C e-receipt | ⚠️ | ✅ | ❌ | ✅ | ✅ | ❌ | ❌ |
| **Withholding tax (WHT)** | ✅ | ⚠️ | ❌ | ❌ | ❌ | ❌ | ❌ |
| VAT 14% | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ |
| **Schedule tax (ضريبة الجدول)** | ✅ | ⚠️ | ❌ | ❌ | ⚠️ | ❌ | ❌ |
| **Penalty Shield** | ✅ (UNIQUE) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Monthly / quarterly tax return | ⚠️ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| Stamp tax | ⚠️ | ✅ | ❌ | ❌ | ⚠️ | ❌ | ❌ |
| Digital token signing | ❌ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |

**Critical findings:**
- **Wafeq supports only Saudi ZATCA**, not Egyptian ETA. Wafeq cannot
  compete in Egypt on e-invoicing — yet sells aggressively in
  Egypt because Egyptian buyers don't always realize this until
  after they've signed up.
- **QuickBooks, Xero, FreshBooks have NO Egyptian e-invoicing
  support at all**. They handle generic invoices; ETA submission
  requires manual export + third-party gateways.
- Only DaftarX + Edara + DEXEF + Hunt ERP cover the full
  Egyptian tax surface natively.
- **Penalty Shield is unique to DaftarX**, confirmed across all 12
  competitors.

---

### 1.3 Sales + Invoicing

| Feature | DaftarX | Edara | Daftra | Wafeq | QuickBooks | Xero | FreshBooks |
|---|---|---|---|---|---|---|---|
| Sales invoices | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Credit / debit notes | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Quotations** | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Recurring invoices** | ✅ (just shipped L3) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Custom templates | ⚠️ | ✅ | ✅ | ✅ (50+) | ✅ | ✅ | ✅ |
| Email send | ✅ (just shipped L1) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| WhatsApp send | ✅ (just shipped L1) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| **Auto payment reminders** | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Multiple price lists | ❌ | ✅ | ✅ | ✅ | ⚠️ | ✅ | ❌ |
| Online payment links | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Batch invoicing | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ | ❌ |

**Universal gap on competitors but live on DaftarX:** WhatsApp send.
Wafeq is the only other one with it (partial). DaftarX's L1 ships
both email + WhatsApp send — confirms the gap was a real deal-killer.

**Universal feature DaftarX is still missing:** Quotations + Auto
payment reminders. Every competitor has these. Both are 1-week
features. Should land in v3 wave 2 or v4.

---

### 1.4 Purchases + Suppliers

| Feature | DaftarX | Edara | Daftra | Wafeq | DEXEF | QuickBooks |
|---|---|---|---|---|---|---|
| **Purchase orders (PO)** | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Supplier invoices | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Compare supplier quotes | ❌ | ✅ | ❌ | ✅ | ✅ | ❌ |
| Returns management | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Supplier dues tracking | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ |

**Gap:** Purchase orders. 6 of 6 competitors ship them. DaftarX's
"informal SMB doesn't need POs" assumption holds for 5-10-employee
shops but breaks above that. The v3 anti-roadmap lists POs as
"deferred until a customer pays for it" — this matrix confirms it's
a real ceiling.

---

### 1.5 Inventory + Warehouses

| Feature | DaftarX | Edara | Daftra | Wafeq | DEXEF | Sahl | QuickBooks |
|---|---|---|---|---|---|---|---|
| Basic inventory | ✅ (Phase D) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Multi-warehouse** | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Reorder alerts | ✅ (just shipped) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Barcode | ❌ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| Avg / FIFO costing | ❌ | ✅ | ⚠️ | ✅ | ✅ | ✅ | ✅ |
| Inter-warehouse transfers | ❌ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| Physical count | ❌ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| Product variants | ❌ | ✅ | ⚠️ | ❌ | ✅ | ❌ | ✅ |

**Gap:** Multi-warehouse + cost methods + inter-warehouse transfers.
All Egyptian SMB competitors ship these. Already in v3 L4 — sized
XL (~1 week), with multi-location as the lite-version.

---

### 1.6 HR + Payroll

| Feature | DaftarX | Edara | Wafeq | DEXEF | Hunt ERP | QuickBooks |
|---|---|---|---|---|---|---|
| Employee management | ❌ | ✅ | ❌ | ✅ | ✅ | ✅ |
| Payroll | ❌ | ✅ | ❌ | ✅ | ✅ | ✅ |
| Egyptian social insurance | ❌ | ✅ | ❌ | ✅ | ✅ | ❌ |
| Income tax on salaries | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ |

**Anti-roadmap finding:** v3 §2 lists Payroll as "do not build
before 100 customers". This matrix confirms only DEEPLY-localized
Egyptian competitors ship it (Edara, DEXEF, Hunt ERP). Global
players don't bother. Stay out of this lane unless a paying
customer offers 100K+ EGP upfront.

---

### 1.7 Reports + Analytics

| Feature | DaftarX | Edara | Wafeq | QuickBooks | Xero | Zoho |
|---|---|---|---|---|---|---|
| Standard reports | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Interactive dashboard | ⚠️ (partial) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Custom reports | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Cross-period comparison | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Excel / PDF export | ✅ (just shipped CSV) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Budgets + forecasts | ❌ | ✅ | ❌ | ✅ | ✅ | ✅ |
| 40+ report library | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |

L2's CSV export shipped, but the bigger gap (custom report
designer + cross-period comparison + budget forecasting) remains.
v3's anti-roadmap correctly defers the dashboard-designer trap;
this matrix doesn't change that.

---

### 1.8 AI — the strategic opportunity

| Feature | DaftarX | Wafeq | QuickBooks | Xero | FreshBooks | Zoho |
|---|---|---|---|---|---|---|
| OCR for invoices / receipts | ❌ | ✅ (Arabic) | ✅ | ✅ | ✅ | ✅ |
| Auto-categorise expenses | ❌ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Smart bank reconciliation | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ |
| Cash-flow prediction | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ |
| Anomaly / error detection | ❌ | ❌ | ⚠️ | ⚠️ | ❌ | ❌ |
| Smart assistant chatbot | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Arabic NL queries** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Auto-generated reports | ❌ | ❌ | ⚠️ | ❌ | ❌ | ✅ |

**Two strategic findings — this is the biggest insight in the doc:**

1. **No Arabic accounting software offers AI features.** Wafeq has
   one (Arabic OCR). Nobody offers Arabic NL queries, Arabic
   chatbot, Arabic auto-categorisation. The global players (QB,
   Xero) have AI but in English only — useless for Egyptian SMB
   accountants.

2. **Gartner 2024:** AI saves accountants 5.4 hrs/week + automates
   80%+ of tax-return prep. The market is moving — accountants who
   try AI in English are converting their workflow. The first
   Arabic AI-native tool wins this segment.

**v3 doesn't address this at all.** New M-phase added below.

---

### 1.9 Integrations + Infrastructure

| Feature | DaftarX | Edara | Daftra | Wafeq | QuickBooks | Xero |
|---|---|---|---|---|---|---|
| Open API | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Webhooks | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Mobile app | ❌ (PWA only) | ⚠️ | ✅ | ✅ | ✅ | ✅ |
| Bank integration | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ |
| Payment gateways (Paymob / Fawry) | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ |
| eCommerce integration | ❌ | ❌ | ✅ | ❌ | ✅ | ✅ |
| POS | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Customer portal | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| Advanced roles / permissions | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ |

QuickBooks ships 650+ integrations; Xero ships 600+. v3 L6
covers Paymob/Fawry (which 5 of 6 Egyptian-relevant competitors
already have).

---

## 2. Pricing intelligence (May 2026, EGP)

| Competitor | Monthly EGP | Model | Currency billed |
|---|---|---|---|
| Sahl | ~1,750 one-time | Permanent license | EGP |
| DEXEF | ~9,000 one-time | Permanent license | EGP |
| Daftra | 500–2,000 / mo | Subscription | EGP |
| Hunt ERP | ~1,000 / mo | Subscription | EGP |
| Edara | ~1,500–4,000 / mo | Subscription | USD-pegged |
| QuickBooks | ~500–2,000 / mo | Subscription | USD |
| Wafeq | ~1,300–3,250 / mo | Subscription | USD |
| Zoho Books | ~1,000–3,500 / mo | Subscription | USD |
| Odoo | ~3,500+ / mo | Subscription | USD |
| **DaftarX (current)** | **Solo 290 / SMB 670 / Enterprise 1,460 / mo equiv** | **Annual** | **EGP** |

**DaftarX pricing is genuinely the lowest in EGP terms.** The
3,500 EGP / year Solo tier translates to ~290 EGP / month, ~half
the cheapest competitor.

**Recommended pricing alternative** (from the analysis):

| Tier | Monthly EGP | Target segment | Key features |
|---|---|---|---|
| Free | 0 | Freelancer trial / hook | E-invoice up to 50/mo |
| Basic | 299 / mo | Sole proprietor | Invoices + tax + basic reports |
| Business | 599 / mo | Small co. ≤10 employees | + Inventory + Purchases + Cost centers |
| Pro | 999 / mo | Medium co. | + Full AI + Advanced reports + Multi-company |
| Enterprise | custom | Larger / firms | + API + Dedicated support + SLA |

**Difference from current tiers:** the analysis pitches monthly
billing (lower friction) + a true Free tier (acquisition channel).
v3 currently has annual-only pricing and no free tier. This is a
pricing-strategy question worth a focused discussion — not a
build task.

---

## 3. Win strategies per competitor

| Competitor | Their strength | DaftarX's wedge |
|---|---|---|
| **Edara** | Full ERP, deep Egyptian accounting | Same core + Penalty Shield + AI + 60% cheaper in EGP |
| **Daftra** | Good Arabic UX + eCommerce | Deeper accounting + ETA depth + Penalty Shield + AI |
| **Wafeq** | Best Arabic UX + Arabic OCR | Real Egyptian ETA (Wafeq does Saudi ZATCA only!) + WHT + Schedule tax |
| **DEXEF** | Permanent-license affordability | Modern cloud UI + AI + continuous updates + zero install |
| **QuickBooks** | 650+ integrations, global brand | 100% Arabic + ETA + Egyptian tax + EGP pricing + Arabic support |
| **Xero** | Best per-user pricing, clean UX | Arabic + ETA + Egyptian tax + Arabic AI + cheaper |
| **Odoo** | Most feature-complete | Ready to run today + Egyptian specialization + 80% cheaper |

**The killer line for sales conversations:** "Wafeq sells in Egypt
but only supports Saudi ZATCA — your invoices won't reach the
Egyptian ETA correctly". This is a real, documented gap a buyer
can verify in 30 seconds on Wafeq's own site.

---

## 4. The biggest strategic miss in v3

v3 §0 says "the bottleneck is distribution + customer count, not
feature depth." Mostly true — but the analysis surfaces ONE
strategic feature gap that's worth investing in even before
customer #1:

**Arabic-native AI features.** No competitor (Egyptian, Arab, or
global) ships Arabic OCR + Arabic NL queries + Arabic
auto-categorisation. With Claude available, DaftarX can ship this
in 4-6 weeks. Gartner's 5.4-hrs/week productivity gain is the
exact pitch SMB accountants respond to (their #1 complaint is
"the manual data entry kills me").

This is the **demo moment** that converts:

> *Accountant pastes 5 photos of paper receipts into DaftarX.
> Claude reads them, fills in date / vendor / amount / category /
> VAT. Time per receipt: 5 seconds vs 90 seconds manually.*

That demo wins the deal against Edara, DEXEF, Daftra, even
QuickBooks Egypt — because none of them have it.

**Add M-phase to v3** — see Section 6 of the roadmap.

---

## 5. References

| # | Source | Used for |
|---|---|---|
| [1] | DualEntry "AI in Accounting: The Complete 2026 Guide" | AI market size + Gartner 5.4-hrs stat |
| [2] | Wafeq website | Confirming Wafeq supports ZATCA only, not ETA |
| [3] | Wafeq feature page | WhatsApp send capability |
| [4] | DualEntry (same source) | 80%+ tax-return automation stat |
| [5] | Webgility comparison | QuickBooks 650+ integrations |
| [6] | Webgility comparison | Xero 600+ integrations |
| [7] | getedara.com/pricing | Edara pricing model |
| [8] | daftra.com/plans | Daftra pricing model |
| [9] | dexef.com | DEXEF feature + pricing |
| [10] | hunt-eg.com blog | Odoo implementation cost in Egypt |
