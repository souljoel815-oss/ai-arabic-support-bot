# Unblockers — what's stopping the last 5 roadmap items

As of 2026-05-11, every engineering item in the post-MVP roadmap
that didn't need a user-supplied input has shipped (see
[`specs/008-egypt-tax-accounting/post-mvp-roadmap-v2.md`](specs/008-egypt-tax-accounting/post-mvp-roadmap-v2.md#status-as-of-2026-05-11)
for the full status table).

Five items remain. Each needs a concrete decision or credential you
have to provide. This doc lists exactly **what** is needed, **where**
to obtain it, **how long** the unblocker takes, and **what ships**
once it lands.

---

## 1. G0 — Pricing decision  *(unblocks: G1.1 landing page)*

**What I need from you:** three EGP-per-year numbers — Basic,
Pro, Enterprise. The roadmap is built on the "tax-compliance
insurance, not generic accounting" pitch (pay 3K/year vs. 20K-200K
in penalties), so the bottom tier should be in the 2K-5K range to
beat Daftra/Wafeq's lowest plan and feel like an obvious yes.

**Suggested anchors** (you decide the actual numbers):
- **Basic** EGP 3,000/year — single user, ETA + Form 41
- **Pro** EGP 9,000/year — 5 users, multi-cashbox, bank import
- **Enterprise** EGP 24,000/year — unlimited users, Firm Portal,
  on-prem or VPS install

**How long:** 1 hour (decision + record in CRM/sales doc).

**What ships once landed:** G1.1 landing page (~3-4 days work) can
quote real prices instead of "TBD". G1.5 self-service portal pricing
table also fills in.

---

## 2. G0 — Domain  *(unblocks: G1.1 landing, G2.4 marketing copy, G4.1 auto-update URL)*

**What I need from you:** purchase + DNS access for one of
`daftarx.com`, `daftarx.app`, or `daftarx.eg`. `.com` and `.app`
are ~$15/year on any registrar; `.eg` requires an Egyptian entity
and goes through https://www.egregistry.eg.

**How long:** 30 min (registrar checkout) → 24-48h DNS propagation.

