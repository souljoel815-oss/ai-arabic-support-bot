# Phase 0 Research: DaftarX Website + Customer Portal

**Plan**: [plan.md](./plan.md)
**Date**: 2026-05-18

Each decision below resolves a technology / architecture choice from `plan.md`'s Technical Context. There are no `NEEDS CLARIFICATION` markers carried over from the spec — `/speckit-clarify` Session 2026-05-18 closed the 5 ambiguous areas there. This file captures the technical choices the spec deliberately deferred.

---

## 1. Marketing pages + portal in one process vs split deployment

**Decision**: Single ASP.NET Core 8 process hosting both surfaces. Razor Pages for marketing under the root URL prefix (`/`, `/features`, `/pricing`, `/downloads`, `/privacy/*`, etc.), Blazor Server for the portal mounted under `/portal/*` with cookie auth required. Cloudflare in front handles edge caching: marketing routes carry `Cache-Control: public, max-age=3600`; portal routes carry `Cache-Control: no-store`.

**Rationale**: Single process keeps the deploy pipeline simple, lets the marketing pages reference the same `Pricing` model object the portal uses for tier upgrades (single source of truth for tier names + EGP prices), and uses the existing team's .NET expertise without forcing a JavaScript build pipeline. Cloudflare's path-based cache rules separate the cache postures cleanly without needing two origins.

**Alternatives considered**:
- *Marketing as Astro/Next.js static site + portal as separate Blazor app*: Two repos, two deploy pipelines, two CI lanes, two error-tracking dashboards. The performance gain from a static site is mostly cancelled by Cloudflare edge caching of the Razor pages. Not worth the operational overhead for a single-team project.
- *Both as Blazor WebAssembly*: WASM ships ~3-5 MB on first load — fatal for marketing SEO scores + Egyptian-broadband first-paint times.
- *Both as Blazor Server but using only Razor Components everywhere*: Marketing pages don't need a SignalR connection per visitor; the server cost of holding open WebSockets for anonymous prospects is a budget-killer at SEO scale.

---

## 2. Identity stack (per FR-032 — separate from on-prem product)

**Decision**: ASP.NET Core Identity with EF Core SQL Server stores, in a completely separate `DaftarXPortal` database. PortalUser class is independent of any on-prem product user. TOTP MFA via `Otp.NET` (the same library the on-prem product uses for its MFA, so the team already knows it).

**Rationale**: AspNetCore.Identity ships with battle-tested password hashing (PBKDF2), email confirmation, account-recovery flows, lockout, and session management. Using it costs ~30 min of setup vs hand-rolling an auth system over a long weekend. The separate database satisfies FR-032 mechanically — there's no physical join from PortalUser to the on-prem product's user store, so accidental coupling is impossible.

**Alternatives considered**:
- *Auth0 / Clerk / Supabase Auth*: External dependency + monthly cost + GDPR-style data-residency questions for Egyptian customers. AspNetCore.Identity is the local-control, zero-vendor-lock-in default.
- *Shared identity with the on-prem product*: Explicitly rejected by FR-032 — the spec made the call.
- *Hand-rolled password hashing*: Anti-pattern; the framework's PBKDF2 work-factor schedule is what we want.

---

## 3. Payment processor

**Decision**: Paymob hosted-checkout integration. Paymob is the single Egyptian aggregator that covers all four required FR-015 methods (credit/debit card, Fawry, Vodafone Cash, InstaPay) plus offline bank-transfer reconciliation through their dashboard. We integrate via their hosted-iframe checkout (PCI scope = SAQ A, the minimum), and the portal receives state via the standard webhook callback in `contracts/payment-webhook.md`.

**Rationale**: Going direct to each of the 4 providers separately would mean 4 separate integrations, 4 separate reconciliation pipelines, 4 separate compliance posture proofs, and 4 separate sets of customer-support escalation paths. Paymob is the consolidator that every Egyptian SaaS startup we surveyed uses (Khazna, Sumerge, MoneyHash all standardised on Paymob v3). Hosted-checkout is the lowest-PCI-scope option — the customer's card never touches our origin.

**Alternatives considered**:
- *Fawry direct + InstaPay direct + Vodafone Cash direct + Stripe for card*: Four times the integration cost; Stripe doesn't sell to Egyptian merchants directly.
- *MoneyHash (Paymob competitor)*: Newer, smaller transaction volume, less proven reconciliation tooling. Worth revisiting in year 2.
- *Building our own payment-aggregator integration*: PCI compliance + bank settlement contracts = wrong job for a vendor of accounting software, not a fintech.

---

## 4. Transactional email provider

**Decision**: Resend.com for transactional email (invitations, signup confirmations, payment receipts, ticket notifications, refund notifications, trial-ending reminders). Templates authored as MJML, compiled at build time into HTML + plain-text variants. Domain authentication via SPF + DKIM + DMARC on `daftarx.app`.

