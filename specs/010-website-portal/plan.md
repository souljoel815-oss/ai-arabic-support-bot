# Implementation Plan: DaftarX Website + Customer Portal

**Branch**: `010-website-portal` | **Date**: 2026-05-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/010-website-portal/spec.md`

## Summary

Stand up `daftarx.app` as two surfaces hosted from a single deploy: (1) a fast, SEO-friendly **public marketing site** (homepage, features, pricing, downloads, privacy policy, contact) that prospects browse without auth, and (2) an authenticated **customer portal** behind a `/portal/*` URL prefix where customers manage their commercial relationship — subscriptions, paid licence tokens (signed by the existing `Issue-License.ps1` flow + `vendor-keys.json`), billing, downloads, support tickets, and team-member memberships.

The 5 clarifications from `/speckit-clarify` Session 2026-05-18 sharpen the architecture: trial happens entirely client-side in the on-prem product (FR-029/FR-030 — no portal endpoint), paid activation uses the existing manual HWID-copy flow (FR-031 — no new signing protocol), portal identity is fully separate from the on-prem product's user store (FR-032), tier changes are self-service with proration (FR-033), and refunds are limited to a 7-day first-period window (FR-034).

Both surfaces share a single ASP.NET Core 8 codebase — the marketing pages are server-rendered Razor with output caching for SEO + sub-3 s p75 render targets, the portal is interactive Blazor Server with cookie auth + MFA. The portal's commercial database is a brand-new schema (Customer Organisation / Subscription / Licence / Invoice / Ticket / Sales Lead / Audit Log) deliberately isolated from the existing on-prem `EgyptTax` accounting database — the two share git history but no runtime data.

## Technical Context

**Language/Version**: C# 12 on .NET 8.0 LTS (matches the existing `src/EgyptTax.Web` Blazor Server stack — same SDK, same hosting model, same developer skill set). Razor Pages for marketing, Blazor Server for the portal.
**Primary Dependencies**:
- *Server*: ASP.NET Core 8 (Razor Pages + Blazor Server), Entity Framework Core 8 (SQL Server provider matching the existing infrastructure), Serilog with the same sinks as `EgyptTax.Web`, AspNetCore.Identity for Team Member identity (password + TOTP MFA), DataProtection for cookie + secret protection, AspNetCore.Localization for the ar-EG / en-US split.
- *Reuse from monorepo*: the existing `EgyptTax.Web.Tools.LicenseIssueHost` flow (today a CLI verb on `EgyptTax.Web.exe`) is wrapped as an injectable service so the portal calls it in-process — same Ed25519 keypair, same signed-token format, customers' existing on-prem `LicenseVerifier` accepts portal-issued tokens unchanged.
- *Payments*: Paymob hosted-checkout for card + Fawry + Vodafone Cash + InstaPay (single Egyptian aggregator covers all four required methods per FR-015 + research §3). Bank transfer is manual reconciliation with a vendor-only admin tool.
- *Email*: Resend.com transactional API (the simplest reliable provider with Egyptian deliverability) for invitations, payment receipts, ticket notifications, trial-ending reminders. Templates rendered server-side as MJML.
- *PDF receipts*: QuestPDF (royalty-free for revenue < $1M/yr, MIT-equivalent licence) generates the Arabic-RTL invoice PDFs required by FR-016.
- *Frontend assets*: Tailwind CSS (v4, CLI-only — no Node runtime needed at build time) for marketing styling; Material 3 + minimal custom CSS for the portal. No JavaScript framework — Blazor Server handles the interactive bits.
**Storage**:
- *Portal DB*: new SQL Server database `DaftarXPortal` (development override allows SQLite via `appsettings.Development.json` for fast local iteration). Holds the 8 commercial entities documented in `data-model.md`. Schema lives in `src/EgyptTax.Portal.Infrastructure/Migrations/`.
- *No overlap* with the existing on-prem `EgyptTax` accounting database — portal queries are commercial-only (subscriptions, licences, invoices, tickets), accounting data stays on the customer's own server.
- *Blob storage* for invoice PDFs + ticket attachments: Azure Blob Storage or S3-compatible bucket (research §6 narrows). Local-disk fallback during dev.
**Testing**: xUnit + FluentAssertions for unit + integration (mirrors `EgyptTax.UnitTests`). Playwright in C# bindings for the marketing-surface end-to-end + portal critical flows (signup → trial-conversion → licence transfer). bUnit for Blazor Server component tests where they're cheaper than Playwright. Contract tests for the 5 contracts in `contracts/` are xUnit cases against `WebApplicationFactory<Program>`.
**Target Platform**: Linux server (Ubuntu 22.04 LTS via Docker), hosted in a single Cairo-or-Frankfurt data centre (sub-3 s render target from Cairo broadband per FR-025 + SC-006). Cloudflare in front for CDN edge caching of marketing pages + DDoS protection.
**Project Type**: Web application (two surfaces hosted from one ASP.NET Core process, OR split into two if Cloudflare edge-cache rules become awkward — research §1 settles this).
**Performance Goals**: SC-006 — every marketing page renders in under 3 s at p75 from a Cairo broadband connection on a mid-range device. SC-002 — signup → tier select → payment → token download → installer download in under 15 min. SC-003 — licence transfer in under 5 min. Cloudflare caching gives marketing pages effectively-zero TTFB after first hit.
**Constraints**: EGP-only billing in v1 (FR-015). Single-region hosting in v1. Privacy policy URL `daftarx.app/privacy/android` is a stable contract with the Android app's Play Store data-safety form (FR-006 + SC-004) — any URL change must be coordinated with feature 009's release. Trial state is OWNED by the on-prem product, NEVER tracked by the portal (FR-030). Portal identity is fully separate from on-prem product identity (FR-032) — no SSO, no cross-system password sync.
**Scale/Scope**: Year-1 target is 200 paying customers with ≤ 10 Team Members each → ≤ 2,000 Team Member rows, ≤ 2,000 Subscriptions, ≤ 4,000 Licences (each Subscription has 1-3 licences for the LAN + multi-device cases), ≤ 24,000 Invoices/year (monthly cadence dominates), ≤ 6,000 Support Tickets/year. Single-region SQL Server with daily backups is enough. Surface is ~15 marketing pages + ~12 portal screens (Dashboard, Licences, Subscription, Billing, Downloads, Support, Settings, Members, Security, Activate-paid, Transfer-licence, Account-deletion).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Spec-First Development** | ✅ Pass | `spec.md` complete with 34 functional requirements (28 original + 6 from `/speckit-clarify` Session 2026-05-18), 6 prioritised user stories, 9 measurable success criteria, 8 key entities, 0 `[NEEDS CLARIFICATION]` markers. All ambiguity resolved into the Clarifications section + Assumptions. |
| **II. Plan Before Code** | ✅ Pass | This plan documents language, dependencies, storage, testing, target platform, project structure, and explicit constitutional reuse points (the on-prem licence-signing flow). No code may be written until tasks are generated from this plan. |
| **III. Test-First Discipline** | ✅ Pass (commitment) | Contract tests for the 5 portal endpoints (signup, activate-paid-licence, transfer-licence, payment-webhook, invite-member) and integration tests for every cross-boundary flow (Paymob hosted-checkout round-trip, Resend transactional email round-trip, in-process licence-signing call, blob-storage attachment upload, Cloudflare cache invalidation hook) MUST be written and observed failing before their production code, recorded in `tasks.md` per `/speckit-tasks`. |
| **IV. Simplicity & YAGNI** | ✅ Pass | Two surfaces in one process is the simplest architecture that satisfies the spec — separating them into two services would add a hop without solving any concrete problem. The clarifications explicitly reduced complexity: no SSO (FR-032), no new activation endpoint (FR-031), no portal-side trial tracking (FR-030), no mid-period refund logic (FR-034). The one reused dependency (the in-process licence-signing call) is the simplest way to honour the spec's requirement to keep the existing token format. No Complexity Tracking entries needed. |
| **V. Incremental, Independently Testable Delivery** | ✅ Pass | The 6 user stories decompose into 3 P1 (Marketing, Licence self-service, Signup→Pay→Download) + 3 P2 (Support, Multi-user, Privacy URL). P1 stories together are the MVP that lets the vendor start taking real money. P2 stories layer orthogonally — none of them blocks any of the others. |

**Re-check after Phase 1 design**: see [Post-Design Constitution Check](#post-design-constitution-check) below.

## Project Structure

### Documentation (this feature)

```text
specs/010-website-portal/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── signup.md                # POST /api/v1/portal/signup
│   ├── activate-paid-licence.md # POST /api/v1/portal/licences/activate
│   ├── transfer-licence.md      # POST /api/v1/portal/licences/{id}/transfer
│   ├── payment-webhook.md       # POST /api/v1/portal/payments/webhook (Paymob callback)
│   └── invite-member.md         # POST /api/v1/portal/organisations/{id}/invitations
├── checklists/
│   └── requirements.md  # spec-quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/EgyptTax.Portal.Web/                          # NEW: Razor Pages (marketing) + Blazor Server (portal)
├── Pages/                                        # Razor Pages — marketing surface
│   ├── Index.cshtml                              # Homepage (/, /ar, /en)
│   ├── Features.cshtml                           # /features
│   ├── Pricing.cshtml                            # /pricing
│   ├── Downloads.cshtml                          # /downloads — MSIs + Play Store + side-load APK
│   ├── About.cshtml
│   ├── Contact.cshtml                            # creates a Sales Lead
│   ├── Privacy/
│   │   ├── Index.cshtml                          # /privacy (vendor-wide)
│   │   └── Android.cshtml                        # /privacy/android (stable Play Store URL)
│   ├── Terms.cshtml
│   ├── Refund.cshtml
│   └── _ViewImports.cshtml
├── Portal/                                       # Blazor Server (authenticated)
│   ├── App.razor
│   ├── Pages/
│   │   ├── Dashboard.razor
│   │   ├── Licences/
│   │   │   ├── List.razor
│   │   │   ├── ActivatePaid.razor                # paste HWID → download token
│   │   │   └── Transfer.razor                    # retire old HWID, sign new
│   │   ├── Subscription/
│   │   │   ├── Index.razor
│   │   │   ├── Upgrade.razor                     # FR-033 instant proration
│   │   │   └── Downgrade.razor                   # FR-033 scheduled at renewal
│   │   ├── Billing/
│   │   │   ├── Invoices.razor                    # list + PDF download
│   │   │   └── PaymentMethods.razor
│   │   ├── Downloads.razor                       # latest MSI/APK by tier
│   │   ├── Support/
│   │   │   ├── List.razor
│   │   │   ├── New.razor
│   │   │   └── Detail.razor
│   │   ├── Organisation/
│   │   │   ├── Members.razor                     # invite + role
│   │   │   └── AuditLog.razor
│   │   ├── Account/
│   │   │   ├── Security.razor                    # MFA, sessions, auth-events log
│   │   │   └── Delete.razor                      # 30-day soft-delete
│   │   └── ConvertTrial.razor                    # FR-029 "Subscribe to keep going"
│   └── Shared/
├── Identity/                                     # AspNetCore.Identity, separate store per FR-032
│   ├── PortalUser.cs                             # NOT shared with on-prem product
│   ├── PortalRole.cs
│   └── TotpMfaService.cs
├── Localization/                                 # ar-EG / en-US per FR-008
├── wwwroot/
│   ├── css/
│   ├── images/
│   └── _assets/
├── Program.cs                                    # ASP.NET Core composition root
├── appsettings.json
└── EgyptTax.Portal.Web.csproj

src/EgyptTax.Portal.Application/                  # NEW: command/query handlers
├── Subscriptions/
│   ├── CreateTrialSubscriptionHandler.cs         # invoked by Signup
│   ├── ConvertTrialToPaidHandler.cs              # FR-029
│   ├── UpgradeTierHandler.cs                     # FR-033 instant proration
│   ├── ScheduleDowngradeHandler.cs               # FR-033 pending until renewal
│   └── RefundFirstPeriodHandler.cs               # FR-034 7-day window
├── Licences/
│   ├── ActivatePaidLicenceHandler.cs             # FR-031 manual HWID input
│   ├── TransferLicenceHandler.cs                 # FR-014 retire + reissue
│   ├── LicenceSigningService.cs                  # wraps the existing LicenseIssueHost
│   └── HwidValidator.cs                          # rejects malformed + cross-customer collisions
├── Payments/
│   ├── PaymobAdapter.cs                          # four Egyptian methods + card
│   ├── PaymentWebhookHandler.cs                  # idempotent state machine
│   └── ManualBankTransferReconciler.cs           # vendor admin tool
├── Support/
│   ├── CreateTicketHandler.cs                    # 3 attachments, 5 MB each
│   ├── ReplyTicketHandler.cs
│   └── SlaCalculator.cs                          # FR-019 tier-based SLA
├── Organisations/
│   ├── InviteMemberHandler.cs                    # FR-020 7-day token
│   ├── RemoveMemberHandler.cs                    # FR-022 5-min session kill
│   └── DeleteAccountHandler.cs                   # FR-024 30-day soft-delete
├── Audit/
│   └── AuditLogWriter.cs                         # FR-023
└── EgyptTax.Portal.Application.csproj

src/EgyptTax.Portal.Infrastructure/               # NEW: persistence + outbound integrations
├── Persistence/
│   ├── PortalDbContext.cs                        # SQL Server (SQLite override for dev)
│   ├── Entities/                                 # 8 entities from data-model.md
│   │   ├── CustomerOrganisation.cs
│   │   ├── TeamMember.cs
│   │   ├── Subscription.cs
│   │   ├── Licence.cs
│   │   ├── Invoice.cs
│   │   ├── SupportTicket.cs
│   │   ├── SalesLead.cs
│   │   └── AuditLogEntry.cs
│   └── Configurations/                           # IEntityTypeConfiguration<> per entity
├── Migrations/                                   # EF Core migrations (00_Initial + per-feature)
├── Email/
│   ├── ResendTransactionalEmailService.cs        # invitations, receipts, notifications
│   └── Templates/                                # MJML → HTML
├── Payments/
│   └── PaymobHttpClient.cs                       # raw HTTPS adapter
├── Storage/
│   └── BlobAttachmentStore.cs                    # Azure Blob or S3-compatible
└── EgyptTax.Portal.Infrastructure.csproj

src/EgyptTax.Web.Tools/                           # EXISTING: hosts LicenseIssueHost.cs
└── LicenseIssueHost.cs                           # exists from feature 008; portal injects this

tests/EgyptTax.Portal.UnitTests/                  # NEW
├── Subscriptions/
├── Licences/
├── Payments/
├── Support/
└── Organisations/

tests/EgyptTax.Portal.IntegrationTests/           # NEW — WebApplicationFactory<Program>
├── Contracts/
│   ├── SignupEndpointTests.cs
│   ├── ActivatePaidLicenceEndpointTests.cs
│   ├── TransferLicenceEndpointTests.cs
│   ├── PaymentWebhookEndpointTests.cs
│   └── InviteMemberEndpointTests.cs
└── Flows/
    ├── TrialConversionFlowTests.cs
    ├── TierUpgradeProrationTests.cs
    └── RefundWindowTests.cs

tests/EgyptTax.Portal.E2ETests/                   # NEW — Playwright in C#
├── Marketing/
│   ├── HomepageRenderTests.cs                    # SC-006 p75 < 3 s
│   ├── PricingNavigationTests.cs                 # SC-001 ≤ 3 clicks
│   └── PrivacyAndroidUrlStabilityTests.cs        # SC-004
└── Portal/
    ├── SignupToDownloadFlowTests.cs              # SC-002 < 15 min
    ├── LicenceTransferFlowTests.cs               # SC-003 < 5 min
    └── MemberInviteFlowTests.cs                  # SC-007 < 3 min

deploy/portal/                                    # NEW — deployment artifacts
├── Dockerfile                                    # multi-stage .NET 8 build → distroless runtime
├── docker-compose.yml                            # local stack: app + SQL Server + Azurite blob
├── cloudflare/
│   └── cache-rules.json                          # marketing cached 1 h; portal never cached
└── README.md                                     # ops runbook
```

**Structure Decision**: Three new .NET projects (`EgyptTax.Portal.Web` + `EgyptTax.Portal.Application` + `EgyptTax.Portal.Infrastructure`) mirroring the existing `EgyptTax.Web` / `EgyptTax.Application` / `EgyptTax.Infrastructure` layering. The portal projects sit alongside the on-prem product's projects in the same solution but reference NOTHING from `EgyptTax.Web`'s own domain — they only call into `EgyptTax.Web.Tools.LicenseIssueHost` (the existing licence signer). This isolation keeps the portal deployable independently of the on-prem product and prevents accidental coupling between commercial relationship data (portal) and customer accounting data (on-prem). Marketing pages + portal pages share one ASP.NET Core process per research §1 — the second surface is a Blazor Server area mounted under `/portal/*` with cookie auth required.

## Complexity Tracking

> No constitutional violations to record. The clarifications in Session 2026-05-18 explicitly REDUCED complexity at five separate points:
>
> - FR-030: no portal-side trial tracking — the entire trial state machine collapses to "doesn't exist in the portal database".
> - FR-031: no new activation endpoint — the existing manual HWID-copy flow is reused exactly.
> - FR-032: no SSO / no cross-system password sync — saves an entire identity-federation subsystem.
> - FR-034: no mid-period refund logic — saves a proration-credit calculator on the refund path.
> - FR-033 downgrade-at-renewal: no immediate proration on downgrade — saves a refund-on-downgrade calculator.
>
> The one place where reasonable people might argue for added complexity is the two-surfaces-in-one-process decision (some teams would split marketing into a static-site-generator + portal into a separate ASP.NET Core app). That decision is research §1 — the simpler unified-process approach won unless / until concrete edge-cache rules force a split.

## Post-Design Constitution Check

*Re-evaluated after Phase 1 design completed (see `data-model.md`, `contracts/`, `quickstart.md`).*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Spec-First Development** | ✅ Pass | Phase 1 artifacts cite `spec.md` (34 FRs) for every entity, contract, and quickstart step. No new requirements introduced during design. |
| **II. Plan Before Code** | ✅ Pass | Plan, data-model, and contracts are the complete pre-code package. `/speckit-tasks` consumes these next. |
| **III. Test-First Discipline** | ✅ Pass | Each contract in `contracts/` enumerates explicit assertions that become failing tests before the endpoint's production code lands. Integration test directory pre-allocated under `tests/EgyptTax.Portal.IntegrationTests/Contracts/` with one file per contract. |
| **IV. Simplicity & YAGNI** | ✅ Pass | Data model is 8 entities (vs the spec's 8 key entities — 1:1 mapping, no speculative extras). One new database, one new ASP.NET Core process, one wrap around the existing licence signer. No abstractions for "future iOS portal" or "future multi-currency". |
| **V. Incremental, Independently Testable Delivery** | ✅ Pass | Quickstart documents the P1 MVP slice (marketing pages live + signup + trial-conversion + manual paid activation + downloads) end-to-end without any P2 dependencies. The P2 stories (Support, Multi-user, Privacy URL stability) add tables + screens that don't disturb the P1 surface. |
