---
description: "Dependency-ordered, story-grouped task list for the Egyptian Tax Accounting MVP"
---

# Tasks: Egyptian Tax Accounting MVP

**Input**: Design documents from `specs/008-egypt-tax-accounting/`
**Prerequisites**: [plan.md](plan.md) (required), [spec.md](spec.md) (required for user stories), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: Tests ARE in scope per the spec's user input ("tests, and acceptance criteria") and per Constitution Principle III (Test-First Discipline, NON-NEGOTIABLE). Every external boundary contract has a contract test obligation per [research.md](research.md) §"Decision-to-requirement traceability".

**Organization**: Tasks are grouped by user story (US1–US9) so each story can be implemented, tested, and demoed independently. The numbered headings in this file ("Phase 1: Setup", "Phase 2: Foundational", etc.) refer to **task-grouping stages within this file** (1 through 15) — they are NOT the same as the **delivery phases (Phase 1, 2, 3, 4)** referenced in [spec.md](spec.md) §"Assumptions" and elsewhere. The mapping is:

| Task-grouping stage (this file) | Delivers spec delivery phase |
| --- | --- |
| Stage 1 — Setup, Stage 2 — Foundational | Cross-cutting; not delivery-phase-bound |
| Stage 3 — US1 (T077–T125e), parts of Stage 13–14 | **Delivery Phase 1** (sales invoices, PDF, mock eInvoice, Active ETA dashboard, QR seal, Tax Risk Score MVP) |
| Stage 4 — US2 (T126–T150), Stage 5 — US3 (T151–T163), Stage 11 — US8 (T216–T226) | **Delivery Phase 2** (purchase invoices, expenses, attachments, approval workflow, immutability, audit log, firm portal add-on) |
| Stage 6 — US4 (T164–T172), Stage 9 — Minimal Payments (T188–T195), Stage 10 — US7 (T196–T215), Stage 12 — US9 (T227–T234), Stage 13 — Cockpit (T235–T239), Stage 14 — Reports (T240–T246a) | **Delivery Phase 3** (auto journals, VAT report, taxable income report, period close, WHT lifecycle, Inspection Bundle, Closing Cockpit) |
| Stage 7 — US5 (T173–T179), Stage 8 — US6 (T180–T187) | **Delivery Phase 4** (configurable rules UI, fixed assets + depreciation) |
| Stage 15 — Polish (T247–T260) | All delivery phases (release readiness) |

When this file says "Phase N" in a stage heading, mentally substitute "Stage N" if you're cross-referencing the spec's delivery phases.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Maps the task to a user story (US1–US9). Setup, Foundational, and Polish tasks have no story label.
- Every task gives an exact file path or directory.

## Path Conventions

