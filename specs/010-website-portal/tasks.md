---

description: "Task list for 010-website-portal feature implementation"
---

# Tasks: DaftarX Website + Customer Portal

**Input**: Design documents from `/specs/010-website-portal/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Test tasks are INCLUDED. Constitution III (Test-First Discipline) is non-negotiable and the plan commits to contract tests for every new portal endpoint plus integration tests for every cross-boundary flow (Paymob round-trip, Resend dispatch, in-process licence-signing, Azure Blob upload, Cloudflare cache invalidation). Tests MUST be written and observed failing before their production code lands.

**Organization**: Tasks are grouped by user story (US1–US6) to enable independent implementation and testing. P1 stories (US1, US2, US3) together form the shippable MVP per `plan.md`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US6); omitted for Setup, Foundational, and Polish phases
- All file paths are project-relative

## Path Conventions (from plan.md)

- **Portal Web module**: `src/EgyptTax.Portal.Web/`
- **Portal Application module**: `src/EgyptTax.Portal.Application/`
- **Portal Infrastructure module**: `src/EgyptTax.Portal.Infrastructure/`
- **Unit tests**: `tests/EgyptTax.Portal.UnitTests/`
- **Integration tests**: `tests/EgyptTax.Portal.IntegrationTests/`
- **E2E tests (Playwright)**: `tests/EgyptTax.Portal.E2ETests/`
- **Deploy artifacts**: `deploy/portal/`
- **Reused from on-prem**: `src/EgyptTax.Web.Tools/` (LicenseIssueHost wrapped by the portal)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the three new .NET projects + version pins + dockerised local stack + CI pipeline. No business code yet.

- [ ] T001 Create `src/EgyptTax.Portal.Web/EgyptTax.Portal.Web.csproj` targeting net8.0 per plan.md Technical Context
- [ ] T002 [P] Create `src/EgyptTax.Portal.Application/EgyptTax.Portal.Application.csproj` targeting net8.0
- [ ] T003 [P] Create `src/EgyptTax.Portal.Infrastructure/EgyptTax.Portal.Infrastructure.csproj` targeting net8.0
- [ ] T004 Add the three new project references to the existing solution file at repo root
- [ ] T005 [P] Add package refs to `EgyptTax.Portal.Web.csproj`: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Components.Server`, `Microsoft.AspNetCore.Mvc.RazorPages`, `Microsoft.AspNetCore.Localization.Routing`, `Serilog.AspNetCore`, `Serilog.Sinks.File`, `Serilog.Sinks.ApplicationInsights`, `QuestPDF`
- [ ] T006 [P] Add package refs to `EgyptTax.Portal.Application.csproj`: `Microsoft.Extensions.Logging.Abstractions`, `MediatR` (for handler dispatch), `FluentValidation`
- [ ] T007 [P] Add package refs to `EgyptTax.Portal.Infrastructure.csproj`: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Sqlite` (dev override), `Azure.Storage.Blobs`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Http.Polly`
- [ ] T008 [P] Add project reference from `EgyptTax.Portal.Application.csproj` to `EgyptTax.Portal.Infrastructure.csproj` (Web composes both)
- [ ] T009 [P] Add project reference from `EgyptTax.Portal.Web.csproj` to `EgyptTax.Web.Tools.csproj` (to wrap the existing `LicenseIssueHost` per research §10)
- [ ] T010 [P] Create `deploy/portal/Dockerfile` (multi-stage .NET 8 SDK → distroless runtime) per plan.md
- [ ] T011 [P] Create `deploy/portal/docker-compose.yml` (portal-web + SQL Server 2022 + Azurite) for local dev stack per plan.md
- [ ] T012 [P] Create `deploy/portal/cloudflare/cache-rules.json` (marketing routes `max-age=3600`; portal/api/identity routes `no-store`) per research §9
- [ ] T013 [P] Create `deploy/portal/README.md` ops runbook (deploy procedure, secret rotation, incident response)
- [ ] T014 [P] Set up Tailwind CSS v4 at `src/EgyptTax.Portal.Web/wwwroot/css/input.css` + `tailwind.config.js`; document the `npx tailwindcss --watch` invocation in `quickstart.md`
- [ ] T015 [P] Create `.github/workflows/portal-build.yml` CI workflow with three stages (build+unit, integration, deploy-on-main) per research §16
- [ ] T016 [P] Create `src/EgyptTax.Portal.Web/appsettings.json` + `appsettings.Development.json` (Development sets SQLite override + fake Resend SMTP) per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting infrastructure every user story needs — DbContext + Identity store + licence-signing service + audit-log writer + locale routing + cookie auth + Serilog wiring. No user-story work can start until this phase is complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T017 Create `PortalDbContext` at `src/EgyptTax.Portal.Infrastructure/Persistence/PortalDbContext.cs` deriving from `IdentityDbContext<PortalUser>`; auto-discover entity configurations via `ApplyConfigurationsFromAssembly`
- [ ] T018 [P] Create `PortalUser` at `src/EgyptTax.Portal.Web/Identity/PortalUser.cs` extending `IdentityUser` with `LocalePreference`, `DisplayName`, `SoftDeletedAtUtc` per data-model.md §2 + FR-032
- [ ] T019 [P] Create `PortalRole` at `src/EgyptTax.Portal.Web/Identity/PortalRole.cs` (placeholder — actual role logic is org-scoped via `OrganisationMembership.Role`)
- [ ] T020 [P] Create `TotpMfaService` at `src/EgyptTax.Portal.Web/Identity/TotpMfaService.cs` using `Otp.NET` per research §2 for FR-011
- [ ] T021 [P] Configure AspNetCore.Identity in `Program.cs`: PBKDF2 password hashing, lockout (5 attempts / 15 min), email confirmation required, TOTP MFA opt-in, separate cookie name `.DaftarXPortal.Identity` to avoid colliding with any on-prem cookie if dev runs both on localhost
- [ ] T022 [P] Create `CustomerOrganisation` entity at `src/EgyptTax.Portal.Infrastructure/Persistence/Entities/CustomerOrganisation.cs` per data-model.md §1
- [ ] T023 [P] Create `OrganisationMembership` entity at `src/EgyptTax.Portal.Infrastructure/Persistence/Entities/OrganisationMembership.cs` per data-model.md §2 join table
- [ ] T024 [P] Create `AuditLogEntry` entity at `src/EgyptTax.Portal.Infrastructure/Persistence/Entities/AuditLogEntry.cs` per data-model.md §8
- [ ] T025 [P] Create `IEntityTypeConfiguration<>` classes for the three foundational entities at `src/EgyptTax.Portal.Infrastructure/Persistence/Configurations/` (CustomerOrganisationConfiguration, OrganisationMembershipConfiguration with the filtered unique index on `(OrganisationId, TeamMemberId) WHERE RevokedAtUtc IS NULL`, AuditLogEntryConfiguration)
- [ ] T026 Create initial EF Core migration `00_Initial` at `src/EgyptTax.Portal.Infrastructure/Migrations/` covering Identity tables + CustomerOrganisation + OrganisationMembership + AuditLogEntry; apply it locally to confirm the DbContext composes correctly
- [ ] T027 [P] Define `IAuditLogWriter` + impl at `src/EgyptTax.Portal.Application/Audit/AuditLogWriter.cs` (FR-023, FR-028 — every state change writes one row, scoped to OrganisationId, payload NEVER contains tokens/PII per data-model.md §8)
- [ ] T028 [P] Define `ILicenceSigningService` at `src/EgyptTax.Portal.Application/Licences/ILicenceSigningService.cs` + impl `LicenceSigningService.cs` that wraps the existing `EgyptTax.Web.Tools.LicenseIssueHost` flow per research §10
- [ ] T029 [P] Create `OrganisationScopeMiddleware` at `src/EgyptTax.Portal.Web/Middleware/OrganisationScopeMiddleware.cs` rejecting URL-tampering attempts that try to read org-B data while signed in to org-A (FR-021)
- [ ] T030 [P] Configure ASP.NET Core localization in `Program.cs`: ar-EG default RTL, en-US fallback, URL-prefix routing (`/ar/*`, `/en/*`) per FR-008 + research §8; create `LocaleResolver` middleware
- [ ] T031 [P] Define `IEmailService` at `src/EgyptTax.Portal.Application/Email/IEmailService.cs` + `DevStdoutEmailService` (writes to stdout in Development) + `ResendTransactionalEmailService` scaffold in Infrastructure per research §4
- [ ] T032 [P] Wire Serilog at `Program.cs` with JSON file sink + Application Insights sink per research §15
- [ ] T033 [P] Create Mission Control base layout at `src/EgyptTax.Portal.Web/Pages/Shared/_Layout.cshtml` (gold + charcoal palette mirroring the on-prem product + the Android app)
- [ ] T034 [P] Create `_PortalLayout.razor` at `src/EgyptTax.Portal.Web/Portal/Shared/_PortalLayout.razor` (Blazor Server layout, auth required, sidebar nav)
- [ ] T035 Configure two-surface routing in `Program.cs`: Razor Pages for `/*` (marketing), Blazor Server with cookie auth required for `/portal/*` per plan.md "Project Type"
- [ ] T036 [P] Add `Program.cs` `MapHealthChecks("/health")` returning the DbContext + Resend + Azure Blob connectivity status

