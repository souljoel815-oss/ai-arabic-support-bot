# Phase 0 Research: DaftarX Website + Customer Portal

**Plan**: [plan.md](./plan.md)
**Date**: 2026-05-18 (stack-switched from .NET to Laravel 11 — same date, replaces the .NET research at commit 6bf395e)

This document records the 17 technical decisions taken before any Laravel code lands. Each entry follows the format:

- **Decision**: what was chosen
- **Rationale**: why
- **Alternatives considered**: what else was evaluated + why rejected

---

## §1 — Hosting platform

**Decision**: Hostinger Shared Hosting (Premium or Business plan — both run PHP 8.4, Nginx + PHP-FPM, MySQL 8, cron with 1-minute resolution).

**Rationale**: The customer purchased Hostinger before the architecture conversation. The Shared plan is what's paid for, and it covers everything Laravel needs: PHP 8.4, MySQL 8, SSH access (Premium+), Composer, `.htaccess` / Nginx URL rewriting, Let's Encrypt HTTPS, daily backups. Cloudflare in front (free tier) gives the edge cache + DDoS that satisfy SC-006 from a Cairo broadband connection.

**Alternatives considered**:
- *Hostinger VPS* (~$5-10/mo) — strictly better technically (Docker, long-running workers, no per-request time limit) but rejected because we have a budget already spent on Shared and the spec's scale target (200 customers year 1) fits comfortably in shared resources.
- *Azure App Service / DigitalOcean App Platform* — better .NET / Docker support but the customer doesn't have an account there.
- *Self-host on a Hetzner box* — cheapest long-term but the customer doesn't operate Linux servers.

## §2 — Application framework

**Decision**: Laravel 11 (current LTS — security patches through Aug 2026, framework patches through Mar 2026).

**Rationale**: Laravel is the canonical "boring web app" PHP stack: opinionated MVC, mature ORM (Eloquent), built-in auth scaffold (Breeze), first-class testing (Pest), excellent docs. Standard Laravel directory layout means any Laravel developer can navigate the codebase immediately. Hostinger's Shared plan supports Laravel out of the box (Composer + `.htaccess` rewrite to `public/index.php` work without special config).

**Alternatives considered**:
- *CodeIgniter 4* — lighter, simpler, but no queue, no Mailable system, no auth scaffold. We'd reimplement those, eating any "simpler" savings.
- *Symfony 7* — more enterprise-grade architecture (DI container, contracts, Doctrine ORM) but the learning curve is steeper and shared hosting is awkward.
- *No framework — raw PHP* — fastest startup, slowest maintenance. Auth / sessions / CSRF / routing reinvention turns into a security risk.

## §3 — Payments

**Decision**: Paymob v3 API via direct HTTPS calls (`Illuminate\Http\Client`). Single Egyptian aggregator covers all four required methods (card / Fawry / Vodafone Cash / InstaPay) per FR-015.

**Rationale**: Paymob is the dominant Egyptian aggregator and the only one with documented APIs for all four required methods + automated payouts in EGP. Hosted-checkout flow means the portal never touches card numbers (PCI scope = SAQ-A, the easiest tier). HMAC webhook verification protects the asynchronous callback path. No PHP SDK is needed — the API surface is 4-5 endpoints that map cleanly to a thin HTTP adapter.

**Alternatives considered**:
- *Stripe* — better DX, terrible Egypt support (no Fawry, no Vodafone Cash, EGP only partially supported).
- *PayTabs* — Egyptian-friendly but lower integration quality (sparse docs, undocumented webhook quirks per community reports).
- *Multi-provider abstraction layer* — over-engineering for v1.

## §4 — Email

**Decision**: Resend.com via SMTP (`smtp.resend.com:587`), Laravel's `mail` driver pointed at it. Templates as Blade Mailables.

