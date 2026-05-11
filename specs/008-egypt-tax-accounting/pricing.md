# Pricing Decision (G0.1)

**Decided:** 2026-05-11
**Revised:** 2026-05-11 (3 tiers → 4 editions per Gux.13 spec)
**Status:** ✅ locked for v1
**Supersedes:** previous 3-tier scheme (Basic / Pro / Enterprise)

This is the canonical pricing reference. Anything that displays
prices to customers — landing page (G1.1), self-service portal
(G1.5), accountant commission ledger (G1.4), license-issuance tool
(`Issue-License.ps1`), edition gating in
[`gux-13-admin-panel.md`](gux-13-admin-panel.md) — pulls from these
numbers.

---

## The four editions

| Edition | Annual | Monthly | Annualized monthly | Monthly premium |
|---|---:|---:|---:|---:|
| **Solo** (فردي) | 3,500 EGP | 350 EGP | 4,200 EGP | +20% |
| **SMB** (أعمال صغيرة) | 8,000 EGP | 750 EGP | 9,000 EGP | +12% |
| **Enterprise** (مؤسسات) | 17,500 EGP | 1,650 EGP | 19,800 EGP | +13% |
| **Firm** (مكاتب محاسبة) | 30,000 EGP | 2,800 EGP | 33,600 EGP | +12% |

Monthly is intentionally priced higher when annualized — covers
higher churn risk and steers customers toward annual where you
collect cash up front. Solo's monthly premium is the largest
because Solo customers are the most price-sensitive and most
likely to abuse the monthly option for a one-month spike.

**User & company limits:**

| | Solo | SMB | Enterprise | Firm |
|---|---|---|---|---|
| Users | 1 | 3 | Unlimited | Unlimited |
| Companies | 1 | 1 | 3 | Unlimited |

---

## Feature ladder

Source of truth is §7.2 of [`gux-13-admin-panel.md`](gux-13-admin-panel.md);
this is the same matrix collapsed to the most operationally
relevant rows.

| Feature | Solo | SMB | Enterprise | Firm |
|---|:-:|:-:|:-:|:-:|
| **Core compliance** | | | | |
| Sales/purchase invoices + ETA submission | ✓ | ✓ | ✓ | ✓ |
| VAT return generator (G3.3) | ✓ | ✓ | ✓ | ✓ |
| Form 41 WHT generator | ✓ | ✓ | ✓ | ✓ |
| Penalty Shield | ✓ | ✓ | ✓ | ✓ |
| Compliance Calendar + Tax deadline alerts | ✓ | ✓ | ✓ | ✓ |
| ETA error translator (Arabic) | ✓ | ✓ | ✓ | ✓ |
| Customer referral codes (G4.3) | ✓ | ✓ | ✓ | ✓ |
| Auto-update banner (G4.1) | ✓ | ✓ | ✓ | ✓ |
| **Operations** | | | | |
| Closing Cockpit | — | ✓ | ✓ | ✓ |
| Trial Balance + custom Chart of Accounts | — | ✓ | ✓ | ✓ |
| Multi-cashbox | — | ✓ | ✓ | ✓ |
| Bank CSV import (G1.3) | — | ✓ | ✓ | ✓ |
| Bulk invoice upload (G1.2) | — | ✓ | ✓ | ✓ |
| Receipt OCR scanner (G3.2) | — | ✓ | ✓ | ✓ |
| WhatsApp invoice delivery (G2.2) | — | ✓ | ✓ | ✓ |
| ETA item-code suggester (G2.1) | — | ✓ | ✓ | ✓ |
| Multi-user + roles | — | ✓ (3) | ✓ (unlimited) | ✓ (unlimited) |
| Audit log + Document 360 | — | ✓ | ✓ | ✓ |
| Email invoice (SMTP direct) | — | ✓ | ✓ | ✓ |
| Auto-backup (scheduled) | — | ✓ | ✓ | ✓ |
| Bilingual invoices (AR+EN) | — | ✓ | ✓ | ✓ |
| **Enterprise** | | | | |
| Annual income-tax return (G3.4) | — | — | ✓ | ✓ |
| WHT certificate management | — | — | ✓ | ✓ |
| Compliance Health dashboard (G2.3) | — | — | ✓ | ✓ |
| Multi-company | — | — | ✓ (3) | ✓ (unlimited) |
| Priority support (24h SLA, WhatsApp) | — | — | ✓ | ✓ |
| Cloud backup (G4.2, when shipped) | — | Add-on | ✓ | ✓ |
| Arabic AI tax assistant (G3.1, when shipped) | — | — | ✓ | ✓ |
| **Firm Portal** | | | | |
| Multi-company switcher | — | — | — | ✓ |
| Accountant commission ledger (G1.4) | — | — | — | ✓ |
| Client onboarding wizard | — | — | — | ✓ |
| Bulk operations across companies | — | — | — | ✓ |

---

## Why these numbers

### Solo — 3,500 EGP/year

**Frame:** "أقل من 350 جنيه في الشهر — أرخص من غرامة تأخير
واحدة. وبياناتك على جهازك."

- Cheaper than Wafeq Starter (9,660 EGP) and Daftra Basic
  (12,720 EGP), but not suspiciously cheap. ~4 EGP/day.
- Targets the freelancer / micro-business / sole proprietor —
  often the newly-mandated wave from Decree 281/2025 (revenue
  threshold dropped to 250K EGP).
- Single user, single company. Core compliance only — no
  bank import, no bulk operations, no multi-cashbox. Operator who
  needs more upgrades to SMB.