**What ships once landed:**
- G1.1 landing page deployed to `https://daftarx.com`
- G4.1 auto-update banner can point at a real manifest URL (today
  it's a placeholder)
- G2.4 marketing copy gets an `info@daftarx.com` contact email

---

## 3. G0.3 — Payment processor  *(unblocks: G1.5 self-service portal)*

**What I need from you:** a Paymob merchant account (recommended) or
a documented manual InstaPay flow.

**Paymob path** (recommended):
1. Apply at https://accept.paymob.com/portal2/en/register — needs
   commercial registration + tax card + bank account.
2. Approval takes 5-10 business days.
3. Once approved, you'll get: API key, public key, integration ID,
   HMAC secret. Drop these into:
   - `appsettings.Production.json` → `Paymob` section
   - Or env vars `PAYMOB_API_KEY`, `PAYMOB_INTEGRATION_ID`, `PAYMOB_HMAC`
4. Tell me when ready and I'll wire G1.5 (~2 days).

**InstaPay path** (cheaper for v1, adds friction):
- You give customers your wallet number on the landing page; they
  pay manually; you confirm + run Issue-License.ps1 to mint their
  token. No code changes needed beyond a "Pay via InstaPay"
  page on G1.1.

**How long:** Paymob = 5-10 business days for approval; InstaPay = same day.

**What ships once landed:** G1.5 self-service portal — customer
visits site, picks a plan, pays, the portal mints + emails the
license token. Eliminates the manual `Issue-License.ps1` step.

---

## 4. G3.1 — Arabic AI tax assistant  *(unblocks: chatbot inside the app)*

**What I need from you:**

1. **An Anthropic API key.** Sign up at https://console.anthropic.com,
   verify the email, create an organization, generate a key under
   "API Keys".
2. **A monthly budget cap.** Anthropic console → Settings → Limits.
   Recommend starting at $50/month (Egyptian SMB usage will be
   well under this).
3. **Approval to send anonymized customer-tax-question text to
   Anthropic.** No PII (names, TINs, amounts) — the planned
   prompt template strips those before sending. Confirm you're
   OK with this in writing (a one-line email reply is fine; I'll
   capture it in the commit message).

**How long:** 30 min for the API key + budget cap; 1 day for the
PII-stripping review + sign-off.

**What ships once landed:** G3.1 chatbot at `/assistant` (~2-3 days).
Operator types a question in Arabic ("متى آخر موعد لتقديم الإقرار
الضريبي؟"); the assistant answers using the bundled Egyptian tax
knowledge base + Claude Sonnet 4.6 for the natural-language layer.

**Estimated monthly cost:** $5-15/month for typical SMB usage
(50-200 questions/month at ~1k tokens each).

---

## 5. G4.2 — Optional cloud backup  *(unblocks: opt-in encrypted backups to S3)*

**What I need from you:** an S3-compatible bucket + IAM credentials.
"S3-compatible" means AWS S3, Backblaze B2, Wasabi, Cloudflare R2,
DigitalOcean Spaces — any of them. You pick based on cost + data
residency preference.

**Cheapest path for Egyptian customers:**
- **Cloudflare R2** — $0.015/GB/month, no egress fees, Cairo
  POP (~10ms). Sign up at https://dash.cloudflare.com → R2.
  Create a bucket named `daftarx-backups` and an API token with
  Object Read+Write scoped to that bucket.

**What I need:**
1. Endpoint URL (e.g., `https://<account>.r2.cloudflarestorage.com`)
2. Bucket name
3. Access key ID
4. Secret access key
5. Region (use `auto` for R2)

Drop into `appsettings.Production.json` → `Backup` section, or env
vars `BACKUP_ENDPOINT`, `BACKUP_BUCKET`, `BACKUP_KEY_ID`, `BACKUP_KEY_SECRET`.

**How long:** 30 min for the bucket + token; 1 day for me to wire
it (Hangfire job + AES-256 encryption + integrity manifest).

**What ships once landed:** G4.2 cloud backup — daily cron uploads
the SQLite/SQL Server backup encrypted with the customer's
license-derived key. Restore via `Restore-FromCloud.ps1`. Customer
opt-in via Settings → Backup.

---

## What does NOT need anything from you

These are all live on the current branch and ready to use:

- ✅ Bulk invoice upload (`/invoices/bulk`)
- ✅ Bank CSV import (`/payments/bank-statements/import`)
- ✅ Accountant commissions (`/firm-portal/commissions`)
- ✅ ETA item-code suggester (live on `/items`)
- ✅ WhatsApp dispatch (mock by default; swap dispatcher for the
  real Meta Cloud one when you have keys — not on the critical
  path since the mock writes the audit row + logs the message)
- ✅ Compliance Health page (`/compliance/health`)
- ✅ Receipt OCR (`/expenses/new` "🧾 Scan receipt" — drop
  tessdata files next to the exe, no other config)
- ✅ VAT-return generator (`/tax/vat-return`)
- ✅ Income-tax return generator (`/tax/income-tax-return`)
- ✅ Auto-update banner (placeholder manifest URL until G0.2 landed)
- ✅ Customer referral codes (`/settings/referrals`)
- ✅ Full Gux UX overhaul (12 modules)

---

## TL;DR

| Need | Time to get | Unblocks |
|---|---|---|
| Three pricing tiers | 1 hour | G1.1 |
| `daftarx.{com,app,eg}` | 30 min + 48h DNS | G1.1, G2.4, G4.1 |
| Paymob account | 5-10 business days | G1.5 |
| Anthropic API key + $50/mo budget | 30 min | G3.1 |
| S3-compatible bucket + token | 30 min | G4.2 |

Ping me with any of the above and I'll ship the corresponding
feature within the listed effort window.