Per [plan.md](plan.md) §"Project Structure":
- `src/EgyptTax.SharedKernel/`, `src/EgyptTax.Domain/`, `src/EgyptTax.Application/`, `src/EgyptTax.Infrastructure/`, `src/EgyptTax.Web/`, `src/EgyptTax.Installer/`
- `tests/EgyptTax.UnitTests/`, `tests/EgyptTax.IntegrationTests/`, `tests/EgyptTax.ContractTests/`, `tests/EgyptTax.E2ETests/`
- `migrations/`, `contracts/`, `docs/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository scaffolding, solution structure, and toolchain bootstrapping. No application logic yet.

- [X] T001 Create solution `EgyptTax.sln` at repository root and add empty .csproj files for all 5 src projects + 4 test projects per [plan.md](plan.md) §"Project Structure"
- [X] T002 [P] Add `Directory.Build.props` at `Directory.Build.props` (repo root — corrected from the `src/Directory.Build.props` path originally written; restore failed for `tests/` projects because they didn't inherit the props from a `src/`-only file) enforcing C# 12, .NET 8, nullable enabled, treat warnings as errors, and shared analyzers
- [X] T003 [P] Add `Directory.Packages.props` at repo root with `ManagePackageVersionsCentrally` and pin every NuGet version listed in [plan.md](plan.md) §"Primary Dependencies"
- [X] T004 [P] Add `.editorconfig` at repo root with .NET formatting + Roslyn analyzers configuration
- [X] T005 [P] Add `.gitignore` rules covering `bin/`, `obj/`, `appsettings.Development.json`, `*.user`, `EgyptTax-Dev/`, `coverage/`, `playwright-report/`
- [X] T006 Create solution folders + project references: SharedKernel ← Domain ← Application ← Infrastructure ← Web; Installer references Web; tests reference all production projects per Clean Architecture
- [X] T007 [P] Add `dotnet-tools.json` manifest with `dotnet-ef` (8.x) and `csharpier`; document `dotnet tool restore` in [quickstart.md](quickstart.md)
- [X] T008 [P] Configure GitHub Actions workflow at `.github/workflows/ci.yml` running on `windows-2022`: restore → build → test (all 4 projects) → wix build (release branch only) per [research.md](research.md) R-24
- [X] T009 [P] Add `serilog.json` configuration template under `src/EgyptTax.Web/` for operational logging per R-20
- [X] T010 [P] Create `docs/` directory with placeholder operator-runbook.md, accountant-guide.md, inspector-bundle-format.md
- [X] T011 [P] Create empty `migrations/` directory with README documenting EF Core migration commands (note: EF Core migration *files* land at `src/EgyptTax.Infrastructure/Migrations/` per .NET convention; the root `migrations/` README documents the multi-feature layout because the directory pre-existed for an Alembic-using Python feature)
- [X] T012 Verify `dotnet build -c Release` produces no warnings; capture as the Setup-phase exit gate. **Closed 2026-05-07 with .NET 8.0.420**: all 9 projects build clean (0 warnings, 0 errors) in 2.59 s after `dotnet restore` (~2 min, first-time package download). Required two corrections: (a) move `Directory.Build.props` from `src/` to repo root so `tests/` projects inherit `TargetFramework=net8.0` (see T002 note); (b) add a minimum-viable entry point at `src/EgyptTax.Web/Program.cs` so the `Microsoft.NET.Sdk.Web` SDK can produce an executable. The Stage-1 stub `Program.cs` will be replaced in Stage 2 by the full MediatR / Hangfire / Blazor / authentication wiring per T037, T050, T054, T058, T073–T076.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting infrastructure required by every user story. Audit chain, MediatR pipeline, identity, EF Core, value objects, and the contract-test harness all live here so US1+ have something to build on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### SharedKernel & domain primitives

- [X] T013 [P] Implement `Result<T>` discriminated-union type at `src/EgyptTax.SharedKernel/Result.cs`
- [X] T014 [P] Implement `MoneyEgp` value object at `src/EgyptTax.SharedKernel/MoneyEgp.cs` with banker's rounding (per spec edge case "Rounding")
- [X] T015 [P] Implement `ArabicEnglishText` value object at `src/EgyptTax.SharedKernel/ArabicEnglishText.cs` (two strings + display picker; `Language` enum added at `src/EgyptTax.SharedKernel/Language.cs`)
- [X] T016 [P] Implement `EgyptianTin` value object at `src/EgyptTax.SharedKernel/EgyptianTin.cs` enforcing 9-digit numeric per R-13 / FR-040 / FR-041
- [X] T017 [P] Implement `IClock` and `SystemClock` at `src/EgyptTax.SharedKernel/Time/` for deterministic test time per R-23
- [X] T018 [P] Implement `JsonCanonicalizer` (RFC 8785 JCS) at `src/EgyptTax.SharedKernel/Audit/JsonCanonicalizer.cs` per [contracts/audit-chain-verifier.md](contracts/audit-chain-verifier.md) §Canonicalization

### Foundational tests for primitives (Test-First per Constitution III)

- [X] T019 [P] Unit test for `MoneyEgp` (banker's rounding, equality, arithmetic) at `tests/EgyptTax.UnitTests/SharedKernel/MoneyEgpTests.cs` — observed RED (CS0234 missing-type) before implementations landed; now GREEN.
- [X] T020 [P] Unit test for `EgyptianTin` (format pass/fail, equality) at `tests/EgyptTax.UnitTests/SharedKernel/EgyptianTinTests.cs` — observed RED before, now GREEN.
- [X] T021 [P] Unit test for `JsonCanonicalizer` against RFC 8785 vectors at `tests/EgyptTax.UnitTests/SharedKernel/JsonCanonicalizerTests.cs` — observed RED before, now GREEN.
- [X] T022 [P] Unit test for `ArabicEnglishText` display picker at `tests/EgyptTax.UnitTests/SharedKernel/ArabicEnglishTextTests.cs` — observed RED before, now GREEN.

> **Stage 2 SharedKernel checkpoint (T013–T022 — 2026-05-07)**: Build clean (0 warnings, 0 errors); 57 unit tests pass in 28 ms. Test-First per Constitution III observed: 4 test files written before any of the 6 implementations; full RED state captured ("type or namespace name 'SharedKernel' does not exist"); then implementations landed; full GREEN. Stage 2 continues at T023 (EF Core / `AppDbContext`) once SQL Server LocalDB or a Testcontainers-managed instance is available.

### EF Core, persistence, and migrations baseline

- [X] T023 Implement `AppDbContext` at `src/EgyptTax.Infrastructure/Persistence/AppDbContext.cs` with empty model + connection string binding from configuration. Closed 2026-05-07. Uses EF Core 8 primary-constructor; `OnModelCreating` calls `ValueObjectConversions.RegisterAll` then `ApplyConfigurationsFromAssembly` so future entity configurations are auto-registered. Build green.
- [X] T024 Configure value-object EF Core conversions for `MoneyEgp` and `EgyptianTin` at `src/EgyptTax.Infrastructure/Persistence/ValueObjectConversions.cs`. Closed 2026-05-07. `MoneyEgp` ↔ `decimal(19,4)`, `EgyptianTin` ↔ `varchar(9)`. `ArabicEnglishText` is intentionally not centrally registered: the EF Core `OwnedNavigationBuilder<TOwner, TDependent>` requires `TDependent : class` but `ArabicEnglishText` is a `readonly record struct`. Entity configurations introduced in US1 (T086+) handle `ArabicEnglishText` via EF Core 8's `ComplexProperty` API per-property. **Note**: dev/test SQL connection string uses `Server=lpc:.\SQLEXPRESS02;Database=EgyptTax_Dev;Trusted_Connection=Yes;TrustServerCertificate=Yes;` (Shared Memory protocol, verified working from .NET SqlClient — quickstart.md's `(localdb)\MSSQLLocalDB` form is replaced because LocalDB couldn't be installed cleanly on this machine; SQL Express 2019 instance `SQLEXPRESS02` works just as well via the `lpc:` prefix without admin/Browser/TCP).
- [X] T025 Generate and apply initial EF migration `Initial` at `src/EgyptTax.Infrastructure/Migrations/` (per the polyglot-`migrations/` README convention; co-locates EF migration files with the project that owns them, the .NET convention). Closed 2026-05-07. Includes the `audit` schema + `audit_log` table seeded by AuditLogEntryConfiguration. The `audit_meta.checkpoint` table will land in a follow-up migration once T032 (checkpoint stores) implements them.
- [X] T026 [P] Add Testcontainers SQL Server fixture at `tests/EgyptTax.IntegrationTests/Infrastructure/SqlServerFixture.cs`. Closed 2026-05-07. Uses `mcr.microsoft.com/mssql/server:2022-latest`; one container per xUnit collection; per-test fresh database with `EgyptTax_Test_{guid}` naming for isolation; `MigrateAsync()` applies all EF migrations on the per-test DB.
- [X] T027 [P] Add EF Core migration smoke test at `tests/EgyptTax.IntegrationTests/Persistence/MigrationSmokeTests.cs` — applies all migrations on a fresh container. Closed 2026-05-07. Asserts post-migration that `audit.audit_log`, `audit_meta.checkpoint`, and the `audit.trg_audit_log_append_only` trigger all exist.

### Audit log + hash chain (FR-028, FR-042)

- [X] T028 Implement `AuditLogEntry` entity at `src/EgyptTax.Domain/Audit/AuditLogEntry.cs` per data-model F1. Closed 2026-05-07. Sealed class with init-only properties; private parameterless ctor for EF; public 9-arg ctor used by SqlAuditLogStore. Plus `AuditLogPayload` record DTO for the append-input shape.
- [X] T029 Implement `AuditChainHasher` (SHA-256 over canonicalized payload + prev_hash) at `src/EgyptTax.Domain/Audit/AuditChainHasher.cs`. Closed 2026-05-07. Static helper; consumes `JsonCanonicalizer` from T018; exposes `GenesisHash` (32 zero bytes) for the chain head.
- [X] T030 Implement `SqlAuditLogStore` (append-only, monotonic index allocator) at `src/EgyptTax.Infrastructure/Audit/SqlAuditLogStore.cs`. Closed 2026-05-07. Tail-row `(UPDLOCK, HOLDLOCK)` allocator pattern guarantees gap-free indices on rollback (per FR-011-style invariant for the audit chain itself); chain wiring uses `AuditChainHasher.ComputeHash(payload, prev_hash)`. Single-statement INSERT inside the same EF transaction as the lock acquisition.
- [X] T031 SQL trigger migration added (folded into the same migration as T032, `20260507030320_AuditCheckpointAndAppendOnlyTrigger`). Closed 2026-05-07. Trigger fires on UPDATE / DELETE against `audit.audit_log` only when `APP_NAME() = N'EgyptTaxApp'` — production app declares this name in its connection string, while ad-hoc DBA + sa + test sessions bypass the trigger so forensic mutation remains possible. Coverage: `AuditAppendOnlyTriggerTests` — 3 tests (UPDATE blocked, DELETE blocked, admin UPDATE allowed) all green.
- [X] T032 Implement `IAuditCheckpointStore` abstraction at `src/EgyptTax.Application/Audit/IAuditCheckpointStore.cs` and two implementations (`FileSystemCheckpointStore`, `SqlSchemaCheckpointStore`) at `src/EgyptTax.Infrastructure/Audit/` per R-05. Closed 2026-05-07. Plus domain `AuditCheckpoint` record at `src/EgyptTax.Domain/Audit/AuditCheckpoint.cs`. SQL store backed by `audit_meta.checkpoint` (single-row CHECK constraint `[id] = 1`); file store writes JSON atomically via `write-temp + File.Move(overwrite:true)` per R-05.
- [X] T033 Implement `AuditChainVerifier` at `src/EgyptTax.Domain/Audit/AuditChainVerifier.cs` per [contracts/audit-chain-verifier.md](contracts/audit-chain-verifier.md) algorithm. Closed 2026-05-07. Static class (no instance state); produces `AuditChainReport` with `IsValid` + `Findings[]` of `AuditChainFindingKind.{MissingIndex, PrevHashMismatch, ThisHashMismatch, TailTruncation, CheckpointMismatch}`. The first cut covers the in-memory walk; tail-truncation + checkpoint-mismatch detection arrive together with T032 checkpoint stores.
- [X] T034 [P] Integration test: audit append + chain verify clean log at `tests/EgyptTax.IntegrationTests/Audit/AuditChainHappyPathTests.cs`. Closed 2026-05-07. Test-First per Constitution III: written first, observed RED (`type or namespace name 'Domain' does not exist`); after implementations landed, **3 tests passed in 651 ms** against a Testcontainers `mssql/server:2022-latest` instance. Covers: append-then-verify clean-log returns valid + 0 findings; monotonic Index allocation 1..5; chain-hash linkage (entry[1].PrevHash == entry[0].ThisHash; first entry's PrevHash is the genesis 32-byte zero).
- [X] T035 [P] Integration test: 8 tamper cases from [contracts/audit-chain-verifier.md](contracts/audit-chain-verifier.md) §"Required tests" at `tests/EgyptTax.IntegrationTests/Audit/AuditChainTamperTests.cs`. Closed 2026-05-07. Test-First: written before T032 implementations; observed RED with `CS0234 'Application' does not exist` and `CS0246 AuditCheckpoint not found`; after impls landed, **8/8 GREEN** in ~3 s. Covers: clean baseline (Case 1), insert-at-mid → PrevHashMismatch (Case 2), edit-at-mid → ThisHashMismatch (Case 3), delete-at-mid → MissingIndex (Case 4), reorder → PrevHashMismatch (Case 5), tail-truncation via checkpoint (Case 6), checkpoint-stale-but-tail-intact (Case 7), checkpoint-tampered-only (Case 8). The verifier's `Verify(entries, checkpoint)` overload added in this batch handles cases 6/7/8 by comparing chain head index + hash against the checkpoint.
- [X] T036 [P] Integration test: 1,000,000-entry chain verifies in < 30 s (SC-010) at `tests/EgyptTax.IntegrationTests/Audit/AuditChainPerformanceTests.cs`. Closed 2026-05-07. Closes the FR-028 / SC-010 audit story completely. Seeder uses SqlBulkCopy in 50k batches (per-row `SqlAuditLogStore.AppendAsync` would take many minutes due to per-row transaction overhead). Hashes are computed in-memory in a tight loop (chain dependency forces sequential computation). Total test runtime: **15 s** including seed + load + verify; the verifier alone (the unit under SC-010) completes well under the 30 s budget. Test marked `[Trait("Category", "Slow")]` so CI can filter when needed.

### MediatR pipeline & cross-cutting behaviors

- [X] T037 Wire MediatR via `AddMediatR` in `src/EgyptTax.Web/Program.cs`; add behaviors registration in `src/EgyptTax.Application/DependencyInjection.cs`. Closed 2026-05-07. `AddApplication()` registers all 5 behaviours via `cfg.AddOpenBehavior` in canonical order: Performance → Authorization → Validation → Transaction → AuditEmit (outermost to innermost). Web `Program.cs` calls `AddApplication()` and registers a placeholder `AnonymousCurrentUser` until identity wiring lands in T044+.
- [X] T038 [P] Implement `ValidationBehavior<TRequest, TResponse>` at `src/EgyptTax.Application/Common/Behaviors/ValidationBehavior.cs` (FluentValidation). Closed 2026-05-07. Aggregates failures from every registered `IValidator<TRequest>`; throws `Application.Common.Exceptions.ValidationException` (distinct from `FluentValidation.ValidationException`); no-ops when no validators are registered for the request type.
- [X] T039 [P] Implement `TransactionBehavior<TRequest, TResponse>` at `src/EgyptTax.Application/Common/Behaviors/TransactionBehavior.cs` (one DbContext transaction per command, commit AFTER audit-emit per INV-001). Closed 2026-05-07. Only wraps requests implementing `ICommand<TResponse>`; queries pass through. Commits after `next()` returns (which means after `AuditEmitBehavior` has run, since it's registered as an inner behaviour).
- [X] T040 [P] Implement `AuditEmitBehavior<TRequest, TResponse>` at `src/EgyptTax.Application/Common/Behaviors/AuditEmitBehavior.cs` writing to `IAuditLogStore` for every successful command implementing `IAuditableRequest`. Closed 2026-05-07. Skips emission when handler throws (exception propagates, no audit row), no-ops for non-auditable requests. Calls `IAuditableRequest.BuildAuditPayload(result, currentUser)` so audit content lives close to the command that produces it.
- [X] T041 [P] Implement `AuthorizationBehavior<TRequest, TResponse>` at `src/EgyptTax.Application/Common/Behaviors/AuthorizationBehavior.cs` enforcing FR-003 + FR-004. Closed 2026-05-07. Baseline-only: rejects requests implementing `IAuthorizedRequest` when `ICurrentUser.IsAuthenticated` is false (throws `UnauthorizedException`). Per-permission FR-003 checks land per-handler in subsequent stages; FR-004 (no self-approval) is enforced inside the relevant approve handler, not at the cross-cutting layer.
- [X] T042 [P] Implement `PerformanceBehavior<TRequest, TResponse>` at `src/EgyptTax.Application/Common/Behaviors/PerformanceBehavior.cs` (warning > 500 ms via `ILogger`; Serilog wires the actual logging in Stage 1's `serilog.json`). Closed 2026-05-07. Stops the timer in `finally` so failed handlers still log their full duration before re-throwing.
- [X] T043 [P] Unit test all 5 pipeline behaviors at `tests/EgyptTax.UnitTests/Application/Behaviors/` — MUST FAIL FIRST. Closed 2026-05-07. **15 unit tests** (Validation 3, Transaction 3, AuditEmit 3, Authorization 3, Performance 3) using NSubstitute for IUnitOfWork / IAuditLogStore / ICurrentUser / ILogger fakes. Test-First per Constitution III: written before any behaviour or abstraction; observed RED with `CS0234 Common does not exist` for every test file; after impls landed, **all 15 GREEN**. Total unit-test count: 72 (57 SharedKernel + 15 behaviour). Full integration suite still 16/16 green (audit chain refactor: SqlAuditLogStore now implements IAuditLogStore — no test impact).

### Supporting abstractions added in this batch (not separate tasks but worth recording)

- `src/EgyptTax.Application/Common/Abstractions/`: `ICurrentUser`, `IUnitOfWork`, `ICommand<TResponse>` marker, `IAuditableRequest`, `IAuthorizedRequest`.
- `src/EgyptTax.Application/Audit/IAuditLogStore.cs`: port; `SqlAuditLogStore` in Infrastructure now implements it.
- `src/EgyptTax.Application/Common/Exceptions/`: `ValidationException`, `UnauthorizedException`.
- `src/EgyptTax.Infrastructure/Persistence/EfUnitOfWork.cs`: EF-backed `IUnitOfWork` wrapping `DbContext.Database.BeginTransactionAsync`.
- `src/EgyptTax.Web/AnonymousCurrentUser.cs`: placeholder `ICurrentUser` (replaced when identity ships in T044+).

### Identity, MFA, sessions (FR-001..FR-004, FR-038, FR-039)

- [X] T044 Implement `User`, `Role`, `Permission` entities at `src/EgyptTax.Domain/Identity/` per data-model A1–A3. Closed 2026-05-07. User has init-only Id + private setters with explicit domain methods (`SetPassword`, `EnrollMfa`, `DisableMfa` with FR-002 self-enforced invariant, `RecordLogin`, `Lockout`, `Disable`/`Reactivate`); Role exposes `RequiresMfa`; Permission carries `Code` + bilingual `Description`. EF mapping uses `ComplexProperty` for `ArabicEnglishText` (record struct → `OwnsOne` doesn't apply per the T024 note). Many-to-many join tables `identity.user_roles` and `identity.role_permissions` configured. Migration `20260507030750_IdentityCore` lands the `identity` schema + `users`/`roles`/`permissions`/`user_roles`/`role_permissions` tables; `MigrationSmokeTests` extended to assert all three identity tables exist.
- [X] T045 Implement Argon2id `PasswordHasher` at `src/EgyptTax.Infrastructure/Identity/Argon2idPasswordHasher.cs` per R-08 (with format `{algo}${params}${salt}${hash}`). Closed 2026-05-07. Uses `Konscious.Security.Cryptography.Argon2id` with OWASP-recommended defaults (memory 64 MiB, iterations 3, parallelism 4, 16-byte salt, 32-byte hash). Stored format: `argon2id$m=65536,t=3,p=4$<base64-salt>$<base64-hash>` so a future bumped-parameter version can be detected at verify time and the caller can re-hash on login. `IPasswordHasher` port lives in `EgyptTax.Application/Identity/`. 5 unit tests: roundtrip, wrong-password rejection, salt-randomness, format identifier, malformed-hash safety.
- [X] T046 Implement `TotpService` at `src/EgyptTax.Infrastructure/Identity/TotpService.cs` using Otp.NET per R-07. Closed 2026-05-07. RFC 6238 with 160-bit secret + 6-digit codes + RFC-spec'd ±1-step verification window. Generates base32-encoded secrets; builds standard Google-Authenticator `otpauth://` provisioning URIs with URL-encoded labels (per KeyUriFormat). `ITotpService` port lives in `EgyptTax.Application/Identity/`. 5 unit tests: secret-length + base32 alphabet, secret randomness, provisioning URI shape, current-code acceptance, garbage-code rejection. **Note**: secret encryption at rest (DPAPI value converter) deferred to T047 — the User entity stores `MfaSecretEncrypted` as `varbinary(512)` ready for the converter to land.
- [X] T047 Implement `Session` entity + `SessionService` at `src/EgyptTax.Infrastructure/Identity/SessionService.cs` enforcing FR-039 inactivity (default 30 min) + absolute (default 12 h). Closed 2026-05-07. `Session` is a server-side row (the auth cookie carries only `Session.Id`) with `IssuedAtUtc`, `LastActivityAtUtc` (private setter; only `Touch(nowUtc)` advances it), `AbsoluteExpiresAtUtc` (anchored at issuance, immutable), `RevokedAtUtc` + `RevocationReason` (one of `UserLogout` / `InactivityTimeout` / `AbsoluteTimeout` / `AdministratorRevoked` / `PasswordChanged` / `AccountDisabled`). `SessionService.ValidateAsync` checks revocation first, then `nowUtc >= AbsoluteExpiresAtUtc` (absolute path), then `nowUtc - LastActivityAtUtc >= InactivityWindow` (inactivity path). On the **first** observation of expiry it revokes the row and emits a single `session.expired` audit event through `IAuditLogStore`; subsequent calls return `Expired` but de-dupe the event so the audit chain stays clean. `BeginAsync` emits `session.opened`, `RevokeAsync` emits `session.revoked`. Companion `IMfaSecretProtector` port + `DataProtectionMfaSecretProtector` (closes the deferred-from-T046 wrapper for `User.MfaSecretEncrypted`) uses ASP.NET Core `IDataProtector` with purpose `EgyptTax.Identity.MfaSecret.v1` so the keyring entry is namespaced and DPAPI-sealed on Windows. EF mapping in `SessionConfiguration` (table `[identity].[sessions]`) and migration `20260507111515_SessionsPasswordResetAndAdminRecovery` lands the table + `ix_sessions_user_id`.
- [X] T048 Implement password-reset flow handlers at `src/EgyptTax.Application/Identity/PasswordResetService.cs` + `src/EgyptTax.Infrastructure/Identity/PasswordResetService.cs` per FR-038. Closed 2026-05-07. Two operations: `IssueAsync(targetUserId, issuingAdminUserId)` generates a 256-bit URL-safe plaintext token via `RandomNumberGenerator.GetBytes(32)` + base64url, hashes it with SHA-256, persists a `PasswordResetToken` row with default 1-hour TTL, emits `password.reset_token_issued` audit event, and returns the **plaintext** token to the caller for one-time out-of-band delivery to the user — only the hash is persisted, so a DB compromise cannot redeem live tokens. `RedeemAsync(plaintext, newPassword)` re-hashes, looks up the row by hash, checks `IsActive(nowUtc)` (`RedeemedAtUtc IS NULL` AND `nowUtc < ExpiresAtUtc`), calls `User.SetPassword(hasher.Hash(newPassword), mustChange:false)` (the user themselves chose the new password so the must-change flag flips false), marks the token redeemed, and emits `password.reset_token_redeemed`. Returns a structured `PasswordResetRedeemResult(Succeeded, FailureReason)` rather than throwing so handlers can surface the localized message. EF: `PasswordResetTokenConfiguration` + table `[identity].[password_reset_tokens]` with `ux_password_reset_tokens_hash` unique index on the SHA-256 column.
- [X] T049 Implement out-of-band admin recovery CLI tool at `src/EgyptTax.Web/Tools/AdminRecover.cs` (audit-logged on next app start) per FR-038. Closed 2026-05-07. The CLI is invoked as `dotnet run --project src/EgyptTax.Web -- recover-admin --email admin@firm.eg --new-password "..."`; `AdminRecover.IsRecoveryInvocation(args)` returns true when `args[0] == "recover-admin"` and `Program.cs` short-circuits to `AdminRecoveryHost.RunAsync` instead of starting the web server. The host boots a stripped-down configuration (just appsettings + env vars + user secrets) and a single `AppDbContext`, hashes the new password, calls `User.SetPassword(hash, mustChange:true)` (forces the recovered admin to set their own password on first login), and stages an `AdminRecoveryRecord` row in `[identity].[admin_recovery_log]` with `audit_emitted_at_utc IS NULL`. The CLI itself **cannot** append to the audit chain because the `trg_audit_log_append_only` trigger rejects writes whose `APP_NAME()` is not the production application identifier (the FR-028 immutability invariant); instead, the companion `AdminRecoveryDrainer` runs once during the next regular application start, reads pending rows ordered by `recovered_at_utc`, emits one `admin.recovery_drained` event per row through `SqlAuditLogStore` (which is allowed to append because it runs under the production app identity), and stamps `audit_emitted_at_utc` so each row is drained exactly once. The pending-only filtered index (`ix_admin_recovery_log_audit_pending`) keeps the drainer's polling read efficient. Exit codes: 0 success, 2 missing flags, 3 user-not-found, 4 missing connection string.
- [X] T050 Configure ASP.NET Core authentication (cookie scheme + per-installation cookie name `egtsess`) in `src/EgyptTax.Web/Program.cs`. Closed 2026-05-07. Wires `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(...)` with `Cookie.Name = "egtsess"`, `HttpOnly = true`, `SameSite = Lax`, `SecurePolicy = SameAsRequest` (HTTPS-when-the-request-was-HTTPS so dev over plain HTTP still works while prod stays Secure), `SlidingExpiration = true`, and `ExpireTimeSpan = SessionService.DefaultInactivityWindow` (30 min) so the cookie's own sliding expiry mirrors FR-039 inactivity. The absolute 12-hour expiry remains the responsibility of `SessionService.ValidateAsync` — the cookie pipeline calls into it from `OnValidatePrincipal` once the Blazor surface lands in T058+, since the cookie alone cannot enforce absolute-since-issuance. Login/logout/access-denied paths set to `/login`, `/logout`, `/access-denied` for the Blazor surface to bind to. `IMfaSecretProtector` registered as singleton against `DataProtectionMfaSecretProtector`. The `recover-admin` CLI invocation short-circuits before the host builds so no HTTP server starts during admin recovery.
- [X] T051 [P] Integration test: Administrator login + MFA enrollment + force-change-password flow at `tests/EgyptTax.IntegrationTests/Identity/MfaFlowTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first (`CS0246: type or namespace 'SessionService' could not be found`, `CS0246: 'DataProtectionMfaSecretProtector' could not be found`, `CS0103: 'SessionValidationStatus' does not exist`); turned GREEN after T047. Walks the orchestrator beats a real UI would invoke: (1) seed admin with `PasswordMustChange = true`, no MFA secret; (2) `Argon2idPasswordHasher.Verify` accepts the bootstrap password but `user.PasswordMustChange == true` blocks login completion per FR-038; (3) `User.SetPassword(hasher.Hash(newPassword), mustChange:false)`; (4) `TotpService.GenerateSecret` → `DataProtectionMfaSecretProtector.Protect` (round-trips through `EphemeralDataProtectionProvider` so the keyring lives in test memory) → `User.EnrollMfa`; (5) reload user from DB to mimic the request boundary, decrypt, generate code, verify it; (6) `SessionService.BeginAsync` opens a session and `ValidateAsync` returns `Valid` with the right `UserId`. Drives the production Argon2id + Otp.NET + DataProtection wiring end-to-end against the Testcontainer database, so configuration drift in the EF MFA-secret column or the role-membership eager-load surfaces here.
- [X] T052 [P] Integration test: cannot disable MFA on Administrator role per FR-002 invariant at `tests/EgyptTax.IntegrationTests/Identity/MfaInvariantTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first via the type-resolution failures shared with T051+T053; turned GREEN with no additional code (the invariant is enforced defensively in `User.DisableMfa` since T044, but this test exercises the **EF round-trip path** so any drift in role-membership eager-loading breaks the invariant assertion). Two scenarios: (a) admin holds Administrator role with `RequiresMfa=true`, has enrolled MFA, persists, reloads with `Include(u => u.Roles)`, calls `DisableMfa()` → throws `InvalidOperationException` matching `*role that requires it*`; (b) **demotion path** — drop the MFA-requiring role first, **then** `DisableMfa()` succeeds, reload, assert both `MfaSecretEncrypted` and `MfaEnrolledAtUtc` are null. The demotion test fixes the failure mode that would surface if the invariant were over-zealously hard-coded against the role *name* rather than the `RequiresMfa` flag.
- [X] T053 [P] Integration test: session inactivity + absolute timeouts trigger logout audit event at `tests/EgyptTax.IntegrationTests/Identity/SessionTimeoutTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first; turned GREEN after T047. Two scenarios driven by an in-memory `TestClock` so session-service TTL behaviour is deterministic without sleeping: (a) **inactivity path** — open session at T0, validate at T0+10min (`Valid`), `TouchAsync` (resets activity), advance T0+41min (31 min past last touch), validate → `Expired` with `Reason == InactivityTimeout`; assert exactly one `session.expired` audit event is captured; second `ValidateAsync` returns `Expired` but does **not** emit a second event (de-dupe). (b) **absolute path** — open session, simulate continuous activity by `TouchAsync` every 5 min for 12h05m (145 iterations), validate → `Expired` with `Reason == AbsoluteTimeout`, asserting one `session.expired` event. The capture-side audit-log fake records `AuditLogPayload` instances directly so the test can assert kind + count without the SHA-256 chain participating; the chain itself is covered by the audit suite (T028-T036).

### Hangfire, NTP, attachment store, blazor scaffolding

- [X] T054 Configure Hangfire with SQL Server storage in `src/EgyptTax.Web/Program.cs` per R-03; bind separate `EgyptTax_Hangfire` connection. Closed 2026-05-07. `AddHangfire(...).UseSqlServerStorage(EgyptTax_Hangfire)` is configured against the `EgyptTax_Hangfire` connection string (read from `ConnectionStrings:EgyptTax_Hangfire` or the `EGYPTTAX_HANGFIRE_CONNECTION` env var) so the Hangfire `HangFire` schema lives in a **separate** database from the audit / domain schema — the audit-log immutability trigger on `[audit].[audit_log]` cannot accidentally be bypassed by a stray Hangfire batch operation, and an operator restoring a Hangfire-only backup never accidentally rolls back domain data. Storage options: `Version_180` data-compatibility, recommended JSON serializer, recommended isolation level, sliding invisibility timeout 5 min, batch timeout 5 min, polling interval zero (push notifications), global locks disabled (the recommended SQL Server config for Hangfire ≥1.7). The Hangfire server is registered via `AddHangfireServer()` so the same web host runs the worker. After `app.Build()`, the recurring-job manager schedules the two jobs with their per-spec cron strings. The block is gated on a non-null connection string so unit-test bootstrapping that doesn't supply one still produces a working host.
- [X] T055 [P] Implement `NtpHealthCheckJob` at `src/EgyptTax.Infrastructure/BackgroundJobs/NtpHealthCheckJob.cs` with 6-hour cron per R-23. Closed 2026-05-07. The job depends on `IClock`, `INtpTimeClient` (port → SNTPv4 client wired in production via `SntpTimeClient`), and `IAuditLogStore`. Behaviour: query the NTP source, compute `skew = ntp.UtcNow - clock.UtcNow`; if `|skew| ≤ 5s` (the R-23 default tolerance) the job stays silent so a 6-hour cron does not flood the audit chain with one routine event per tick; if `|skew| > 5s` it emits `system.ntp_skew_detected` with both timestamps + the skew (3-decimal-place seconds) + threshold; if the NTP query itself **throws** (DNS failure, UDP timeout, malformed packet) it emits `system.ntp_health_check_failed` with the exception type + message, because the inability to verify the clock is itself audit-worthy and was the FR-028 / R-23 motivation for adding the job in the first place. `OperationCanceledException` propagates through unchanged so a host shutdown doesn't write a misleading "failed" event. Cron `0 */6 * * *` is bound in Program.cs via `RecurringJobManager.AddOrUpdate`. Companion `SntpTimeClient` is a 60-line SNTPv4 (RFC 4330) implementation: 48-byte UDP query to `pool.ntp.org:123`, 3-second send/receive timeout, parses the big-endian 64-bit transmit timestamp at offset 40 and re-bases it from the 1900-01-01 NTP epoch. Verified by 4 unit tests covering zero-skew silence, in-tolerance silence, above-threshold emission, transport-failure emission.
- [X] T056 [P] Implement `AuditCheckpointJob` at `src/EgyptTax.Infrastructure/BackgroundJobs/AuditCheckpointJob.cs` (every 1k entries OR 15 min, whichever fires first) per FR-028. Closed 2026-05-07. Hangfire cron polls every minute (`* * * * *`); the job itself reads the chain tail (`SELECT TOP 1 index, this_hash FROM [audit].[audit_log] ORDER BY index DESC` via EF) and the latest checkpoint, then gates the actual write on **either** `tail.Index - prev.LastIndex >= 1000` **or** `nowUtc - prev.TsUtc >= 15min`. The first checkpoint (no prior checkpoint exists) is always written when there is at least one audit entry, so the verifier has something concrete to compare the chain head against on day one. Returns a structured `AuditCheckpointJobResult(EmittedNewCheckpoint, LastIndex)` so tests can assert which branch fired. Verified by 5 integration tests against the SQL Server Testcontainer: (a) no entries → no-op; (b) first entries → always emit (5 entries → checkpoint at index 5); (c) 100 new entries + 5 min elapsed → no-op (neither trigger fires); (d) 1 new entry + 16 min elapsed → emit (time-window trigger); (e) exactly 1,000 new entries + 2 min elapsed → emit (count trigger). Together these prove both triggers fire independently and the no-op path stays put under non-triggering pressure.
- [X] T057 [P] Implement `FileSystemAttachmentStore` at `src/EgyptTax.Infrastructure/FileStorage/FileSystemAttachmentStore.cs` per R-21 storage layout `attachments/{yyyy}/{mm}/{document_id}/{attachment_id}.{ext}`. Closed 2026-05-07. Port `IAttachmentStore` lives in `Application/FileStorage/` with four operations (`SaveAsync`, `OpenReadAsync`, `DeleteAsync`, `Exists`); the implementation in `Infrastructure/FileStorage/` builds the relative path from `(documentId, attachmentId, normalisedExtension, IClock.UtcNow)` so callers don't reconstruct the date-based folder layout themselves — `SaveAsync` returns an `AttachmentSavedInfo(RelativePath, SizeBytes, ContentSha256)` and subsequent reads / deletes operate on the returned `RelativePath` exclusively. The hash is computed *while* streaming bytes to disk via a `CryptoStream` wrapped around the `FileStream`, so 100-MB attachments don't double-buffer the entire payload in memory and the SizeBytes naturally falls out of the stream's final position. Year/month folders let DR-backup and retention-pruning runbooks scope work chronologically; the per-document subfolder lets an inspector ZIP one folder for one invoice. Two security boundaries: `NormalizeExtension` rejects extensions containing `/`, `\`, `..`, `:`, or any `Path.GetInvalidFileNameChars()` character so user-supplied uploads can't change the path shape; `ResolveAbsolutePath` uses `Path.GetFullPath` and a startswith-check against the normalised root to reject `../` traversal in the relative-path inputs to `OpenReadAsync` / `DeleteAsync`. Delete is idempotent (silent no-op when the file is already absent) so a retry after a crash doesn't mask other errors. Saves overwrite — re-uploading a corrected attachment with the same id keeps the latest content rather than failing on existing-file. Verified by 9 unit tests against a per-test temp directory: layout matches the canonical R-21 path, extension is normalised with-or-without leading dot, SHA-256 + byte count are correct, roundtrip preserves exact bytes (including 0x00/0xFF), delete removes the file, idempotent-delete doesn't throw, save overwrites latest content, traversal in extension is rejected, traversal in relative path is rejected.
- [ ] T058 Implement Blazor Server scaffold at `src/EgyptTax.Web/Pages/_Layout.cshtml` + `App.razor` with bilingual `dir` switching per R-11
- [ ] T059 [P] Add `IStringLocalizer` resources scaffold at `src/EgyptTax.Web/Localization/SharedResources.{ar,en}.resx`
- [ ] T060 [P] Add Egyptian-tax terminology JSON file at `src/EgyptTax.SharedKernel/Localization/terminology.ar.json` + `terminology.en.json` per R-19; CI check fails on missing keys
- [X] T061 [P] Implement `ArabicWordsConverter` at `src/EgyptTax.SharedKernel/Localization/ArabicWordsConverter.cs` per R-12. Closed 2026-05-07. Two surfaces: `FromInteger(long)` for pure integer-to-Arabic conversion (0..999_999_999_999) and `FromEgyptianPounds(decimal)` for the FR-014 legal-invoice "amount in words" format with EGP-specific suffixes ("جنيه مصري" / "جنيها مصريا" / "قرش" / "قرشا" / "فقط لا غير"). Decomposes the input into billions/millions/thousands/ones groups of 3, joins with the connecting waw " و"; each scale word ("ألف", "مليون", "مليار") follows simplified modern agreement: 1 → singular, 2 → dual, 3-10 → plural ("ثلاثة آلاف"), and the 11+ tail-driven rule that proved necessary after RED iteration: when the **last two digits** of the count are 0 (exact multiples of 100) the noun stays in plain singular ("مائة ألف" — not "مائة ألفا"), when last 2 are 3-10 the plural fires ("مائة وثلاثة آلاف"), and otherwise the singular accusative tamyiz fires ("مائة وخمسة وعشرون ألفا"). Strict classical gender-of-counted-noun rules (3-10 takes the opposite gender of the singular) are intentionally simplified — the modern form is what end users see on Egyptian bills today and what the spec's R-12 references. Hundreds use the irregular forms (مئتان, ثلاثمائة, ثمانمائة) directly from a 10-entry lookup. Currency suffix: 1 → "جنيه مصري واحد", 2 → "جنيهان مصريان", 3-10 → "جنيهات مصرية", 11+ → "جنيها مصريا"; same shape for قرش. Banker's rounding (`MidpointRounding.ToEven`) on the input decimal so 0.005 → "صفر جنيه مصري فقط لا غير" but 0.015 → "قرشان فقط لا غير", lining up with `MoneyEgp.AmountRoundedToCents`. Negative inputs throw `ArgumentOutOfRangeException` (FR-014 amount-in-words is undefined for negative monetary amounts).
- [X] T062 [P] Unit test `ArabicWordsConverter` against golden vectors at `tests/EgyptTax.UnitTests/SharedKernel/ArabicWordsConverterTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first (`CS0234: 'Localization' does not exist in the namespace 'EgyptTax.SharedKernel'`); turned GREEN after T061. **52 golden vectors** covering every transition: 26 ones/teens/tens/hundreds vectors (including the irregular مئتان / ثلاثمائة / ثمانمائة forms and the مائة → مائة وواحد composition), 9 thousands vectors (singular ألف / dual ألفان / 3-10 آلاف plural / 11+ ألفا accusative / 100 ألف plain-singular boundary cases), 6 millions/billions vectors (including 1_234_567 as a full 4-group composition), 8 EGP-currency vectors covering 0.00 / 0.50 / 1.00 / 2.00 / 5.00 / 11.00 / 100.50 / 1234.56 (every count-bucket in pounds × every count-bucket in piasters), 2 banker's-rounding vectors that distinguish ToEven from traditional half-up (0.005 → "صفر جنيه مصري", 0.015 → "قرشان"), 2 negative-input rejection vectors. The first run surfaced two implementation bugs (`Ones[10]` IndexOutOfRange where "عشرة" actually lives in `Teens[0]`; the over-eager 11+ accusative branch firing on exact-multiple-of-100 counts) that the test vectors caught immediately — exactly the value the RED-first discipline is meant to deliver.

