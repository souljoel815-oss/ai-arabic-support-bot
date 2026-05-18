# Implementation Plan: DaftarX Website + Customer Portal

**Branch**: `010-website-portal` | **Date**: 2026-05-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/010-website-portal/spec.md`

> **Stack switch (2026-05-18)**: This plan was originally written for ASP.NET Core 8 + Blazor Server + SQL Server + Docker (commit 6bf395e). The customer is hosting on a **Hostinger Shared Hosting** plan which does not support .NET 8 / Docker / WebSockets, so the architecture was rewritten for Laravel 11 + PHP 8.4 + MySQL 8 + Blade templates. The previous .NET design lives in git history (commits 6bf395e and earlier 63f8596) if a future VPS upgrade reverses the constraint. The spec.md user stories + functional requirements + clarifications are unchanged — only the implementation stack moved.

## Summary

Stand up `daftarx.app` as a **classic LAMP-style web app** (Linux + Nginx + MySQL + PHP via Hostinger) serving both surfaces from one Laravel 11 codebase: (1) a fast, SEO-friendly **public marketing site** (homepage, features, pricing, downloads, privacy policy, contact) rendered as Blade pages cached at the Cloudflare edge, and (2) an authenticated **customer portal** behind a `/portal/*` URL prefix where customers manage their commercial relationship — subscriptions, paid licence tokens (signed in-process via PHP's `sodium_crypto_sign_detached` against the existing Ed25519 `vendor-keys.json` keypair), billing, downloads, support tickets, and team-member memberships.

The 5 clarifications from `/speckit-clarify` Session 2026-05-18 sharpen the architecture in ways that map cleanly to the Laravel stack: trial happens entirely client-side in the on-prem product (FR-029/FR-030 — no portal endpoint at all), paid activation uses the existing manual HWID-copy flow (FR-031 — Laravel controller signs an envelope identical to the on-prem `LicenseEnvelope` format), portal identity is fully separate from the on-prem product's user store (FR-032 — Laravel Breeze owns its own `users` table on a separate MySQL database), tier changes are self-service with proration (FR-033 — Laravel handles all the math in `SubscriptionService`), and refunds are limited to a 7-day first-period window (FR-034 — computed inline in the Invoice Eloquent model).

The portal's commercial database is a brand-new MySQL schema (Customer Organisation / Subscription / Licence / Invoice / Ticket / Sales Lead / Audit Log) deliberately isolated from the existing on-prem `EgyptTax` accounting database — the two share git history but no runtime data, no SSO, no cross-system database link.

## Technical Context

**Language/Version**: PHP 8.4 (Hostinger's current latest; Laravel 11 requires PHP 8.2+, 8.4 works with no caveats). All code is server-rendered Blade — no SPA, no Inertia, no client-side framework. The few interactive bits (form validation, language switcher) use Alpine.js (already bundled with Laravel Breeze).

**Primary Dependencies**:
- *Framework*: Laravel 11 (LTS — security patches through Aug 2026, framework patches through Mar 2026). Standard `laravel/laravel` skeleton + `laravel/breeze` for auth scaffolding.
- *Auth*: Laravel Breeze (cookie session auth + email confirmation) + `pragmarx/google2fa-laravel` for TOTP MFA. PBKDF2 → bcrypt password hashing (Laravel default).
- *Reuse from monorepo*: PHP's built-in `sodium_crypto_sign_detached` produces Ed25519 signatures byte-compatible with the on-prem `EgyptTax.Web.Licensing.LicenseVerifier`. Same `vendor-keys.json` file is read by both; `LicenceSigningService` (PHP) and `LicenseVerifier` (C#) interoperate cleanly because Ed25519 is a standard with no implementation variance.
- *Payments*: Paymob hosted-checkout via direct HTTP integration (no SDK — `Illuminate\Http\Client` posts to `https://accept.paymob.com/api/...`). Single Egyptian aggregator covers all four required methods per FR-015 + research §3. Bank transfer is manual reconciliation with a vendor-only admin Artisan command.
- *Email*: Resend.com transactional API via SMTP (Laravel's `mail` driver supports SMTP natively — Resend exposes SMTP on `smtp.resend.com:587`). Templates rendered as Blade Mailables + MJML compiled to HTML during build.
- *PDF receipts*: `barryvdh/laravel-dompdf` generates the Arabic-RTL invoice PDFs required by FR-016. DOMPDF supports Arabic shaping via the built-in DejaVu Sans + an embedded Cairo font.
- *Frontend assets*: Tailwind CSS v3 + Vite (Laravel 11 default). No JavaScript framework — Alpine.js for the rare interactive widget (already in Breeze).

**Storage**:
- *Portal DB*: new MySQL 8 database `daftarx_portal` on the same Hostinger MySQL instance. Local dev override via SQLite file `database/database.sqlite` for zero-config startup. Holds the 8 commercial entities documented in `data-model.md`. Schema lives in `portal/database/migrations/`.
- *No overlap* with the existing on-prem `EgyptTax` accounting database — portal queries are commercial-only (subscriptions, licences, invoices, tickets), accounting data stays on the customer's own server.
- *File storage* for invoice PDFs + ticket attachments: Laravel `Storage::disk('local')` writing to `portal/storage/app/private/` on Hostinger (no S3 / Azure Blob in v1 — shared hosting includes plenty of disk). Path layout: `storage/app/private/invoices/{org-id}/{invoice-number}.pdf` and `storage/app/private/tickets/{org-id}/{ticket-id}/{attachment-id}.{ext}`. Migrating to Backblaze B2 or an S3-compatible bucket lands as a Phase 9 polish task if disk pressure becomes real.

**Testing**: Pest (Laravel 11's default test framework — more readable than raw PHPUnit) for unit + feature tests. Laravel's HTTP test helpers (`$this->get(...)`, `$this->post(...)`) handle contract tests inline — no separate WebApplicationFactory equivalent needed. Browser tests use Laravel Dusk (Selenium-based) for the marketing-surface end-to-end + portal critical flows (signup → trial-conversion → licence transfer). Dusk runs locally + in CI.

**Target Platform**: Hostinger Shared Hosting (Linux + Nginx + PHP-FPM, MySQL 8). Single-region — Hostinger's Egypt-nearby data centres (Frankfurt / Singapore — Hostinger doesn't have Cairo POPs but the latency target is met via Cloudflare edge cache). Cloudflare in front for CDN + DDoS + the marketing-page edge cache.

**Project Type**: Web application (single Laravel app, two-surface routing — Blade pages under `/*` for marketing, Blade pages under `/portal/*` behind `auth` middleware for the portal). No SPA, no separate API tier in v1.

**Performance Goals**: SC-006 — every marketing page renders in under 3 s at p75 from a Cairo broadband connection on a mid-range device. Achieved via (a) Cloudflare edge cache giving sub-100 ms TTFB after first hit, (b) Blade view caching (`php artisan view:cache` in CI), (c) `config:cache` + `route:cache` on every deploy, (d) eager-loading via Eloquent's `with()` to kill N+1s. SC-002 — signup → tier select → payment → token download → installer download in under 15 min. SC-003 — licence transfer in under 5 min.

**Constraints**:
- EGP-only billing in v1 (FR-015).
- Single-region hosting in v1 (Hostinger Shared = no multi-region).
- Privacy policy URL `daftarx.app/privacy/android` is a stable contract with the Android app's Play Store data-safety form (FR-006 + SC-004) — any URL change must be coordinated with feature 009's release.
- Trial state is OWNED by the on-prem product, NEVER tracked by the portal (FR-030).
- Portal identity is fully separate from on-prem product identity (FR-032) — no SSO, no cross-system password sync.
- **Hostinger Shared Hosting limitations** that shape architecture:
  - ❌ No long-running processes — background work happens via Laravel queue (`database` driver) + cron entry hitting `php artisan schedule:run` every minute. NO Hangfire equivalent.
  - ❌ No WebSockets — no Blazor Server / real-time push. Everything is request/response.
  - ❌ No Docker / no system-level service install — pure PHP + composer.
  - ⚠️ PHP execution time limit ~30-120s per request — long uploads + PDF generation chunked into queued jobs.
  - ⚠️ Cron resolution = 1 minute (Hostinger Premium) or 15 minutes (Hostinger Single — won't work for `schedule:run` which needs per-minute resolution).

**Scale/Scope**: Year-1 target is 200 paying customers with ≤ 10 Team Members each → ≤ 2,000 TeamMember rows, ≤ 2,000 Subscriptions, ≤ 4,000 Licences (each Subscription has 1-3 licences for the LAN + multi-device cases), ≤ 24,000 Invoices/year (monthly cadence dominates), ≤ 6,000 Support Tickets/year. Single MySQL database with Hostinger's daily backups is enough. Surface is ~15 marketing pages + ~12 portal screens (Dashboard, Licences, Subscription, Billing, Downloads, Support, Settings, Members, Security, Activate-paid, Transfer-licence, Account-deletion).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Spec-First Development** | ✅ Pass | `spec.md` complete with 34 functional requirements, 6 prioritised user stories, 9 measurable success criteria, 8 key entities, 0 `[NEEDS CLARIFICATION]` markers. All ambiguity resolved into the Clarifications section + Assumptions. Stack switch is an implementation concern, not a spec concern — spec wording is framework-agnostic. |
| **II. Plan Before Code** | ✅ Pass | This plan documents language, dependencies, storage, testing, target platform, project structure, and the explicit reuse contract (the Ed25519 signing keypair shared with on-prem). No code may be written until tasks are regenerated from this plan. |
| **III. Test-First Discipline** | ✅ Pass (commitment) | Contract tests for the 5 portal endpoints (signup, activate-paid-licence, transfer-licence, payment-webhook, invite-member) and integration tests for every cross-boundary flow (Paymob hosted-checkout round-trip, Resend SMTP round-trip, in-process licence-signing call, local-disk attachment upload, Cloudflare cache invalidation hook) MUST be written and observed failing before their production code, recorded in `tasks.md` per `/speckit-tasks`. Laravel + Pest make this easy — every test is a top-level PHP file under `tests/`. |
| **IV. Simplicity & YAGNI** | ✅ Pass | The Laravel rewrite reduces complexity vs the .NET attempt: no Blazor Server (= no WebSocket lifecycle), no two-project split (Application vs Infrastructure — Laravel uses a flat `app/` directory), no Hangfire (= queue is just database rows), no Docker (= deploy is git pull + composer install). The clarifications already simplified the domain (no SSO per FR-032, no new activation endpoint per FR-031, no portal-side trial per FR-030, no mid-period refund per FR-034). No Complexity Tracking entries needed. |
| **V. Incremental, Independently Testable Delivery** | ✅ Pass | The 6 user stories decompose into 3 P1 (Marketing, Licence self-service, Signup→Pay→Download) + 3 P2 (Support, Multi-user, Privacy URL). P1 stories together are the MVP that lets the vendor start taking real money. P2 stories layer orthogonally — none of them blocks any of the others. Same as the .NET design — the constitution check passes for the same reasons. |

**Re-check after Phase 1 design**: see [Post-Design Constitution Check](#post-design-constitution-check) below.

## Project Structure

### Documentation (this feature)

```text
specs/010-website-portal/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output — Laravel-stack decisions
├── data-model.md        # Phase 1 output — Eloquent models + migrations
├── quickstart.md        # Phase 1 output — composer + artisan workflow
├── contracts/           # Phase 1 output — endpoint contracts (URL + JSON shapes only)
│   ├── signup.md                # POST /api/v1/portal/signup
│   ├── activate-paid-licence.md # POST /api/v1/portal/licences/activate
│   ├── transfer-licence.md      # POST /api/v1/portal/licences/{id}/transfer
│   ├── payment-webhook.md       # POST /api/v1/portal/payments/webhook (Paymob callback)
│   └── invite-member.md         # POST /api/v1/portal/organisations/{id}/invitations
├── checklists/
│   └── requirements.md  # spec-quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks command - regenerated for Laravel)
```

### Source Code (repository root)

```text
portal/                                         # NEW — Laravel 11 app root
├── app/
│   ├── Http/
│   │   ├── Controllers/
│   │   │   ├── Marketing/                      # HomeController, PricingController, etc.
│   │   │   ├── Portal/                         # DashboardController, LicenceController, etc.
│   │   │   └── Api/
│   │   │       └── PaymentWebhookController.php
│   │   ├── Middleware/
│   │   │   ├── OrganisationScope.php           # FR-021 — rejects cross-org URL tampering
│   │   │   ├── OrganisationMfaPolicy.php       # FR-011 / T155 — enforce TOTP for Owners
│   │   │   └── LocaleResolver.php              # /ar/* + /en/* prefix routing
│   │   └── Requests/                           # FormRequest validators per endpoint
│   ├── Models/                                 # Eloquent models — 1:1 with data-model.md entities
│   │   ├── CustomerOrganisation.php
│   │   ├── TeamMember.php                      # extends Illuminate\Foundation\Auth\User
│   │   ├── OrganisationMembership.php
│   │   ├── Subscription.php
│   │   ├── Licence.php
│   │   ├── Invoice.php
│   │   ├── SalesLead.php
│   │   ├── SupportTicket.php
│   │   ├── SupportTicketReply.php
│   │   ├── SupportTicketAttachment.php
│   │   ├── AuditLogEntry.php
│   │   ├── Invitation.php                      # US5 — token hash + 7-day expiry
│   │   └── DownloadArtifactVersion.php         # FR-017 — prior-3 versions
│   ├── Services/
│   │   ├── Licences/
│   │   │   ├── LicenceSigningService.php       # sodium_crypto_sign_detached wrap
│   │   │   ├── HwidValidator.php
│   │   │   └── LicencePayloadCanonicalizer.php # JSON byte-compatible with on-prem
│   │   ├── Subscriptions/
│   │   │   ├── ConvertTrialToPaidService.php   # FR-029 single-tap
│   │   │   ├── UpgradeTierService.php          # FR-033 instant proration
│   │   │   ├── ScheduleDowngradeService.php    # FR-033 at-renewal
│   │   │   ├── RefundFirstPeriodService.php    # FR-034 7-day window
│   │   │   └── CancelSubscriptionService.php   # FR-013 Cancel action
│   │   ├── Payments/
│   │   │   ├── PaymobAdapter.php               # raw HTTPS adapter (4 methods + card)
│   │   │   ├── PaymentWebhookHandler.php       # HMAC verify + idempotent state machine
│   │   │   └── ManualBankTransferReconciler.php
│   │   ├── Support/
│   │   │   ├── CreateTicketService.php         # FR-018 — 3 attachments × 5 MB
│   │   │   └── SlaCalculator.php               # FR-019 — tier-based SLA
│   │   ├── Organisations/
│   │   │   ├── InviteMemberService.php         # FR-020 — 7-day token, hashed at rest
│   │   │   ├── AcceptInvitationService.php
│   │   │   ├── RemoveMemberService.php         # FR-022 — 5-min session kill
│   │   │   └── DeleteAccountService.php        # FR-024 — 30-day soft-delete
│   │   ├── Audit/
│   │   │   └── AuditLogWriter.php              # FR-023 — forbidden-substring guard
│   │   └── Email/
│   │       └── ResendMailer.php                # wraps Laravel's Mail::send with retry
│   ├── Mail/                                   # Mailable classes
│   │   ├── SignupConfirmation.php
│   │   ├── PaymentReceipt.php
│   │   ├── OrganisationInvitation.php
│   │   ├── RefundConfirmation.php
│   │   └── ...
│   ├── Console/
│   │   ├── Commands/
│   │   │   ├── MarkInvoicePaid.php             # vendor admin CLI per quickstart.md
│   │   │   ├── PurgeSoftDeletedAccounts.php    # FR-024 nightly job
│   │   │   ├── ComputeSupportSlaRollup.php     # SC-005 / T156
│   │   │   └── ProcessRenewals.php             # renewal job per Subscription lifecycle
│   │   └── Kernel.php                          # registers scheduled commands
│   └── Providers/
│       ├── AppServiceProvider.php
│       └── AuthServiceProvider.php             # role-based gates
├── database/
│   ├── migrations/                             # 1 file per table change, ordered by timestamp
│   ├── seeders/                                # test fixtures for dev
│   └── factories/                              # Pest factories for tests
├── lang/                                       # FR-008 — bilingual
│   ├── ar/                                     # ar/messages.php + ar/marketing.php + ...
│   └── en/
├── resources/
│   ├── views/
│   │   ├── layouts/
│   │   │   ├── marketing.blade.php             # public surface — header + footer
│   │   │   └── portal.blade.php                # authenticated surface — sidebar nav
│   │   ├── marketing/
│   │   │   ├── home.blade.php
│   │   │   ├── features.blade.php
│   │   │   ├── pricing.blade.php
│   │   │   ├── downloads.blade.php
│   │   │   ├── about.blade.php
│   │   │   ├── contact.blade.php
│   │   │   ├── terms.blade.php
│   │   │   ├── refund.blade.php
│   │   │   └── privacy/
│   │   │       ├── index.blade.php
│   │   │       └── android.blade.php           # FR-006 — stable URL
│   │   ├── portal/
│   │   │   ├── dashboard.blade.php
│   │   │   ├── licences/{list,activate-paid,transfer}.blade.php
│   │   │   ├── subscription/{index,upgrade,downgrade,cancel,convert-trial}.blade.php
│   │   │   ├── billing/{invoices,payment-methods,refund}.blade.php
│   │   │   ├── downloads.blade.php
│   │   │   ├── support/{list,new,detail}.blade.php
│   │   │   ├── organisation/{members,audit-log,security-policy}.blade.php
│   │   │   └── account/{security,delete}.blade.php
│   │   ├── auth/                               # Breeze-generated (login, register, etc.)
│   │   └── components/                         # shared Blade components
│   │       ├── language-switcher.blade.php
│   │       ├── footer.blade.php
│   │       └── tier-card.blade.php
│   ├── css/app.css                             # Tailwind input
│   ├── js/app.js                               # Alpine + Vite bootstrap
│   └── mail/                                   # MJML→HTML compiled templates
├── routes/
│   ├── web.php                                 # marketing + portal browser routes
│   ├── api.php                                 # /api/v1/portal/* — JSON endpoints + webhook
│   └── auth.php                                # Breeze-generated
├── public/                                     # web server document root
├── tests/
│   ├── Feature/
│   │   ├── Contracts/                          # 5 contract test files per contracts/*.md
│   │   ├── Flows/                              # cross-boundary integration tests
│   │   ├── Marketing/                          # Dusk e2e for marketing pages
│   │   └── Portal/                             # Dusk e2e for portal flows
│   └── Unit/                                   # pure unit tests (no DB, no HTTP)
├── composer.json
├── package.json                                # Vite + Tailwind
├── vite.config.js
├── tailwind.config.js
├── .env.example                                # committed; .env gitignored
└── artisan                                     # Laravel CLI

deploy/portal/                                  # NEW — Hostinger deploy runbook
├── README.md                                   # deploy procedure, secret rotation, incident response
├── nginx.conf.snippet                          # only if Hostinger needs custom config (rare on shared)
├── .htaccess                                   # for Apache plans — Laravel's default works
└── deploy.sh                                   # git pull + composer install + artisan migrate runner
```

**Structure Decision**: Single `portal/` directory at repo root containing a self-contained Laravel 11 project. Laravel's standard structure (`app/Http/Controllers`, `app/Models`, `app/Services`, `resources/views`) is the canonical layout — no further sub-projects, no shared library DLLs to manage. The portal sits alongside the on-prem `src/EgyptTax.*` .NET projects in the same git repo but the two systems share NO runtime code — only the `vendor-keys.json` Ed25519 keypair (which is itself outside the repo, mounted as a secret on the production server). This isolation keeps the portal deployable independently of the on-prem product and prevents accidental coupling between commercial relationship data (portal) and customer accounting data (on-prem). All FR-032 separation requirements are enforced at the database boundary: the portal's MySQL database has no foreign key, no view, and no network reachability to the customers' own on-prem `EgyptTax` databases.

## Complexity Tracking

> No constitutional violations to record. The Laravel rewrite + the original clarifications combine to make this the simplest viable architecture:
>
> - Laravel 11 → one framework, one directory, one composer.json. No project/solution multi-targeting.
> - PHP's built-in `sodium` extension → Ed25519 signing with zero external crypto library (BouncyCastle replaced).
> - MySQL + database-driver queues → no Redis dependency on shared hosting.
> - Blade + Alpine.js → no SPA build pipeline, no client-side router, no Blazor lifecycle.
> - Cloudflare cache → no manual CDN config beyond `.htaccess` cache headers.
>
> The five FR-clarifications continue to reduce complexity (no portal-side trial / no new activation endpoint / no SSO / no mid-period refund / no immediate downgrade refund) — the Laravel implementation honours each by simply not implementing the feature, the same way the .NET attempt did.

## Post-Design Constitution Check

*Re-evaluated after Phase 1 design completed (see `data-model.md`, `contracts/`, `quickstart.md`).*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Spec-First Development** | ✅ Pass | Phase 1 artifacts cite `spec.md` (34 FRs) for every Eloquent model, contract, and quickstart step. No new requirements introduced during design. |
| **II. Plan Before Code** | ✅ Pass | Plan, data-model, and contracts are the complete pre-code package. `/speckit-tasks` consumes these next to regenerate `tasks.md` against the Laravel stack. |
| **III. Test-First Discipline** | ✅ Pass | Each contract in `contracts/` enumerates explicit assertions that become failing Pest tests before the endpoint's production code lands. Feature test directory pre-allocated under `portal/tests/Feature/Contracts/` with one file per contract. |
| **IV. Simplicity & YAGNI** | ✅ Pass | Data model is 13 Eloquent models — the 8 from spec.md Key Entities plus the 5 supporting types added in the audit (Invitation, SupportTicketReply, SupportTicketAttachment, DownloadArtifactVersion, plus the implicit OrganisationMembership join). All map 1:1 to data-model.md tables. One new MySQL database, one Laravel app, one in-process licence signer. No abstractions for "future iOS portal" or "future multi-currency". |
| **V. Incremental, Independently Testable Delivery** | ✅ Pass | Quickstart documents the P1 MVP slice (marketing pages live + signup + trial-conversion + manual paid activation + downloads) end-to-end without any P2 dependencies. The P2 stories (Support, Multi-user, Privacy URL stability) add tables + screens that don't disturb the P1 surface. Identical to the .NET design's incremental story split — the stack switch doesn't change the slicing. |