**Checkpoint**: Foundation ready — DbContext composes, Identity logs in, licence-signing service is callable, audit-log writer works, locale routing serves bilingual pages. User stories can begin.

---

## Phase 3: User Story 1 - Prospect discovers DaftarX (Priority: P1) 🎯 part of MVP

**Goal**: Marketing surface live in both Arabic (RTL default) and English. Prospect can navigate Homepage → Pricing → "Start trial" CTA in ≤ 3 clicks; the four tiers (Solo, SMB, Enterprise, Firm) display with EGP prices and a feature checklist; every marketing page renders < 3 s p75 from a Cairo broadband connection.

**Independent Test**: From a clean browser (no cookies), open `https://localhost:5050`, navigate Homepage → Pricing → click any tier's "Start trial" CTA. Confirm: language defaults to ar-EG RTL, four tiers visible with EGP prices + feature checklists, signup form reachable. Run `tests/EgyptTax.Portal.E2ETests/Marketing/HomepageRenderTests.cs` to verify SC-006 < 3 s p75 budget.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T037 [P] [US1] Playwright e2e test at `tests/EgyptTax.Portal.E2ETests/Marketing/HomepageRenderTests.cs` asserting SC-006 (< 3 s p75 first render for `/`, `/ar`, `/en`)
- [ ] T038 [P] [US1] Playwright e2e test at `tests/EgyptTax.Portal.E2ETests/Marketing/PricingNavigationTests.cs` asserting SC-001 (Homepage → Pricing → trial CTA in ≤ 3 clicks)
- [ ] T039 [P] [US1] Playwright e2e test at `tests/EgyptTax.Portal.E2ETests/Marketing/LanguageSwitcherTests.cs` asserting FR-008 (locale toggle preserves current page + scroll position)

### Implementation for User Story 1

- [ ] T040 [P] [US1] Create `src/EgyptTax.Portal.Web/Pages/Index.cshtml` + `Index.cshtml.cs` homepage with hero + value props + three primary CTAs (Start trial / See pricing / Download) per FR-001
- [ ] T041 [P] [US1] Create `src/EgyptTax.Portal.Web/Pages/Features.cshtml` + `Features.cshtml.cs` enumerating the 27-feature catalog from the on-prem `Feature.cs` organised by category per FR-002
- [ ] T042 [P] [US1] Create `src/EgyptTax.Portal.Web/Pages/Pricing.cshtml` + `Pricing.cshtml.cs` with 4 tier cards (Solo, SMB, Enterprise, Firm) + monthly/annual EGP prices + feature checklist + comparison matrix per FR-003
- [ ] T043 [P] [US1] Create `src/EgyptTax.Portal.Web/Pages/Downloads.cshtml` + `Downloads.cshtml.cs` listing DaftarX-Setup.msi + DaftarX-Client-Setup.msi (from feature 008) + Google Play badge + side-load APK link (from feature 009) with file size + version + checksum per FR-004
- [ ] T044 [P] [US1] Create `src/EgyptTax.Portal.Web/Pages/About.cshtml` + `About.cshtml.cs` per FR-005
- [ ] T045 [P] [US1] Create `src/EgyptTax.Portal.Web/Pages/Contact.cshtml` + `Contact.cshtml.cs` with form posting to a `CreateSalesLeadHandler` stub (full persistence lands in US3 with the SalesLead entity); WhatsApp + phone + email channels rendered statically
- [ ] T046 [P] [US1] Create `src/EgyptTax.Portal.Web/Pages/Privacy/Index.cshtml` (vendor-wide privacy policy) per FR-006
- [ ] T047 [P] [US1] Create `src/EgyptTax.Portal.Web/Pages/Terms.cshtml` + `src/EgyptTax.Portal.Web/Pages/Refund.cshtml` per FR-007
- [ ] T048 [P] [US1] Author Tailwind CSS marketing styles at `src/EgyptTax.Portal.Web/wwwroot/css/input.css` using CSS logical properties (`margin-inline-start` etc.) so the same stylesheet serves RTL + LTR per research §8
- [ ] T049 [P] [US1] Create `_LanguageSwitcher.cshtml` partial at `src/EgyptTax.Portal.Web/Pages/Shared/_LanguageSwitcher.cshtml` that toggles between `/ar/...` and `/en/...` preserving the current page path
- [ ] T050 [P] [US1] Create resource files for every marketing page: `Index.ar-EG.resx` + `Index.en-US.resx`, same pair for Features, Pricing, Downloads, About, Contact, Terms, Refund, Privacy
- [ ] T051 [P] [US1] Create `_Footer.cshtml` partial linking Terms, Refund, Privacy, About from every marketing page per FR-007
- [ ] T052 [US1] Wire Razor Pages locale-aware routing in `Program.cs` so `/ar/pricing` and `/en/pricing` both resolve to `Pages/Pricing.cshtml` with the right culture
- [ ] T053 [US1] Add structured data (JSON-LD `Organization` + `Product` + `Offer` schemas), `sitemap.xml` generation, and `robots.txt` per FR-009 SEO requirement
- [ ] T054 [P] [US1] Add Open Graph + Twitter Card meta tags to every marketing page for social sharing
- [ ] T055 [P] [US1] Add `<link rel="alternate" hreflang>` tags for both locales on every marketing page (Google indexes ar + en as distinct pages)