- 3,500 is ~1.4% of revenue at the new mandated 250K minimum.
  Psychologically passable, deductible as a business expense.

### SMB — 8,000 EGP/year

**Frame:** less than an accountant's monthly fee for a small
business. Operations features (bank import, bulk upload, OCR,
WhatsApp) make the tier actually save the operator time, not just
satisfy ETA.

- Competes with Wafeq Starter (9,660) and undercuts Daftra Basic
  (12,720), but includes features they charge extra for.
- 3 users — fits a typical small business with the owner +
  bookkeeper + cashier.
- Firm commission per SMB referral via G1.4: **1,600 EGP**
  (20% of first-year revenue).

### Enterprise — 17,500 EGP/year

**Frame:** for businesses that have outgrown SMB — multiple
locations, real headcount, need income-tax automation + multi-
company.

- Significantly cheaper than Daftra Advanced (26,235) and Wafeq
  Premium (23,892); DaftarX wins on on-premise + Arabic-first +
  Compliance Health + multi-company.
- Up to 3 companies — covers the typical Egyptian SMB owner with a
  primary business + 1-2 sister entities.
- Includes the still-shipping items (G3.1 AI assistant + G4.2
  cloud backup) so Enterprise customers get them automatically
  when they ship — no upsell email needed.
- Firm commission per Enterprise referral: **3,500 EGP**.

### Firm — 30,000 EGP/year

**Frame:** the only product in Egypt with a real accountant-firm
portal. Daftra has nothing close. Wafeq's "Accountant Perks" is
basic.

- Buyer here is the accounting firm itself.
- Unlimited users + unlimited companies — a firm running 50 SMB
  clients can deploy a single Firm-edition install and manage
  them all.
- Firm commission per Firm referral: **6,000 EGP** — large enough
  to motivate firm-to-firm recommendations.
- Higher absolute price but justified by the unique Firm Portal +
  commission-ledger + bulk-cross-company operations.

---

## Renewal model

**Manual renewal — no auto-charge in v1.**

- Reminders fire at 30 / 14 / 7 days before expiry (Hangfire job,
  to be added when G1.5 portal ships).
- Customer goes back to the portal and re-buys for another year.
- 7-day grace after expiry where the app shows a reminder banner
  but stays functional; on day 8 falls back to read-only mode
  (existing license-gate behaviour).

**Why manual instead of auto-renew:**

- Egyptian SMBs are wary of recurring card charges; "buy software
  once a year" matches the existing mental model (think: Microsoft
  Office, antivirus subscriptions).
- Less Paymob complexity in G1.5 (no recurring billing setup, no
  failed-card handling, no proration on plan changes).
- Lower retention than auto-renew, but reminder cadence + the
  in-app banner mitigate it.

Auto-renew is a candidate for a future G0.4 unblocker once we have
real renewal data and Paymob's recurring-billing API is wired.

---

## Edition gating (technical)

Per the Gux.13 spec, the license token encodes the edition + the
explicit `Features[]` array + `MaxUsers` + `MaxCompanies`. A
`LicenseGate.Require(Feature.X)` check sits in front of every
gated handler and surfaces an Arabic upgrade prompt on
`LicenseRestrictionException`.

Plan-ID convention for the self-service portal (G1.5):
- `solo-annual`, `solo-monthly`
- `smb-annual`, `smb-monthly`
- `enterprise-annual`, `enterprise-monthly`
- `firm-annual`, `firm-monthly`

---

## Resolved deferrals

Two questions left open in the previous (3-tier) version of this
doc are now resolved:

- **Solo tier below Basic** → ✅ adopted as the new entry tier.
- **Custom tier above Enterprise** → ✅ adopted as the Firm
  edition (with concrete features + price, not "contact us").

---

## How to use this doc

- **Landing page (G1.1):** quote the four annual prices in the
  pricing table; show monthly as a toggle. Use the "أرخص من
  غرامة تأخير" anchor in the Solo copy.
- **Self-service portal (G1.5):** pricing-tier table reads from
  these numbers + the plan-IDs above.
- **Edition gating (Gux.13):** `LicenseGate.Require()` rejects
  features not in the active edition's `Features[]`. Use the
  feature matrix above as the source of truth for which features
  belong to which edition.
- **`Issue-License.ps1`:** the `-Edition` parameter accepts
  `Solo`, `SMB`, `Enterprise`, or `Firm`. Update its help text +
  the `LicensePayload` minting code to encode the edition + the
  derived MaxUsers + MaxCompanies + Features.
- **G1.4 commission ledger:** when an operator records a referral,
  the `LicenseAnnualPriceEgp` field expects one of `3500`, `8000`,
  `17500`, or `30000`. The 20% commission rate yields 700 / 1,600
  / 3,500 / 6,000 EGP per referral.

---

## Change log

| Date | Change | Reason |
|---|---|---|
| 2026-05-11 (am) | Initial pricing locked: 3 tiers — Basic 2,500 / Pro 7,500 / Enterprise 21,000 EGP | G0.1 unblocker resolved; landing page + portal could reference real numbers |
| 2026-05-11 (pm) | Restructured to 4 editions per Gux.13 spec: Solo 3,500 / SMB 8,000 / Enterprise 17,500 / Firm 30,000 EGP | Gux.13 §7 introduces edition-aware license gating; the 4-edition ladder maps better to the Solo (newly-mandated micro) + Firm (accountant practice) personas. Resolves both deferred questions from the original lock |