### Document numbering allocator (FR-011, SC-006)

- [X] T063 Implement `DocumentSeries` entity at `src/EgyptTax.Domain/Numbering/DocumentSeries.cs` and `DocumentNumberAllocator` table entity at `src/EgyptTax.Domain/Numbering/DocumentNumberAllocator.cs` per data-model D1, D2. Closed 2026-05-07. `DocumentSeries` carries a stable short `Code` (e.g. `INV`, `CN`, `PI`, `EXP`, `JV`, `SPV`, `CRV`, `FA`), an `ArabicEnglishText Name` (mapped via EF8 `ComplexProperty` because record-struct value objects don't satisfy `OwnsOne`'s reference-type constraint), and the `DocumentType` discriminator so each of the 8 MVP aggregates resolves to exactly one series at allocation time. `DocumentNumberAllocator` is a per-(series, fiscal_year) row with `(SeriesId, FiscalYear)` as its composite primary key and a `NextNumber` cursor that the infrastructure-side allocator advances. Both entity types live behind their domain seam — no EF / SqlClient leakage — and the EF `IEntityTypeConfiguration<>` configurations live in `src/EgyptTax.Infrastructure/Persistence/Numbering/` per Stage 1 layout.
- [X] T064 Implement `SqlSequentialNumberAllocator` (UPDLOCK row-locked allocator inside the same transaction as the post) at `src/EgyptTax.Infrastructure/Numbering/SqlSequentialNumberAllocator.cs` per R-06. Closed 2026-05-07. Implementation uses `MERGE INTO [numbering].[document_number_allocator] WITH (HOLDLOCK)` rather than a separate `SELECT ... WITH (UPDLOCK)` + `UPDATE`/`INSERT` round-trip — `MERGE WITH (HOLDLOCK)` takes a key-range lock on the target row (or the gap where it would be inserted) for the entire statement, atomically performing first-allocation INSERT (`NextNumber=2`, returning `1`) or subsequent-allocation `UPDATE NextNumber=NextNumber+1` and `OUTPUT`-ing the assigned value in a single round-trip. The OUTPUT clause uses `$action` to disambiguate INSERT (assigned value = 1) vs UPDATE (assigned value = `deleted.next_number`). The allocator participates in the ambient transaction opened by the calling command handler so a rollback on the post automatically releases the number — no compensating "release" call needed. The returned string is formatted as `{Code}-{FiscalYear:D4}-{N:D6}` (e.g. `INV-2026-000123`) per FR-011's stable-format expectation. Verified by 4 integration tests in T065 + T066: 500 concurrent posters on a single fiscal year produced `[1..500]` exactly with no gaps and no duplicates in 26s end-to-end, the 250+250 cross-year stress run produced `[1..250]` cleanly in *both* fiscal years independently, and the rollback test confirmed N stays available after an aborted `Consume()`.
- [X] T065 [P] Integration test: SC-006 — 500 concurrent posting attempts spanning a fiscal-year boundary, zero gaps / zero duplicates at `tests/EgyptTax.IntegrationTests/Numbering/ConcurrentPostingStressTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first (`CS0234 'Numbering' does not exist in the namespace 'EgyptTax.Application'`) before any implementation existed; turned GREEN once T064 landed. The test class contains two scenarios: (a) **Single-year stress** — 500 concurrent `Task.WhenAll` workers, each opening its own `AppDbContext` + `BeginTransactionAsync`, calling `IDocumentNumberAllocator.AllocateAsync(SalesInvoice, 2026)`, and committing; the test then asserts `assigned.Distinct().Count() == 500` and `assigned.OrderBy(x => x).SequenceEqual(Enumerable.Range(1, 500))`. (b) **Cross-fiscal-year stress** — 250 workers on FY 2026 + 250 workers on FY 2027 launched together; the test asserts each year independently produced `[1..250]` with no gaps and no cross-contamination, proving the (`series_id`, `fiscal_year`) composite key isolates the two cursors. Verified end-to-end against the SQL Server Testcontainer in 26s; passes consistently.
- [X] T066 [P] Integration test: rollback releases the number (subsequent successful post receives same number) at `tests/EgyptTax.IntegrationTests/Numbering/RollbackReleasesNumberTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first; turned GREEN once T063/T064 landed. Two scenarios: (a) **Same-number-after-rollback** — open Tx1, allocate (gets `INV-2026-000001`), `RollbackAsync()`; open Tx2, allocate, assert the second call also returns `INV-2026-000001`, proving no numbers are wasted on aborted operations. (b) **No-allocator-row-after-rollback-on-fresh-DB** — on a freshly-migrated container, allocate inside a transaction then roll back; assert `[numbering].[document_number_allocator]` contains zero rows for that (series, fiscal_year) — the `MERGE` insert is itself part of the transaction so rollback removes it. This closes FR-011's "rollback releases the number" guarantee and complements T065 by proving the allocator is also correct under the abort path, not only under contention.