**Checkpoint**: Marketing surface fully functional and SEO-indexable. US1 is independently shippable as a "coming soon — sign up to be notified" landing page even before any portal work lands.

---

## Phase 4: User Story 2 - Licence self-service for HWID transfer (Priority: P1)

**Goal**: A customer with an active Solo licence can log in to the portal, open License Management, transfer their licence to a new hardware id, and download the new signed token — all in under 5 minutes (SC-003), no support ticket required.

**Independent Test**: Sign in as a customer with one active licence. From License Management, paste a different mocked HWID into the Transfer dialog, complete the flow. Confirm: new token downloads, old HWID's row marked `Transferred` with `RetiredAtUtc` populated, two audit-log rows written (`licence.retired` + `licence.activated` with cross-references in payloads).

### Tests for User Story 2

- [ ] T056 [P] [US2] Contract test at `tests/EgyptTax.Portal.IntegrationTests/Contracts/ActivatePaidLicenceEndpointTests.cs` — 10 assertions per [contracts/activate-paid-licence.md](./contracts/activate-paid-licence.md)
- [ ] T057 [P] [US2] Contract test at `tests/EgyptTax.Portal.IntegrationTests/Contracts/TransferLicenceEndpointTests.cs` — 10 assertions per [contracts/transfer-licence.md](./contracts/transfer-licence.md) including the concurrent-transfer optimistic-concurrency case
- [ ] T058 [P] [US2] Playwright e2e at `tests/EgyptTax.Portal.E2ETests/Portal/LicenceTransferFlowTests.cs` asserting SC-003 (transfer flow < 5 min wall-clock)
- [ ] T059 [P] [US2] Unit test for `HwidValidator` at `tests/EgyptTax.Portal.UnitTests/Licences/HwidValidatorTests.cs` (format check + cross-customer collision)

### Implementation for User Story 2