**Rationale**: Resend has clean deliverability into Egyptian inboxes (which Gmail rate-limits aggressively for low-reputation senders), a friendly API surface, and template management that doesn't require a Node runtime at request time. MJML solves the "email HTML is still 1998-grade table layouts" problem so the templates stay maintainable. The flat $20/month for our expected year-1 volume (~10k emails/month at peak) is trivial.

**Alternatives considered**:
- *SendGrid*: Larger competitor, more expensive, slightly worse Egyptian deliverability per the surveys; legacy tooling.
- *Amazon SES*: Cheapest per-email but requires significant warm-up to escape the "transactional sandbox" sender reputation. For 200 customers we'd never escape sandbox in v1.
- *Self-hosted Postfix*: Egyptian ISPs blocklist self-hosted SMTP almost on sight. Hard no.

---

## 5. PDF invoice generation (FR-016)

**Decision**: QuestPDF for server-side rendering of Arabic-RTL PDF invoices. Layout authored as fluent C# code so it composes with the existing invoice-number sequence + the Egyptian-tax-line requirements the on-prem product already encodes (we reuse the same invoice-template structure the on-prem product uses for sales invoices, just with vendor data + Subscription line items).

**Rationale**: QuestPDF is one of the very few PDF libraries that does Arabic RTL correctly out of the box (most break on bidi line-breaking, on connected-letter shaping, or both). The licence is free for revenue < $1M/yr (we're well under) and MIT-equivalent above the threshold — no surprise vendor lock-in. Authoring layouts in C# means designers + devs share the same diff review, no separate template-engine dependency.

**Alternatives considered**:
- *DinkToPdf / wkhtmltopdf*: Wraps a C++ HTML-to-PDF engine; Arabic shaping is broken in practice (we've seen this on the on-prem product's earlier attempt).
- *iText / PDFsharp*: Either AGPL (toxic for a closed-source codebase) or weaker RTL support.
- *Browserless HTML-to-PDF*: Requires a headless Chromium running alongside our process; heavy on memory + a known source of Linux-container instability.

---

## 6. Blob storage (invoice PDFs + ticket attachments)

**Decision**: Azure Blob Storage (Hot tier for active invoices/attachments, Cool tier auto-rotation after 90 days via a lifecycle rule). Dev environment runs Azurite (the local Azure-Blob emulator) so devs never need cloud credentials to build + run.

**Rationale**: Azure is the cloud the vendor's team already operates the on-prem desktop installer's update endpoint on, so we get a single billing relationship + a single ops dashboard. Lifecycle rules to Cool tier give us 60% storage-cost reduction on year-old artefacts. Azurite means CI runs against the same API surface as production without any conditional code paths.

**Alternatives considered**:
- *S3 / Cloudflare R2*: Both fine technically; switching costs the team a learning-curve hit for no concrete benefit in year 1.
- *Local-disk only*: Doesn't survive a single-region failover; loses everything on a host loss. Hard no for invoice artefacts (which have regulatory retention requirements).

---

## 7. SQL Server vs PostgreSQL for portal DB

**Decision**: SQL Server (Azure SQL or self-managed on the same Linux VM as the app). EF Core 8 SQL Server provider. Dev override allows SQLite for fast iteration.

**Rationale**: The existing on-prem product uses SQL Server; the team's EF Core migration muscle memory is SQL-Server-flavoured (filtered indexes, sequence objects, MERGE statements). Picking the same engine means one set of migration patterns, one query-tuning skill, one backup playbook. Year-1 scale (200 customers, ≤ 24k invoices/year) is comfortably within SQL Server Express's free tier limits, then bumps to Standard as growth dictates.

**Alternatives considered**:
- *PostgreSQL*: Technically excellent but the team doesn't operate one today; introducing it doubles the ops surface for a project that has no PostgreSQL-specific need.
- *SQLite in production*: Charming for tiny apps; loses to SQL Server on concurrent-writer scenarios (payment webhooks + portal UI both writing the Invoice table simultaneously).

---

## 8. Localization (FR-008 — ar-EG primary, en-US fallback)

**Decision**: ASP.NET Core's built-in `IStringLocalizer` + `.resx` files per page, with URL-prefix locale resolution (`/ar/...` and `/en/...`). The marketing site's content is in `Pages/.../Index.ar-EG.resx` + `Index.en-US.resx`; the portal's UI text is in shared resource files under `Localization/SharedResource.{locale}.resx`. RTL layout via CSS logical properties (`margin-inline-start` instead of `margin-left`) so the same stylesheet handles both directions.

**Rationale**: `.resx` is the framework-native localization story — no new dependency. URL-prefix locales are an SEO win (Google indexes the Arabic + English versions as distinct pages) and an unambiguous switcher experience (the user always knows which version they're on). CSS logical properties remove the need for a separate `.rtl.css` and the maintenance burden of keeping them in sync.

**Alternatives considered**:
- *Header-based locale detection only*: Single URL serves both locales depending on `Accept-Language` — bad for SEO (Google sees one URL), bad for sharing (link doesn't preserve locale).
- *JavaScript-based locale switching (i18next)*: Hydration delay on first paint; not appropriate for marketing pages that want fast SEO render.

---

## 9. Cloudflare cache strategy

**Decision**: Cloudflare in front of the origin with two cache rules:
- Marketing routes (`/`, `/ar/*`, `/en/*`, `/features`, `/pricing`, `/downloads`, `/privacy/*`, `/about`, `/contact`, `/terms`, `/refund`): cached at edge for 1 hour, purged on deploy via a CI hook that hits Cloudflare's purge API with the affected route set.
- Portal routes (`/portal/*`, `/api/*`, `/identity/*`): `Cache-Control: no-store` set at the origin; Cloudflare passes through unchanged.

**Rationale**: Marketing pages are the SEO + first-impression surface — every ms of TTFB matters, and they change infrequently. Portal pages are per-user authenticated data and MUST never be cache-shared between users. The two-rule split satisfies both. Cloudflare's purge-by-URL API is well-documented and integrates cleanly with the deploy pipeline.

**Alternatives considered**:
- *Cache portal pages too with `Vary: Cookie`*: Theoretically safer than no-cache, but burning Cloudflare cache slots on per-user pages with near-zero hit rate is a worse trade than just not caching.
- *No CDN, origin only*: Cairo broadband + a Frankfurt origin = ~150ms transatlantic latency PER request. Cloudflare's Cairo edge cuts that to ~10ms. Massive win for SEO + UX.

---

## 10. Licence signing — in-process call vs separate API

**Decision**: Wrap the existing `EgyptTax.Web.Tools.LicenseIssueHost` class as an injectable `ILicenceSigningService`. The portal calls it in-process. Same Ed25519 keypair + `vendor-keys.json` file the existing on-prem product reads. The portal deploy has read-only access to `vendor-keys.json` via a Docker secret mount.

**Rationale**: The existing `LicenseIssueHost.Run(args)` is a static CLI entrypoint; wrapping it as a service that takes a typed request object instead of a string array is ~30 lines of glue. Calling it in-process avoids an HTTP hop, avoids needing a second deploy unit, and inherits the existing signing logic 1:1 (so the on-prem product's verifier accepts portal-issued tokens unchanged). The vendor-keys.json secret is sensitive but already managed today; the portal just gets a mount.

**Alternatives considered**:
- *Standalone signing microservice*: Would isolate the key material to a single deployable, but introduces a network call, a secret-rotation coordination point, and a deploy dependency. Wrong shape for v1.
- *Re-implement signing in the portal*: Risks divergence from the existing format; pointless duplication.

---

## 11. Trial state ownership (FR-030)

**Decision**: The portal database has NO trial-related tables, columns, or state machines. The `Subscription` entity has a `Status` enum with values `Active`, `PastDue`, `Cancelled`, `Paused` — `Trial` is intentionally absent. Trial state is owned entirely by the on-prem product's existing `LicenseStatus.RecordTrial` path.

**Rationale**: Mandated by FR-030 (which itself came from the user's Q2 clarification "Auto-trial on install, no activation"). Removing trial state from the portal eliminates an entire table + an entire state-machine + a clock-skew risk between the portal's day-14 mark and the on-prem product's day-14 mark. The simplification is so deep it's worth restating: the portal has NO concept of "is this customer in trial". The portal only knows about paid Subscriptions.

**Alternatives considered**:
- *Track trial in the portal anyway, just for reporting*: Doubles the source of truth, creates a sync requirement that doesn't exist today, and gives the operations team conflicting trial-end times when the clocks disagree.

---

## 12. Refund handling (FR-034 — 7-day first-period window only)

**Decision**: The `Invoice` entity gets a computed `RefundEligibility` field (`Eligible | NotEligible_OutsideWindow | NotEligible_Renewal | NotEligible_TierUpgrade`). The 7-day window starts at `Invoice.PaidAtUtc` and applies only when `Invoice.Kind == FirstPeriod`. The Billing page surfaces eligibility inline per invoice. Refund button triggers a Paymob refund API call + writes an `AuditLogEntry` with the actor + amount.

**Rationale**: Computed at read time (no scheduler needed to flip eligibility flags). The kind discriminator on Invoice (FirstPeriod / Renewal / TierUpgrade / Addon) keeps the eligibility rule trivial. Paymob's refund API is idempotent against the original transaction id, so retries are safe.

**Alternatives considered**:
- *Store refund-window expiry as a real timestamp + scheduled job to flag invoices*: Scheduler complexity for no real benefit; the read-time computation is constant-time and obvious.

---

## 13. Year-1 scale + DB sizing

**Decision**: Year-1 target = 200 paying customers. Estimated table sizes:
- `CustomerOrganisations` ≤ 250 rows (some signups never pay)
- `TeamMembers` ≤ 2,000 rows (avg ~10/org for accounting firms, lower for SMEs)
- `Subscriptions` ≤ 250 rows (one per paying customer)
- `Licences` ≤ 750 rows (avg 3 per Subscription for LAN + multi-device cases)
- `Invoices` ≤ 24,000 rows/year (monthly cadence dominates for Solo/SMB; annual for Enterprise/Firm)
- `SupportTickets` ≤ 6,000 rows/year (30 tickets/customer/year heuristic)
- `SalesLeads` ≤ 5,000 rows/year (mostly unconverted)
- `AuditLogEntries` ≤ 250,000 rows/year (each state change writes one)

Total year-1 data footprint < 500 MB. SQL Server Express's 10 GB limit is fine; bump to Standard when AuditLogEntries cross 1 M rows (~year 4 at current growth).

**Rationale**: Estimate from comparable Egyptian SaaS startups + the vendor's pipeline. Scale is intentionally MODEST in year 1; v2 spec when we cross 5,000 customers will revisit DB sharding + read-replica strategy.

---

## 14. Backup + retention

**Decision**: Daily full backup + hourly differential to a separate Azure storage account (different region from production) with 30-day retention. Invoice PDFs in blob storage have 7-year retention per Egyptian tax-record requirements (the same rule the on-prem product follows for its own customer invoices). Account-deletion soft-delete (FR-024) is a 30-day flag on `CustomerOrganisation` — the nightly purge job moves soft-deleted rows to an audit-only archive table after 30 days, retaining only the AuditLogEntry rows.

**Rationale**: 30-day DB backup retention covers the typical "we accidentally deleted a customer's record" recovery window. 7-year invoice retention is the regulatory floor. Cross-region backup storage costs ~$5/month at year-1 scale and protects against the single-region failure scenario.

---

## 15. Observability + uptime target

**Decision**: Serilog with two sinks: structured JSON logs to a file (collected by the vendor's existing log pipeline) and a separate sink to Application Insights for searchable production diagnostics. Single uptime SLO target: 99.5% calendar-month availability (~3.5 hours of allowed downtime per month). Status page at `status.daftarx.app` shows incidents in real time (UptimeRobot pings every 5 min from 3 geographies).

**Rationale**: 99.5% is achievable on a single-region single-instance deploy with daily maintenance windows; pushing to 99.9% would require multi-region active-active which is a year-2 feature. Serilog matches the existing on-prem product's logging stack so the team transfers their skill. App Insights gives ad-hoc query power without standing up a separate ELK / Loki pipeline.

---

## 16. CI / CD pipeline

**Decision**: GitHub Actions workflow on push to `010-website-portal` and on every PR. Three stages:
1. **Build + unit-test**: `dotnet build` + `dotnet test tests/EgyptTax.Portal.UnitTests`. ~5 min.
2. **Integration test**: spin up SQL Server + Azurite via docker-compose, run `tests/EgyptTax.Portal.IntegrationTests`. ~10 min.
3. **Deploy on main merge**: build Docker image, push to Azure Container Registry, restart the App Service slot, run Playwright E2E smoke against the staging slot, swap slots on green. ~15 min.

**Rationale**: Three-stage gating catches contract + integration issues before they reach customers. Slot-swap deploys give us instant rollback (swap-back) if the post-deploy smoke fails. GitHub Actions is the existing CI the team uses for the on-prem product, so zero net new tooling.

---

## 17. Out-of-scope debt acknowledgments

The following are deliberately NOT addressed in this plan; they belong in follow-up features:

- **White-label / reseller portal for accounting firms** — spec out-of-scope; future feature.
- **Multi-currency** — spec assumption pins to EGP for v1; expansion = a separate `pricing-multi-currency` feature.
- **Live chat widget** — out-of-scope per spec.
- **Affiliate / referral program** — out-of-scope; the existing in-product `/settings/referrals` covers product-side; portal variant is a future feature.
- **Customer's data backup/restore from cloud** — out-of-scope; the on-prem product owns business data.
- **Multi-region active-active** — year-2 capacity planning, not a v1 functional requirement.
- **Native mobile app for the portal** — out-of-scope; responsive web is enough.
- **Public API for customer-built integrations** — FR-018 hints at an "API keys" section but the spec doesn't pin scope; defer to a `portal-public-api` feature once a customer asks.
- **In-portal product help videos** — defer to a separate `customer-education` feature.
