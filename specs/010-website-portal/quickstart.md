# Quickstart: DaftarX Website + Customer Portal

**Plan**: [plan.md](./plan.md)
**Audience**: A developer cloning the `speckit-co` repo who wants to build + run the portal locally and validate the P1 MVP slice end-to-end.

This walks the P1 MVP (marketing pages live + signup + manual paid activation + downloads page) without any P2 dependencies (no support tickets, no multi-user invitations, no Play Store privacy URL guarantees yet — those land in their own task phases).

---

## Prerequisites

1. **Laravel Herd** for Windows — bundles PHP 8.4 + Composer + Node.js + MySQL in one installer. Download from [herd.laravel.com](https://herd.laravel.com). Or roll your own with PHP 8.4 + Composer 2.x + Node 20+ + MySQL 8 / MariaDB.
2. **Git** for cloning + version control.
3. **A copy of `vendor-keys.json`** in `<repo root>/vendor-keys.json` — the Ed25519 keypair the licence signer uses. The file is gitignored; pull it from the vendor's password manager. Without it, the paid-licence activation flow throws.

Optional but recommended:
- **TablePlus** or **Sequel Ace** for browsing the MySQL / SQLite dev DB.
- **Mailpit** for inspecting the dev emails — install via `composer global require mailpit/mailpit` or download from [mailpit.axllent.org](https://mailpit.axllent.org). Laravel's `mail` driver defaults to logging emails when no SMTP is configured, but Mailpit's UI is nicer for verifying templates.

---

## First build

From the repo root:

```pwsh
cd portal

# Install PHP dependencies (composer install respects composer.lock)
composer install

# Install Node dependencies for Vite + Tailwind asset build
npm install

# Copy the env template + generate the APP_KEY
copy .env.example .env
php artisan key:generate

# Apply migrations against the dev SQLite DB (zero config — file appears at database/database.sqlite)
php artisan migrate

# Seed dev fixtures (3 customer orgs + test users + pre-bound licences for the SC-003 flow)
php artisan db:seed

# Compile Tailwind + Alpine via Vite (use --watch in another terminal for hot-reload)
npm run dev
```

Run:

```pwsh
php artisan serve --port=5050
```

Open `http://localhost:5050` — the homepage should render in Arabic with the gold/charcoal theme.

> **Note**: Laravel Herd users can skip `php artisan serve` and add the `portal/public` directory as a Herd site — Herd serves it at `http://portal.test` with HTTPS via Herd's local cert authority.

---

## P1 walkthrough

### Marketing surface

1. **Homepage** at `http://localhost:5050/` — RTL Arabic by default, "ابدأ التجربة" CTA prominent.
2. **Language toggle** → switch to `/en` — page renders LTR English, scroll position preserved.
3. **Pricing** at `/pricing` (or `/ar/pricing` / `/en/pricing`) — four tier cards (Solo / SMB / Enterprise / Firm), each with monthly + annual EGP prices, feature checklist, "Start trial" CTA.
4. **Downloads** at `/downloads` — desktop installer + LAN-client installer + Play Store badge + side-load APK link.
5. **Privacy policy** at `/privacy/android` — renders the vendor's policy (loaded from `resources/views/marketing/privacy/android.blade.php` per FR-006).

Run `php artisan dusk --filter=HomepageRenderTest` against `localhost:5050` to verify the SC-006 < 3 s p75 render budget on your machine.

### Signup

1. Click "ابدأ التجربة" on the Solo tier card → land on `/register`.
2. Fill email + password (≥ 12 chars) + organisation Arabic name + display name → submit.
3. Check the Mailpit UI at `http://localhost:8025` (or `storage/logs/laravel-*.log` if you skipped Mailpit) for the email-confirmation link.
4. Click the link → land on `/portal/dashboard` signed in.

### Trial behaviour (no portal involvement)

Per FR-029 / FR-030, the trial happens entirely client-side in the on-prem product. The portal dashboard shows "No active subscription yet — your installed DaftarX is in trial. To subscribe, click below." There's no Trial row in the database — the dashboard inspects whether the organisation has any non-`Cancelled` `Subscription` rows and renders the trial-banner UI when there are none.

### Convert trial → paid (manual activation)

1. From the dashboard, click "اشترك في الخطة" / "Subscribe to a plan".
2. Pick Solo + monthly → land on payment-method selection.
3. Pick "Bank transfer" (no real Paymob integration in dev) — the portal generates an Invoice with `status = "Pending"` and a vendor-only admin command can mark it `Paid`.
4. From a terminal:
   ```pwsh
   cd portal
   php artisan invoice:mark-paid INV-2026-00001
   ```
5. Back in the dashboard, the Subscription is now `Active`. Click "Activate paid licence" → paste your machine's HWID (any 16-char hex with dashes works in dev) → click Activate.
6. Click "Download token" → save the `.token` file.
7. Open the on-prem DaftarX (from feature 008) → drop the file at `%PROGRAMDATA%\DaftarX\license\license.token` → restart the service → confirm the dashboard shows the Solo edition with the correct expiry.

This is the SC-002 (signup → installer download) + SC-003 (licence activation) flow end-to-end.

---

## Running the tests

```pwsh
cd portal

# Unit tier — fast (no DB, no network)
php artisan test --testsuite=Unit

# Feature tier — includes contract tests + integration flows; uses SQLite in-memory by default
php artisan test --testsuite=Feature

# Browser tier (Dusk) — needs Chrome installed; runs against `php artisan serve`
php artisan dusk
```

The feature tests use the `RefreshDatabase` trait which migrates a fresh in-memory SQLite per test class, plus a fake Paymob HMAC verifier (always returns valid) and a fake Resend SMTP driver that captures emails for assertions. No external credentials needed.

---

## Common dev pitfalls

- **`vendor-keys.json` missing**: `LicenceSigningService` throws on startup of any signing route. Drop the file at the repo root from the vendor's password manager. Until then, the homepage + login + signup still work; only the paid-licence activation flow fails.
- **MySQL connection refused**: dev defaults to SQLite, but if you flip `.env` to MySQL and forget to start MySQL via Herd / XAMPP, you'll see `SQLSTATE[HY000] [2002]`. Either start MySQL or revert `.env`'s `DB_CONNECTION=sqlite`.
- **Email link 404s when clicked**: the dev environment's `APP_URL` is misconfigured. Check `.env` — should be `http://localhost:5050` (or `http://portal.test` if you use Herd).
- **Marketing page renders English even though browser is Arabic**: the locale cookie from a previous test is sticky. Clear cookies for `localhost:5050` and reload — the `LocaleResolver` middleware will pick up the `Accept-Language` header.
- **Vite asset 404s after a refresh**: the `npm run dev` watcher isn't running. Restart it.
- **`php artisan` fails with "Mix manifest not found"**: you're on a version without the Mix → Vite migration. Run `npm run build` once to generate `public/build/manifest.json`.
- **HTTPS error from Herd**: Herd's local cert isn't trusted by the OS yet. Run `herd trust` once.

---

## Backend dependencies that aren't yet wired

The P1 MVP intentionally STUBS these external integrations so the walkthrough works without real credentials:

- **Paymob** — production uses real HTTPS to Paymob's v3 API. Dev uses a fake adapter that always returns a valid HMAC + treats every `Pending` invoice as payable via the `invoice:mark-paid` Artisan command.
- **Resend SMTP** — production sends real transactional emails via `smtp.resend.com:587`. Dev defaults to Laravel's `log` mail driver (emails appear in `storage/logs/laravel-*.log`) OR Mailpit's local SMTP at `localhost:1025` if you started Mailpit.
- **Local disk storage** — both prod and dev use `Storage::disk('local')` writing to `storage/app/private/`. No Azurite / Azure equivalent needed.
- **Cloudflare cache purge** — production hits Cloudflare's purge API on deploy. Dev never caches (Cloudflare isn't in the loop locally).

These all swap to their real implementations via `.env` configuration on the production Hostinger box. Don't commit production credentials — they live in the production `.env` on Hostinger (NOT in git).

---

## Where things live

| Concern | File |
|---------|------|
| Marketing homepage | `portal/resources/views/marketing/home.blade.php` + `portal/app/Http/Controllers/Marketing/HomeController.php` |
| Pricing page | `portal/resources/views/marketing/pricing.blade.php` + `PricingController.php` |
| Downloads page | `portal/resources/views/marketing/downloads.blade.php` + `DownloadsController.php` |
| Privacy policy (Android) | `portal/resources/views/marketing/privacy/android.blade.php` |
| Portal dashboard | `portal/resources/views/portal/dashboard.blade.php` + `DashboardController.php` |
| Activate paid licence | `portal/resources/views/portal/licences/activate-paid.blade.php` + `LicenceController@activatePaid` |
| Transfer licence | `portal/resources/views/portal/licences/transfer.blade.php` + `LicenceController@transfer` |
| Licence signing service (sodium Ed25519) | `portal/app/Services/Licences/LicenceSigningService.php` |
| Eloquent models | `portal/app/Models/` (1:1 with data-model.md tables) |
| EF Core migrations equivalent | `portal/database/migrations/` |
| Composition root | `portal/app/Providers/AppServiceProvider.php` + `bootstrap/app.php` |
| Routes — browser | `portal/routes/web.php` |
| Routes — JSON API + webhook | `portal/routes/api.php` |
| Routes — auth (Breeze) | `portal/routes/auth.php` |
| Localization strings | `portal/lang/{ar,en}/*.php` |
| Cloudflare cache rules | `deploy/portal/cloudflare/cache-rules.json` |
| Hostinger deploy runbook | `deploy/portal/README.md` |

---

## Deploying to Hostinger

Not covered in this quickstart — see [`deploy/portal/README.md`](../../deploy/portal/README.md) for the full procedure. TL;DR:

1. GitHub Actions builds the project (Vite asset compile + `composer install --no-dev` simulated).
2. On push to `main`, the deploy workflow rsyncs `portal/` to Hostinger via SSH.
3. The workflow runs `composer install --no-dev`, `php artisan migrate --force`, `php artisan config:cache`, `php artisan view:cache` on the remote.
4. The workflow hits the Cloudflare purge API to invalidate stale marketing HTML.

The Hostinger document root is `public_html/`; the deploy creates a symlink `public_html → /home/<user>/portal/public` so Laravel's normal `public/` directory is what the web server sees.

---

## After P1

Once the P1 slice is running end-to-end:

1. Add Support Tickets (US4) — new tables + Blade pages, no impact on existing flow.
2. Add Multi-user invitations (US5) — extend OrganisationMembership + Resend templates.
3. Verify the privacy URL (US6) — at this point the P1 marketing page already serves it; just lock the route in a CI smoke test.
4. Polish — Cloudflare cache rules deployment, SEO structured-data, performance regression tests against the 3-second p75 budget.