- [ ] T060 [P] [US2] Create `Subscription` entity + configuration + migration at `src/EgyptTax.Portal.Infrastructure/Persistence/Entities/Subscription.cs` per data-model.md §3 (NO `Trial` status per FR-030)
- [ ] T061 [P] [US2] Create `Licence` entity + configuration + migration at `src/EgyptTax.Portal.Infrastructure/Persistence/Entities/Licence.cs` per data-model.md §4 — composite unique index on Hwid filtered on `RetiredAtUtc IS NULL`
- [ ] T062 [P] [US2] Implement `HwidValidator` at `src/EgyptTax.Portal.Application/Licences/HwidValidator.cs` — regex `^[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}$` + cross-customer collision query
- [ ] T063 [P] [US2] Implement `ActivatePaidLicenceHandler` at `src/EgyptTax.Portal.Application/Licences/ActivatePaidLicenceHandler.cs` per [contracts/activate-paid-licence.md](./contracts/activate-paid-licence.md) Behaviour section
- [ ] T064 [P] [US2] Implement `TransferLicenceHandler` at `src/EgyptTax.Portal.Application/Licences/TransferLicenceHandler.cs` per [contracts/transfer-licence.md](./contracts/transfer-licence.md) Behaviour section (FR-014: retire + reissue + 2 audit rows)
- [ ] T065 [US2] Map `POST /api/v1/portal/licences/activate` + `POST /api/v1/portal/licences/{id}/transfer` + `GET /api/v1/portal/licences/{id}/token.token` endpoints in `Program.cs`
- [ ] T066 [P] [US2] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Licences/List.razor` showing active + retired licences with action buttons (Download token, Transfer, Renew). When the Subscription's `Tier ∈ {Enterprise, Firm}`, render the "Priority support" badge inline next to the tier name (spec US2 AS#3 — surfaces the Feature.PrioritySupport entitlement at the licence-management surface, not just on the Support form)
- [ ] T067 [P] [US2] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Licences/ActivatePaid.razor` (paste HWID → submit → download token)
- [ ] T068 [P] [US2] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Licences/Transfer.razor` (confirm dialog → submit → download new token)
- [ ] T069 [P] [US2] Resource files (ar + en) at `src/EgyptTax.Portal.Web/Resources/Pages/Portal/Licences/` for all US2 UI strings

**Checkpoint**: A customer can complete the SC-003 5-minute licence transfer end-to-end. US2 is independently shippable but depends on having paid customers (US3 creates them); for v1 testing the team seeds test customers manually via the EF Core migrations.

---

## Phase 5: User Story 3 - Signup → Pay → Download (Priority: P1) 🎯 MVP

**Goal**: A new prospect clicks "Start trial" from the marketing site, signs up with email + password (no payment up-front per FR-029), the trial runs entirely client-side in the on-prem product (FR-030), and when they convert to paid they pick a tier + payment method (Fawry / InstaPay / Vodafone Cash / card via Paymob), receive a PDF invoice, and reach the downloads page with their licence token ready to activate — all under 15 minutes (SC-002).

**Independent Test**: From homepage click "Start trial" → complete signup with a real email → confirm email link → enter portal → click "Subscribe to keep going" → pick Solo + Monthly → choose Fawry → enter mock reference code in dev → portal receives webhook → invoice marked Paid → land on downloads page → activate licence per US2 flow.

### Tests for User Story 3

- [ ] T070 [P] [US3] Contract test at `tests/EgyptTax.Portal.IntegrationTests/Contracts/SignupEndpointTests.cs` — 10 assertions per [contracts/signup.md](./contracts/signup.md)
- [ ] T071 [P] [US3] Contract test at `tests/EgyptTax.Portal.IntegrationTests/Contracts/PaymentWebhookEndpointTests.cs` — 10 assertions per [contracts/payment-webhook.md](./contracts/payment-webhook.md) including HMAC verification + idempotency
- [ ] T072 [P] [US3] Integration test at `tests/EgyptTax.Portal.IntegrationTests/Flows/TrialConversionFlowTests.cs` (FR-029 single-tap convert with no re-entered fields)
- [ ] T073 [P] [US3] Integration test at `tests/EgyptTax.Portal.IntegrationTests/Flows/TierUpgradeProrationTests.cs` (FR-033 instant proration on upgrade)
- [ ] T074 [P] [US3] Integration test at `tests/EgyptTax.Portal.IntegrationTests/Flows/TierDowngradeAtRenewalTests.cs` (FR-033 downgrade scheduled at next renewal — NOT mid-period)
- [ ] T075 [P] [US3] Integration test at `tests/EgyptTax.Portal.IntegrationTests/Flows/RefundWindowTests.cs` (FR-034 7-day first-period only)
- [ ] T076 [P] [US3] Playwright e2e at `tests/EgyptTax.Portal.E2ETests/Portal/SignupToDownloadFlowTests.cs` asserting SC-002 (full flow < 15 min wall-clock)
- [ ] T077 [P] [US3] Integration test at `tests/EgyptTax.Portal.IntegrationTests/Flows/InvoicePdfGenerationTests.cs` (QuestPDF renders Arabic RTL correctly with bidi text + Egyptian VAT line per FR-016)

### Implementation for User Story 3

- [ ] T078 [P] [US3] Create `Invoice` entity + configuration + migration at `src/EgyptTax.Portal.Infrastructure/Persistence/Entities/Invoice.cs` per data-model.md §5 — composite unique on `(OrganisationId, InvoiceNumber)` + filtered unique on `PaymobTransactionId` for webhook idempotency
- [ ] T079 [P] [US3] Create `SalesLead` entity + configuration + migration at `src/EgyptTax.Portal.Infrastructure/Persistence/Entities/SalesLead.cs` per data-model.md §7
- [ ] T080 [P] [US3] Implement `CreateSalesLeadHandler` at `src/EgyptTax.Portal.Application/Sales/CreateSalesLeadHandler.cs` (called by US1 Contact page's form post)
- [ ] T081 [P] [US3] Implement `SignupHandler` at `src/EgyptTax.Portal.Application/Identity/SignupHandler.cs` per [contracts/signup.md](./contracts/signup.md) Behaviour section
- [ ] T082 [US3] Map `POST /api/v1/portal/signup` + email-confirmation endpoint in `Program.cs`
- [ ] T083 [P] [US3] Implement `CreateTrialSubscriptionHandler` at `src/EgyptTax.Portal.Application/Subscriptions/CreateTrialSubscriptionHandler.cs` — per FR-030 this creates ONLY an analytics-intent record, NEVER a Subscription row with `Status = Trial`
- [ ] T084 [P] [US3] Implement `ConvertTrialToPaidHandler` at `src/EgyptTax.Portal.Application/Subscriptions/ConvertTrialToPaidHandler.cs` (FR-029 single-tap with no re-entered fields)
- [ ] T085 [P] [US3] Implement `UpgradeTierHandler` at `src/EgyptTax.Portal.Application/Subscriptions/UpgradeTierHandler.cs` (FR-033 instant proration — compute unused-EGP credit + new-tier prorated charge, issue fresh licences at new tier, retire old-tier licences with `RetiredReason = "TierUpgraded"`)
- [ ] T086 [P] [US3] Implement `ScheduleDowngradeHandler` at `src/EgyptTax.Portal.Application/Subscriptions/ScheduleDowngradeHandler.cs` (FR-033 sets `Subscription.PendingTierChangeTo` to take effect at next renewal — no mid-period refund)
- [ ] T087 [P] [US3] Implement `RefundFirstPeriodHandler` at `src/EgyptTax.Portal.Application/Subscriptions/RefundFirstPeriodHandler.cs` (FR-034 7-day window only on FirstPeriod invoices)
- [ ] T088 [P] [US3] Implement `PaymobHttpClient` at `src/EgyptTax.Portal.Infrastructure/Payments/PaymobHttpClient.cs` (raw HTTPS adapter wrapping the Paymob v3 API)
- [ ] T089 [P] [US3] Implement `PaymobAdapter` at `src/EgyptTax.Portal.Application/Payments/PaymobAdapter.cs` covering card + Fawry + Vodafone Cash + InstaPay per FR-015 + research §3
- [ ] T090 [P] [US3] Implement `PaymentWebhookHandler` at `src/EgyptTax.Portal.Application/Payments/PaymentWebhookHandler.cs` per [contracts/payment-webhook.md](./contracts/payment-webhook.md) Behaviour section (idempotent state machine, HMAC verification)
- [ ] T091 [US3] Map `POST /api/v1/portal/payments/webhook` endpoint in `Program.cs` with HMAC-verification middleware
- [ ] T092 [P] [US3] Implement `ManualBankTransferReconciler` at `src/EgyptTax.Portal.Application/Payments/ManualBankTransferReconciler.cs` + a vendor-only `dotnet run --project src/EgyptTax.Portal.AdminCli -- mark-invoice-paid INV-2026-NNNNN` CLI per quickstart.md
- [ ] T093 [P] [US3] Implement `ArabicInvoiceTemplate` at `src/EgyptTax.Portal.Infrastructure/InvoicePdf/ArabicInvoiceTemplate.cs` using QuestPDF (Arabic RTL + Egyptian VAT line + sequential per-org invoice number per FR-016)
- [ ] T094 [P] [US3] Implement `BlobAttachmentStore` at `src/EgyptTax.Portal.Infrastructure/Storage/BlobAttachmentStore.cs` (Azure Blob in prod, Azurite local override) per research §6
- [ ] T095 [P] [US3] Implement `ResendTransactionalEmailService` at `src/EgyptTax.Portal.Infrastructure/Email/ResendTransactionalEmailService.cs` with retry-on-transient-failure (Polly) per research §4
- [ ] T096 [P] [US3] MJML email templates at `src/EgyptTax.Portal.Infrastructure/Email/Templates/` compiled to HTML at build: `signup-confirmation`, `payment-receipt`, `refund-confirmation`, `trial-ending-T-7`, `trial-ending-T-1`, `renewal-failed`
- [ ] T097 [P] [US3] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Account/Signup.razor` + `EmailConfirm.razor`
- [ ] T098 [P] [US3] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Dashboard.razor` showing subscription status + active licences + next renewal + open tickets + recent downloads (FR-012)
- [ ] T099 [P] [US3] Create Blazor pages `src/EgyptTax.Portal.Web/Portal/Pages/Subscription/Index.razor` + `Upgrade.razor` + `Downgrade.razor` + `ConvertTrial.razor`
- [ ] T100 [P] [US3] Create Blazor pages `src/EgyptTax.Portal.Web/Portal/Pages/Billing/Invoices.razor` (list + PDF download + `RefundEligibility` badge) + `PaymentMethods.razor` + `Refund.razor`
- [ ] T101 [P] [US3] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Downloads.razor` (authenticated, ties to active Subscription's tier — Solo doesn't see Enterprise downloads)
- [ ] T102 [P] [US3] Resource files (ar + en) at `src/EgyptTax.Portal.Web/Resources/Pages/Portal/` for all US3 UI strings
- [ ] T103 [P] [US3] Add a "Subscribe to keep going" CTA on the dashboard when the customer has NO active Subscription (the FR-029 + FR-030 trial-ended UX)
- [ ] T153 [P] [US3] Implement `CancelSubscriptionHandler` at `src/EgyptTax.Portal.Application/Subscriptions/CancelSubscriptionHandler.cs` per data-model.md §3 lifecycle (`[Active] → [Active until CurrentPeriodEndUtc] → renewal-job → [Cancelled]`). Sets `Subscription.CancelledAtUtc = now`, keeps `Status = Active` until the period ends, then the renewal job flips it. Issues a confirmation email via the `subscription-cancelled` MJML template. Owner-only; writes a `subscription.cancelled` audit-log entry (FR-013 action button). Also add a `Subscription/Cancel.razor` Blazor page with confirmation + "keep going" reverse-CTA.