**Rationale**: Resend has the simplest dev-experience among modern transactional providers (Postmark / SendGrid / Mailgun), with explicit MENA / Egypt deliverability. SMTP-via-Laravel-mail means the codebase has no Resend-specific code — switching providers is a `.env` change. Hostinger Shared blocks raw SMTP outbound on port 25 but allows port 587 (Resend's SMTP submission port), so this works on the cheap plan.

**Alternatives considered**:
- *Resend HTTP API* — slightly faster + better error reporting but locks the codebase to Resend. SMTP is the lowest-common-denominator.
- *Hostinger's built-in SMTP relay* — limited to ~200 emails/day on Shared.
- *Amazon SES* — cheapest long-term but Egypt-specific delivery rate is documented-as-poor.

## §5 — PDF invoice generation

**Decision**: `barryvdh/laravel-dompdf` (Composer package wrapping DOMPDF), with Arabic shaping via the embedded DejaVu Sans + Cairo fonts.

**Rationale**: DOMPDF is the only PHP PDF library with reliable Arabic-RTL rendering that runs without ImageMagick / Ghostscript (both blocked on Hostinger Shared). DejaVu Sans handles bidi text + Arabic shaping correctly when paired with Cairo font for branded headings. Workflow: Blade template → HTML → DOMPDF → PDF, all in one PHP request.

**Alternatives considered**:
- *mPDF* — better Arabic shaping out of the box but 4x larger Composer dependency footprint + slower per-PDF render time.
- *wkhtmltopdf* — requires a binary install (not available on Hostinger Shared).
- *Snappy / wkhtmltopdf-as-API service* — adds an external dependency for a feature we can render in-process.

## §6 — File storage

**Decision**: Local disk via Laravel's `Storage::disk('local')` writing to `portal/storage/app/private/` on Hostinger. Path layout: `invoices/{org-id}/{invoice-number}.pdf` + `tickets/{org-id}/{ticket-id}/{attachment-id}.{ext}`. No S3 / Azure Blob in v1.

**Rationale**: Hostinger Shared plans include 100-200 GB of disk; the scale target (year-1: ~24,000 invoices × ~50 KB + ~6,000 ticket attachments × ~2 MB = ~13 GB) fits with room to spare. Local disk = zero external dependency, zero monthly cost, simple backup story.

**Alternatives considered**:
- *Backblaze B2* — $5/TB/mo, S3-compatible, sane EU regions. Right answer when scale exceeds ~50 GB or when we add a second region.
- *Cloudflare R2* — cheapest egress-free but adds account + access-key complexity for a feature we can do for free on local disk.
- *AWS S3* — Egypt-region only landed in 2024, still pricier than B2.

## §7 — Database

**Decision**: MySQL 8 (Hostinger default) as the portal's `daftarx_portal` schema. Local dev override: SQLite file at `database/database.sqlite` for zero-config startup.

**Rationale**: MySQL is what Hostinger provisions automatically; no upcharge, no special config. Eloquent supports both MySQL and SQLite seamlessly so the dev override doesn't fork the code. The portal schema is small (8-13 tables) and read-mostly with bursts on payment-webhook processing — MySQL's row-locked InnoDB handles this trivially.

**Alternatives considered**:
- *PostgreSQL* — better feature set but Hostinger Shared plans don't provision Postgres.
- *MariaDB* — drop-in MySQL replacement, no concrete advantage.
- *SQLite in production* — single-file simplicity but Hostinger's shared filesystem doesn't guarantee fsync semantics across multiple PHP-FPM workers.

## §8 — Localization + RTL handling

**Decision**: Laravel's built-in `__('key')` + `lang/{ar,en}/*.php` files for all UI strings. URL-prefix routing (`/ar/*` + `/en/*`) via a `LocaleResolver` middleware that reads the first path segment and sets `App::setLocale()`. Default locale is `ar-EG`; RTL handled by CSS logical properties (`margin-inline-start`, `padding-inline-end`).

**Rationale**: Laravel's localization is feature-complete (pluralisation, parameter interpolation, nested file structure) and well-trodden by Arabic + Persian + Hebrew Laravel communities. CSS logical properties let one stylesheet serve both directions — no per-locale CSS fork. URL-prefix routing is SEO-friendly (Google indexes ar + en as distinct pages per FR-009 + the `hreflang` alternates).

**Alternatives considered**:
- *Spatie's `laravel-translatable`* — adds per-row translation in the DB. Overkill for static UI strings.
- *Subdomain-per-locale* (`ar.daftarx.app`) — better SEO isolation but requires DNS + SSL config per subdomain.
- *Cookie-only locale detection* — invisible to Google; would tank SC-001 indirectly.

## §9 — Cloudflare cache strategy

**Decision**: Cloudflare in front of the Hostinger origin. Two cache rules in `deploy/portal/cloudflare/cache-rules.json`:
1. **Marketing routes** (`/`, `/features`, `/pricing`, `/downloads`, `/about`, `/contact`, `/terms`, `/refund`, `/privacy/*`) cache HTML at edge for **1 hour**, browser TTL 5 min, purged on every deploy via the Cloudflare purge API.
2. **Portal + identity + API routes** (`/portal/*`, `/api/*`, `/login`, `/register`) — `Cache-Control: no-store`, never cached.

**Rationale**: The 1-hour edge cache absorbs almost all marketing traffic with sub-100 ms TTFB from Cairo (Cloudflare has Egypt POPs), comfortably hitting SC-006's 3-second p75 target. Authenticated portal pages contain per-user data + session cookies — caching them at the edge would leak data between customers, so they get `no-store`.

**Alternatives considered**:
- *Origin-only* (no Cloudflare) — Hostinger's Frankfurt POP has ~80-100 ms latency from Cairo; TLS handshake + initial connection costs eat the 3-second budget on cold visits.
- *Longer edge TTL (1 day)* — risks stale pricing / downloads pages for a full day after the vendor pushes a fix.
- *Cache by query string* — risks duplicating cache entries for tracking params.

## §10 — Licence signing reuse

**Decision**: PHP's built-in `sodium_crypto_sign_detached()` (libsodium binding) produces Ed25519 signatures byte-compatible with the on-prem `EgyptTax.Web.Licensing.LicenseVerifier`. Same `vendor-keys.json` keypair file is read by both processes. No code shared, no .NET binary invoked from PHP.

**Rationale**: Ed25519 is a deterministic standard — the same private key + same canonical-bytes input produces the same signature in any language. The on-prem product uses BouncyCastle (.NET), the portal uses sodium (PHP) — both libraries implement RFC 8032 unchanged. The contract test (`portal/tests/Feature/Contracts/ActivatePaidLicenceTest.php` per [contracts/activate-paid-licence.md](./contracts/activate-paid-licence.md)) verifies the round trip end-to-end.

**Alternatives considered**:
- *PHP-FFI calling BouncyCastle.dll* — couples the portal to the on-prem .NET runtime; defeats FR-032 isolation.
- *Subprocess to `dotnet run --project EgyptTax.Web -- license-issue ...`* — requires .NET 8 runtime on Hostinger (not available on Shared); slow per-call.
- *Reimplement Ed25519 in pure PHP* — security risk; sodium is the right answer.

## §11 — Trial state ownership

**Decision**: The portal never stores trial state (per FR-030). There is no `Subscription` row for a trial. The portal's dashboard inspects whether the customer has any non-`Cancelled` paid Subscription rows — if zero, the dashboard renders the "Subscribe to keep going" CTA. Trial duration + feature gating remain entirely client-side in the on-prem product's `LicenseStatus.RecordTrial` flow.

**Rationale**: Reduces complexity (no trial state machine, no trial-renewal cron, no trial-extension admin tool). Matches what the clarification chose. The on-prem product already has battle-tested trial logic.

**Alternatives considered**:
- *Track trial start/end server-side* — would let the portal show "8 days left". Rejected because the trial happens on a machine the portal can't observe; any server-side counter would drift from reality.

## §12 — Refund handling

**Decision**: 7-day full refund on the first paid Subscription period only (per FR-034). Computed inline in the `Invoice` Eloquent model via a `getRefundEligibilityAttribute()` accessor that returns `eligible` / `not_eligible_renewal` / `not_eligible_window_expired` / `not_eligible_tier_change`. The refund itself reverses the Paymob transaction via their refund API.

**Rationale**: Self-service refunds reduce support load. The 7-day window prevents abuse (the customer already had 14 days of trial to evaluate). First-period only prevents long-tenured customers from refunding-then-rejoining as a discount tactic. Computing eligibility as an accessor (not a column) means the rule can be tweaked without a migration.

**Alternatives considered**:
- *30-day refund window* — too generous; encourages "tire-kicker" subscriptions.
- *No self-service refunds (ticket-only)* — adds support friction; rejected.

## §13 — Scale + capacity planning

**Decision**: Year-1 target = 200 paying customers × 10 Team Members average = ≤ 2,000 portal users. Hostinger Shared plan handles this with ~10% of its resource budget per the published limits (~300 concurrent connections, ~100,000 file inodes, ~100 GB disk).

**Rationale**: 200 customers × ~30 page-views/customer/week = ~6,000 portal page-views/week ≈ 1 request/min average. Even a 10x peak is comfortably under the Hostinger limits. Marketing-page traffic is mostly absorbed by Cloudflare edge cache so origin load is dominated by portal traffic.

**Alternatives considered**:
- *Sizing for 10,000 customers from day one* — over-engineering. Scale concerns trigger an architecture review at 1,000 customers (~5x current target).

## §14 — Backups

**Decision**: Hostinger's automated daily backups (included in Premium+ plans) cover both the MySQL database + the `storage/` tree. Retention: 7 days on Hostinger's side. Weekly off-site dump to Backblaze B2 via a cron-driven Artisan command (`php artisan backup:weekly-snapshot`) — retained 90 days.

**Rationale**: Defence-in-depth — Hostinger's backups protect against operational errors; Backblaze copies protect against catastrophic Hostinger-side incidents. 90-day retention covers the typical disclosure-to-discovery window for security incidents.

**Alternatives considered**:
- *Daily off-site* — heavy disk + bandwidth use for marginal additional safety.
- *Manual weekly* — too easy to forget; automated is the only reliable answer.

## §15 — Observability

**Decision**: Laravel's built-in log channel writes to `storage/logs/laravel-{date}.log` with daily rotation (30-day retention). UptimeRobot free tier pings `/health` from 3 geographies (Cairo, Frankfurt, US-East) every 5 minutes. No Application Insights / Sentry / Datadog in v1.

**Rationale**: For 200 customers the operational visibility cost-benefit doesn't justify a paid APM. Laravel's `Log::error()` + the daily-rotated files give enough for incident postmortems. UptimeRobot's free tier covers up-down monitoring without code changes. Migrating to Sentry's free tier (5,000 events/month) is a Phase 9 polish task if error volume warrants it.

**Alternatives considered**:
- *Sentry from day one* — adds Composer dep + per-error tracking. Right answer at scale.
- *Application Insights via the Laravel SDK* — Azure-coupled, no clear win over Sentry.

## §16 — CI/CD

**Decision**: GitHub Actions runs the build + tests on every push (`pest`, `php artisan test`, asset build via Vite). On push to `main`, a separate workflow rsyncs `portal/` to Hostinger via SSH (Premium+ plans expose SSH), runs `composer install --no-dev`, `php artisan migrate --force`, `php artisan config:cache`, and `php artisan view:cache`. No Docker, no container registry.

**Rationale**: SSH + rsync is the canonical "boring" deploy for PHP apps. Faster than Docker pipelines (no image build), simpler than Hostinger's built-in Git auto-deploy (which doesn't run migrations or asset builds). The SSH key is a GitHub Actions secret; rotation procedure documented in `deploy/portal/README.md`.