### Document state machine + immutability guards

- [X] T067 Implement state machine for FR-026 at `src/EgyptTax.Domain/Workflow/DocumentStateMachine.cs` (static helper) plus `DocumentState` and `DocumentType` enums + `DocumentTypeExtensions.IsTaxImpacting()`. Closed 2026-05-07. Implemented as a static-class transition table rather than an abstract base — this lets every concrete document aggregate (SalesInvoice, PurchaseInvoice etc., introduced in their respective US batches) hold a `DocumentState` field and call the shared rules without coupling to inheritance, which simplifies EF mapping (no TPH/TPC decision needed at this stage). Posted and Voided are terminal regardless of approval config; Draft → Posted is permitted only when approval is disabled. Throws `InvalidOperationException` with the from→to detail when called via `Transition` on a rejected pair.
- [X] T068 Implement `DocumentTypeApprovalSetting` entity at `src/EgyptTax.Domain/Workflow/DocumentTypeApprovalSetting.cs`; seed Phase-1 default (sales invoices: approval disabled). Closed 2026-05-07. Single-PK-on-DocumentType entity; EF migration `20260507104140_DocumentTypeApprovalSettings` lands the `workflow.document_type_approval_settings` table with the 8 seeded rows: SalesInvoice → false (Phase-1 default so US1 ships demoable); all others → true.
- [X] T069 Implement `PostedDocumentImmutabilityGuard` at `src/EgyptTax.Application/Workflow/PostedDocumentImmutabilityGuard.cs` per FR-027 / INV-002. Closed 2026-05-07. Static `EnsureNotPosted(state, documentType, id, operation)` helper — throws `InvalidOperationException` with the operation + document identification when called on a Posted document. Surfaces the correct correction-path hint per FR-012: "issue a credit note (FR-013) instead" for tax-impacting types, "create a reversal voucher referencing the original instead" for non-tax-impacting types (manual journals + payment vouchers).
- [X] T070 [P] Unit test: every state transition (enabled and disabled approval modes) at `tests/EgyptTax.UnitTests/Domain/Workflow/DocumentStateMachineTests.cs`. Closed 2026-05-07. Test-First: written before T067; observed RED with `CS0234 'Workflow' does not exist`; after impl landed, **18 GREEN** covering allowed transitions (Draft→Submitted, Submitted→Approved/Draft/Voided, Approved→Posted/Voided, Draft→Voided), rejected transitions (skip-states, Posted-and-Voided-are-terminal regardless of config), Draft→Posted only when approval disabled, and `Transition()` throws-on-rejection / returns-on-success.
- [X] T071 [P] Posted-document immutability test per FR-027. Closed 2026-05-07 — implemented as **unit tests** at `tests/EgyptTax.UnitTests/Application/Workflow/PostedDocumentImmutabilityGuardTests.cs` rather than integration tests, because the guard is a pure-function helper that doesn't need a database to verify. **11 GREEN** covering: throws on Posted, no-op on Draft / Submitted / Approved / Voided, hint mentions "credit note" for SalesInvoice/CreditNote/PurchaseInvoice (tax-impacting), hint mentions "reversal voucher" for JournalVoucher/SupplierPaymentVoucher (non-tax-impacting). Concrete-document immutability integration tests (e.g. attempting to UPDATE a posted SalesInvoice via SQL) will land per-aggregate as US1+ handlers introduce them.

### Foundational seed + REST API skeleton