**Checkpoint**: A new prospect can complete the full SC-002 < 15-minute signup→pay→download flow end-to-end. Combined with US1 (marketing surface) and US2 (licence transfer self-service), this is the shippable P1 MVP.

---

## Phase 6: User Story 4 - Support tickets (Priority: P2)

**Goal**: Customer submits a ticket from the portal with category + priority + description + attachments (up to 3 files × 5 MB), sees the SLA badge for their tier (24h Solo/SMB, 4h Enterprise/Firm), and receives email notifications as vendor staff replies.

**Independent Test**: Sign in, create new ticket with all fields populated + a 2 MB screenshot attached. Confirm: ticket appears in customer's list with status `Open` + correct SLA badge, vendor-staff view shows the attachment + customer's tier badge.

### Tests for User Story 4

- [ ] T104 [P] [US4] Integration test at `tests/EgyptTax.Portal.IntegrationTests/Flows/SupportTicketLifecycleTests.cs` (create → reply → resolve → re-open transitions)
- [ ] T105 [P] [US4] Integration test at `tests/EgyptTax.Portal.IntegrationTests/Flows/SupportTicketAttachmentLimitsTests.cs` (3-file cap, 5-MB cap, MIME-type whitelist enforcement)
- [ ] T106 [P] [US4] Unit test for `SlaCalculator` at `tests/EgyptTax.Portal.UnitTests/Support/SlaCalculatorTests.cs` (24h vs 4h based on Subscription.Tier; business-hours-only counting per FR-019)

### Implementation for User Story 4

