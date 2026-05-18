---

description: "Task list for 010-website-portal feature implementation (Laravel 11 stack)"
---

# Tasks: DaftarX Website + Customer Portal

**Input**: Design documents from `/specs/010-website-portal/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)
**Stack**: Laravel 11 + PHP 8.4 + MySQL 8 + Blade + Tailwind v3 + Vite (rewritten 2026-05-18 from the .NET design; the original 156 tasks lived at commit 6bf395e — most have direct Laravel equivalents).

**Tests**: Test tasks are INCLUDED. Constitution III (Test-First Discipline) is non-negotiable and the plan commits to contract tests for every new portal endpoint plus integration tests for every cross-boundary flow (Paymob round-trip, Resend SMTP dispatch, in-process licence-signing, local-disk attachment upload, Cloudflare cache invalidation). Tests MUST be written and observed failing before their production code lands.

**Organization**: Tasks are grouped by user story (US1–US6) to enable independent implementation and testing. P1 stories (US1, US2, US3) together form the shippable MVP per `plan.md`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US6); omitted for Setup, Foundational, and Polish phases
- All file paths are project-relative

## Path Conventions (from plan.md)

- **Laravel app root**: `portal/`
- **Controllers**: `portal/app/Http/Controllers/`
- **Models**: `portal/app/Models/`
- **Services**: `portal/app/Services/`
- **Migrations**: `portal/database/migrations/`
- **Blade views**: `portal/resources/views/`
- **Routes**: `portal/routes/{web.php,api.php,auth.php}`
- **Translations**: `portal/lang/{ar,en}/*.php`
- **Tests**: `portal/tests/Feature/` (integration + contract + Dusk e2e) + `portal/tests/Unit/`
- **Deploy artifacts**: `deploy/portal/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold the Laravel 11 project + install dependencies + configure dev env + CI pipeline. No business code yet.

- [X] T001 Run `composer create-project laravel/laravel:^11.0 portal --prefer-dist` to scaffold the Laravel app at `portal/`
- [X] T002 Run `composer require laravel/breeze --dev` then `php artisan breeze:install blade --dark` for cookie-auth + email-confirm scaffolding
- [X] T003 [P] Run `composer require pragmarx/google2fa-laravel barryvdh/laravel-dompdf bacon/bacon-qr-code` for TOTP MFA + PDF + QR-code generation
- [X] T004 [P] Run `composer require spatie/laravel-permission` (optional but recommended for the role-based gates in US5)
- [X] T005 [P] Copy `portal/.env.example` to `portal/.env` + run `php artisan key:generate` + configure `DB_CONNECTION=sqlite` for dev with `database/database.sqlite` touched
- [X] T006 [P] Run `npm install` then verify `npm run build` succeeds (Vite + Tailwind already wired by Breeze)
- [X] T007 [P] Configure `portal/config/database.php` MySQL connection to use `daftarx_portal` schema in production; dev defaults to SQLite via `.env`
- [X] T008 [P] Configure `portal/config/mail.php` SMTP driver pointing at Resend (`smtp.resend.com:587`) for production; default to `log` driver in `.env` for dev
- [X] T009 [P] Create `portal/config/paymob.php` with `api_key`, `hmac_secret`, integration IDs per payment method (read from env)
- [X] T010 [P] Create `portal/config/licence.php` with `vendor_keys_path` (default `base_path('../vendor-keys.json')`)
- [X] T011 [P] Configure `portal/config/queue.php` to use `database` driver in production; `sync` driver in dev
- [X] T012 [P] Create `deploy/portal/README.md` ops runbook (Hostinger deploy procedure, secret rotation, incident response)
- [X] T013 [P] Create `deploy/portal/cloudflare/cache-rules.json` (marketing routes `max-age=3600`; portal/api/auth routes `no-store`) per research §9
- [X] T014 [P] Create `.github/workflows/portal-build.yml` CI workflow with three stages (build+unit, integration, deploy-on-main via rsync over SSH) per research §16
- [X] T015 [P] Configure `portal/tailwind.config.js` to scan `resources/views/**/*.blade.php` + add brand colors (gold + charcoal) mirroring the on-prem product
- [X] T016 [P] Pin `composer.json` PHP requirement to `^8.2` (so Hostinger PHP 8.2+ all work) and Laravel to `^11.0`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting infrastructure every user story needs — TeamMember + CustomerOrganisation + OrganisationMembership entities + AuditLogEntry + licence-signing service + audit-log writer + locale routing + cookie auth + Laravel logging. No user-story work can start until this phase is complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T017 Create initial migration `2026_05_18_000001_create_customer_organisations_table.php` at `portal/database/migrations/` per data-model.md §1
- [X] T018 [P] Replace Breeze's default `users` migration with `2026_05_18_000002_create_team_members_table.php` (rename table to `team_members`, switch primary key to `char(36)` UUID, add `locale_preference` + `display_name` + `mfa_secret` + `mfa_enabled_at` + `last_login_at` + `soft_deleted_at` columns per data-model.md §2 + FR-032)
- [X] T019 [P] Create migration `2026_05_18_000003_create_organisation_memberships_table.php` per data-model.md §3 join table (include the filtered-unique-on-active index trick using MySQL 8 generated columns)
- [X] T020 [P] Create migration `2026_05_18_000004_create_audit_log_entries_table.php` per data-model.md §9
- [X] T021 Create `portal/app/Models/TeamMember.php` extending `Illuminate\Foundation\Auth\User` with `HasUuids` trait, casts (`encrypted` for `mfa_secret`, `datetime` for the timestamp columns), and `belongsToMany(CustomerOrganisation::class)->using(OrganisationMembership::class)` relationship
- [X] T022 [P] Create `portal/app/Models/CustomerOrganisation.php` with `HasUuids` trait + relationships per data-model.md §1
- [X] T023 [P] Create `portal/app/Models/OrganisationMembership.php` as a `Pivot` model with `HasUuids` trait per data-model.md §3
- [X] T024 [P] Create `portal/app/Models/AuditLogEntry.php` with `HasUuids` trait + casts per data-model.md §9
- [X] T025 [P] Update `portal/config/auth.php` to point at `App\Models\TeamMember::class` (Breeze defaults to `App\Models\User`)
- [X] T026 Apply migrations locally via `php artisan migrate` to confirm the schema composes correctly on both SQLite (dev) and MySQL (production check)
- [X] T027 [P] Create `portal/app/Services/Audit/AuditLogWriter.php` per FR-023 + FR-028 — every state change writes one row, scoped to `customer_organisation_id`, payload NEVER contains tokens/PII per data-model.md §9; rejects rows whose payload contains forbidden substrings (`password`, `secret`, `token`, etc.)
- [X] T028 [P] Create `portal/app/Services/Licences/LicencePayloadCanonicalizer.php` + `portal/app/Services/Licences/LicenceSigningService.php` using PHP's `sodium_crypto_sign_detached()` to produce envelopes byte-compatible with the on-prem `LicenseVerifier` per research §10; reads `vendor-keys.json` from the path in `config/licence.php`
- [X] T029 [P] Create `portal/app/Http/Middleware/OrganisationScope.php` rejecting URL-tampering attempts that try to read org-B data while signed in to org-A (FR-021); registered in `bootstrap/app.php` for the `portal` route group only
- [X] T030 [P] Create `portal/app/Http/Middleware/LocaleResolver.php` reading the first URL path segment (`/ar/*` or `/en/*`) + setting `App::setLocale()`; register globally; create base `lang/ar/messages.php` + `lang/en/messages.php` with FR-008 strings
- [X] T031 [P] Create `portal/app/Services/Email/ResendMailer.php` wrapping `Mail::send` with a Polly-style retry on transient failures (Laravel doesn't have Polly — use a simple while-loop with exponential backoff) per research §4 + T095
- [X] T032 [P] Configure Laravel's log channel in `portal/config/logging.php` for daily rotation (30-day retention) + JSON formatter for prod per research §15
- [X] T033 [P] Create base marketing layout at `portal/resources/views/layouts/marketing.blade.php` (gold + charcoal palette mirroring the on-prem product + the Android app)
- [X] T034 [P] Create authenticated portal layout at `portal/resources/views/layouts/portal.blade.php` (auth required via the `auth` middleware in routes; sidebar nav with the 8 portal sections per plan.md)
- [X] T035 Configure two-surface routing in `portal/routes/web.php`: marketing routes under `/` (and `/ar/*` + `/en/*`), portal routes under `/portal/*` behind the `auth` middleware group
- [X] T036 [P] Add a `Route::get('/health', ...)` in `web.php` returning JSON with DB connectivity status (uses `DB::connection()->getPdo()` smoke check)

**Checkpoint**: Foundation ready — Eloquent models compose, migrations apply, Breeze auth flow signs users in, licence-signing service is callable, audit-log writer works, locale routing serves bilingual pages. User stories can begin.

---

## Phase 3: User Story 1 - Prospect discovers DaftarX (Priority: P1) 🎯 part of MVP

**Goal**: Marketing surface live in both Arabic (RTL default) and English. Prospect can navigate Homepage → Pricing → "Start trial" CTA in ≤ 3 clicks; the four tiers (Solo, SMB, Enterprise, Firm) display with EGP prices and a feature checklist; every marketing page renders < 3 s p75 from a Cairo broadband connection.

**Independent Test**: From a clean browser (no cookies), open `http://localhost:5050`, navigate Homepage → Pricing → click any tier's "Start trial" CTA. Confirm: language defaults to ar-EG RTL, four tiers visible with EGP prices + feature checklists, signup form reachable. Run `php artisan dusk --filter=HomepageRenderTest` to verify SC-006 < 3 s p75 budget.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T037 [P] [US1] Dusk e2e at `portal/tests/Browser/Marketing/HomepageRenderTest.php` asserting SC-006 (< 3 s p75 first render for `/`, `/ar`, `/en`)
- [ ] T038 [P] [US1] Dusk e2e at `portal/tests/Browser/Marketing/PricingNavigationTest.php` asserting SC-001 (Homepage → Pricing → trial CTA in ≤ 3 clicks)
- [ ] T039 [P] [US1] Dusk e2e at `portal/tests/Browser/Marketing/LanguageSwitcherTest.php` asserting FR-008 (locale toggle preserves current page + scroll position)

### Implementation for User Story 1

- [ ] T040 [P] [US1] Create `portal/app/Http/Controllers/Marketing/HomeController.php` + `portal/resources/views/marketing/home.blade.php` (hero + value props + three primary CTAs Start trial / See pricing / Download) per FR-001
- [ ] T041 [P] [US1] Create `FeaturesController.php` + `marketing/features.blade.php` enumerating the 27-feature catalog from the on-prem `Feature` enum organised by category per FR-002 (the feature list is hardcoded in PHP; no DB table needed)
- [ ] T042 [P] [US1] Create `PricingController.php` + `marketing/pricing.blade.php` with 4 tier cards + monthly/annual EGP prices + feature checklist + comparison matrix per FR-003
- [ ] T043 [P] [US1] Create `DownloadsController.php` + `marketing/downloads.blade.php` listing DaftarX-Setup.msi + DaftarX-Client-Setup.msi (from feature 008) + Google Play badge + side-load APK link (from feature 009) with file size + version + checksum per FR-004
- [ ] T044 [P] [US1] Create `AboutController.php` + `marketing/about.blade.php` per FR-005
- [ ] T045 [P] [US1] Create `ContactController.php` + `marketing/contact.blade.php` with form posting to `CreateSalesLeadAction` stub (full persistence lands in US3 with the SalesLead entity); WhatsApp + phone + email channels rendered statically
- [ ] T046 [P] [US1] Create `PrivacyController.php` + `marketing/privacy/index.blade.php` (vendor-wide privacy policy) per FR-006
- [ ] T047 [P] [US1] Create `TermsController.php` + `marketing/terms.blade.php` + `RefundController.php` + `marketing/refund.blade.php` per FR-007
- [ ] T048 [P] [US1] Author Tailwind CSS marketing styles at `portal/resources/css/app.css` using CSS logical properties (`margin-inline-start` etc.) so the same stylesheet serves RTL + LTR per research §8
- [ ] T049 [P] [US1] Create `_language-switcher.blade.php` partial at `portal/resources/views/components/language-switcher.blade.php` that toggles between `/ar/...` and `/en/...` preserving the current page path
- [ ] T050 [P] [US1] Create translation files for every marketing page: `lang/ar/marketing.php` + `lang/en/marketing.php` with one nested key per page (`home.hero_title`, `pricing.solo_tier_name`, etc.)
- [ ] T051 [P] [US1] Create `_footer.blade.php` partial linking Terms, Refund, Privacy, About from every marketing page per FR-007
- [ ] T052 [US1] Wire locale-aware routing in `portal/routes/web.php` so `/ar/pricing` and `/en/pricing` both resolve to `PricingController@show` with the right culture (uses `Route::prefix('{locale}')` with a route constraint + a `LocaleResolver` middleware on the group)
- [ ] T053 [US1] Add structured data (JSON-LD `Organization` + `Product` + `Offer` schemas), `sitemap.xml` generation via a controller, and `robots.txt` per FR-009 SEO requirement
- [ ] T054 [P] [US1] Add Open Graph + Twitter Card meta tags to every marketing page for social sharing
- [ ] T055 [P] [US1] Add `<link rel="alternate" hreflang>` tags for both locales on every marketing page (Google indexes ar + en as distinct pages)

**Checkpoint**: Marketing surface fully functional and SEO-indexable. US1 is independently shippable as a "coming soon — sign up to be notified" landing page even before any portal work lands.

---

## Phase 4: User Story 2 - Licence self-service for HWID transfer (Priority: P1)

**Goal**: A customer with an active Solo licence can log in to the portal, open License Management, transfer their licence to a new hardware id, and download the new signed token — all in under 5 minutes (SC-003), no support ticket required.

**Independent Test**: Sign in as a customer with one active licence. From License Management, paste a different mocked HWID into the Transfer dialog, complete the flow. Confirm: new token downloads, old HWID's row marked `retired` with `retired_at` populated, two audit-log rows written (`licence.retired` + `licence.activated` with cross-references in payloads).

### Tests for User Story 2

- [ ] T056 [P] [US2] Contract test at `portal/tests/Feature/Contracts/ActivatePaidLicenceEndpointTest.php` — 10 assertions per [contracts/activate-paid-licence.md](./contracts/activate-paid-licence.md)
- [ ] T057 [P] [US2] Contract test at `portal/tests/Feature/Contracts/TransferLicenceEndpointTest.php` — 10 assertions per [contracts/transfer-licence.md](./contracts/transfer-licence.md) including the concurrent-transfer optimistic-concurrency case
- [ ] T058 [P] [US2] Dusk e2e at `portal/tests/Browser/Portal/LicenceTransferFlowTest.php` asserting SC-003 (transfer flow < 5 min wall-clock)
- [ ] T059 [P] [US2] Unit test for `HwidValidator` at `portal/tests/Unit/Licences/HwidValidatorTest.php` (format check + cross-customer collision)

### Implementation for User Story 2

- [ ] T060 [P] [US2] Create migration `create_subscriptions_table.php` + `Subscription` Eloquent model per data-model.md §4 (NO `Trial` status per FR-030)
- [ ] T061 [P] [US2] Create migration `create_licences_table.php` + `Licence` Eloquent model per data-model.md §5 — composite unique index on `hwid` filtered on `retired_at IS NULL`
- [ ] T062 [P] [US2] Implement `portal/app/Services/Licences/HwidValidator.php` — regex `^[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}$` + cross-customer collision query
- [ ] T063 [P] [US2] Implement `portal/app/Services/Licences/ActivatePaidLicenceService.php` per [contracts/activate-paid-licence.md](./contracts/activate-paid-licence.md) Behaviour section
- [ ] T064 [P] [US2] Implement `portal/app/Services/Licences/TransferLicenceService.php` per [contracts/transfer-licence.md](./contracts/transfer-licence.md) Behaviour section (FR-014: retire + reissue + 2 audit rows wrapped in `DB::transaction`)
- [ ] T065 [US2] Map `POST /api/v1/portal/licences/activate` + `POST /api/v1/portal/licences/{id}/transfer` + `GET /api/v1/portal/licences/{id}/token.token` routes in `portal/routes/api.php`; create `LicenceController.php` thin wrapper
- [ ] T066 [P] [US2] Create `portal/resources/views/portal/licences/list.blade.php` showing active + retired licences with action buttons (Download token, Transfer, Renew). When the Subscription's `tier ∈ {Enterprise, Firm}`, render the "Priority support" badge inline next to the tier name (spec US2 AS#3 — surfaces the Feature.PrioritySupport entitlement at the licence-management surface, not just on the Support form)
- [ ] T067 [P] [US2] Create `portal/resources/views/portal/licences/activate-paid.blade.php` (paste HWID → submit → download token)
- [ ] T068 [P] [US2] Create `portal/resources/views/portal/licences/transfer.blade.php` (confirm dialog → submit → download new token)
- [ ] T069 [P] [US2] Translation files (ar + en) at `portal/lang/{ar,en}/licences.php` for all US2 UI strings

**Checkpoint**: A customer can complete the SC-003 5-minute licence transfer end-to-end. US2 is independently shippable but depends on having paid customers (US3 creates them); for v1 testing the team seeds test customers manually via `database/seeders/`.

---

## Phase 5: User Story 3 - Signup → Pay → Download (Priority: P1) 🎯 MVP

**Goal**: A new prospect clicks "Start trial" from the marketing site, signs up with email + password (no payment up-front per FR-029), the trial runs entirely client-side in the on-prem product (FR-030), and when they convert to paid they pick a tier + payment method (Fawry / InstaPay / Vodafone Cash / card via Paymob), receive a PDF invoice, and reach the downloads page with their licence token ready to activate — all under 15 minutes (SC-002).

**Independent Test**: From homepage click "Start trial" → complete signup with a real email → confirm email link → enter portal → click "Subscribe to keep going" → pick Solo + Monthly → choose Fawry → enter mock reference code in dev → portal receives webhook → invoice marked Paid → land on downloads page → activate licence per US2 flow.

### Tests for User Story 3

- [ ] T070 [P] [US3] Contract test at `portal/tests/Feature/Contracts/SignupEndpointTest.php` — 10 assertions per [contracts/signup.md](./contracts/signup.md)
- [ ] T071 [P] [US3] Contract test at `portal/tests/Feature/Contracts/PaymentWebhookEndpointTest.php` — 10 assertions per [contracts/payment-webhook.md](./contracts/payment-webhook.md) including HMAC verification + idempotency
- [ ] T072 [P] [US3] Integration test at `portal/tests/Feature/Flows/TrialConversionFlowTest.php` (FR-029 single-tap convert with no re-entered fields)
- [ ] T073 [P] [US3] Integration test at `portal/tests/Feature/Flows/TierUpgradeProrationTest.php` (FR-033 instant proration on upgrade)
- [ ] T074 [P] [US3] Integration test at `portal/tests/Feature/Flows/TierDowngradeAtRenewalTest.php` (FR-033 downgrade scheduled at next renewal — NOT mid-period)
- [ ] T075 [P] [US3] Integration test at `portal/tests/Feature/Flows/RefundWindowTest.php` (FR-034 7-day first-period only)
- [ ] T076 [P] [US3] Dusk e2e at `portal/tests/Browser/Portal/SignupToDownloadFlowTest.php` asserting SC-002 (full flow < 15 min wall-clock)
- [ ] T077 [P] [US3] Integration test at `portal/tests/Feature/Flows/InvoicePdfGenerationTest.php` (DOMPDF renders Arabic RTL correctly with bidi text + Egyptian VAT line per FR-016)

### Implementation for User Story 3

- [ ] T078 [P] [US3] Create migration `create_invoices_table.php` + `Invoice` Eloquent model per data-model.md §6 — composite unique on `(customer_organisation_id, invoice_number)` + filtered unique on `paymob_transaction_id` for webhook idempotency; `getRefundEligibilityAttribute()` accessor implements FR-034
- [ ] T079 [P] [US3] Create migration `create_sales_leads_table.php` + `SalesLead` Eloquent model per data-model.md §7
- [ ] T080 [P] [US3] Implement `portal/app/Services/Sales/CreateSalesLeadService.php` (called by US1 Contact page's form post)
- [ ] T081 [P] [US3] Implement `portal/app/Services/Identity/SignupService.php` per [contracts/signup.md](./contracts/signup.md) Behaviour section
- [ ] T082 [US3] Map `POST /api/v1/portal/signup` + email-confirmation route in `portal/routes/api.php` + `portal/routes/auth.php` (Breeze already provides `/register` + `/verify-email` — extend rather than replace)
- [ ] T083 [P] [US3] Implement `portal/app/Services/Subscriptions/CreateTrialIntentService.php` — per FR-030 this creates ONLY an analytics-intent record, NEVER a Subscription row with `status = 'Trial'`
- [ ] T084 [P] [US3] Implement `portal/app/Services/Subscriptions/ConvertTrialToPaidService.php` (FR-029 single-tap with no re-entered fields)
- [ ] T085 [P] [US3] Implement `portal/app/Services/Subscriptions/UpgradeTierService.php` (FR-033 instant proration — compute unused-EGP credit + new-tier prorated charge, issue fresh licences at new tier, retire old-tier licences with `retired_reason = "TierUpgraded"`)
- [ ] T086 [P] [US3] Implement `portal/app/Services/Subscriptions/ScheduleDowngradeService.php` (FR-033 sets `Subscription.pending_tier_change_to` to take effect at next renewal — no mid-period refund)
- [ ] T087 [P] [US3] Implement `portal/app/Services/Subscriptions/RefundFirstPeriodService.php` (FR-034 7-day window only on FirstPeriod invoices)
- [ ] T088 [P] [US3] Implement `portal/app/Services/Payments/PaymobHttpClient.php` (raw `Illuminate\Http\Client` adapter wrapping the Paymob v3 API)
- [ ] T089 [P] [US3] Implement `portal/app/Services/Payments/PaymobAdapter.php` covering card + Fawry + Vodafone Cash + InstaPay per FR-015 + research §3
- [ ] T090 [P] [US3] Implement `portal/app/Services/Payments/PaymentWebhookHandler.php` per [contracts/payment-webhook.md](./contracts/payment-webhook.md) Behaviour section (idempotent state machine, HMAC verification using `hash_hmac`)
- [ ] T091 [US3] Map `POST /api/v1/portal/payments/webhook` route in `portal/routes/api.php` with `PaymobHmacMiddleware.php` for signature verification
- [ ] T092 [P] [US3] Implement `portal/app/Console/Commands/MarkInvoicePaid.php` (vendor-only `php artisan invoice:mark-paid INV-2026-NNNNN` CLI per quickstart.md)
- [ ] T093 [P] [US3] Implement `portal/app/Services/InvoicePdf/ArabicInvoiceRenderer.php` using DOMPDF (Arabic RTL + Egyptian VAT line + sequential per-org invoice number per FR-016); Blade template at `portal/resources/views/invoices/template.blade.php`
- [ ] T094 [P] [US3] Configure `portal/config/filesystems.php` `local` disk + `Storage::disk('local')->put('invoices/{org-id}/{invoice-number}.pdf', $pdfBytes)` pattern in `ArabicInvoiceRenderer`
- [ ] T095 [P] [US3] Configure Resend SMTP in `portal/config/mail.php` + queue-able Mailable classes at `portal/app/Mail/{SignupConfirmation,PaymentReceipt,RefundConfirmation,TrialEndingT7,TrialEndingT1,RenewalFailed}.php`
- [ ] T096 [P] [US3] Blade email templates at `portal/resources/views/emails/{signup-confirmation,payment-receipt,refund-confirmation,trial-ending-T-7,trial-ending-T-1,renewal-failed}.blade.php` with bilingual variants based on the recipient's `locale_preference`
- [ ] T097 [P] [US3] Replace Breeze's default `register.blade.php` to also accept `organisation_legal_name_ar` + `display_name` per [contracts/signup.md](./contracts/signup.md); Breeze handles `verify-email.blade.php` out of the box
- [ ] T098 [P] [US3] Create `portal/resources/views/portal/dashboard.blade.php` + `DashboardController.php` showing subscription status + active licences + next renewal + open tickets + recent downloads (FR-012)
- [ ] T099 [P] [US3] Create `portal/resources/views/portal/subscription/{index,upgrade,downgrade,convert-trial,cancel}.blade.php` + `SubscriptionController.php` handling all 5 transitions
- [ ] T100 [P] [US3] Create `portal/resources/views/portal/billing/{invoices,payment-methods,refund}.blade.php` + `BillingController.php` (invoices list + PDF download + `refund_eligibility` badge per Invoice)
- [ ] T101 [P] [US3] Create `portal/resources/views/portal/downloads.blade.php` + `PortalDownloadsController.php` (authenticated, ties to active Subscription's tier — Solo doesn't see Enterprise downloads)
- [ ] T102 [P] [US3] Translation files (ar + en) at `portal/lang/{ar,en}/{subscription,billing,dashboard,downloads}.php` for all US3 UI strings
- [ ] T103 [P] [US3] Add a "Subscribe to keep going" CTA on the dashboard when the customer has NO active Subscription (the FR-029 + FR-030 trial-ended UX)
- [ ] T153 [P] [US3] Implement `portal/app/Services/Subscriptions/CancelSubscriptionService.php` per data-model.md §4 lifecycle (`[Active] → [Active until current_period_end_at] → renewal-job → [Cancelled]`). Sets `Subscription.cancelled_at = now()`, keeps `status = Active` until the period ends, then the renewal job flips it. Queues a `SubscriptionCancelled` Mailable. Owner-only; writes a `subscription.cancelled` audit-log entry (FR-013 Cancel action). Cancel view at `portal/resources/views/portal/subscription/cancel.blade.php` with confirmation + "keep going" reverse-CTA

**Checkpoint**: A new prospect can complete the full SC-002 < 15-minute signup→pay→download flow end-to-end. Combined with US1 (marketing surface) and US2 (licence transfer self-service), this is the shippable P1 MVP.

---

## Phase 6: User Story 4 - Support tickets (Priority: P2)

**Goal**: Customer submits a ticket from the portal with category + priority + description + attachments (up to 3 files × 5 MB), sees the SLA badge for their tier (24h Solo/SMB, 4h Enterprise/Firm), and receives email notifications as vendor staff replies.

**Independent Test**: Sign in, create new ticket with all fields populated + a 2 MB screenshot attached. Confirm: ticket appears in customer's list with status `Open` + correct SLA badge, vendor-staff view shows the attachment + customer's tier badge.

### Tests for User Story 4

- [ ] T104 [P] [US4] Integration test at `portal/tests/Feature/Flows/SupportTicketLifecycleTest.php` (create → reply → resolve → re-open transitions)
- [ ] T105 [P] [US4] Integration test at `portal/tests/Feature/Flows/SupportTicketAttachmentLimitsTest.php` (3-file cap, 5-MB cap, MIME-type whitelist enforcement)
- [ ] T106 [P] [US4] Unit test for `SlaCalculator` at `portal/tests/Unit/Support/SlaCalculatorTest.php` (24h vs 4h based on Subscription.tier; business-hours-only counting per FR-019)

### Implementation for User Story 4

- [ ] T107 [P] [US4] Create migrations + Eloquent models for `SupportTicket` + `SupportTicketReply` + `SupportTicketAttachment` per data-model.md §8
- [ ] T108 [P] [US4] Implement `portal/app/Services/Support/CreateTicketService.php` (FR-018 + MIME whitelist + 5-MB cap + 3-file cap + total 15-MB cap; saves attachments via `Storage::disk('local')->putFileAs(...)` under `tickets/{org-id}/`)
- [ ] T109 [P] [US4] Implement `portal/app/Services/Support/ReplyTicketService.php` — sets `support_tickets.first_reply_at` on the first vendor reply (T156 / SC-005 measurement)
- [ ] T110 [P] [US4] Implement `portal/app/Services/Support/SlaCalculator.php` (FR-019 — reads `Subscription.tier`, computes business-hours-bounded deadline)
- [ ] T111 [US4] Map `POST /api/v1/portal/support/tickets` + `POST /api/v1/portal/support/tickets/{id}/replies` + `GET /api/v1/portal/support/attachments/{id}` routes in `portal/routes/api.php` + `SupportTicketController.php`
- [ ] T112 [P] [US4] Create `portal/resources/views/portal/support/list.blade.php` (ticket list with status + SLA badge per ticket)
- [ ] T113 [P] [US4] Create `portal/resources/views/portal/support/new.blade.php` (form: category + priority + description + 3-file upload; priority=High disabled for Solo per FR-018)
- [ ] T114 [P] [US4] Create `portal/resources/views/portal/support/detail.blade.php` (thread view with vendor replies inline, reply form at the bottom)
- [ ] T115 [P] [US4] Blade Mailable templates: `TicketCreated.blade.php`, `TicketReplied.blade.php`, `TicketResolved.blade.php` under `resources/views/emails/support/`
- [ ] T116 [P] [US4] Translation files (ar + en) for all US4 UI strings + the "Priority support" badge copy from FR-019

**Checkpoint**: Customer + vendor staff have a complete ticket-lifecycle UX. US4 is independently functional and integrates cleanly with US1–US3.

---

## Phase 7: User Story 5 - Multi-user invitations (Priority: P2)

**Goal**: Owner invites bookkeepers + billing admins by email with per-member roles (Owner, BillingAdmin, SupportAdmin, ReadOnly). Invitee receives email with single-use 7-day-expiry link, sets password, lands in the same organisation as the owner. Invited member can move from clicking the link to seeing the dashboard in under 3 minutes (SC-007).

**Independent Test**: Sign in as Owner, invite a fresh email as `BillingAdmin`. From a clean browser session, click the invitation link, set password, accept. Confirm: invitee sees Billing pages but cannot transfer licences (owner-only action — UI hides the button + API returns 403 if URL-tampered).

### Tests for User Story 5

- [ ] T117 [P] [US5] Contract test at `portal/tests/Feature/Contracts/InviteMemberEndpointTest.php` — 12 assertions per [contracts/invite-member.md](./contracts/invite-member.md) including token-hashing-at-rest + 7-day expiry + single-use semantics
- [ ] T118 [P] [US5] Integration test at `portal/tests/Feature/Flows/MemberRemovalSessionTerminationTest.php` (FR-022 — removed member's sessions terminated within 5 minutes via the `security_stamp_version` bump)
- [ ] T119 [P] [US5] Integration test at `portal/tests/Feature/Flows/AtLeastOneActiveOwnerInvariantTest.php` (cannot remove the only remaining Owner — invariant guards the org from becoming inaccessible)
- [ ] T120 [P] [US5] Dusk e2e at `portal/tests/Browser/Portal/MemberInviteFlowTest.php` asserting SC-007 (invite-click-to-dashboard < 3 min wall-clock)

### Implementation for User Story 5

- [ ] T121 [P] [US5] Create migration `create_invitations_table.php` + `Invitation` Eloquent model per data-model.md §10 (stores SHA-256 HASH of token, not raw — per contracts/invite-member.md test #11)
- [ ] T122 [P] [US5] Implement `portal/app/Services/Organisations/InviteMemberService.php` per [contracts/invite-member.md](./contracts/invite-member.md) Behaviour section
- [ ] T123 [P] [US5] Implement `portal/app/Services/Organisations/AcceptInvitationService.php` (single-use, marks `accepted_at`, creates TeamMember if email isn't already a portal user)
- [ ] T124 [P] [US5] Implement `portal/app/Services/Organisations/RemoveMemberService.php` (FR-022 — flip `revoked_at`, bump `security_stamp_version`, invalidate active sessions within 5 min via a `CheckOrganisationMembershipStamp` middleware, refuse if removing the only active Owner)
- [ ] T125 [US5] Map `POST /api/v1/portal/organisations/{id}/invitations` + `POST /api/v1/portal/invitations/accept` + `DELETE /api/v1/portal/organisations/{id}/memberships/{memberId}` routes in `portal/routes/api.php`
- [ ] T126 [P] [US5] Create authorization Gates at `portal/app/Providers/AuthServiceProvider.php` — one per role × per action (e.g. `Gate::define('manage-licences', ...)`, `Gate::define('manage-billing', ...)`, etc.)
- [ ] T127 [P] [US5] Create `portal/resources/views/portal/organisation/members.blade.php` (invite form + member list + role chips + revoke buttons; Owner-only visibility)
- [ ] T128 [P] [US5] Create `portal/resources/views/portal/invitations/accept.blade.php` (landing page for invitation links — set password + accept)
- [ ] T129 [P] [US5] Apply role-based visibility checks across all portal Blade views — e.g. `transfer.blade.php` hidden for `BillingAdmin` (Owner-only per FR-022 implicit, License management is an Owner concern)
- [ ] T130 [P] [US5] Blade Mailable templates: `OrganisationInvitation.blade.php` (bilingual based on inviter's `locale_preference` fallback), `MemberRemoved.blade.php`
- [ ] T131 [P] [US5] Translation files (ar + en) for all US5 UI strings

**Checkpoint**: An accounting firm subscribing to Firm tier can invite their team + assign per-member roles. US5 doesn't disturb US1–US4 — a single-user Solo customer never opens this page.

---

## Phase 8: User Story 6 - Privacy URL stable for Play Store (Priority: P2)

**Goal**: `daftarx.app/privacy/android` always returns 200 with the vendor's privacy policy in both Arabic and English. The Android app's Play Store Data Safety form (per feature 009 FR-018) depends on this URL never moving. Any planned URL change MUST be preceded by a Play-listing update.

**Independent Test**: Request `https://daftarx.app/privacy/android` from anywhere on the public internet (no auth). Confirm: 200 response, bilingual policy text, "last updated" date visible, page renders < 3 s.

### Tests for User Story 6

- [ ] T132 [P] [US6] Dusk e2e at `portal/tests/Browser/Marketing/PrivacyAndroidUrlStabilityTest.php` asserting SC-004 (URL returns 200 with bilingual content on every deploy)

### Implementation for User Story 6

- [ ] T133 [P] [US6] Create `portal/resources/views/marketing/privacy/android.blade.php` + route in `portal/routes/web.php` rendering the policy at the stable URL `/privacy/android` per FR-006 (separate from `/privacy` index even if content is similar — the URL itself is the contract)
- [ ] T134 [P] [US6] Translation files `lang/ar/privacy_android.php` + `lang/en/privacy_android.php` with the full policy text covering Crashlytics diagnostics + FCM device tokens per feature 009 FR-018
- [ ] T135 [P] [US6] Add a CI smoke step to `.github/workflows/portal-build.yml` that hits `/privacy/android` on the staging slot after every deploy and fails the build if it doesn't return 200 with the expected content (prevents accidental URL removal)
- [ ] T136 [P] [US6] Implement version archival: when the privacy policy text changes, the previous version is preserved at `/privacy/android/history/{YYYY-MM-DD}` so prior consent claims remain auditable (the translation files become append-only — older versions stored as `lang/{ar,en}/privacy_android_2026_05_18.php` etc.)

**Checkpoint**: The Android app's Play Store listing remains compliant for as long as the website stays up. US6's surface is tiny (one page + one CI check) but the compliance dependency is real.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Performance + accessibility + account lifecycle + audit log viewer + production deployment polish. Most of these touch multiple user stories and benefit from being deferred until the core flows are working.

- [ ] T137 [P] Performance regression test at `portal/tests/Browser/Performance/MarketingPagesPerformanceTest.php` asserting SC-006 (3 s p75 render from Cairo simulator) on every CI build
- [ ] T138 [P] Accessibility audit at `portal/tests/Browser/Accessibility/Wcag21AaComplianceTest.php` integrating `axe-core` against all marketing pages + portal core flows per FR-027
- [ ] T139 [P] Implement security pages at `portal/resources/views/portal/account/security.blade.php` — MFA setup (TOTP per FR-011 using `pragmarx/google2fa-laravel`), active sessions list, auth-events log retained 90 days per FR-028
- [ ] T140 [P] Create `portal/resources/views/portal/account/delete.blade.php` (FR-024 — request account deletion with 30-day soft-delete window, 30-day cancellation grace period)
- [ ] T141 [P] Implement `portal/app/Services/Organisations/DeleteAccountService.php` (sets `soft_deleted_at` on the CustomerOrganisation + cascade-flag children per data-model.md cross-entity rules)
- [ ] T142 [P] Implement nightly Artisan command `portal/app/Console/Commands/PurgeSoftDeletedAccounts.php` scheduled via `app/Console/Kernel.php` daily — hard-deletes accounts past 30-day window, RETAINS audit-log rows per data-model.md §9 retention rule
- [ ] T143 [P] Create `portal/resources/views/portal/organisation/audit-log.blade.php` — paginated, filterable, Owner-only audit-log viewer per FR-023
- [ ] T144 [P] Cloudflare cache purge integration in CI: on every deploy, hit Cloudflare's purge API to invalidate marketing-route cache per research §9; document the API token rotation procedure
- [ ] T145 [P] Production environment variables documentation at `deploy/portal/README.md` (Paymob credentials, Resend API key, Hostinger SSH key, HMAC secret, Cloudflare API token, vendor-keys.json mount path)
- [ ] T146 [P] Status page setup at `status.daftarx.app` + UptimeRobot pings from 3 geographies (Cairo, Frankfurt, US-East) every 5 min per research §15
- [ ] T147 [P] Implement rate limiting on the public signup + contact-form endpoints (5/hour/IP for signup, 10/hour/IP for contact form) using Laravel's `RateLimiter::for(...)` in `app/Providers/AppServiceProvider.php`
- [ ] T148 [P] Implement bilingual error pages at `portal/resources/views/errors/{404,500,503}.blade.php` — fail-friendly per FR-015 even at the platform level
- [ ] T149 [P] Update `CLAUDE.md` `<!-- SPECKIT START -->` block to reference `010-website-portal/tasks.md` so future agents resume mid-implementation correctly
- [ ] T150 [P] Document the new portal endpoints in the existing `008-egypt-tax-accounting` + `009-android-app` quickstarts so the on-prem + mobile teams know about the shared `LicenceSigningService` reuse + portal-issued tokens
- [ ] T151 [P] Add a `<x-locale-prefix>` Blade component + `_ViewImports`-equivalent (Blade global aliases in `bootstrap/app.php`) so every Blade view can call `{{ __('...') }}` without explicit import directives
- [ ] T152 [P] Add `portal/.env.example` template with placeholders for every production secret (committed as `.env.example`; actual `.env` gitignored)
- [ ] T154 [P] FR-017 version-history surface: create migration `create_download_artifact_versions_table.php` + `DownloadArtifactVersion` Eloquent model per data-model.md §11. Update `portal/resources/views/portal/downloads.blade.php` (T101) to render a "prior versions" dropdown per artefact showing the most recent 3 non-retired rows where `released_at < latest.released_at`. Owners download any of them; analytics events written via `AuditLogWriter` (`download.rollback`)
- [ ] T155 [P] FR-011 organisation-level MFA enforcement: `requires_mfa_for_owners` (bit, default false) already on `CustomerOrganisation` migration (T017); add Owner-only `portal/resources/views/portal/organisation/security-policy.blade.php` to toggle it; add middleware `portal/app/Http/Middleware/OrganisationMfaPolicy.php` that — when an Owner signs in to an organisation with `requires_mfa_for_owners = true` and their `TeamMember.mfa_enabled_at IS NULL` — redirects to `account/security` with an `mfa_required` flash message and refuses to authorise any other route until TOTP is enrolled. Unit test the middleware against the four matrix cases (owner-on/off × policy-on/off)
- [ ] T156 [P] SC-005 SLA measurement: `first_reply_at` already on `SupportTicket` migration (T107) — set on the first vendor-staff reply per `ReplyTicketService` (T109); create daily Artisan command `portal/app/Console/Commands/ComputeSupportSlaRollup.php` (scheduled via `Kernel.php`) that writes a daily row to a new `support_sla_rollups` table (date, total_tickets, replies_within_sla, percent_within_sla, by_tier breakdown); create vendor-only `portal/resources/views/ops/sla-dashboard.blade.php` (guarded by a `vendor-staff` gate distinct from CustomerOrganisation Owner) rendering a 30-day chart so the team knows when SC-005 is being missed and by which tier

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — **BLOCKS** all user stories.
- **User Stories (Phase 3+)**: Each depends only on Foundational. Once Foundational is done, the six stories can proceed in parallel if staffed; otherwise sequentially in priority order (US1 + US2 + US3 → US4 + US5 + US6).
- **Polish (Phase 9)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **US1 (Marketing, P1)**: Foundational only. Independently shippable as a "coming soon — sign up to be notified" landing page even without portal work.
- **US2 (Licence self-service, P1)**: Foundational + Subscription/Licence entities (T060/T061). Owns the entities so independently testable once seeded data exists.
- **US3 (Signup → Pay → Download, P1)**: Foundational + Subscription/Licence/Invoice/SalesLead entities. Shares the Subscription/Licence entities with US2 — to land in parallel, the team coordinates a single migration that introduces both at once (T060 + T061 + T078 + T079 → one combined migration set).
- **US4 (Support tickets, P2)**: Foundational only. Independent of US1–US3 (a customer who signed up via US3 manages tickets here; the entities are isolated).
- **US5 (Multi-user invitations, P2)**: Foundational + the OrganisationMembership join (already in Phase 2 — T019). Independent of US3 (single-Owner organisations created by US3 just have one membership row).
- **US6 (Privacy URL, P2)**: Independent of every other story. Could land first if needed for Play Store compliance ahead of feature 009 shipping.

### Within Each User Story

- Tests (test-first per Constitution III) **MUST** be written and observed failing before implementation.
- Within the test group, tasks marked `[P]` can run in parallel (different test files).
- Implementation order: migrations + models → services → controllers/routes → Blade views → translations.
- Each story is complete only when its independent-test scenario passes (Dusk e2e for visible flows, Feature tests for backend flows).

### Parallel Opportunities

- All Setup tasks marked `[P]` (T003–T016) can run in parallel after T001 + T002 (which scaffold the Laravel project + Breeze).
- All Foundational tasks marked `[P]` (T018–T034) can run in parallel after T017 (first migration) lands.
- Once Foundational is done, the six user stories can proceed in parallel.
- Within each story, all `[P]` tests can run in parallel; all `[P]` implementations can run in parallel.
- Polish tasks are mostly all `[P]` since they touch independent files.

---

## Parallel Example: User Story 3

```bash
# Launch US3 tests in parallel (different files):
Task: "T070 [P] [US3] Contract test for POST /api/v1/portal/signup in portal/tests/Feature/Contracts/SignupEndpointTest.php"
Task: "T071 [P] [US3] Contract test for POST /api/v1/portal/payments/webhook in portal/tests/Feature/Contracts/PaymentWebhookEndpointTest.php"
Task: "T072 [P] [US3] Integration test for trial-conversion flow in portal/tests/Feature/Flows/TrialConversionFlowTest.php"
Task: "T076 [P] [US3] Dusk e2e for signup-to-download in portal/tests/Browser/Portal/SignupToDownloadFlowTest.php"

# Launch US3 entity migrations in parallel (different files):
Task: "T078 [P] [US3] Create Invoice migration + model"
Task: "T079 [P] [US3] Create SalesLead migration + model"

# Launch US3 services in parallel (different files):
Task: "T084 [P] [US3] Implement ConvertTrialToPaidService"
Task: "T085 [P] [US3] Implement UpgradeTierService"
Task: "T087 [P] [US3] Implement RefundFirstPeriodService"
Task: "T088 [P] [US3] Implement PaymobHttpClient"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 + 3)

1. Complete Phase 1: Setup (T001–T016).
2. Complete Phase 2: Foundational (T017–T036) — **CRITICAL**, blocks every story.
3. Complete Phase 3: User Story 1 — Marketing surface (T037–T055).
4. Complete Phase 4: User Story 2 — Licence self-service (T056–T069).
5. Complete Phase 5: User Story 3 — Signup → Pay → Download (T070–T103, T153).
6. **STOP and VALIDATE**: Run `portal/tests/Browser/Marketing/*` + `portal/tests/Browser/Portal/SignupToDownloadFlowTest.php` + `LicenceTransferFlowTest.php`. Demo to stakeholders.
7. Deploy: this is a complete vendor-facing website + functional portal that lets the team start collecting payments.

### Incremental Delivery (recommended)

1. Setup + Foundational → Foundation ready.
2. Add US1 → Test → Deploy as "marketing site only, signup coming soon".
3. Add US2 + US3 in parallel → Test → Deploy (**MVP — first paying customer possible**).
4. Add US4 + US5 + US6 in parallel → Test → Deploy (full v1 portal).
5. Polish phase → Production hardening.

### Parallel Team Strategy

With three developers:

1. Team completes Setup + Foundational together (~1 week).
2. Once Foundational is done:
   - **Dev A**: US1 (Marketing) — front-end-heavy, mostly Blade + Tailwind.
   - **Dev B**: US2 + US3 (Licence + Signup → Pay → Download) — backend-heavy, the integration-rich path.
   - **Dev C**: US4 + US5 (Support + Multi-user) — Blade-heavy with simpler backends.
3. US6 (Privacy URL) is a 1-day task for any free developer.
4. Polish phase shared by all three.

---

## Notes

- `[P]` tasks = different files, no dependencies — safe for parallel execution.
- `[Story]` label maps each task to a specific user story for traceability with `spec.md`.
- Each user story is independently completable and testable; stopping after any story's checkpoint yields working, demonstrable value.
- Verify tests fail before implementing (Constitution III).
- Commit after each task or each tight logical group; never commit a failing build to main.
- The portal NEVER stores trial state (FR-030); CreateTrialIntentService is analytics-only.
- The portal NEVER calls the customer's on-prem server for auth (FR-032); identity stores are physically separate databases.
- The portal NEVER introduces a new activation endpoint (FR-031); paid licences use the existing manual HWID-copy flow.
- The portal NEVER issues a mid-period refund (FR-034); only first-period FirstPeriod invoices within 7 days are refundable.
- Cross-cutting items integrated into the right stories so no separate "FR-029 task" / "FR-031 task" / etc. is needed:
  - **FR-006** (privacy URL) → US6 (T133–T136) + the vendor-wide `/privacy` in US1 (T046).
  - **FR-011** (TOTP MFA, per-user + org-policy enforcement) → Setup (T003 google2fa) + Polish (T139 Security page for per-user, T155 OrganisationMfaPolicy middleware for org-level "require MFA for Owners").
  - **FR-013** (Owner licence actions: Download / Transfer / Renew / Cancel) → US2 (T066 List, T067 Activate, T068 Transfer) + US3 (T153 CancelSubscriptionService for the Cancel action).
  - **FR-014** (licence transfer) → US2 (T064 — service + the 2-audit-row write).
  - **FR-015** (4 payment methods) → US3 (T089 Paymob adapter + T088 HTTP client).
  - **FR-016** (Arabic PDF invoices) → US3 (T093 DOMPDF renderer).
  - **FR-017** (downloads page + 3 prior versions for rollback) → US3 (T101 Downloads.blade.php for latest) + Polish (T154 DownloadArtifactVersion entity + prior-3 dropdown per artefact).
  - **FR-018** (Play Data Safety dependency) → US6 (T133 + T134 publish the policy).
  - **FR-019** (SLA badge) → US4 (T110 SlaCalculator + the badge rendering in T112/T113) + US2 (T066 surfaces the Priority-support badge on the Licences/List for Enterprise/Firm per US2 AS#3).
  - **FR-020** (invitations) → US5 (T122 InviteMember + T123 Accept).
  - **FR-021** (organisation scoping) → Foundational (T029 middleware).
  - **FR-022** (member removal session kill) → US5 (T124 RemoveMember).
  - **FR-023** (audit log) → Foundational (T027 writer) + Polish (T143 viewer).
  - **FR-024** (account deletion) → Polish (T140 page + T141 service + T142 purge command).
  - **FR-027** (WCAG accessibility) → Polish (T138 axe-core integration).
  - **FR-028** (auth-event logging) → Polish (T139 Security page).
  - **FR-029** (single-tap trial-to-paid) → US3 (T084 ConvertTrialToPaidService + T103 dashboard CTA).
  - **FR-030** (no portal-side trial) → Foundational (Subscription entity in T060 has NO `Trial` status; CreateTrialIntentService in T083 is analytics-only).
  - **FR-031** (manual install-first activation) → US2 (T063 ActivatePaidLicenceService + T067 activate-paid.blade.php).
  - **FR-032** (identity separation) → Foundational (T018 TeamMember + T025 separate auth config + T029 OrganisationScope middleware).
  - **FR-033** (tier upgrade instant proration / downgrade at renewal) → US3 (T085 UpgradeTierService + T086 ScheduleDowngradeService).
  - **FR-034** (7-day first-period refund) → US3 (T087 RefundFirstPeriodService + the computed `refund_eligibility` on Invoice in T078).
  - **SC-005** (95% of tickets get a vendor reply within 24 business hours) → Polish (T156 first_reply_at + daily rollup command + Ops/SlaDashboard).