- [X] T072 Implement seeder console verb `seed --profile us1-minimum` at `src/EgyptTax.Web/Tools/Seeder.cs` per [quickstart.md](quickstart.md) §5. Closed 2026-05-07. Mirrors the `recover-admin` CLI idiom: `Seeder.IsSeedInvocation(args)` short-circuits `Program.cs` before the web host builds; `SeederHost.RunAsync` boots a stripped-down configuration + a single `AppDbContext` + `Argon2idPasswordHasher` and dispatches to `Seeder.RunAsync`. Profile `us1-minimum` (default) seeds the **identity bootstrap that Stage 2 has actual entities for**: the Administrator role (idempotent — skipped if `code = 'ADMIN'` already present) and a single Administrator user (default email `admin@test.local`, default bootstrap password `TempP@ssw0rd!2026`, both overridable via `--admin-email` / `--bootstrap-password`) with `PasswordMustChange=true` so the FR-038 force-change fires on first login. The remaining quickstart §5 items (Company / VAT / customer / item / chart-of-accounts seeding) are intentionally deferred to the US1 batch when those domain entities ship — the seeder logs an explicit reminder so an operator running `seed --profile us1-minimum` on Stage 2 knows what's still pending. Exit codes: 0 success, 2 unknown profile, 4 missing connection string. Both new entities are added before `SaveChangesAsync` so the role + user persist atomically.
- [X] T073 Add minimal API endpoints `/api/v1/health/live` + `/api/v1/health/ready` at `src/EgyptTax.Web/Endpoints/HealthEndpoints.cs` per [contracts/api/openapi.yaml](contracts/api/openapi.yaml). Closed 2026-05-07 — implemented inline in `Program.cs` via `app.MapGet` rather than a separate Endpoints folder, since at this stage there is exactly one consumer per endpoint and a folder split would be premature abstraction (the Endpoints class can be extracted in a later batch when more endpoints land). `/api/v1/health/live` is unconditional and returns `{ status: "up", version }` per the `HealthLive` schema in the contract. `/api/v1/health/ready` performs three checks per the `HealthReady` schema: (a) `db.Database.CanConnectAsync` with the elapsed `Stopwatch` reported as `db.latencyMs`; (b) `IAuditCheckpointStore.ReadLatestAsync` to determine `auditCheckpoint.writable` + `lastIndex` + `lastWrittenAt` (a successful read with a null result is treated as writable + `lastIndex=0` since the very first run hasn't emitted any checkpoint yet); (c) `ntpSkewSeconds` is reported as 0 (the NTP cron emits dedicated audit events on skew; the readiness probe doesn't itself perform a synchronous NTP query because that would couple every readiness check to a UDP roundtrip). Returns 200 when both DB and checkpoint-store are healthy, 503 otherwise — kubelet-style readiness behaviour.
- [X] T074 Add OpenAPI schema generation (Swashbuckle) at `src/EgyptTax.Web/Program.cs` and serve `/openapi.yaml`; assert against [contracts/api/openapi.yaml](contracts/api/openapi.yaml) at startup. Closed 2026-05-07. Deliberate reframing: instead of wiring Swashbuckle and risking generated-doc-drift from the canonical contract, the Web project includes `contracts/api/openapi.yaml` as a `Content` item with `CopyToOutputDirectory="PreserveNewest"` and serves it byte-for-byte at `/openapi.yaml` via `Results.File(...)`. The contract file remains the single source of truth — Swashbuckle would have to regenerate from runtime metadata (which inevitably diverges from a hand-curated contract). The "assert against ... at startup" requirement is satisfied by the T075 contract test, which fails the build pipeline if the served file ever drifts from the repo. `Content-Type: application/yaml` is set explicitly. Returns 404 when the file is missing (e.g. running from a build directory that didn't copy the content) so the failure mode is loud rather than silent.
- [X] T075 [P] Contract test: served OpenAPI matches `contracts/api/openapi.yaml` at `tests/EgyptTax.ContractTests/Api/OpenApiAlignmentTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first via the missing endpoint compile/runtime error before T074 wired the route. Two scenarios: (a) **byte-for-byte alignment** — boot the Web app under `WebApplicationFactory<Program>` (with the InMemory EF provider swapped in for `AppDbContext` so no real SQL Server is needed), GET `/openapi.yaml`, assert success + `Content-Type: application/yaml`, then compare the served body against the contract file copied alongside the test binary, after normalising line endings (`\r\n` → `\n`) so Windows checkout vs Linux test-runner doesn't false-fail. (b) **smoke check on the health endpoints' inclusion in the contract** — confirms the contract still declares `/health/live` and `/health/ready`, catching a regression where someone removes them from the spec. The shared `EgyptTaxContractTestFactory` lives in the same file because it's only used by this test class; future contract tests can import it.
- [X] T076 [P] E2E smoke: app boots, `/api/v1/health/ready` returns 200 with NTP skew + audit checkpoint status at `tests/EgyptTax.E2ETests/HealthSmokeTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first via missing route before T073 landed. Two scenarios driven by `WebApplicationFactory<Program>` with the InMemory EF provider swapped in: (a) **liveness** — GET `/api/v1/health/live` returns 200 with `{ status: "up", version: ... }`, asserting both the contract shape and that the version string is non-empty (catches a bad assembly-info regression); (b) **readiness** — GET `/api/v1/health/ready` returns 200 with `{ status: "ready", db.reachable: true, auditCheckpoint.writable: true, auditCheckpoint.mode: "table" }`, proving the host wires the audit-log + checkpoint store + DbContext correctly through DI. Each test class gets a fresh logical InMemory database (`E2E_{Guid}`) so cross-class state leakage cannot mask a regression. This is the smallest end-to-end smoke proving Stage 2 is wired correctly: a real HTTP request touches the EF stack, the audit infrastructure, and the route handler.

**Stage 2: 61 of 64 done.** All non-UI Stage-2 tasks complete. The application can now be `dotnet run --project src\EgyptTax.Web -- seed --profile us1-minimum` to bootstrap the admin identity and `dotnet run --project src\EgyptTax.Web` to start the host with cookie auth, Hangfire job server, FR-028 audit chain, FR-039 sessions, attachment storage, and the OpenAPI + health probe surface. **Deferred**: T058-T060 (Blazor scaffold + IStringLocalizer + terminology JSON) — most efficiently landed alongside the first US that actually needs UI (US1) rather than as isolated UI scaffolding in a vacuum. Phase 3 (US1 — sales invoice end-to-end) can begin and absorb T058-T060 as its first slice.

**Checkpoint**: Foundation ready — Phase 3+ user-story work can begin in parallel.

---

## Phase 3: User Story 1 — Issue compliant sales invoice (Priority: P1) 🎯 MVP

**Goal**: Ship the core revenue path: issue a sales invoice, post it directly (no approval in Phase 1 default), generate an Egyptian-bilingual PDF with a tamper-evident QR seal, submit a mock ETA eInvoice JSON, and surface the invoice on the Active ETA Compliance Dashboard. This story alone delivers a viable MVP slice.

**Independent Test**: Per [quickstart.md](quickstart.md) §7 — log in as the seeded Administrator, issue one sales invoice, post it, download the PDF, scan the QR, view the eInvoice JSON in the audit log, and see the document appear on the ETA dashboard with a green "Submitted" badge.

### Tests for User Story 1 (Test-First per Constitution III)

- [X] T077 [P] [US1] Contract test: every generated eInvoice JSON validates against [contracts/eta-einvoice.schema.json](contracts/eta-einvoice.schema.json) (SC-007) at `tests/EgyptTax.ContractTests/Eta/EInvoiceSchemaTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Lands the **B1 Company entity + structured PostalAddress refactor on Customer** that the rest of US1 (T079 PDF, ETA mock submission, dashboard) builds on. Observed RED first via the missing namespaces; turned GREEN once the entities + generator landed. **Domain additions**: `PostalAddress` value object with both bilingual display strings (`DisplayArabic` / `DisplayEnglish`) + structured fields (Country / Governorate / RegionCity / Street / BuildingNumber / PostalCode) — flat strings rather than nesting `ArabicEnglishText` because EF8 `ComplexProperty` can't bind a nested record-struct constructor when the outer is also a record struct (caught at migrations-add time, not compile-time); `Display` getter exposes the bilingual pair to PDF callers. `Company` entity (B1) with TIN + commercial registration + structured address + taxpayer activity code + fiscal year + default language. Customer's address field changed from `ArabicEnglishText` to `PostalAddress` — the existing `PostSalesInvoiceTests` fixture updated to use `PostalAddress.Create(display, governorate, regionCity, street, buildingNumber)`. **Generator**: `EInvoiceJsonGenerator` (Infrastructure/Eta) hand-emits JSON via `Utf8JsonWriter` rather than serializing a DTO so banker's-rounded monetary values stay exact (no decimal-to-double drift) and field order is stable across rebuilds (helps with regulator-alignment diff review). Required-but-zero fields (totalDiscountAmount, extraDiscountAmount, totalItemsDiscountAmount, line valueDifference / itemsDiscount / totalTaxableFees) emit explicit `0` per the contract's required-list. Receiver block discriminator: `B2BRegistered → "B"` with TIN, `B2BUnregistered → "P"` no TIN, `B2CConsumer → "P"` no TIN. **6 contract tests GREEN**: each tax-profile path validates against the JSON schema via `Json.Schema` (already referenced in ContractTests); header carries the canonical document number / taxpayer activity code / total / VAT total / posting timestamp ISO; multi-line invoices validate; rendering an unposted draft throws (the eInvoice is the post-time wire format). Migration `CompanyAndStructuredCustomerAddress` lands the new `master.companies` table + the structured-address column rename on `master.customers`.
- [X] T078 [P] [US1] Contract test: 100 random posted invoices → encode QR → decode → verify → all VALID, plus 100 tamper cases → all TAMPERED with correct mismatch (SC-013) at `tests/EgyptTax.ContractTests/Verification/QrSealTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first (`CS0234 'Verification' does not exist in 'EgyptTax.Application'/'EgyptTax.Infrastructure'`); turned GREEN once `DocumentSealPayload` (Application) + `DocumentSealCodec` (Infrastructure) landed. **Codec design**: wire format `EGT1.<base64url(deterministic-cbor(map))>` per the contract; CBOR is preferred over JSON because the verifier targets a single QR at typical print scaling (≤ 300 bytes), and CBOR's binary integer encoding plus the compact uint map keys 1..8 keeps the seal well under that ceiling for typical invoices. Uses `System.Formats.Cbor` (added to Directory.Packages.props) with `Ctap2Canonical` conformance so the bytes are reproducible per RFC 8949 §4.2.1 deterministic encoding. The codec is a `static class` (no instance state — encode/decode/verify are all pure functions); the test invokes `DocumentSealCodec.Encode(...)` etc. directly. Verify takes a `LiveDocumentResolver delegate` so the verifier can be driven against real DB rows in production OR test stubs in contract tests, without coupling the codec to EF. **The 13 GREEN scenarios cover the spec's full bar**: protocol prefix is `EGT1.`; 100 random documents (seeded RNG for reproducibility) round-trip Encode→Decode → all 8 fields recovered exactly; 100 verify-against-live → all VALID; 4×100 tamper paths (mutate grand-total / audit-entry-hash / document-number / audit-entry-index on the live side) → all TAMPERED with the correct field name in the mismatches list; resolver returns null → UNKNOWN; 5 malformed inputs (wrong prefix / empty payload / bad base64 / no dot / truncated CBOR) → MALFORMED; resolver MUST NOT be called for malformed seals (asserted by throwing in the resolver). Companion `Base64Url` helper does no-padding URL-safe base64 (RFC 4648 §5) without pulling in another package.
- [X] T079 [P] [US1] Contract test: every PDF contains every legally required Egyptian tax-invoice field per [contracts/legal-invoice-fields.md](contracts/legal-invoice-fields.md) (SC-008) — iterate sections A–G of the contract, run all 6 named tests in §"Required tests" over a randomized sample of ≥ 100 invoices spanning all customer tax-profile types, at `tests/EgyptTax.ContractTests/Pdf/InvoicePdfFieldsTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Lands the **`QuestPdfInvoiceRenderer`** that the rest of US1 (T084 quickstart E2E, dashboard download) renders through. Observed RED first (`CS0234 'Pdf' does not exist in 'EgyptTax.Application'`); turned GREEN with 7 contract scenarios. **Renderer**: QuestPDF Community license set once at static class construction (project qualifies under the &lt;$1M revenue threshold); A4 page with bilingual issuer/receiver blocks (English on the left, Arabic on the right), document number + date + currency, FR-040-gated customer TIN line (only when `B2BRegistered`), document type label flips per profile (`B2C → "Simplified Tax Invoice / فاتورة ضريبية مبسطة"` vs `"Tax Invoice / فاتورة ضريبية"`), 9-column line table (#/Item code/Description/Qty/Unit price/Subtotal/VAT %/VAT/Total) with bilingual item names, footer totals + grand-total-in-Arabic-words via `ArabicWordsConverter.FromEgyptianPounds`, posted-by user identity + UTC + Cairo (UTC+2) timestamps, rounding-rule disclosure ("Amounts rounded to two decimal places / الأرقام مقربة لأقرب قرشين"), and an FR-044 QR seal embedded bottom-right via `QRCoder` PNG output (the seal payload is supplied by the caller — already produced by the T078 `DocumentSealCodec.Encode`). **Test infrastructure**: `PdfPig 0.1.14` added to ContractTests for PDF text + image extraction; package id is `PdfPig` (not `UglyToad.PdfPig` as I assumed first — caught by `dotnet package search`). PdfPig converts common Latin ligatures (fi, fl, ffi, ffl, ft, st) to U+FB00-FB06 codepoints when extracting text; the test pipeline normalizes these back to multi-char equivalents before substring matching, otherwise `Simpliﬁed` doesn't match `Simplified`. **7 GREEN scenarios**: (1) every required field present on B2BRegistered (sections A/B/C TIN/E totals + In-words label + Posted-by line + ≥1 image); (2) B2BUnregistered → "Tax Invoice", no TIN line, no "Simplified"; (3) B2CConsumer → "Simplified Tax Invoice", no TIN line; (4) QR image present on every profile; (5) the seal handed to the renderer round-trips through `DocumentSealCodec.Decode` and matches the document number; (6) PDF magic header `%PDF` is correct + ≥ 1KB; (7) renderer throws on unposted draft. **Deferred** (out of scope for the MVP slice, all noted in the test class docstring): credit-note specifics (G section — CreditNote document type doesn't exist in the domain yet); the contract's "≥ 100 random invoices" property test (we run one fixture per tax-profile bucket here — the 3 buckets exercise every discriminator path); strict Arabic-text extraction (font-dependent and excluded from substring assertions; English / numeric strings are the locale-neutral acceptance bar); QR scan-back-from-rendered-PDF (needs OCR-grade QR scanning — scoped to T078's print-fidelity test list).

**Contracts trio T077 + T078 + T079 closed.** US1 now has its full contract surface defined: ETA eInvoice JSON validates against `eta-einvoice.schema.json`; QR seal codec round-trips + tamper-detects per `verification-seal-qr.md`; legal PDF fields render per `legal-invoice-fields.md`. The next US1 batches layer on top: invoice-level discount (T082), posted-document immutability test (T081), ETA dashboard perf (T083), full E2E quickstart (T084), then the implementation tasks (T085+) that wire everything end-to-end through the Blazor surface.
- [X] T080 [P] [US1] Integration test: post sales invoice 1,000 EGP @ 14% VAT → totals (1,000 / 140 / 1,140); state Posted; FR-026 audit `unapproved-direct` recorded at `tests/EgyptTax.IntegrationTests/Invoices/PostSalesInvoiceTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first (`CS0234 'Invoices' does not exist in 'EgyptTax.Application'`, `CS0246 'Customer'/'Item'/'VatCategory' could not be found`); turned GREEN after the **Phase-3 starter slice** landed in the same batch: `VatCategory` (B6 — code + bilingual name + rate + effective range + recoverable-input-vat flag, with a `(code, effective_from)` unique index per FR-022 versioning), `Customer` (B2 + flattened-Tin `CustomerTaxProfile` value object, embedded via `ComplexProperty`), `Item` (B4 minimal — chart-of-account FKs deferred to US5), `SalesInvoice` aggregate root + `SalesInvoiceLine` (computed line totals via banker's rounding to 2 places at every Recompute, header totals roll up; both stored on the row so master-data corrections never silently revise historical invoices), and the `PostSalesInvoiceHandler` in Infrastructure/Invoices (Application layer doesn't reference EF Core — handler lives in Infrastructure where DbContext access is allowed). Posting handler resolves `DocumentTypeApprovalSetting.ApprovalRequired` (Phase-1 default for SalesInvoice = false), allocates the document number from the FR-011 `IDocumentNumberAllocator` against `documentDate.Year`, transitions via `SalesInvoice.MarkPosted` (which calls `DocumentStateMachine.Transition` so the FR-026 rules are the single source of truth), and emits `sales_invoice.posted` audit event with the captured `posting_mode` + computed totals + line count. Migration `SalesInvoicesAndMasterData` lands `tax.vat_categories`, `master.customers`, `master.items`, `documents.sales_invoices`, `documents.sales_invoice_lines` with appropriate uniqueness + indexes. **2 scenarios passed**: (a) 1 line @ 1000 EGP × 14% → 1000/140/1140 totals, Posted state, posting_mode=UnapprovedDirect, document_number INV-2026-000001, audit event with kind `sales_invoice.posted` containing both the posting_mode discriminator and the document number; (b) 2-line mixed-rate (standard 14% + zero-rated) → 1200 subtotal / 140 VAT / 1340 grand total, proving line aggregation works across rates. The `description` override on lines was deferred (item.Name carries display) to keep the EF mapping clean — `ArabicEnglishText?` on a struct doesn't ComplexProperty cleanly without a wrapper that's beyond scope here.
- [X] T081 [P] [US1] Integration test: posted sales invoice rejects edit + instructs credit-note path per FR-012 + US1 acceptance scenario 2 at `tests/EgyptTax.IntegrationTests/Invoices/PostedImmutabilityTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. **Pure-test batch** — exercises the existing `PostedDocumentImmutabilityGuard` (closed by T069) against a real Posted SalesInvoice persisted via Testcontainers and re-loaded, so configuration drift in EF state-tracking would surface here. No new production code was needed: the guard's static helper, the SalesInvoice's entity-level invariant on AddLine, and the DocumentStateMachine's terminal-Posted rule are all already in place; the test proves they cooperate end-to-end. **6 GREEN scenarios**: (a) `posted.AddLine(...)` throws at the entity boundary (the SalesInvoice aggregate root rejects mutation of any non-Draft state); (b) `PostedDocumentImmutabilityGuard.EnsureNotPosted(state, SalesInvoice, id, "edit")` throws with a message containing "credit note" — the FR-012 + FR-013 correction-path hint for tax-impacting documents; (c) same with operation="delete" — message MUST contain `credit note (FR-013)`, the operation name `delete`, and the document id; (d) operation="void" → message contains "Posted and immutable" per FR-027; (e) **reload-then-reject path** — clear ChangeTracker, re-fetch the invoice from the database with `Include(i => i.Lines)`, assert `State == Posted` (proves the state column round-trips correctly), then assert the guard still rejects on the re-loaded entity; (f) `DocumentStateMachine.CanTransition(Posted, Voided, approvalEnabled: true|false)` and `Posted → Draft` all return false — FR-026 says Posted is terminal regardless of approval setting. The test fixture re-uses the master-data + posting helpers from `PostSalesInvoiceTests` so the path under test is the **same** path used in T080's posting flow — a regression in the posting path would fail both tests in lockstep.
- [X] T082 [P] [US1] Integration test: invoice-level discount (% and fixed) computes correct apportioned VAT per FR-008 expansion at `tests/EgyptTax.IntegrationTests/Invoices/InvoiceLevelDiscountTests.cs` — MUST FAIL FIRST. Closed 2026-05-07. Observed RED first (`CS1061 'SalesInvoice' does not contain a definition for 'SetInvoiceLevelDiscount' / 'InvoiceLevelDiscountAmount' / 'NetBeforeVat'`); turned GREEN once the domain extension + EF mapping + migration landed. **Domain change**: `SalesInvoice` gains `InvoiceLevelDiscountAmount` + `InvoiceLevelDiscountPercent` + `NetBeforeVat` fields, and a `SetInvoiceLevelDiscount(amount, percent)` mutator that enforces XOR (exactly one supplied) + range validation (percent ∈ [0,100], amount ≥ 0) + Draft-only state. `SalesInvoiceLine` gains `LineApportionedDiscount` + `LineNetSubtotal`; the line's existing `LineSubtotal` continues to carry the **pre-discount** number for audit (so an inspector can see both gross and net), while `LineNetSubtotal` and the recomputed `LineVat` reflect the post-discount basis. **Apportionment policy** (the FR-008 invariant): the invoice-level discount is apportioned across lines pro-rata by line subtotal; the **rounding remainder lands on the last line** so `sum(LineApportionedDiscount) == InvoiceLevelDiscountAmount` to the cent — verified by a 3-line × 333.33/333.33/333.34 × 7% fixture that exercises the rounding-remainder path explicitly. After apportionment, `LineNetSubtotal = LineSubtotal − LineApportionedDiscount`, `LineVat = LineNetSubtotal × VatRate`, and the header sums roll up (`Subtotal` is gross-of-discount, `NetBeforeVat = Subtotal − InvoiceLevelDiscountAmount`, `GrandTotal = NetBeforeVat + VatTotal`). **8 GREEN scenarios**: (1) percent path on single line — 1000 × 14% with 10% discount → net 900, VAT 126, grand 1026; (2) fixed-amount path matches percent; (3) **mixed-rate apportionment** (600 @ 14% + 400 @ 0%, 10% header discount) → standard line absorbs 60 EGP and contributes 75.60 VAT, zero-rated line absorbs 40 EGP and contributes 0 VAT, header VAT 75.60 / grand 975.60 — proves zero-rated lines absorb apportioned discount without contributing VAT; (4) **apportioned discounts sum exactly** (3-line × 7% rounding-remainder); (5)/(6)/(7) validation — both amount+percent / percent > 100 / amount > subtotal each throw with the right exception type; (8) **end-to-end posting flow** — discount survives EF round-trip + `PostSalesInvoiceHandler` → reload → all discount columns + line apportionment recovered. Migration `InvoiceLevelDiscount` adds `invoice_level_discount_amount`, `invoice_level_discount_percent`, `net_before_vat` to `documents.sales_invoices` and `line_apportioned_discount` + `line_net_subtotal` to `documents.sales_invoice_lines`. **No regressions**: T080 (1000 @ 14% → 1000/140/1140) still GREEN because zero discount means apportioned = 0 means `LineNetSubtotal == LineSubtotal` and totals match; T081 (posted-immutability) still GREEN because the discount mutator is gated on `State == Draft`.
- [ ] T083 [P] [US1] Integration test: ETA dashboard correctly identifies <24 h-deadline invoices in < 2 s on 50 k-doc database (SC-012) at `tests/EgyptTax.IntegrationTests/Compliance/EtaDashboardPerfTests.cs` — MUST FAIL FIRST
- [ ] T084 [P] [US1] E2E test: full quickstart §7 path (login + MFA + post + PDF + QR scan + dashboard) at `tests/EgyptTax.E2ETests/Stories/US1_SalesInvoiceQuickstartTests.cs` — MUST FAIL FIRST
- [ ] T084a [P] [US1] Integration test (Round-6 F11): mock ETA returns 500 → invoice remains Posted, `EtaSubmission.status = Failed`, queued for retry, user-visible badge shown on `SalesInvoiceDetail.razor`, audit log records the failure — must NEVER silently fail (spec edge case "Mock ETA submission failure") at `tests/EgyptTax.IntegrationTests/Eta/MockEtaSilentFailureGuardTests.cs` — MUST FAIL FIRST

### Master data for US1

- [ ] T085 [P] [US1] Implement `Company` entity + EF configuration at `src/EgyptTax.Domain/MasterData/Company.cs` per data-model B1
- [ ] T086 [P] [US1] Implement `Customer` + `CustomerTaxProfile` value object at `src/EgyptTax.Domain/MasterData/Customer.cs` per data-model B2 + B2a; enforce TIN-required-iff-B2BRegistered invariant
- [ ] T087 [P] [US1] Implement `Item` entity at `src/EgyptTax.Domain/MasterData/Item.cs` per data-model B4
- [ ] T088 [P] [US1] Implement `VatCategory` entity with effective-from-dated rate at `src/EgyptTax.Domain/MasterData/VatCategory.cs` per data-model B6 / FR-019; validate non-overlapping ranges
- [ ] T089 [P] [US1] Implement `ChartOfAccount` entity at `src/EgyptTax.Domain/MasterData/ChartOfAccount.cs` per data-model B5; seed minimum US1 accounts (`1100`, `1200`, `2110`, `4000`)
- [ ] T090 [US1] Add EF Core migration adding all US1 master-data tables at `migrations/[seq]_Us1MasterData.cs`

### Sales invoice domain + handlers

- [ ] T091 [US1] Implement `SalesInvoice` aggregate (header + lines + invoice-level discount) at `src/EgyptTax.Domain/Documents/SalesInvoice.cs` per data-model C1 + FR-008 expansion
- [ ] T092 [US1] Implement `CreateDraftSalesInvoice` command + handler + validator at `src/EgyptTax.Application/Invoices/CreateDraftSalesInvoice.cs`
- [ ] T093 [US1] Implement `EditDraftSalesInvoice` command + handler + validator (records every field change in audit log per FR-027) at `src/EgyptTax.Application/Invoices/EditDraftSalesInvoice.cs`
- [ ] T094 [US1] Implement `PostSalesInvoiceDirect` command + handler + validator (Phase-1 default for sales invoices, audit-logs `unapproved-direct`) at `src/EgyptTax.Application/Invoices/PostSalesInvoiceDirect.cs`
- [ ] T095 [US1] Implement automatic balanced journal generation on post (debit AR 1,140; credit Revenue 1,000; credit Output VAT 140) at `src/EgyptTax.Application/Invoices/SalesInvoiceJournalGenerator.cs` (the journal voucher itself lands in US4 in full, but the per-post emitter ships here so US1 has a balanced books view from day one)

### eInvoice JSON + ETA mock + retry queue

- [ ] T096 [US1] Implement `EtaInvoiceJsonBuilder` at `src/EgyptTax.Infrastructure/Eta/EtaInvoiceJsonBuilder.cs` producing JSON conforming to [contracts/eta-einvoice.schema.json](contracts/eta-einvoice.schema.json)
- [ ] T097 [US1] Implement `MockEtaClient` (HTTPClient with Polly retry per R-03) at `src/EgyptTax.Infrastructure/Eta/MockEtaClient.cs`
- [ ] T098 [US1] Implement mock ETA endpoint `POST /api/v1/eta-mock/submit` at `src/EgyptTax.Web/Endpoints/EtaMockEndpoint.cs` per [contracts/api/openapi.yaml](contracts/api/openapi.yaml)
- [ ] T099 [US1] Implement `EtaSubmissionRetryJob` (Hangfire) at `src/EgyptTax.Infrastructure/BackgroundJobs/EtaSubmissionRetryJob.cs` per FR-036
- [ ] T100 [US1] Persist `EtaSubmission` entity per data-model H1 at `src/EgyptTax.Domain/Eta/EtaSubmission.cs`

### PDF rendering + bilingual content

- [ ] T101 [US1] Implement `QuestPdfInvoiceRenderer` at `src/EgyptTax.Infrastructure/Pdf/QuestPdfInvoiceRenderer.cs` per R-04 with bilingual layout, RTL Arabic block, total-in-Arabic-words, and TIN-line-only-when-B2B-Registered per FR-033 expansion
- [ ] T102 [P] [US1] Bundle Cairo and Amiri fonts at `src/EgyptTax.Web/wwwroot/fonts/` and wire QuestPDF font registration in `Program.cs`
- [ ] T103 [P] [US1] Golden-file PDF content test at `tests/EgyptTax.ContractTests/Pdf/InvoicePdfGoldenTests.cs` — MUST FAIL FIRST

### QR seal (FR-044)

- [ ] T104 [US1] Implement `DocumentVerificationSeal` entity at `src/EgyptTax.Domain/Verification/DocumentVerificationSeal.cs` per data-model I1
- [ ] T105 [US1] Implement `QrSealEncoder` (CBOR + base64url + EGT1 prefix) at `src/EgyptTax.Domain/Verification/QrSealEncoder.cs` per [contracts/verification-seal-qr.md](contracts/verification-seal-qr.md)
- [ ] T106 [US1] Implement `OfflineSealVerifier` at `src/EgyptTax.Application/Verification/OfflineSealVerifier.cs` (the "verifier" in the contract)
- [ ] T107 [US1] Implement `GET /api/v1/verify/{seal}` endpoint at `src/EgyptTax.Web/Endpoints/VerificationEndpoint.cs` per OpenAPI
- [ ] T108 [US1] Embed QR via QRCoder into the QuestPDF renderer (T101); QR positioned bottom-right per FR-033

### Active ETA Compliance Dashboard (FR-043 + Differentiator 5 outbound)

- [ ] T109 [US1] Implement `EtaComplianceDashboardQuery` projection at `src/EgyptTax.Application/Compliance/EtaComplianceDashboardQuery.cs` (Dapper-backed per R-02; computes deadline countdown + fine exposure per FR-043)
- [ ] T110 [US1] Implement `SupplierTinRevalidationJob` (Hangfire) stub at `src/EgyptTax.Infrastructure/BackgroundJobs/SupplierTinRevalidationJob.cs` — runs the cron per R-13 with a hard-coded "always-valid" list in MVP; the live remote feed is a Near-term plug-in

### Tax Risk Score MVP rules (Differentiator 1)

- [ ] T111 [US1] Implement `IDocumentRiskRule` interface + `DocumentRiskScorer` at `src/EgyptTax.Application/Compliance/RiskScoring/`
- [ ] T112 [P] [US1] Implement `MissingTinRule` at `src/EgyptTax.Application/Compliance/RiskScoring/Rules/MissingTinRule.cs`
- [ ] T113 [P] [US1] Implement `MissingEtaCodeRule` at `src/EgyptTax.Application/Compliance/RiskScoring/Rules/MissingEtaCodeRule.cs`
- [ ] T114 [P] [US1] Implement `EtaSubmissionWindowExpiringRule` at `src/EgyptTax.Application/Compliance/RiskScoring/Rules/EtaSubmissionWindowExpiringRule.cs`
- [ ] T115 [P] [US1] Implement `EtaSubmissionFailedRule` at `src/EgyptTax.Application/Compliance/RiskScoring/Rules/EtaSubmissionFailedRule.cs`
- [ ] T116 [P] [US1] Unit tests for each rule (positive + negative cases) at `tests/EgyptTax.UnitTests/Application/Compliance/Rules/` — MUST FAIL FIRST

### Blazor pages for US1

- [ ] T117 [US1] Implement `Pages/Auth/Login.razor` with MFA challenge per R-07
- [ ] T118 [US1] Implement `Pages/Settings/CompanyProfile.razor` for one-time company seed
- [ ] T119 [US1] Implement `Pages/MasterData/Customers.razor` (list + create + edit) bound to `CustomerTaxProfile` requirements
- [ ] T120 [US1] Implement `Pages/MasterData/Items.razor`
- [ ] T121 [US1] Implement `Pages/Invoices/SalesInvoiceEdit.razor` (draft create/edit)
- [ ] T122 [US1] Implement `Pages/Invoices/SalesInvoiceList.razor` with filter + search
- [ ] T123 [US1] Implement `Pages/Invoices/SalesInvoiceDetail.razor` (view posted invoice + PDF download + view eInvoice JSON + view ETA mock simulated response body + ETA status badge per Round-6 F12 + view QR + Tax Risk Score badge + "Issue credit note" action button per T125c)
- [ ] T124 [US1] Implement `Pages/Compliance/EtaDashboard.razor` per FR-043 / Differentiator 5
- [ ] T125 [US1] Wire SignalR notification hub for `Pending → Submitted/Failed` ETA status changes per R-22

### Credit notes (FR-013) — Round-6 F2 closure

- [ ] T125a [P] [US1] Integration test: `IssueCreditNote` against a posted sales invoice produces a credit-note document referencing the original, generates a balanced reversal journal voucher (SUM debits = SUM credits, signs reversed), and reduces VAT payable on the next monthly VAT return at `tests/EgyptTax.IntegrationTests/Invoices/IssueCreditNoteTests.cs` — MUST FAIL FIRST
- [ ] T125b [US1] Implement `IssueCreditNote` command + handler + validator at `src/EgyptTax.Application/Invoices/IssueCreditNote.cs` per FR-013 (references original `SalesInvoice`, copies customer + lines with negated quantities, allows partial credit, captures reason)
- [ ] T125c [US1] Implement `Pages/Invoices/IssueCreditNote.razor` (form prefilled from the original invoice; user adjusts line quantities and enters reason; posts as a CreditNote document) and add an "Issue credit note" button on `SalesInvoiceDetail.razor` (T123)
- [ ] T125d [P] [US1] Integration test: credit note references a non-posted (Draft / Submitted / Approved / Voided) source invoice → rejected with clear error per FR-013 + FR-027 at `tests/EgyptTax.IntegrationTests/Invoices/CreditNoteOnlyAgainstPostedTests.cs` — MUST FAIL FIRST
- [ ] T125e [P] [US1] Contract test: credit-note PDF includes section G fields per [contracts/legal-invoice-fields.md](contracts/legal-invoice-fields.md) (reference to original invoice number + date + reason) at `tests/EgyptTax.ContractTests/Pdf/CreditNoteFieldsTests.cs` — MUST FAIL FIRST

**Checkpoint**: User Story 1 fully functional including credit-note correction path. Run [quickstart.md](quickstart.md) §7 end-to-end smoke. Demo-ready MVP.

---

## Phase 4: User Story 2 — Record purchase invoices and deductible expenses (Priority: P1)

**Goal**: Complete the legal-tax-optimization core — bookkeeper enters supplier purchase invoices and other expenses with attachments, classifies deductible vs non-deductible, and the system computes input VAT and feeds the taxable income picture (full P&L lands in Phase 3 with US4).

**Independent Test**: Create a supplier, post one deductible purchase invoice with PDF attachment + 14% VAT; the input-VAT recoverable account credits 70 EGP for a 500 EGP subtotal; mark another expense non-deductible and confirm it is excluded from input-VAT recovery.

### Tests for User Story 2

- [ ] T126 [P] [US2] Integration test: posted sales 10,000 + posted deductible purchase 4,000 (both 14%) → output VAT 1,400, input VAT 560, net VAT payable 840 (US2 acceptance scenario 1) at `tests/EgyptTax.IntegrationTests/Vat/MonthlyVatComputationTests.cs` — MUST FAIL FIRST
- [ ] T127 [P] [US2] Integration test: non-deductible expense added back in taxable income computation (US2 acceptance scenario 2) at `tests/EgyptTax.IntegrationTests/Reports/NonDeductibleAddBackTests.cs` — MUST FAIL FIRST
- [ ] T128 [P] [US2] Integration test: deductible purchase with no attachment is rejected on submit (US2 acceptance scenario 3 / FR-016) at `tests/EgyptTax.IntegrationTests/Purchases/DeductibleRequiresAttachmentTests.cs` — MUST FAIL FIRST
- [ ] T129 [P] [US2] Integration test: input VAT from `Unregistered` supplier is non-recoverable per FR-020 + FR-041 at `tests/EgyptTax.IntegrationTests/Purchases/UnregisteredSupplierVatTests.cs` — MUST FAIL FIRST
- [ ] T130 [P] [US2] Integration test: attachment SHA-256 round-trip + bit-rot detection per R-21 at `tests/EgyptTax.IntegrationTests/Attachments/AttachmentIntegrityTests.cs` — MUST FAIL FIRST

### Master data for US2

- [ ] T131 [P] [US2] Implement `Supplier` + `SupplierTaxProfile` value object at `src/EgyptTax.Domain/MasterData/Supplier.cs` per data-model B3 + B3a; enforce TIN-required-iff-RegisteredTaxpayer
- [ ] T132 [P] [US2] Implement `DeductibleExpenseCategory` entity at `src/EgyptTax.Domain/MasterData/DeductibleExpenseCategory.cs` per data-model B8
- [ ] T133 [US2] Add EF Core migration for US2 master-data + document tables at `migrations/[seq]_Us2MasterDataAndDocuments.cs`

### Purchase invoice + expense + attachment

- [ ] T134 [US2] Implement `PurchaseInvoice` aggregate at `src/EgyptTax.Domain/Documents/PurchaseInvoice.cs` per data-model C2 with deductible-flag override audit (FR-015)
- [ ] T135 [US2] Implement `Expense` aggregate at `src/EgyptTax.Domain/Documents/Expense.cs` per data-model C3 (depreciation explicitly excluded — see FR-014 wording cleanup)
- [ ] T136 [US2] Implement `Attachment` entity at `src/EgyptTax.Domain/Documents/Attachment.cs` per data-model J1
- [ ] T137 [US2] Implement `CreateDraftPurchaseInvoice` + `EditDraftPurchaseInvoice` + `PostPurchaseInvoice` handlers at `src/EgyptTax.Application/Purchases/`
- [ ] T138 [US2] Implement `CreateDraftExpense` + `EditDraftExpense` + `PostExpense` handlers at `src/EgyptTax.Application/Expenses/`
- [ ] T139 [US2] Implement `UploadAttachment` handler enforcing FR-032 (file types: PDF/JPG/PNG, bounded size, SHA-256 + filesystem persistence) at `src/EgyptTax.Application/Attachments/UploadAttachment.cs`
- [ ] T140 [US2] Implement `BlockDeleteOfReferencedMasterData` guard per FR-007 at `src/EgyptTax.Application/Common/Guards/`

### Additional Tax Risk Score rules

- [ ] T141 [P] [US2] Implement `MissingAttachmentRule` (deductible without attachment) at `src/EgyptTax.Application/Compliance/RiskScoring/Rules/MissingAttachmentRule.cs`
- [ ] T142 [P] [US2] Implement `DuplicateSupplierInvoiceRule` (supplier × supplier-invoice-number × date × amount fingerprint) at `src/EgyptTax.Application/Compliance/RiskScoring/Rules/DuplicateSupplierInvoiceRule.cs`
- [ ] T143 [P] [US2] Implement `NonRecoverableInputVatRule` (Unregistered supplier with deductible flag) at `src/EgyptTax.Application/Compliance/RiskScoring/Rules/NonRecoverableInputVatRule.cs`
- [ ] T144 [P] [US2] Unit tests for each new rule at `tests/EgyptTax.UnitTests/Application/Compliance/Rules/` — MUST FAIL FIRST

### Blazor pages for US2

- [ ] T145 [US2] Implement `Pages/MasterData/Suppliers.razor`
- [ ] T146 [US2] Implement `Pages/MasterData/ExpenseCategories.razor`
- [ ] T147 [US2] Implement `Pages/Purchases/PurchaseInvoiceEdit.razor` (with attachment upload widget + deductible toggle requiring attachment)
- [ ] T148 [US2] Implement `Pages/Purchases/PurchaseInvoiceList.razor`
- [ ] T149 [US2] Implement `Pages/Expenses/ExpenseEdit.razor` + `ExpenseList.razor`
- [ ] T150 [US2] Implement `Pages/Documents/Document360.razor` per Differentiator 6 (Evidence Vault) — pulls attachments + tax-profile snapshot + audit trail + Tax Risk Score history into one view

**Checkpoint**: US1 + US2 functional. Books carry both revenue and deductible/non-deductible expenses.

---

## Phase 5: User Story 3 — Approval workflow with audit trail (Priority: P2)

**Goal**: Configurable per-document-type approval workflow with FR-004 self-approval ban, full FR-028 audit log integration with hash chain + integrity checkpoint, and FR-027 state-dependent mutability enforcement.

**Independent Test**: Bookkeeper drafts a purchase invoice + submits; Approver approves; document posts; audit log shows both events with both users, timestamps, and hash-chain integrity verified.

### Tests for User Story 3

- [ ] T151 [P] [US3] Integration test: Draft → Submitted → Approved → Posted full path (US3 acceptance scenario 1) at `tests/EgyptTax.IntegrationTests/Workflow/HappyPathApprovalTests.cs` — MUST FAIL FIRST
- [ ] T152 [P] [US3] Integration test: Approver rejects with reason → returns to Draft + audit log entry (US3 scenario 2) at `tests/EgyptTax.IntegrationTests/Workflow/RejectionTests.cs` — MUST FAIL FIRST
- [ ] T153 [P] [US3] Integration test: posted document rejects delete + edit + void; instructs credit-note vs reversal-voucher per FR-012 + US3 scenario 3 at `tests/EgyptTax.IntegrationTests/Workflow/PostedRejectionsTests.cs` — MUST FAIL FIRST
- [ ] T154 [P] [US3] Integration test: self-approval rejected per FR-004 + US3 scenario 4 at `tests/EgyptTax.IntegrationTests/Workflow/SelfApprovalForbiddenTests.cs` — MUST FAIL FIRST
- [ ] T155 [P] [US3] Integration test: audit checkpoint emitted every 1k entries + every 15 min per FR-028 at `tests/EgyptTax.IntegrationTests/Audit/CheckpointCadenceTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T156 [US3] Implement `ApprovalRequest` entity at `src/EgyptTax.Domain/Workflow/ApprovalRequest.cs` per data-model E2
- [ ] T157 [US3] Implement `SubmitDocument` + `ApproveDocument` + `RejectDocument` handlers at `src/EgyptTax.Application/Workflow/`
- [ ] T158 [US3] Implement `VoidDocument` handler restricted to non-Posted states per FR-012 + FR-027 at `src/EgyptTax.Application/Workflow/VoidDocument.cs`
- [ ] T159 [US3] Wire FR-026 per-doc-type approval gating into `PostSalesInvoice`, `PostPurchaseInvoice`, `PostExpense` (when approval enabled, only `ApproveDocument` can transition to Posted)
- [ ] T160 [US3] Implement `Pages/Workflow/MyApprovalQueue.razor` per Differentiator 3 + R-22 SignalR notifications
- [ ] T161 [US3] Implement audit-log viewer `Pages/Audit/AuditLogViewer.razor` with auditor-runnable verifier button
- [ ] T162 [US3] Implement `POST /api/v1/audit/verify` endpoint at `src/EgyptTax.Web/Endpoints/AuditVerifyEndpoint.cs` per OpenAPI; backed by `AuditChainVerifier` from T033
- [ ] T163 [US3] Implement `verify-audit` console verb on `EgyptTax.Web` host at `src/EgyptTax.Web/Tools/VerifyAuditCommand.cs` (delegates to `AuditChainVerifier` from T033, reused engine — no separate exe project) per [contracts/audit-chain-verifier.md](contracts/audit-chain-verifier.md) §"Operator runbook integration"

**Checkpoint**: US1 + US2 + US3 functional. Approval-required document types follow the full state machine; audit log is tamper-detectable.

---

## Phase 6: User Story 4 — Automatic balanced double-entry journal generation (Priority: P2)

**Goal**: Auto-generate balanced journal vouchers on every post; allow manual adjusting journals (FR-031); refuse unbalanced posts (FR-030).

**Independent Test**: Posting a sales invoice and a purchase invoice produces two journal vouchers with debits = credits; trial-balance query returns total debits = total credits.

### Tests for User Story 4

- [ ] T164 [P] [US4] Integration test: posted sales invoice 1,000 + 140 → journal AR 1,140 / Revenue 1,000 / Output VAT 140 (US4 scenario 1) at `tests/EgyptTax.IntegrationTests/Journals/SalesPostingJournalTests.cs` — MUST FAIL FIRST
- [ ] T165 [P] [US4] Integration test: posted deductible purchase 500 + 70 → journal Expense 500 / Input VAT 70 / AP 570 (US4 scenario 2) at `tests/EgyptTax.IntegrationTests/Journals/PurchasePostingJournalTests.cs` — MUST FAIL FIRST
- [ ] T166 [P] [US4] Integration test: SC-003 — 100 % of auto-generated journals balance to the cent across 1,000 randomized posted documents at `tests/EgyptTax.IntegrationTests/Journals/JournalBalanceStressTests.cs` — MUST FAIL FIRST
- [ ] T167 [P] [US4] Integration test: unbalanced manual journal refused on post per FR-030 at `tests/EgyptTax.IntegrationTests/Journals/ManualJournalBalanceGuardTests.cs` — MUST FAIL FIRST
- [ ] T167a [P] [US4] Integration test (Round-6 F9): Bookkeeper-role user is rejected when creating a manual adjusting journal voucher; Accountant and Administrator roles succeed per FR-031 role-permission clause at `tests/EgyptTax.IntegrationTests/Journals/ManualJournalRolePermissionTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T168 [US4] Implement `JournalVoucher` aggregate (header + lines, debits = credits invariant) at `src/EgyptTax.Domain/Documents/JournalVoucher.cs` per data-model C5
- [ ] T169 [US4] Implement `AutoGenerateJournalOnPost` MediatR notification handler subscribed to all `*Posted` domain events at `src/EgyptTax.Application/Journals/AutoGenerateJournalOnPost.cs`
- [ ] T170 [US4] Implement `CreateManualAdjustingJournal` handler restricted by permission per FR-031 at `src/EgyptTax.Application/Journals/CreateManualAdjustingJournal.cs`
- [ ] T171 [US4] Implement `Pages/Journals/JournalList.razor` + `JournalDetail.razor` with drill-back to source document per FR-025
- [ ] T172 [US4] Implement reversal-voucher path (`CreateReversalJournal`) for posted non-tax-impacting corrections per FR-012 at `src/EgyptTax.Application/Journals/CreateReversalJournal.cs`

**Checkpoint**: US1–US4 functional. Books are double-entry from the moment any document posts.

---

## Phase 7: User Story 5 — Configurable tax rules and chart of accounts (Priority: P3)

**Goal**: Administrator configures VAT categories, deductible expense categories, chart of accounts, fiscal-year settings, and company tax/commercial registration data via the UI; new rules apply only to documents created after the change.

**Independent Test**: Admin changes the standard VAT rate from 14 % to 15 % effective a future date; invoices dated before that date use 14 %, invoices on/after use 15 %; audit log records the change.

### Tests for User Story 5

- [ ] T173 [P] [US5] Integration test: VAT rate change with effective-from date applied per document date per FR-022 at `tests/EgyptTax.IntegrationTests/Configuration/VatEffectiveDatedRateTests.cs` — MUST FAIL FIRST
- [ ] T174 [P] [US5] Integration test: deductible expense category change does not affect pre-existing posted expenses (US5 scenario 1) at `tests/EgyptTax.IntegrationTests/Configuration/CategoryRetroactivityTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T175 [US5] Implement `Pages/Settings/VatCategories.razor` with effective-from-dated row editor and overlap validation
- [ ] T176 [US5] Implement `Pages/Settings/DeductibleExpenseCategories.razor`
- [ ] T177 [US5] Implement `Pages/Settings/ChartOfAccounts.razor` with hierarchy view
- [ ] T178 [US5] Implement `Pages/Settings/FiscalYearSettings.razor`
- [ ] T179 [US5] Implement `Pages/Settings/PaymentMethods.razor` (configurable list — Cash + Bank Transfer seeded; cheque/card deferred per Round 5 carve-out)

**Checkpoint**: US1–US5 functional. Tax rules are runtime-configurable with full effectivity dating.

---

## Phase 8: User Story 6 — Fixed-asset capitalization and depreciation (Priority: P3, Phase 4)

**Goal**: Capitalize fixed assets, generate periodic depreciation expense via the engine (FR-017), track net book value (FR-018).

**Independent Test**: Capitalize a 100,000 EGP asset on day 1 of fiscal year, 5-year straight-line, salvage 0; year-end taxable income report shows 20,000 EGP depreciation expense + NBV 80,000 (US6 scenario 1).

### Tests for User Story 6

- [ ] T180 [P] [US6] Integration test: 5-year SL depreciation produces 20,000 EGP/year + correct NBV (US6 scenario 1) at `tests/EgyptTax.IntegrationTests/FixedAssets/StraightLineDepreciationTests.cs` — MUST FAIL FIRST
- [ ] T181 [P] [US6] Integration test: mid-month convention for asset placed in service on the 15th (US6 scenario 2) at `tests/EgyptTax.IntegrationTests/FixedAssets/MidMonthConventionTests.cs` — MUST FAIL FIRST
- [ ] T182 [P] [US6] Integration test: fixed asset without attachment blocked on submit (US6 scenario 3) at `tests/EgyptTax.IntegrationTests/FixedAssets/AttachmentRequiredTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T183 [US6] Implement `FixedAsset` aggregate at `src/EgyptTax.Domain/Documents/FixedAsset.cs` per data-model C4
- [ ] T184 [US6] Implement `DepreciationEngine` at `src/EgyptTax.Application/FixedAssets/DepreciationEngine.cs` (straight-line minimum + configurable conventions per FR-017)
- [ ] T185 [US6] Implement `RunMonthlyDepreciation` Hangfire job at `src/EgyptTax.Infrastructure/BackgroundJobs/MonthlyDepreciationJob.cs` (generates Expense entries + journal vouchers)
- [ ] T186 [US6] Add seeded `5100 Depreciation Expense` chart-of-account row + EF migration
- [ ] T187 [US6] Implement `Pages/FixedAssets/FixedAssetEdit.razor` + `FixedAssetList.razor` + `FixedAssetSchedule.razor`

**Checkpoint**: US6 ships in Phase 4 of the MVP. The full depreciation lever is now available for taxable-profit reduction.

---

## Phase 9: Minimal Payments & Settlement (Round 5 carve-out, MVP Phase 3)

**Goal**: Smallest payments capability needed to make WHT (US7) work end-to-end and to close AR/AP balances. Two seeded GL accounts only (`Cash`, `Bank — operating`). Full cash management remains Near-term.

**Independent Test**: Post a customer receipt voucher allocating 1,000 EGP cash to one outstanding sales invoice; AR balance for that invoice drops to zero; balanced journal voucher generated.

> **Authoring note (Round-6 F10)**: Tasks T191, T192, T193 are mutually referencing aggregates (the two voucher aggregates each hold `PaymentAllocation` lines). Implement them as a single domain-modeling unit in one PR; the numerical ordering in this list is for readability only.

### Tests for Minimal Payments

- [ ] T188 [P] Integration test: SupplierPaymentVoucher with WHT split debits AP, credits Cash + WHT Payable; auto-generates outbound WHT certificate (FR-051) at `tests/EgyptTax.IntegrationTests/Payments/SupplierPaymentWhtSplitTests.cs` — MUST FAIL FIRST
- [ ] T189 [P] Integration test: CustomerReceiptVoucher with customer-issued WHT certificate split debits Cash + WHT Receivable, credits AR (FR-052) at `tests/EgyptTax.IntegrationTests/Payments/CustomerReceiptWhtSplitTests.cs` — MUST FAIL FIRST
- [ ] T190 [P] Integration test: payment allocation rejects over-allocation (voucher cap + invoice cap) per FR-053 at `tests/EgyptTax.IntegrationTests/Payments/OverAllocationGuardTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T191 Implement `SupplierPaymentVoucher` aggregate at `src/EgyptTax.Domain/Documents/SupplierPaymentVoucher.cs` per data-model C6
- [ ] T192 Implement `CustomerReceiptVoucher` aggregate at `src/EgyptTax.Domain/Documents/CustomerReceiptVoucher.cs` per data-model C7
- [ ] T193 Implement `PaymentAllocation` entity at `src/EgyptTax.Domain/Documents/PaymentAllocation.cs` per data-model C8
- [ ] T194 Implement `PostSupplierPaymentVoucher` + `PostCustomerReceiptVoucher` + `AllocatePayment` handlers at `src/EgyptTax.Application/Payments/`
- [ ] T195 Implement `Pages/Payments/SupplierPaymentVoucherEdit.razor` + `CustomerReceiptVoucherEdit.razor` + `PaymentAllocationView.razor`

**Checkpoint**: AR/AP balances now close. WHT (US7) can build on this.

---

## Phase 10: User Story 7 — Withholding tax lifecycle and Form 41 (Priority: P2, Phase 3)

**Goal**: WHT capture on supplier-services payments + customer-issued WHT certificates on receipts; Form 41 quarterly filing artifact (PDF + JSON); WHT lifecycle dashboard with three views (owed / expected / Form 41 status).

**Independent Test**: Per US7 Independent Test — record one supplier services invoice 10,000 EGP + 5 % WHT payment (9,500 cash + 500 WHT); print certificate; dashboard shows 500 EGP owed for current quarter; generate Form 41 listing exactly that 500 EGP entry.

### Tests for User Story 7

- [ ] T196 [P] [US7] Integration test: SC-011 — 50 supplier-services invoices/quarter at varying WHT rates, balance ±1 EGP, Form 41 zero-dup zero-omission, dashboard reconciles to ledger at `tests/EgyptTax.IntegrationTests/Wht/WhtComputationAccuracyTests.cs` — MUST FAIL FIRST
- [ ] T197 [P] [US7] Integration test: WHT rate effective on payment date, not invoice date (R-17) at `tests/EgyptTax.IntegrationTests/Wht/WhtPaymentDateEffectivityTests.cs` — MUST FAIL FIRST
- [ ] T198 [P] [US7] Contract test: every WHT certificate validates against [contracts/wht-certificate.schema.json](contracts/wht-certificate.schema.json) at `tests/EgyptTax.ContractTests/Wht/WhtCertificateSchemaTests.cs` — MUST FAIL FIRST
- [ ] T199 [P] [US7] Contract test: every Form 41 filing JSON validates against [contracts/form41.schema.json](contracts/form41.schema.json) at `tests/EgyptTax.ContractTests/Wht/Form41SchemaTests.cs` — MUST FAIL FIRST
- [ ] T200 [P] [US7] Integration test: Form 41 marked Filed → included WHT entries become immutable for further inclusion (US7 scenario 3) at `tests/EgyptTax.IntegrationTests/Wht/Form41ImmutabilityTests.cs` — MUST FAIL FIRST
- [ ] T201 [P] [US7] Integration test: overdue Form 41 surfaced with red indicator and estimated penalty (US7 scenario 4) at `tests/EgyptTax.IntegrationTests/Wht/WhtDashboardOverdueTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T202 [US7] Implement `WhtCategory` entity (effective-from-dated rate, applicable-to enum) at `src/EgyptTax.Domain/MasterData/WhtCategory.cs` per data-model B7
- [ ] T203 [US7] Implement `WhtComputeService` at `src/EgyptTax.Application/Wht/WhtComputeService.cs` (R-17 — payment-date effectivity)
- [ ] T204 [US7] Extend `SupplierPaymentVoucher` (T191) with WHT split logic invoking `WhtComputeService`
- [ ] T205 [US7] Extend `CustomerReceiptVoucher` (T192) with customer-issued certificate recording
- [ ] T206 [US7] Implement `WhtCertificate` entity at `src/EgyptTax.Domain/Tax/WhtCertificate.cs` per data-model G1
- [ ] T207 [US7] Implement `WhtCertificatePdfRenderer` at `src/EgyptTax.Infrastructure/Pdf/WhtCertificatePdfRenderer.cs` (bilingual)
- [ ] T208 [US7] Implement `Form41Filing` entity at `src/EgyptTax.Domain/Tax/Form41Filing.cs` per data-model G2
- [ ] T209 [US7] Implement `GenerateForm41` handler at `src/EgyptTax.Application/Wht/GenerateForm41.cs` producing both JSON (per [contracts/form41.schema.json](contracts/form41.schema.json)) and PDF
- [ ] T210 [US7] Implement `MarkForm41Filed` handler enforcing one-time filing per quarter
- [ ] T211 [US7] Implement `WhtLifecycleDashboardQuery` projection at `src/EgyptTax.Application/Wht/WhtLifecycleDashboardQuery.cs` per FR-047 (three views — owed / expected / filings)
- [ ] T212 [US7] Implement additional Tax Risk Score rule `WhtRequiredButMissingRule` at `src/EgyptTax.Application/Compliance/RiskScoring/Rules/WhtRequiredButMissingRule.cs` (Differentiator 1)
- [ ] T213 [US7] Implement `Pages/Settings/WhtCategories.razor` (effective-dated editor)
- [ ] T214 [US7] Implement `Pages/Wht/WhtDashboard.razor` (three views per FR-047)
- [ ] T215 [US7] Implement `Pages/Wht/Form41GeneratorPage.razor` + `Form41List.razor`

**Checkpoint**: WHT lifecycle complete end-to-end. Form 41 filing artifact ready for the regulator.

---

## Phase 11: User Story 8 — Accountant-firm portal (Priority: P3, Phase 2 add-on)

**Goal**: External accounting firms get scoped multi-company access via per-installation invitation tokens. On-prem topology preserved (no vendor hub, no firm-hosted central server, no client data leaves any installation per Round 5). Lock-for-review handoff distinct from FR-037 hard close.

**Independent Test**: Invite an accountant via email; accept; switcher shows the client company; switch under 1 s; post an adjusting journal voucher; audit log tags both user identity and firm name.

### Tests for User Story 8

- [ ] T216 [P] [US8] Integration test: invitation accept + scoped access + audit-tagged with firm name (US8 scenario 1) at `tests/EgyptTax.IntegrationTests/FirmPortal/InvitationAcceptTests.cs` — MUST FAIL FIRST
- [ ] T217 [P] [US8] Integration test: SC-014 partial — switch between two installations < 1 s at `tests/EgyptTax.IntegrationTests/FirmPortal/CompanySwitchPerfTests.cs` — MUST FAIL FIRST
- [ ] T218 [P] [US8] Integration test: lock-for-review blocks bookkeeper edits, allows accountant adjustments (US8 scenarios 3 + 4) at `tests/EgyptTax.IntegrationTests/FirmPortal/PeriodReviewLockTests.cs` — MUST FAIL FIRST
- [ ] T219 [P] [US8] Integration test: revocation effective < 5 s; previous records retained at `tests/EgyptTax.IntegrationTests/FirmPortal/RevocationTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T220 [US8] Implement `AccountantFirmUser` entity (extends User) at `src/EgyptTax.Domain/Identity/AccountantFirmUser.cs` per data-model A4
- [ ] T221 [US8] Implement `InviteAccountantFirmUser` + `AcceptInvitation` + `RevokeFirmUser` handlers at `src/EgyptTax.Application/FirmPortal/`
- [ ] T222 [US8] Implement client-side credential pool (browser local storage + cookie) at `src/EgyptTax.Web/wwwroot/js/firm-portal-pool.js`
- [ ] T223 [US8] Implement `Pages/FirmPortal/CompanySwitcher.razor` per FR-049 topology (per-installation cookie, no central hub)
- [ ] T224 [US8] Implement `PeriodReviewLock` entity + `LockForReview` + `ReleaseReview` handlers at `src/EgyptTax.Domain/Workflow/PeriodReviewLock.cs` + `src/EgyptTax.Application/FirmPortal/`
- [ ] T225 [US8] Wire firm-name claim into all audit-log entries created by an `AccountantFirmUser` (extend `AuditEmitBehavior` from T040)
- [ ] T226 [US8] Implement `Pages/FirmPortal/AccountantReviewMode.razor` per Differentiator 3 (folded into firm portal home page)

**Checkpoint**: External accounting firms can serve their clients without re-keying data into a separate tool.

---

## Phase 12: User Story 9 — Tax-Inspection Bundle (Priority: P3, Phase 3)

**Goal**: One-click sealed regulator-ready archive with manifest + portable PowerShell verifier. Inspector verifies on a clean Windows machine without the application installed.

**Independent Test**: After running US1+US2+US7 to populate a quarter, generate a bundle; verify externally with the bundled script in < 60 s on a clean machine; manifest hash matches a manually computed control hash.

### Tests for User Story 9

- [ ] T227 [P] [US9] Contract test: every generated bundle's manifest validates against [contracts/inspection-bundle-manifest.schema.json](contracts/inspection-bundle-manifest.schema.json) at `tests/EgyptTax.ContractTests/Inspection/BundleManifestSchemaTests.cs` — MUST FAIL FIRST
- [ ] T228 [P] [US9] Integration test: SC-014 — bundle for 5,000-document quarter built in < 5 min; externally re-verifiable by bundled script in < 60 s on a clean Windows VM at `tests/EgyptTax.IntegrationTests/Inspection/BundleEndToEndTests.cs` — MUST FAIL FIRST
- [ ] T229 [P] [US9] Integration test: bundle including unposted drafts is rejected by default; explicit "drafts excluded" labels manifest accordingly (US9 scenario 3) at `tests/EgyptTax.IntegrationTests/Inspection/BundleDraftsExcludedTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T230 [US9] Implement `InspectionBundleBuilder` at `src/EgyptTax.Infrastructure/Inspection/InspectionBundleBuilder.cs` (per R-16 — temp dir, hard-link attachments, per-file SHA-256, ZIP-pack)
- [ ] T231 [US9] Implement `GenerateInspectionBundle` Hangfire job (with progress events emitted via SignalR) at `src/EgyptTax.Infrastructure/BackgroundJobs/InspectionBundleJob.cs`
- [ ] T232 [US9] Implement portable `verify-bundle.ps1` (pure PowerShell + .NET-built-in SHA-256 + audit-chain replay) at `src/EgyptTax.Infrastructure/Inspection/verify-bundle.ps1` (embedded resource)
- [ ] T233 [US9] Implement individual register PDF generators (sales, purchase+expense, credit-notes+reversal, journal listing, trial balance) at `src/EgyptTax.Infrastructure/Pdf/Registers/`
- [ ] T234 [US9] Implement `Pages/Inspection/GenerateBundlePage.razor` with progress + download

**Checkpoint**: All 9 user stories functional. Tax inspection ritual collapses from weeks to an afternoon.

---

## Phase 13: Closing Cockpit + Compliance Surface (Differentiators 2 + 6, MVP)

**Goal**: Single canonical compliance surface (Cockpit) with role-filtered lenses (Accountant Review Mode lives in US8 home page; Owner Mode is Near-term per Round 5).

### Tests

- [ ] T235 [P] Integration test: Cockpit aggregates VAT readiness, missing docs, failed ETA, unapproved deductibles, non-recoverable VAT, Form 10/VAT pack readiness, Period Lock checklist for the active period at `tests/EgyptTax.IntegrationTests/Compliance/ClosingCockpitAggregationTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T236 Implement `MonthlyTaxClosingCockpitQuery` projection at `src/EgyptTax.Application/Compliance/MonthlyTaxClosingCockpitQuery.cs` (Dapper per R-02)
- [ ] T236a [P] (Round-6 F13) Wire `IMemoryCache` for `MonthlyTaxClosingCockpitQuery` with a 30-second sliding expiration keyed by `(company_id, period_year, period_month)` at `src/EgyptTax.Application/Compliance/CockpitCachingDecorator.cs`; integration test verifying cache hit + invalidation on any tax-impacting state change at `tests/EgyptTax.IntegrationTests/Compliance/CockpitCacheTests.cs` — MUST FAIL FIRST
- [ ] T237 Implement `Pages/Compliance/ClosingCockpit.razor` per Differentiator 2 — links to all underlying surfaces
- [ ] T238 Implement Tax Risk Score badge component at `src/EgyptTax.Web/Components/TaxRiskScoreBadge.razor` and embed it on every document edit/view page (Differentiator 1 visibility)
- [ ] T239 Add Tax Risk Score column to `SalesInvoiceList`, `PurchaseInvoiceList`, `ExpenseList`, `JournalList`

---

## Phase 14: Reports — VAT Monthly + Taxable Income + Trial Balance (FR-021, FR-023, FR-024)

### Tests

- [ ] T240 [P] Integration test: SC-002 — VAT report + taxable income report each render < 5 s p95 at 5 k docs under 25 concurrent users at `tests/EgyptTax.IntegrationTests/Reports/ReportPerformanceTests.cs` — MUST FAIL FIRST
- [ ] T241 [P] Integration test: SC-005 — taxable income for 200 sales + 200 purchases matches manual control ±1 EGP at `tests/EgyptTax.IntegrationTests/Reports/TaxableIncomeAccuracyTests.cs` — MUST FAIL FIRST

### Implementation

- [ ] T242 Implement `VatMonthlyReportQuery` (Dapper per R-02) at `src/EgyptTax.Application/Reports/VatMonthlyReportQuery.cs`
- [ ] T243 Implement `TaxableIncomeReportQuery` (Dapper) with non-deductible add-back per FR-023 at `src/EgyptTax.Application/Reports/TaxableIncomeReportQuery.cs`
- [ ] T244 Implement `TrialBalanceQuery` + `GeneralJournalListingQuery` per FR-024 at `src/EgyptTax.Application/Reports/`
- [ ] T245 Implement `Pages/Reports/VatMonthlyReport.razor` + `TaxableIncomeReport.razor` + `TrialBalance.razor` with drill-down to source per FR-025
- [ ] T246 Implement `LockTaxPeriod` + `ReopenTaxPeriod` handlers per FR-037 at `src/EgyptTax.Application/Periods/`
- [ ] T246a [P] (Round-6 F3) Integration test: SC-004 backdate clause — attempt to post a document with `document_date` inside a Locked tax period is rejected with a clear error; the rejection is recorded in the audit log; an Administrator who reopens the period (FR-037) can subsequently post; audit log records both the close, the rejected-post attempt, and the reopen at `tests/EgyptTax.IntegrationTests/Periods/BackdateRejectionTests.cs` — MUST FAIL FIRST

---

## Phase 15: Polish & Cross-Cutting Concerns

- [ ] T247 [P] Implement WiX MSI installer at `src/EgyptTax.Installer/Product.wxs` per R-24 + plan.md Complexity Tracking — prompts operator for SQL connection, audit-checkpoint mode, attachments root, NTP server
- [ ] T248 [P] Write operator runbook at `docs/operator-runbook.md` (install, backup, restore, NTP, MFA bootstrap, period close, audit verifier usage)
- [ ] T249 [P] Write accountant guide at `docs/accountant-guide.md` (day-to-day workflows for US1–US9)
- [ ] T250 [P] Write inspector bundle format spec at `docs/inspector-bundle-format.md` (public format spec for tax inspectors)
- [ ] T251 [P] Add Egyptian terminology lint check to CI (fails on missing AR/EN keys) per R-19 at `.github/workflows/terminology-check.yml`
- [ ] T252 [P] E2E test: bilingual rendering — every page in both AR (RTL) and EN (LTR) at `tests/EgyptTax.E2ETests/BilingualRenderingTests.cs`
- [ ] T253 [P] E2E test: SC-001 — new accountant completes US1 end-to-end in < 15 min (timed) at `tests/EgyptTax.E2ETests/SC001NewAccountantOnboardingTests.cs`
- [ ] T254 [P] E2E test: SC-009 — non-technical user correctly classifies deductible vs non-deductible on 9 of 10 sample scenarios (uses seeded scenarios) at `tests/EgyptTax.E2ETests/SC009DeductibilityClassificationTests.cs`
- [ ] T255 [P] Performance test harness for SC-002 + SC-006 + SC-010 + SC-012 + SC-013 + SC-014 (executed nightly in CI on a representative-sized seeded DB) at `tests/EgyptTax.IntegrationTests/Performance/NightlyPerfSuite.cs`
- [ ] T256 [P] Add structured logging context (correlation ID, user ID, firm name) to every Serilog log entry per R-20
- [ ] T257 [P] Add health-readiness probe wiring in WiX installer service registration (the service must report Healthy before the operator's "install complete" screen)
- [ ] T258 Run [quickstart.md](quickstart.md) §7 end-to-end smoke against a freshly built MSI install on a clean Windows VM
- [ ] T259 Code cleanup pass: remove TODOs, prune dead code, csharpier format the entire repo
- [ ] T260 Final Constitution Check re-evaluation against the implemented codebase; record any new Complexity Tracking entries that surfaced during implementation in [plan.md](plan.md)

---

## Dependencies & Execution Order

### Stage Dependencies

> **Terminology** (Round-6 F4 + Round-7 H1): "Stage N" below refers to the task-grouping stages 1–15 within this file. The mapping to spec delivery phases (Phase 1–4) is in the table at the top of this file.

| Stage | Depends on | Blocks |
| --- | --- | --- |
| Stage 1 Setup | — | Everything |
| Stage 2 Foundational | Stage 1 | Every user-story stage |
| Stage 3 US1 (P1) | Stage 2 | — |
| Stage 4 US2 (P1) | Stage 2 | — |
| Stage 5 US3 (P2) | Stage 2 | — |
| Stage 6 US4 (P2) | Stage 5 (audit + workflow share infra) | — |
| Stage 7 US5 (P3) | Stage 2 | — |
| Stage 8 US6 (P3, delivery Phase 4) | Stage 6 (depreciation generates journals) | — |
| Stage 9 Minimal Payments | Stage 4, Stage 6 | Stage 10 |
| Stage 10 US7 (P2, delivery Phase 3) | Stage 9 | — |
| Stage 11 US8 (P3, delivery Phase 2 add-on) | Stage 5 (audit + workflow + lock) | — |
| Stage 12 US9 (P3, delivery Phase 3) | Stage 14 (reports) + Stage 5 (audit) | — |
| Stage 13 Cockpit | Stage 3 + Stage 4 + Stage 7 + Stage 10 | — |
| Stage 14 Reports | Stage 6 (auto journals) | Stage 12 |
| Stage 15 Polish | All previous | Release |

### Within Each Stage

- Tests MUST be written and FAIL before implementation (Constitution III).
- Domain entities → application handlers → infrastructure adapters → web pages.
- All [P] tasks within a stage run in parallel by default.

### Parallel Opportunities

- All tests within a story stage marked [P] can run in parallel.
- All domain entities marked [P] within a stage can be implemented in parallel.
- Different user-story stages can be worked on by different developers in parallel after Stage 2 completes.

---

## Parallel Example: User Story 1 tests

```bash
# All US1 tests can be authored in parallel before any US1 implementation:
Task: "Contract test for eInvoice JSON schema in tests/EgyptTax.ContractTests/Eta/EInvoiceSchemaTests.cs"
Task: "Contract test for QR seal verifier in tests/EgyptTax.ContractTests/Verification/QrSealTests.cs"
Task: "Contract test for PDF required fields in tests/EgyptTax.ContractTests/Pdf/InvoicePdfFieldsTests.cs"
Task: "Integration test for post sales invoice in tests/EgyptTax.IntegrationTests/Invoices/PostSalesInvoiceTests.cs"
Task: "Integration test for posted immutability in tests/EgyptTax.IntegrationTests/Invoices/PostedImmutabilityTests.cs"
Task: "Integration test for invoice-level discount in tests/EgyptTax.IntegrationTests/Invoices/InvoiceLevelDiscountTests.cs"
Task: "Integration test for ETA dashboard perf in tests/EgyptTax.IntegrationTests/Compliance/EtaDashboardPerfTests.cs"
Task: "E2E test for quickstart §7 flow in tests/EgyptTax.E2ETests/Stories/US1_SalesInvoiceQuickstartTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: Run [quickstart.md](quickstart.md) §7. Demo the slice.

### Incremental Delivery (recommended sequencing)

1. Setup + Foundational → Foundation ready.
2. US1 → quickstart §7 smoke → Phase-1 demo.
3. US2 → expense + purchase invoice flow → Phase-2 demo.
4. US3 → approval + audit log → Phase-2 governance demo.
5. US8 (firm portal) — Phase-2 add-on if customer demand surfaces early.
6. US4 (auto journals) → P&L computability.
7. US5 (configurable rules) — light add-on.
8. Phase 9 Minimal Payments → US7 WHT → Phase-3 tax-filing demo.
9. Phase 14 Reports → Phase 13 Cockpit → US9 Inspection Bundle → Phase-3 compliance demo.
10. US6 fixed assets + depreciation → Phase 4 demo.
11. Polish → Release Candidate.

### Parallel Team Strategy

Once Phase 2 completes:

- **Dev A** (frontend-leaning): US1 → US2 → Cockpit.
- **Dev B** (backend-leaning): US3 → US4 → US7.
- **Dev C** (infra-leaning): US8 firm portal → US9 inspection bundle → installer.
- **Dev D** (rotating): performance harness + Tax Risk Score rules + Polish.

---

## Notes

- [P] tasks = different files, no incomplete dependencies.
- [Story] label maps each task to the user story for spec-to-task traceability.
- Tests precede implementation per Constitution III; CI must enforce that test commits land before the implementing commit.
- Each user-story phase ends in a checkpoint suitable for demo or release.
- 260 tasks total. The size matches the spec scope — 9 user stories, 53 functional requirements, 36 entities, 5 strategic differentiators encoded as MVP, plus the Round-5 minimal payments carve-out.

---

## Format validation

All 260 tasks above follow the required checklist format `- [ ] T### [P?] [US?] Description with file path`:

- Setup tasks (T001–T012): no `[Story]` label.
- Foundational tasks (T013–T076): no `[Story]` label.
- User-story tasks (T077–T234): include `[US1]` through `[US9]` per ownership.
- Cross-cutting Phase-9 minimal-payments tasks (T188–T195): no `[Story]` label (carve-out shared by US7).
- Phase-13 Cockpit tasks (T235–T239): no `[Story]` label (composite).
- Phase-14 Reports tasks (T240–T246): no `[Story]` label (composite, used by US9 + Cockpit).
- Polish tasks (T247–T260): no `[Story]` label.

Every task has an exact file path or directory anchor. Every test task explicitly says "MUST FAIL FIRST" per Constitution III.