- [ ] T107 [P] [US4] Create `SupportTicket` + `SupportTicketReply` + `SupportTicketAttachment` entities + configurations + migration per data-model.md §6
- [ ] T108 [P] [US4] Implement `CreateTicketHandler` at `src/EgyptTax.Portal.Application/Support/CreateTicketHandler.cs` (FR-018 + MIME whitelist + 5-MB cap + 3-file cap + total 15-MB cap)
- [ ] T109 [P] [US4] Implement `ReplyTicketHandler` at `src/EgyptTax.Portal.Application/Support/ReplyTicketHandler.cs`
- [ ] T110 [P] [US4] Implement `SlaCalculator` at `src/EgyptTax.Portal.Application/Support/SlaCalculator.cs` (FR-019 — reads Subscription.Tier, computes business-hours-bounded deadline)
- [ ] T111 [US4] Map `POST /api/v1/portal/support/tickets` + `POST /api/v1/portal/support/tickets/{id}/replies` + `GET /api/v1/portal/support/attachments/{id}` endpoints in `Program.cs`
- [ ] T112 [P] [US4] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Support/List.razor` (ticket list with status + SLA badge per ticket)
- [ ] T113 [P] [US4] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Support/New.razor` (form: category + priority + description + 3-file upload; priority=High disabled for Solo per FR-018)
- [ ] T114 [P] [US4] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Support/Detail.razor` (thread view with vendor replies inline, reply form at the bottom)
- [ ] T115 [P] [US4] MJML email templates: `ticket-created`, `ticket-replied`, `ticket-resolved`
- [ ] T116 [P] [US4] Resource files (ar + en) for all US4 UI strings + the "Priority support" badge copy from FR-019

**Checkpoint**: Customer + vendor staff have a complete ticket-lifecycle UX. US4 is independently functional and integrates cleanly with US1–US3.

---

## Phase 7: User Story 5 - Multi-user invitations (Priority: P2)

**Goal**: Owner invites bookkeepers + billing admins by email with per-member roles (Owner, BillingAdmin, SupportAdmin, ReadOnly). Invitee receives email with single-use 7-day-expiry link, sets password, lands in the same organisation as the owner. Invited member can move from clicking the link to seeing the dashboard in under 3 minutes (SC-007).

**Independent Test**: Sign in as Owner, invite a fresh email as `BillingAdmin`. From a clean browser session, click the invitation link, set password, accept. Confirm: invitee sees Billing pages but cannot transfer licences (owner-only action — UI hides the button + API returns 403 if URL-tampered).

### Tests for User Story 5

- [ ] T117 [P] [US5] Contract test at `tests/EgyptTax.Portal.IntegrationTests/Contracts/InviteMemberEndpointTests.cs` — 12 assertions per [contracts/invite-member.md](./contracts/invite-member.md) including token-hashing-at-rest + 7-day expiry + single-use semantics
- [ ] T118 [P] [US5] Integration test at `tests/EgyptTax.Portal.IntegrationTests/Flows/MemberRemovalSessionTerminationTests.cs` (FR-022 — removed member's sessions terminated within 5 minutes)
- [ ] T119 [P] [US5] Integration test at `tests/EgyptTax.Portal.IntegrationTests/Flows/AtLeastOneActiveOwnerInvariantTests.cs` (cannot remove the only remaining Owner — invariant guards the org from becoming inaccessible)
- [ ] T120 [P] [US5] Playwright e2e at `tests/EgyptTax.Portal.E2ETests/Portal/MemberInviteFlowTests.cs` asserting SC-007 (invite-click-to-dashboard < 3 min wall-clock)

### Implementation for User Story 5

- [ ] T121 [P] [US5] Create `Invitation` entity + configuration + migration at `src/EgyptTax.Portal.Infrastructure/Persistence/Entities/Invitation.cs` (stores HASH of token, not raw — per contracts/invite-member.md test #11)
- [ ] T122 [P] [US5] Implement `InviteMemberHandler` at `src/EgyptTax.Portal.Application/Organisations/InviteMemberHandler.cs` per [contracts/invite-member.md](./contracts/invite-member.md) Behaviour section
- [ ] T123 [P] [US5] Implement `AcceptInvitationHandler` at `src/EgyptTax.Portal.Application/Organisations/AcceptInvitationHandler.cs` (single-use, marks `AcceptedAtUtc`, creates TeamMember if email isn't already a portal user)
- [ ] T124 [P] [US5] Implement `RemoveMemberHandler` at `src/EgyptTax.Portal.Application/Organisations/RemoveMemberHandler.cs` (FR-022 — flip `RevokedAtUtc`, invalidate active sessions within 5 min via Identity SecurityStamp rotation, refuse if removing the only active Owner)
- [ ] T125 [US5] Map `POST /api/v1/portal/organisations/{id}/invitations` + `POST /api/v1/portal/invitations/accept` + `DELETE /api/v1/portal/organisations/{id}/memberships/{memberId}` endpoints in `Program.cs`
- [ ] T126 [P] [US5] Create authorization policies at `src/EgyptTax.Portal.Web/Identity/PortalAuthorizationPolicies.cs` — one per role × per action (e.g. `OwnerOnly`, `OwnerOrBillingAdmin`, `Authenticated`)
- [ ] T127 [P] [US5] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Organisation/Members.razor` (invite form + member list + role chips + revoke buttons; Owner-only visibility)
- [ ] T128 [P] [US5] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Invitations/Accept.razor` (landing page for invitation links — set password + accept)
- [ ] T129 [P] [US5] Apply role-based visibility checks across all portal pages — e.g. Licences/Transfer.razor hidden for `BillingAdmin` (Owner-only per FR-022 implicit, License management is an Owner concern)
- [ ] T130 [P] [US5] MJML email templates: `organisation-invitation` (bilingual based on inviter's `LocalePreference` fallback), `member-removed`
- [ ] T131 [P] [US5] Resource files (ar + en) for all US5 UI strings

**Checkpoint**: An accounting firm subscribing to Firm tier can invite their team + assign per-member roles. US5 doesn't disturb US1–US4 — a single-user Solo customer never opens this page.

---

## Phase 8: User Story 6 - Privacy URL stable for Play Store (Priority: P2)

**Goal**: `daftarx.app/privacy/android` always returns 200 with the vendor's privacy policy in both Arabic and English. The Android app's Play Store Data Safety form (per feature 009 FR-018) depends on this URL never moving. Any planned URL change MUST be preceded by a Play-listing update.

**Independent Test**: Request `https://daftarx.app/privacy/android` from anywhere on the public internet (no auth). Confirm: 200 response, bilingual policy text, "last updated" date visible, page renders < 3 s.

### Tests for User Story 6

- [ ] T132 [P] [US6] Playwright e2e at `tests/EgyptTax.Portal.E2ETests/Marketing/PrivacyAndroidUrlStabilityTests.cs` asserting SC-004 (URL returns 200 with bilingual content on every deploy)

### Implementation for User Story 6

- [ ] T133 [P] [US6] Create `src/EgyptTax.Portal.Web/Pages/Privacy/Android.cshtml` + `Android.cshtml.cs` rendering the policy at the stable URL `/privacy/android` per FR-006 (separate from `/privacy` index even if content is similar — the URL itself is the contract)
- [ ] T134 [P] [US6] Resource files `Android.ar-EG.resx` + `Android.en-US.resx` with the full policy text covering Crashlytics diagnostics + FCM device tokens per feature 009 FR-018
- [ ] T135 [P] [US6] Add a CI smoke step to `.github/workflows/portal-build.yml` that hits `/privacy/android` on the staging slot after every deploy and fails the build if it doesn't return 200 with the expected content (prevents accidental URL removal)
- [ ] T136 [P] [US6] Implement version archival: when the privacy policy text changes, the previous version is preserved at `/privacy/android/history/{YYYY-MM-DD}` so prior consent claims remain auditable (the resx files become append-only)

**Checkpoint**: The Android app's Play Store listing remains compliant for as long as the website stays up. US6's surface is tiny (one page + one CI check) but the compliance dependency is real.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Performance + accessibility + account lifecycle + audit log viewer + production deployment polish. Most of these touch multiple user stories and benefit from being deferred until the core flows are working.