**Alternatives considered**:
- *Hostinger Git auto-deploy* (from hPanel) — convenient but doesn't run migrations or `composer install` automatically.
- *Manual FTP / File Manager upload* — manual + error-prone; OK for emergency hotfixes but not as the primary deploy path.

## §17 — Out-of-scope debt

**Decision**: Items explicitly NOT shipping in v1, listed here so they don't come up as "missing" surprises mid-implementation:

- **Multi-currency** — EGP-only per spec assumptions.
- **Multi-region hosting** — single Hostinger plan.
- **Real-time notifications** — no WebSocket support on Shared.
- **Single sign-on with on-prem** — explicitly rejected per FR-032.
- **Mobile app for the portal** — the portal is responsive web only.
- **API for third-party integrations** — the `/api/v1/portal/*` endpoints are internal-only in v1.
- **White-label / per-firm branding** — out of scope for v1 even though the Firm tier is supported.
- **In-app help videos / live chat** — support is via tickets + WhatsApp per spec assumptions.
- **Self-service password rotation policy enforcement** — Laravel Breeze handles password resets; org-level "must change every N days" lands in v2 if a customer asks.
- **GDPR-style "data export"** — FR-024 covers deletion but data-portability export is out of scope for v1.

Each of these has a "land in v2 if asked" disposition — none are blocking the P1 MVP.
