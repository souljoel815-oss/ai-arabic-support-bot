# Implementation Plan: Egyptian Tax Accounting MVP

**Branch**: `008-egypt-tax-accounting` | **Date**: 2026-05-07 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/008-egypt-tax-accounting/spec.md`

## Summary

A production-grade on-premises Egyptian tax-accounting product for small-to-mid Egyptian SMEs (≤25 concurrent users, ≤50,000 documents/year, single-tenant). Delivered in four phases across nine prioritized user stories (US1–US9) covering: sales/purchase invoicing with ETA-aligned eInvoice JSON (mock), bilingual Arabic/English PDFs, deductible/non-deductible expense classification, configurable VAT, automatic balanced double-entry journals, immutable posted documents, append-only SHA-256 hash-chained audit log with tamper-evident integrity checkpoints, configurable approval workflow, fixed-asset depreciation (Phase 4), withholding tax (الخصم والإضافة) lifecycle including Form 41, accountant-firm multi-company portal with on-prem-compatible topology, and a one-click Tax-Inspection Bundle.

The implementation builds on **.NET 8 / C# 12 + Blazor Server + EF Core 8 + SQL Server 2019+ in Clean Architecture**, with five strategic Egypt-First differentiators (Tax Risk Score per document, Active ETA Compliance Dashboard, Tamper-Evident Invoice QR Seal, Monthly Tax Closing Cockpit, Evidence Vault Document 360) layered on top.

## Technical Context

**Language/Version**: C# 12 on .NET 8 LTS (Microsoft long-term support through November 2026 + 18-month extended)
**Primary Dependencies**:
- `Microsoft.AspNetCore.Components.Server` (Blazor Server, RTL-friendly)
- `Microsoft.EntityFrameworkCore.SqlServer` 8.x (data access + migrations)
- `MediatR` 12.x (command/query dispatch + cross-cutting pipeline behaviors)
- `FluentValidation` 11.x (request validation at boundary)
- `QuestPDF` 2024.x (PDF generation with native Arabic + RTL support)
- `QRCoder` 1.x (FR-044 verification-seal QR encoding)
- `Otp.NET` (TOTP MFA per FR-002)
- `Serilog` + Serilog.Sinks.MSSqlServer (structured logging — operational, distinct from FR-028 audit log)
- `Hangfire` 1.8.x with SQL Server storage (durable scheduled jobs: ETA retry queue per FR-036, audit checkpoint emitter per FR-028, supplier-TIN re-validation cron per FR-043)
- `Polly` 8.x (retry/backoff policies on the ETA mock client)
- `CsvHelper` (Smart Migration Assistant CSV import — Near-term)
- `OpenIddict` 5.x (server-side OAuth/OIDC; gated behind a feature flag — used only for the optional Near-term firm-IdP federation enhancement, NOT a vendor IdP)
- `Microsoft.Identity.Client` (only if Near-term Azure AD federation is enabled)
**Storage**:
- SQL Server 2019+ (Standard or Express) for the relational store (operator-installed; for dev, either LocalDB OR a regular Express named instance reached via the Shared Memory protocol — `Server=lpc:.\SQLEXPRESS;...` — works equivalently and avoids needing to start the SQL Browser service or install LocalDB separately)
- Local file system for attachments under `${INSTALL_ROOT}/attachments/{yyyy}/{mm}/{document_id}/...`
- Audit integrity checkpoint stored in operator's choice (FR-028): either dedicated `audit_checkpoint` table on a separate `audit_meta` schema with restricted privileges, OR a write-restricted file under `${INSTALL_ROOT}/audit_checkpoints/`
**Testing**:
- xUnit + FluentAssertions (unit + integration)
- Testcontainers for SQL Server (integration tests with disposable database)
- WireMock.NET (ETA mock-endpoint contract tests + retry-queue tests)
- Playwright (Blazor end-to-end tests including bilingual flows + RTL rendering)
- Schemastore-based JSON Schema validators for contract tests on eInvoice JSON
**Target Platform**:
- Server: Windows Server 2019/2022 or Windows 10 21H2+ (single-machine on-prem)
- Browser: Chromium-based (Edge / Chrome 110+), Firefox 110+, Safari 16+
- Optional Linux server target deferred to Near-term (Blazor Server runs cross-platform, but customer base is overwhelmingly Windows)
**Project Type**: Web application (Blazor Server hosting model — server-rendered with SignalR, no separate frontend project)
**Performance Goals** (anchored on Success Criteria):
- SC-001: New accountant completes end-to-end invoice flow in < 15 minutes
- SC-002: VAT report + taxable income report < 5 s p95 at 5,000 documents under 25 concurrent users; interactive screens < 1 s p95
- SC-006: Zero gaps / zero duplicates in document numbering under 500 concurrent posting attempts
- SC-010: Audit hash-chain verification < 30 s for 1,000,000 entries
- SC-012: ETA Compliance Dashboard identifies all <24h-deadline documents in < 2 s on a 50,000-document database
- SC-013: QR seal offline verification < 1 s/document
- SC-014: Tax-Inspection Bundle for 5,000-document quarter built in < 5 minutes; externally re-verifiable in < 60 s
**Constraints**:
- On-prem deployment only (Round 1 clarification); no vendor-hosted services anywhere on the data path
- Arabic + English bilingual UI with full RTL support; numeric totals in Arabic words on PDFs
- EGP-only currency in MVP
- NTP-synchronized server time (FR-042); operator-visible alert on NTP failure
- TOTP MFA mandatory for Administrator + Approver roles (FR-002)
- ETA integration is **mock-only** in MVP (FR-035); no live ETA endpoint, no signing certificate, no smart card
- Audit log append-only with SHA-256 hash chain + integrity checkpoint (FR-028)
- Posted documents fully immutable (FR-012, FR-027)
**Scale/Scope** (Sizing target — Round 1 clarification):
- Up to 25 concurrent active users
- Up to 50,000 posted documents per fiscal year
- 5 fiscal years online (live database, fully queryable)
- Older years archived, queryable, may run slower
- Single tenant / single company per installation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
| --- | --- | --- |
| **I. Spec-First Development** (NON-NEGOTIABLE) | ✅ PASS | spec.md exists, 5 clarification rounds completed, 0 `[NEEDS CLARIFICATION]` markers remaining |
| **II. Plan Before Code** | ✅ PASS | This document; Constitution Check + Complexity Tracking populated |
| **III. Test-First Discipline** (NON-NEGOTIABLE) | ✅ COMMITTED | Tests are in scope per the user's original input ("tests, and acceptance criteria"). Task generation will enforce Red→Green→Refactor and contract-test-first for every external boundary (ETA eInvoice JSON, REST API surface, Form 41 schema, eInvoice JSON schema, audit-chain verifier protocol). |
| **IV. Simplicity & YAGNI** | ⚠️ JUSTIFIED DEVIATIONS | See Complexity Tracking below. The spec's audit-integrity, multi-company-portal, and 9-user-story scope require specific patterns that exceed the single-project default; each is justified individually. |
| **V. Incremental, Independently Testable Delivery** | ✅ PASS | 9 prioritized user stories (P1–P3) mapped to 4 delivery phases; each story has independent acceptance scenarios; US1 alone is a viable MVP slice (sales invoicing + PDF + mock eInvoice + basic identity); each subsequent story extends rather than blocking value. |

**Re-check after Phase 1 design**: see "Post-Design Constitution Check" at the bottom of this document.

## Project Structure

### Documentation (this feature)

```text
specs/008-egypt-tax-accounting/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── eta-einvoice.schema.json
│   ├── wht-certificate.schema.json
│   ├── form41.schema.json
│   ├── inspection-bundle-manifest.schema.json
│   ├── verification-seal-qr.md
│   ├── audit-chain-verifier.md
│   └── api/
│       └── openapi.yaml
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── EgyptTax.SharedKernel/          # Common primitives: Result<T>, ValueObject base, MoneyEgp, ArabicEnglishText, IClock
├── EgyptTax.Domain/                # Entities, value objects, domain events, domain services
│   ├── Identity/                   # User, Role, AccountantFirmUser, Permission
│   ├── MasterData/                 # Company, Customer, Supplier, Item, ChartOfAccount, VatCategory, WhtCategory, DeductibleExpenseCategory, CustomerTaxProfile, SupplierTaxProfile
│   ├── Documents/                  # SalesInvoice, CreditNote, PurchaseInvoice, Expense, FixedAsset, JournalVoucher, SupplierPaymentVoucher, CustomerReceiptVoucher, PaymentAllocation
│   ├── Workflow/                   # DocumentState, ApprovalRequest, DocumentTypeApprovalSetting, PeriodReviewLock, TaxPeriod
│   ├── Audit/                      # AuditLogEntry, AuditChainHasher, AuditIntegrityCheckpoint
│   ├── Numbering/                  # DocumentSeries, DocumentNumberAllocator
│   ├── Tax/                        # WhtCertificate, Form41Filing, ComplianceRiskItem, TaxRiskScore (rule engine)
│   ├── Eta/                        # EtaSubmission, EtaSubmissionStatus
│   └── Verification/               # DocumentVerificationSeal (QR), VerificationResult
├── EgyptTax.Application/           # Use cases (CQRS handlers), DTOs, port interfaces, validators
│   ├── Common/                     # MediatR pipeline behaviors: validation, transaction, audit-emit, performance, authorization
│   ├── Identity/                   # Login, RegisterUser, EnrollMfa, ResetPassword, AdminRecoverAccount
│   ├── Invoices/                   # CreateDraftSalesInvoice, PostSalesInvoice, IssueCreditNote, etc.
│   ├── Expenses/                   # CreateDraftExpense, ClassifyExpense, AttachReceipt, etc.
│   ├── Wht/                        # ApplyWhtOnPayment, RecordCustomerWhtCertificate, GenerateForm41
│   ├── Reports/                    # GenerateVatMonthlyReport, GenerateTaxableIncomeReport, GenerateTrialBalance, GenerateInspectionBundle
│   ├── Compliance/                 # GetEtaComplianceDashboard, GetWhtLifecycleDashboard, GetClosingCockpit, ScoreDocumentRisk
│   └── FirmPortal/                 # InviteAccountantFirmUser, AcceptInvitation, SwitchCompany, RevokeFirmUser
├── EgyptTax.Infrastructure/        # EF Core, SQL Server, file storage, ETA mock client, hash chain, PDF generator
│   ├── Persistence/                # AppDbContext, configurations, migrations bootstrap
│   ├── Audit/                      # SqlAuditLogStore, FileSystemCheckpointStore, SqlSchemaCheckpointStore
│   ├── Numbering/                  # SqlSequentialNumberAllocator (uses table-lock-per-series for gap-free)
│   ├── Eta/                        # MockEtaClient (WireMock-targetable), EtaRetryQueueWorker
│   ├── Pdf/                        # QuestPdfInvoiceRenderer, ArabicWordsConverter
│   ├── Verification/               # QrSealGenerator, OfflineVerifier
│   ├── Tax/                        # WhtComputeService, Form41Generator, RuleEngine
│   ├── Identity/                   # PasswordHasher (Argon2id), TotpService (Otp.NET), SessionService
│   ├── BackgroundJobs/             # HangfireScheduler, AuditCheckpointJob, EtaSubmissionRetryJob, SupplierTinRevalidationJob, NtpHealthCheckJob
│   ├── FileStorage/                # AttachmentStore (filesystem)
│   └── Inspection/                 # InspectionBundleBuilder, ManifestSigner
├── EgyptTax.Web/                   # Blazor Server + minimal API endpoints
│   ├── Pages/                      # Blazor pages (server-rendered)
│   │   ├── Invoices/, Expenses/, Reports/, Cockpit/, Wht/, Inspection/, FirmPortal/, Settings/, Auth/
│   ├── Components/                 # Shared Blazor components (RTL-aware, bilingual)
│   ├── Endpoints/                  # Minimal API: file upload links (FR-038 reset, Differentiator 8 missing-doc, FR-044 QR verify), Hangfire dashboard, health
│   ├── Localization/               # AR/EN resources, RTL CSS
│   ├── Hubs/                       # SignalR notifications (in-app approval queue)
│   └── Program.cs
└── EgyptTax.Installer/             # WiX installer (MSI), service registration, SQL bootstrap, MFA seed, license check (Near-term)