- [ ] T137 [P] Performance regression test at `tests/EgyptTax.Portal.E2ETests/Performance/MarketingPagesPerformanceTests.cs` asserting SC-006 (3 s p75 render from Cairo simulator) on every CI build
- [ ] T138 [P] Accessibility audit at `tests/EgyptTax.Portal.E2ETests/Accessibility/Wcag21AaComplianceTests.cs` integrating `axe-core` against all marketing pages + portal core flows per FR-027
- [ ] T139 [P] Implement security pages at `src/EgyptTax.Portal.Web/Portal/Pages/Account/Security.razor` — MFA setup (TOTP per FR-011), active sessions list, auth-events log retained 90 days per FR-028
- [ ] T140 [P] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Account/Delete.razor` (FR-024 — request account deletion with 30-day soft-delete window, 30-day cancellation grace period)
- [ ] T141 [P] Implement `DeleteAccountHandler` at `src/EgyptTax.Portal.Application/Organisations/DeleteAccountHandler.cs` (sets `SoftDeletedAtUtc` on the CustomerOrganisation + cascade-flag children per data-model.md cross-entity rules)
- [ ] T142 [P] Implement nightly purge job at `src/EgyptTax.Portal.Infrastructure/Jobs/PurgeSoftDeletedAccountsJob.cs` using Hangfire (already in the on-prem product per research §15) — hard-deletes accounts past 30-day window, RETAINS audit-log rows per data-model.md §8 retention rule
- [ ] T143 [P] Create Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Organisation/AuditLog.razor` — paginated, filterable, Owner-only audit-log viewer per FR-023
- [ ] T144 [P] Cloudflare cache purge integration in CI: on every deploy, hit Cloudflare's purge API to invalidate marketing-route cache per research §9; document the API token rotation procedure
- [ ] T145 [P] Production environment variables documentation at `deploy/portal/README.md` (Paymob credentials, Resend API key, Azure Blob connection string, HMAC secret, Cloudflare API token, vendor-keys.json mount path)
- [ ] T146 [P] Status page setup at `status.daftarx.app` + UptimeRobot pings from 3 geographies (Cairo, Frankfurt, US-East) every 5 min per research §15
- [ ] T147 [P] Implement rate limiting on the public signup + contact-form endpoints (5/hour/IP for signup, 10/hour/IP for contact form) using `Microsoft.AspNetCore.RateLimiting`
- [ ] T148 [P] Implement bilingual error pages at `src/EgyptTax.Portal.Web/Pages/Error/` (404, 500, 503) — fail-friendly per FR-015 even at the platform level
- [ ] T149 [P] Update `CLAUDE.md` `<!-- SPECKIT START -->` block to reference `010-website-portal/tasks.md` so future agents resume mid-implementation correctly
- [ ] T150 [P] Document the new portal endpoints in the existing `008-egypt-tax-accounting` + `009-android-app` quickstarts so the on-prem + mobile teams know about the shared `LicenceSigningService` reuse + portal-issued tokens
- [ ] T151 [P] Add `_ViewImports.cshtml` to import the `IStringLocalizer` namespace globally so every Razor page + Blazor component can call `@Loc["..."]` without explicit using directives
- [ ] T152 [P] Add `appsettings.Production.json` template at `src/EgyptTax.Portal.Web/appsettings.Production.json.template` with placeholders for every production secret (committed as `.template`; actual file gitignored)
- [ ] T154 [P] FR-017 version-history surface: create `DownloadArtifactVersion` entity at `src/EgyptTax.Portal.Infrastructure/Persistence/Entities/DownloadArtifactVersion.cs` (fields: `Id`, `ArtifactKind ∈ {DesktopInstaller, LanClientInstaller, AndroidApk}`, `VersionString`, `ReleasedAtUtc`, `BlobUri`, `Sha256Checksum`, `RetiredAtUtc`) + configuration + migration. Update `Portal/Pages/Downloads.razor` (T101) to render a "prior versions" dropdown per artefact showing the most recent 3 non-retired rows where `ReleasedAtUtc < latest.ReleasedAtUtc`. Owners download any of them; analytics events written via `IAuditLogWriter` (`download.rollback`)
- [ ] T155 [P] FR-011 organisation-level MFA enforcement: add `RequiresMfaForOwners` (bit, default false) to `CustomerOrganisation` (migration); add Owner-only Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Organisation/SecurityPolicy.razor` to toggle it; add middleware `src/EgyptTax.Portal.Web/Middleware/OrganisationMfaPolicyMiddleware.cs` that — when an Owner signs in to an organisation with `RequiresMfaForOwners = true` and their `TeamMember.MfaEnabled = false` — redirects to `Account/Security` with an `mfa_required` flash message and refuses to authorise any other route until TOTP is enrolled. Unit test the middleware against the four matrix cases (owner-on/off × policy-on/off)
- [ ] T156 [P] SC-005 SLA measurement: add `FirstReplyAtUtc` (datetime2, nullable) to `SupportTicket` entity + migration (set on the first vendor-staff reply per `ReplyTicketHandler`); create nightly Hangfire rollup job `src/EgyptTax.Portal.Infrastructure/Jobs/ComputeSupportSlaRollupJob.cs` that writes a daily row to a new `support_sla_rollups` table (date, total_tickets, replies_within_sla, percent_within_sla, by_tier breakdown); create vendor-only Blazor page `src/EgyptTax.Portal.Web/Portal/Pages/Ops/SlaDashboard.razor` (guarded by a `VendorStaff` policy distinct from CustomerOrganisation Owner) rendering a 30-day chart so the team knows when SC-005 is being missed and by which tier

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
- **US3 (Signup → Pay → Download, P1)**: Foundational + Subscription/Licence/Invoice/SalesLead entities. Shares the Subscription/Licence entities with US2 — to land in parallel, the team coordinates a single migration that introduces both at once (T060 + T061 + T078 + T079 → one combined migration).
- **US4 (Support tickets, P2)**: Foundational only. Independent of US1–US3 (a customer who signed up via US3 manages tickets here; the entities are isolated).
- **US5 (Multi-user invitations, P2)**: Foundational + the OrganisationMembership join (already in Phase 2 — T023). Independent of US3 (single-Owner organisations created by US3 just have one membership row).
- **US6 (Privacy URL, P2)**: Independent of every other story. Could land first if needed for Play Store compliance ahead of feature 009 shipping.

### Within Each User Story

- Tests (test-first per Constitution III) **MUST** be written and observed failing before implementation.
- Within the test group, tasks marked `[P]` can run in parallel (different test files).
- Implementation order: entities → handlers → endpoints → UI screens → wiring → resource files.
- Each story is complete only when its independent-test scenario passes (Playwright e2e for visible flows, integration tests for backend flows).

### Parallel Opportunities

- All Setup tasks marked `[P]` (T002–T003, T005–T016) can run in parallel after T001 + T004 (which set up the .NET projects + solution file).
- All Foundational tasks marked `[P]` (T018–T034) can run in parallel after T017 (DbContext) lands.
- Once Foundational is done, the six user stories can proceed in parallel.
- Within each story, all `[P]` tests can run in parallel; all `[P]` implementations can run in parallel.
- Polish tasks are mostly all `[P]` since they touch independent files.

---

## Parallel Example: User Story 3

```bash
# Launch US3 tests in parallel (different files):
Task: "T070 [P] [US3] Contract test for POST /api/v1/portal/signup in tests/EgyptTax.Portal.IntegrationTests/Contracts/SignupEndpointTests.cs"
Task: "T071 [P] [US3] Contract test for POST /api/v1/portal/payments/webhook in tests/EgyptTax.Portal.IntegrationTests/Contracts/PaymentWebhookEndpointTests.cs"
Task: "T072 [P] [US3] Integration test for trial-conversion flow in tests/EgyptTax.Portal.IntegrationTests/Flows/TrialConversionFlowTests.cs"
Task: "T076 [P] [US3] Playwright e2e for signup-to-download in tests/EgyptTax.Portal.E2ETests/Portal/SignupToDownloadFlowTests.cs"

