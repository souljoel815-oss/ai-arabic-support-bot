# Pricing Decision (G0.1)

**Decided:** 2026-05-11
**Status:** ✅ locked for v1
**Supersedes:** None (first locked pricing)

This is the canonical pricing reference. Anything that displays
prices to customers — landing page (G1.1), self-service portal
(G1.5), accountant commission ledger (G1.4), license-issuance tool
(`Issue-License.ps1`) — pulls from these numbers.

---

## The three tiers

| Tier | Annual | Monthly | Annualized monthly | Monthly premium |
|---|---:|---:|---:|---:|
| **Basic** | 2,500 EGP | 250 EGP | 3,000 EGP | +20% |
| **Pro** | 7,500 EGP | 700 EGP | 8,400 EGP | +12% |
| **Enterprise** | 21,000 EGP | 2,000 EGP | 24,000 EGP | +14% |

Monthly is intentionally priced higher when annualized — covers
higher churn risk and steers customers toward annual where you
collect cash up front.

---

## Feature ladder

| Feature | Basic | Pro | Enterprise |
|---|:-:|:-:|:-:|
| ETA submission + dashboard | ✓ | ✓ | ✓ |
| Form 41 WHT generator | ✓ | ✓ | ✓ |
| VAT return (G3.3) | ✓ | ✓ | ✓ |
| Annual income-tax return (G3.4) | ✓ | ✓ | ✓ |
| Penalty Shield + Closing Cockpit | ✓ | ✓ | ✓ |
| Compliance Health + Calendar | ✓ | ✓ | ✓ |
| Audit log + Document 360 + Inspection bundle | ✓ | ✓ | ✓ |
| Single user | ✓ | — | — |
| Up to 5 users | — | ✓ | — |
| Unlimited users | — | — | ✓ |
| Multi-cashbox + bank CSV import (G1.3) | — | ✓ | ✓ |
| Bulk invoice upload (G1.2) | — | ✓ | ✓ |
| Receipt OCR scanner (G3.2) | — | ✓ | ✓ |
| WhatsApp invoice delivery (G2.2) | — | ✓ | ✓ |
| ETA item-code AI suggester (G2.1) | — | ✓ | ✓ |
| Customer referral codes (G4.3) | ✓ | ✓ | ✓ |
| Auto-update banner (G4.1) | ✓ | ✓ | ✓ |
| Firm Portal — multi-company (G1.4) | — | — | ✓ |
| Cloud backup (G4.2, when shipped) | — | — | ✓ |
| Arabic AI tax assistant (G3.1, when shipped) | — | — | ✓ |
| Priority support (24h SLA) | — | — | ✓ |

---

## Why these numbers

### Basic — 2,500 EGP/year

**Frame:** half a single tier-1 ETA penalty (5,000 EGP). The pitch
writes itself in Arabic: *"اشترك بنص غرامة واحدة"* / "Subscribe
for half the cost of one penalty."

- Decisively undercuts Daftra's lowest plan (~5K EGP).
- For a freelancer at the new mandated 250K-EGP minimum revenue
  threshold (Decree 281/2025), it's 1% of revenue —
  psychologically passable, often deductible as a business expense.
- Volume play — primary target is the tens of thousands of
  newly-mandated micro-businesses who've never paid for accounting
  software before.

### Pro — 7,500 EGP/year

**Frame:** less than an accountant's monthly fee.

- 3× Basic — standard SaaS upgrade ratio.
- Sits **just below** Daftra's mid tier so a feature-comparing
  customer who wanted Daftra Standard finds DaftarX Pro at a
  small discount with on-prem + Penalty Shield + OCR + WhatsApp
  delivery thrown in.
- Firm commission per Pro referral: **1,500 EGP** (20% of
  first-year revenue per the G1.4 ledger). Meaningful for firms
  pitching DaftarX to their clients.

### Enterprise — 21,000 EGP/year

**Frame:** firm-scale tooling for accounting practices that want
to run their entire client roster on DaftarX.

- ~3× Pro again — same upgrade ratio.
- Buyer here is the accounting firm itself, not the SMB.
- Firm commission per Enterprise referral: **4,200 EGP** — large
  enough to motivate a partner pitch.
- Includes the still-shipping items (G3.1 AI assistant + G4.2
  cloud backup) so Enterprise customers get those automatically
  when they ship — no upsell email needed.

---

## Renewal model

**Manual renewal — no auto-charge in v1.**

- Reminders fire at 30 / 14 / 7 days before expiry (Hangfire job,
  to be added when G1.5 portal ships).
- Customer goes back to the portal and re-buys for another year.
- Trial-style 7-day grace after expiry where the app shows a
  reminder banner but stays functional; on day 8 it falls back to
  read-only mode (existing license-gate behaviour).

**Why manual instead of auto-renew:**
- Egyptian SMBs are wary of recurring card charges; "buy once a
  year" matches the existing mental model for software licenses
  (think: Microsoft Office, antivirus subscriptions).
- Less Paymob complexity in G1.5 (no recurring-billing setup,
  no failed-card handling, no proration on plan changes).
- Lower retention than auto-renew, but reminder cadence + the
  in-app banner mitigate it.

Auto-renew is a candidate for a future G0.4 unblocker once we have
real renewal data and Paymob's recurring-billing API is wired.

---

## Deferred for next pricing review

Two tier-structure questions deliberately left open today; revisit
once we have ~50 paying customers and can read the demand signal:

1. **Solo tier below Basic** (~1,500 EGP/year) — ETA submission +
   Form 41 + VAT return only, view-only on Penalty Shield. Targets
   freelancers who just need to satisfy the regulator and nothing
   else. Adds it if Basic feels too expensive for the
   newly-mandated wave.
2. **Custom tier above Enterprise** — "contact us" white-glove
   deployment, no fixed price. Adds it if firms keep asking for
   integrations / dedicated support.

---

## How to use this doc

- **Landing page (G1.1):** quote the three annual prices in the
  pricing table; show monthly as a toggle. Use the "اشترك بنص
  غرامة" anchor in the Basic copy.
- **Self-service portal (G1.5):** pricing-tier table reads from
  these numbers. Plan IDs: `basic-annual`, `basic-monthly`,
  `pro-annual`, `pro-monthly`, `enterprise-annual`,
  `enterprise-monthly`.
- **`Issue-License.ps1`:** the `-Edition` parameter accepts
  `Basic`, `Pro`, or `Enterprise`. Update its help text to match
  these prices.
- **G1.4 commission ledger:** when an operator records a referral,
  the `LicenseAnnualPriceEgp` field expects one of `2500`, `7500`,
  or `21000`. The 20% commission rate yields 500 / 1,500 / 4,200
  EGP per referral.

---

## Change log

| Date | Change | Reason |
|---|---|---|
| 2026-05-11 | Initial pricing locked: 2,500 / 7,500 / 21,000 EGP | G0.1 unblocker resolved; landing page + portal can reference real numbers |