tests/
├── EgyptTax.UnitTests/             # Pure-domain + handler unit tests
├── EgyptTax.IntegrationTests/      # EF + SQL via Testcontainers; concurrent-posting, hash-chain, numbering tests
├── EgyptTax.ContractTests/         # JSON Schema validation (eInvoice, Form 41, manifest), API contract via OpenAPI
└── EgyptTax.E2ETests/              # Playwright: bilingual flows, RTL rendering, full US1–US9 paths

migrations/                          # EF Core migration scripts (versioned)
contracts/                           # See Documentation tree above (mirrored)
docs/
├── operator-runbook.md             # Install, backup, restore, NTP, MFA bootstrap, period close
├── accountant-guide.md             # Day-to-day workflows
└── inspector-bundle-format.md      # Public format spec for tax inspectors
```

**Structure Decision**: Web application (Option 2 from the template), specialized as **Clean Architecture in 5 src/ projects + 4 tests/ projects + dedicated `migrations/`, `contracts/`, and `docs/` directories**. Blazor Server is the single web tier (no separate frontend project) because it eliminates a JS toolchain, ships RTL-friendly server-rendered HTML, and integrates natively with the same in-process SignalR used for in-app notifications. The `EgyptTax.Installer` project produces the MSI that ships to operators.

The tree above is the authoritative layout; subsequent task generation must reference these directories rather than re-derive structure.

## Complexity Tracking

> Constitution Check Principle IV (Simplicity & YAGNI) deviations recorded with justification:

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --- | --- | --- |
| **5 src/ projects (Clean Architecture)** instead of single project | Spec scope spans 53 FRs across identity, master data, documents, audit, tax, ETA, reports, portal — with hard boundaries between domain logic and infrastructure (FR-028 audit chain MUST be testable without a database; FR-049 firm portal switching MUST swap auth contexts cleanly). User input explicitly mandates "clean architecture." | Single project would force domain rules and EF Core to share files; FR-028's auditor-runnable verification routine couldn't be unit-tested without a SQL Server; ETA mock client and PDF generator would couple to controllers; future Live ETA swap (Future advanced) would require rewriting application services. |
| **MediatR + pipeline behaviors** instead of direct service calls | Audit-emit, transaction boundary, validation, and authorization are cross-cutting concerns that apply to ~80% of write operations. Centralizing them as pipeline behaviors makes FR-028 audit-emit and FR-053 over-allocation rejection testable in isolation. | Direct service calls would scatter audit-emit and transaction logic across 50+ handlers; missing one would silently break the FR-028 guarantee that "100% of state changes appear in the audit log" (SC-010). |
| **Hangfire** instead of `BackgroundService` | Three durable, retry-needed background jobs: ETA retry queue (FR-036), audit checkpoint emitter (FR-028, every 1k entries OR 15min), supplier-TIN re-validation (FR-043). Hangfire's persistent queue survives app restarts; `BackgroundService` does not. ETA retry on a transient mock failure must persist across an operator-initiated app restart. | `BackgroundService` would lose the retry queue on every restart; FR-036 explicitly requires retry without invoice-data change, and silent retry-loss would violate SC-007 (eInvoice validation on every document). |
| **Separate audit checkpoint store** (file or separate-schema table) instead of co-locating with audit log | FR-028 explicitly requires the integrity checkpoint to live outside the audit table to detect tail truncation by a SQL `sysadmin`. Co-locating would defeat the security property. | A `sysadmin` could `DELETE FROM audit; DELETE FROM audit_checkpoint;` in one transaction and the verifier would be blind. The whole point is privilege separation. |
| **Per-table-lock document-number allocator** instead of SQL `IDENTITY` | FR-011 requires gap-free numbering per `(series, fiscal_year)` window with rollback releasing the number. `IDENTITY` consumes numbers on rollback (gap), and `SEQUENCE` with reset has the same problem. SC-006 stress-tests 500 concurrent posts spanning a fiscal-year boundary. | `IDENTITY`/`SEQUENCE` produce gaps on rollback; the spec explicitly forbids that. A row-locked allocator table is the standard pattern; the alternative (compensating reconciliation jobs to renumber) violates immutability of posted documents (FR-012). |
| **WiX-based MSI installer** instead of `dotnet publish` zip | On-prem product; SC-001 (new accountant end-to-end in 15 min) implies operator-friendly install, not "extract zip then run nine PowerShell commands." MSI handles SQL connection prompt, MFA seed, service registration, and audit-checkpoint storage-mode selection (FR-028 install-time choice). | Zip-based deploy makes the FR-028 installer-time storage choice and the operator-recovery procedure (FR-038) error-prone. Egyptian SMEs are not running deployment automation; they need an .msi. |
| **CockpitCachingDecorator + ICockpitCacheInvalidator** wrapping `IMonthlyTaxClosingCockpitQuery` *(added T236a / Round-6 F13 during implementation)* | Cockpit page is "did I miss anything?" — operators refresh it repeatedly during month-end closing, and the underlying query joins ~10 tables (VAT, ETA, period locks, attachments, risk rules). 30-second sliding expiration absorbs the refresh streak; explicit invalidator hook lets post-handlers bust the entry on tax-impacting state changes so the next refresh is immediate (no waiting for sliding expiration). Added one decorator class + a 1-method invalidator port — consistent with existing decorator usage elsewhere. | Pure read-through cache without an invalidation hook would force operators to wait up to 30 s after posting a doc to see it on the cockpit; that's surprising and undermines the cockpit's "live status" promise. Hand-rolling per-call MemoryCache lookups in every consumer would scatter the policy. |
| **IFirmContextResolver + SqlFirmContextResolver** *(added T225 during implementation)* | `AuditEmitBehavior` overlays the firm name onto audit payloads when the actor is a firm user but the calling command didn't fill `ActorFirmName` (defense in depth — INV-015). Putting the lookup behind a port keeps the behavior testable with a fake resolver and gives a clean injection point for future multi-tenant firm-name resolution. | Inlining the EF lookup directly in the behavior would couple the Application layer to Infrastructure (forbidden by Clean Architecture); duplicating the lookup in every command builder would be brittle and easy to forget on new commands (the bug INV-015 specifically calls out). |

All other Round-1-through-Round-5 design decisions are constitution-compliant and require no Complexity Tracking entry. The two implementation-phase additions above were captured here as soon as they shipped (T260 final re-eval below confirmed no other deviations slipped in).

## Post-Design Constitution Check

*Re-evaluated after Phase 1 artifacts (research.md, data-model.md, contracts/, quickstart.md) generated.*

| Principle | Status | Evidence |
| --- | --- | --- |
| I. Spec-First | ✅ PASS | All Phase 1 artifacts derive from spec.md; no requirements introduced outside the spec. |
| II. Plan Before Code | ✅ PASS | Phase 0 research.md resolved every Technical-Context unknown; Phase 1 contracts make external boundaries explicit. |
| III. Test-First | ✅ PASS | Each contract has a corresponding contract-test target documented in research.md; data-model.md flags every invariant that needs an integration test before its implementation. |
| IV. Simplicity & YAGNI | ✅ PASS | Six justified deviations captured above; no new deviations introduced by the Phase 1 design (data model uses standard EF Core conventions, contracts use plain JSON Schema, no extra abstractions added). |
| V. Incremental Delivery | ✅ PASS | data-model.md groups entities by user story; contracts/ groups schemas by user story they unblock; quickstart.md walks the US1-only minimum path before any other story. |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Post-Implementation Constitution Check (T260)

*Re-evaluated 2026-05-08 against the implemented codebase after US1–US9 functional closure + Polish-phase progress. Suite state at evaluation: 265 unit + 40 contract + 211 integration GREEN.*

| Principle | Status | Evidence |
| --- | --- | --- |
| **I. Spec-First Development** | ✅ PASS | Every shipped FR-numbered behavior traces to spec.md; no out-of-spec features introduced during implementation. The five clarification rounds completed pre-plan held up — no `[NEEDS CLARIFICATION]` resurfaced during build. The two cross-cutting infra additions (CockpitCachingDecorator, IFirmContextResolver) are implementation patterns that serve documented FRs (SC-002 + INV-015 respectively), not new requirements. |
| **II. Plan Before Code** | ✅ PASS | Every commit is traceable to a T-numbered task in `tasks.md`; the directory structure documented in plan.md §"Project Structure" matches the on-disk layout (5 `src/` projects, 4 `tests/` projects, `migrations/` + `contracts/` + `docs/`). The two implementation-phase additions are recorded in Complexity Tracking above. |
| **III. Test-First Discipline** | ✅ PASS | Every user story's `### Tests` section in `tasks.md` precedes its `### Implementation` section, and the per-task closure notes pin the GREEN test count as evidence. Contract tests exist for every external boundary: `eta-einvoice.schema.json`, `wht-certificate.schema.json`, `form41.schema.json`, `inspection-bundle-manifest.schema.json`, `verification-seal-qr.md` — all green at 40 tests. Integration tests cover every cross-aggregate invariant (numbering, audit chain, immutability, period-lock guards) — 211 green. The two perf gates (T240 SC-002 + T217 SC-014) ship as `[Trait("Category","Slow")]` so they're nightly-CI-only without polluting the developer-loop. |
| **IV. Simplicity & YAGNI** | ✅ PASS | The 6 pre-implementation Complexity Tracking entries plus the 2 implementation-phase entries (CockpitCachingDecorator, IFirmContextResolver) are the FULL deviation set. Spot-checked for speculative complexity: no feature flags, no "multi-tenant for the future" plumbing in the single-tenant MVP, no abstractions wrapping single-impl ports without a concrete reason (reviewed: every port has a Sql-or-File-prefixed concrete impl PLUS at least one test fake — both required justifications). The `InspectionBundleProgressNotifier` mirrors the existing `EtaStatusNotifier` exactly so isn't a new abstraction. |
| **V. Incremental, Independently Testable Delivery** | ✅ PASS | Each user story closed in isolation (US1 → US9 + Phase 9 + Differentiator 2 cockpit + R-20 logging). Per-story `### Tests` in tasks.md still apply: each story has its own integration-test suite that runs without depending on later stories (verified by integration-test filter `~Wht`, `~FirmPortal`, `~Reports` etc. running cleanly in isolation). US1 alone (T100–T125) remains a viable MVP slice — sales invoicing + PDF + mock eInvoice + identity — with no later-story plumbing required to render value. |

**Final gate result**: ✅ PASS — implementation honors every constitutional principle. The two implementation-phase Complexity Tracking entries (CockpitCachingDecorator, IFirmContextResolver) are the only new deviations beyond the planning-phase set, and both are justified above with the rejected simpler alternative.

**Outstanding scope** (informational, not a constitution gate): 12 polish-phase tasks remain open per `tasks.md`: T247 (MSI installer), T248–T250 (operator runbook + accountant guide + inspector format spec), T252–T255 (Playwright E2E + nightly perf harness), T257 (health-readiness probe wiring in WiX), T258 (smoke against fresh MSI install), T259 (code cleanup pass), and the items they depend on. None of these introduce new principles to check; they're release-readiness execution items.