# Launch US3 entity work in parallel (different files):
Task: "T078 [P] [US3] Create Invoice entity at src/EgyptTax.Portal.Infrastructure/Persistence/Entities/Invoice.cs"
Task: "T079 [P] [US3] Create SalesLead entity at src/EgyptTax.Portal.Infrastructure/Persistence/Entities/SalesLead.cs"

# Launch US3 handlers in parallel (different files):
Task: "T084 [P] [US3] Implement ConvertTrialToPaidHandler in src/EgyptTax.Portal.Application/Subscriptions/ConvertTrialToPaidHandler.cs"
Task: "T085 [P] [US3] Implement UpgradeTierHandler in src/EgyptTax.Portal.Application/Subscriptions/UpgradeTierHandler.cs"
Task: "T087 [P] [US3] Implement RefundFirstPeriodHandler in src/EgyptTax.Portal.Application/Subscriptions/RefundFirstPeriodHandler.cs"
Task: "T088 [P] [US3] Implement PaymobHttpClient in src/EgyptTax.Portal.Infrastructure/Payments/PaymobHttpClient.cs"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 + 3)

1. Complete Phase 1: Setup (T001–T016).
2. Complete Phase 2: Foundational (T017–T036) — **CRITICAL**, blocks every story.
3. Complete Phase 3: User Story 1 — Marketing surface (T037–T055).
4. Complete Phase 4: User Story 2 — Licence self-service (T056–T069).
5. Complete Phase 5: User Story 3 — Signup → Pay → Download (T070–T103).
6. **STOP and VALIDATE**: Run `tests/EgyptTax.Portal.E2ETests/Marketing/*` + `tests/EgyptTax.Portal.E2ETests/Portal/SignupToDownloadFlowTests.cs` + `LicenceTransferFlowTests.cs`. Demo to stakeholders.
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
   - **Dev A**: US1 (Marketing) — front-end-heavy, mostly Razor + Tailwind.
   - **Dev B**: US2 + US3 (Licence + Signup → Pay → Download) — backend-heavy, the integration-rich path.
   - **Dev C**: US4 + US5 (Support + Multi-user) — Blazor-heavy with simpler backends.
3. US6 (Privacy URL) is a 1-day task for any free developer.
4. Polish phase shared by all three.

---

## Notes

- `[P]` tasks = different files, no dependencies — safe for parallel execution.
- `[Story]` label maps each task to a specific user story for traceability with `spec.md`.
- Each user story is independently completable and testable; stopping after any story's checkpoint yields working, demonstrable value.
- Verify tests fail before implementing (Constitution III).
- Commit after each task or each tight logical group; never commit a failing build to main.
- The portal NEVER stores trial state (FR-030); CreateTrialSubscriptionHandler is analytics-only.
- The portal NEVER calls the customer's on-prem server for auth (FR-032); identity stores are physically separate databases.
- The portal NEVER introduces a new activation endpoint (FR-031); paid licences use the existing manual HWID-copy flow.
- The portal NEVER issues a mid-period refund (FR-034); only first-period FirstPeriod invoices within 7 days are refundable.
- Cross-cutting items integrated into the right stories so no separate "FR-029 task" / "FR-031 task" / etc. is needed:
  - **FR-006** (privacy URL) → US6 (T133–T136) + the vendor-wide `/privacy` in US1 (T046).
  - **FR-011** (TOTP MFA, per-user + org-policy enforcement) → Foundational (T020 TotpMfaService) + Polish (T139 Security page for per-user, T155 OrganisationMfaPolicyMiddleware for org-level "require MFA for Owners").
  - **FR-013** (Owner licence actions: Download / Transfer / Renew / Cancel) → US2 (T066 List, T067 Activate, T068 Transfer) + US3 (T153 CancelSubscriptionHandler + Subscription/Cancel.razor for the Cancel action).
  - **FR-014** (licence transfer) → US2 (T064 — handler + the 2-audit-row write).
  - **FR-015** (4 payment methods) → US3 (T089 Paymob adapter + T088 HTTP client).
  - **FR-016** (Arabic PDF invoices) → US3 (T093 QuestPDF template).
  - **FR-017** (downloads page + 3 prior versions for rollback) → US3 (T101 Downloads.razor for latest) + Polish (T154 DownloadArtifactVersion entity + prior-3 dropdown per artefact).
  - **FR-018** (Play Data Safety dependency) → US6 (T133 + T134 publish the policy).
  - **FR-019** (SLA badge) → US4 (T110 SlaCalculator + the badge rendering in T112/T113) + US2 (T066 surfaces the Priority-support badge on the Licences/List for Enterprise/Firm per US2 AS#3).
  - **FR-020** (invitations) → US5 (T122 InviteMember + T123 Accept).
  - **FR-021** (organisation scoping) → Foundational (T029 middleware).
  - **FR-022** (member removal session kill) → US5 (T124 RemoveMember).
  - **FR-023** (audit log) → Foundational (T027 writer) + Polish (T143 viewer).
  - **FR-024** (account deletion) → Polish (T140 page + T141 handler + T142 purge job).
  - **FR-027** (WCAG accessibility) → Polish (T138 axe-core integration).
  - **FR-028** (auth-event logging) → Polish (T139 Security page).
  - **FR-029** (single-tap trial-to-paid) → US3 (T084 ConvertTrialToPaidHandler + T103 dashboard CTA).
  - **FR-030** (no portal-side trial) → Foundational (Subscription entity in T060 has NO `Trial` status; CreateTrialSubscriptionHandler in T083 is analytics-only).
  - **FR-031** (manual install-first activation) → US2 (T063 ActivatePaidLicenceHandler + T067 ActivatePaid.razor).
  - **FR-032** (identity separation) → Foundational (T018 PortalUser + T021 separate cookie name + T029 OrganisationScope middleware).
  - **FR-033** (tier upgrade instant proration / downgrade at renewal) → US3 (T085 UpgradeTierHandler + T086 ScheduleDowngradeHandler).
  - **FR-034** (7-day first-period refund) → US3 (T087 RefundFirstPeriodHandler + the computed `RefundEligibility` on Invoice in T078).
  - **SC-005** (95% of tickets get a vendor reply within 24 business hours) → Polish (T156 FirstReplyAtUtc + nightly rollup job + Ops/SlaDashboard.razor).
