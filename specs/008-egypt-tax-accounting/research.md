# Phase 0 Research: Egyptian Tax Accounting MVP

**Date**: 2026-05-07 | **Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md)

This document resolves every Technical-Context decision point with a chosen approach, the rationale, and the alternatives considered. Each decision is anchored on the spec's requirements (FR-* / SC-*) so that downstream tasks can cite both the requirement and the chosen technology.

---

## R-01 — Web hosting model: Blazor Server vs Blazor WebAssembly

**Decision**: Blazor Server (server-rendered, SignalR for interactivity).

**Rationale**:
- On-prem deployment (Round 1) means the server is on the customer's LAN; SignalR latency is negligible and the WebAssembly download cost is unjustified.
- All authoritative tax/audit logic must run server-side anyway (FR-028 audit chain, FR-011 numbering, FR-053 over-allocation rejection); WebAssembly would duplicate validation.
- Server-rendered HTML simplifies bilingual SEO-irrelevant pages, RTL handling, and printable-style reports.
- Single deployment artifact (one .dll, no JS toolchain), aligning with the WiX MSI installer.
- FR-039 session timeout maps cleanly to SignalR circuit lifetime.

**Alternatives considered**:
- Blazor WebAssembly + minimal API: rejected (duplication of validation; download cost; harder MFA flow).
- Razor Pages / MVC: rejected (no real-time SignalR push for in-app notifications; verbose for the 9-story scope).
- React/Next.js + .NET API: rejected (two toolchains, two deployment artifacts; no JS expertise need surfaced in the user input).

---

## R-02 — Data access: EF Core 8 vs Dapper vs both

**Decision**: EF Core 8 as the primary ORM; **Dapper used selectively** for two read paths only (Tax-Inspection Bundle export of large registers per FR-048, and Cockpit aggregate queries per Differentiator 2). All write paths and most reads use EF Core.

**Rationale**:
- EF Core's change tracking integrates cleanly with the MediatR transaction pipeline behavior (one DbContext per request, commit on handler success, audit-emit before commit).
- LINQ + value objects + EF Core conversions express domain types (`MoneyEgp`, `ArabicEnglishText`, `EgyptianTin`) without per-query mapping.
- Migrations tooling is essential for the on-prem upgrade path (Roadmap Group 14).
- Dapper for the two hot read paths because EF Core's projection overhead measurably hurts SC-002 (5 s VAT report at 5k docs) and SC-014 (5-min inspection bundle at 5k docs).

**Alternatives considered**:
- Dapper-only: rejected (loses migrations, change tracking, value-object conversions; reinvents transaction scope).
- EF Core only: rejected (Cockpit + Inspection Bundle perf budgets won't hold under 25-user concurrent load).

---

## R-03 — Background jobs: Hangfire vs `BackgroundService` vs Quartz.NET

**Decision**: Hangfire 1.8 with SQL Server storage.

**Rationale**:
- Three durable jobs require persistence across app restarts: ETA retry queue (FR-036), audit checkpoint emitter (FR-028), supplier-TIN re-validation (FR-043).
- Hangfire's SQL Server storage shares the same database as the application — no extra infrastructure for the operator.
- Built-in dashboard simplifies operator runbook.
- Polly handles transient retries inside a job; Hangfire owns durability across restarts and failures.

**Alternatives considered**:
- `IHostedService` / `BackgroundService`: rejected (no durability; ETA retries lost on restart, violating FR-036).
- Quartz.NET: rejected (heavier, more configuration than Hangfire; identical durability story; no operator advantage).

---

## R-04 — PDF generation library

**Decision**: QuestPDF 2024.x.

**Rationale**:
- Best-in-class .NET RTL + Arabic shaping support (a hard requirement for FR-033 bilingual invoices and FR-046 Form 41 PDF).
- MIT-compatible Community license below revenue threshold; commercial license available later.
- Fluent API, easy to embed within domain DTOs without templating.
- Predictable rendering (no headless browser); important for the SC-008 automated PDF-content check.

**Alternatives considered**:
- iText7 (AGPL or paid): rejected (license risk for on-prem commercial product; heavier API).
- PdfSharp + MigraDoc: rejected (RTL/Arabic shaping is fragile; community fixes lag).
- HTML-to-PDF via Puppeteer/Playwright: rejected (operator must install Chromium; SC-008 test fragility).

---

## R-05 — Audit hash chain implementation (FR-028)

**Decision**: In-application SHA-256 chain over canonicalized JSON payloads, stored in SQL Server `audit.audit_log` (append-only by application contract; row trigger blocks UPDATE/DELETE attempted from app accounts; the integrity checkpoint detects DBA-level tampering as designed). Canonicalization uses a deterministic JSON serializer (sorted keys, invariant culture, ISO-8601 UTC timestamps).

**Hash chain entry shape**:
```
{
  "index": <bigint, monotonic>,
  "ts_utc": "<ISO 8601>",
  "actor_user_id": "<guid>",
  "actor_firm_name": "<string|null>",
  "company_id": "<guid>",
  "kind": "<DocumentPosted|FieldChanged|ConfigChanged|LoginSucceeded|...>",
  "payload": { ...event-specific... },
  "prev_hash": "<hex>",
  "this_hash": "<hex>"   // SHA-256( canonicalize(payload) || prev_hash )
}
```

**Rationale**:
- In-application hashing keeps the chain DBA-tamper-detectable (the DBA cannot recompute valid hashes without the canonicalization rules).
- Sorted-keys canonicalization removes serialization ambiguity.
- Index column lets the integrity checkpoint store `(last_index, last_hash, ts_utc)` without scanning the table on every checkpoint.

**Alternatives considered**:
- SQL Server temporal tables: rejected (still admin-erasable; not cryptographically chained; SC-010 demands tamper-detection by a DBA).
- Database-side hashing via `HASHBYTES`: rejected (the DBA could rewrite the function; canonicalization rules belong in the trusted application).
- Blockchain / Merkle tree: rejected (overkill for a single-tenant on-prem MVP; introduces external dependencies).

**Checkpoint cadence**: every 1,000 entries OR every 15 minutes (FR-028, fixed in spec).

**Checkpoint storage mode** (operator picks at install time per FR-028):
- Mode A: file under `${INSTALL_ROOT}/audit_checkpoints/checkpoint.json`, written atomically (write-temp + rename), directory ACL'd to the service account only (the operator is instructed to revoke local Administrator write).
- Mode B: row in `audit_meta.checkpoint` on a separate SQL schema; only the application's service account has INSERT/UPDATE; SQL `sysadmin` is documented as a tamper risk and the file mode is recommended when the operator wants stronger separation.

---

## R-06 — Document-number allocator (FR-011 + SC-006)

**Decision**: Per-`(series, fiscal_year)` row-locked counter table. On post:
1. Open the same DbContext transaction the post uses.
2. `SELECT next_number FROM number_allocator WITH (UPDLOCK, ROWLOCK) WHERE series=? AND fiscal_year=?`.
3. Increment in app, `UPDATE` back, then assign to document.
4. Commit transaction. If the post fails for any reason after step 3, the entire transaction rolls back including the allocator increment — the number is released.
5. On the first post of a new fiscal year, the row is created (or reset) inside the same transaction.

**Rationale**:
- Gap-free guarantee per FR-011: numbers consumed only on successful end-to-end post.
- Hot row but write-only contention is bounded to the single allocator row per series-year; with ≤25 concurrent users and ≤50k docs/year, contention is negligible (~1 post / second peak).
- SC-006's 500-concurrent stress test will demonstrate correctness; testcontainer-based integration test will be a contract for the allocator.

**Alternatives considered**:
- SQL `IDENTITY` / `SEQUENCE`: rejected (gaps on rollback).
- Application-side optimistic counter: rejected (race conditions under concurrent commit).
- Snowflake-style ID: rejected (not gap-free, not human-readable, fails Egyptian numbering convention).

---

## R-07 — TOTP MFA library (FR-002, FR-039)

**Decision**: `Otp.NET` (BouncyCastle-free, MIT-licensed).

**Rationale**:
- Standard RFC 6238 implementation; QR-code provisioning URL works with Google Authenticator, Microsoft Authenticator, Aegis, Authy.
- Small dependency surface; no native binaries.
- Used together with `QRCoder` to render the enrollment QR.

**Alternatives considered**:
- `OtpSharp`: unmaintained.
- Hand-rolled HOTP/TOTP: rejected (security-critical code; reuse vetted library).

---

## R-08 — Password hashing (FR-002, FR-038)

**Decision**: Argon2id via `Konscious.Security.Cryptography.Argon2`, parameters: memory 64 MiB, iterations 3, parallelism 4. Hashes stored as `{algo, params, salt, hash}` opaque string for future migration.

**Rationale**:
- OWASP recommendation as of 2024.
- Memory-hard against GPU attack.
- Migration-friendly format lets us bump parameters as hardware improves.

**Alternatives considered**:
- ASP.NET Core Identity's PBKDF2-SHA256 default: acceptable but weaker against GPU attack; rejected to match production-grade framing in the user input.
- bcrypt: weaker against modern GPU; rejected.

---

## R-09 — ETA eInvoice JSON schema (FR-034, contracts/eta-einvoice.schema.json)

**Decision**: Author a JSON Schema (Draft 2020-12) modeled on the publicly documented Egyptian Tax Authority eInvoice JSON shape (issuer, receiver, document type, totals, taxable items, tax totals, signature placeholder), generated by the application at post time. Mock submission accepts the JSON, persists it, returns a simulated UUID + status. The schema lives in `contracts/eta-einvoice.schema.json` and is enforced by a contract test on every generated invoice JSON (SC-007).

**Rationale**:
- Mock-only in MVP (FR-035), so no live ETA dependency.
- Authoring the schema now (even mock-aligned) means the live ETA swap (Future advanced) is a client-side transport change, not a domain change.
- Contract test catches schema drift before regression.

**Alternatives considered**:
- Use ETA's published OpenAPI spec verbatim: deferred — requires live ETA credentials and the actual SDK; overkill for mock MVP.
- No schema, free-form JSON: rejected (SC-007 requires schema validation).

---

## R-10 — Tamper-evident QR seal (FR-044)

**Decision**: QR encodes a base64url string containing a compact CBOR object: `{ doc_id, doc_no, total, hash, ver_url }`. `hash` is the same SHA-256 as the FR-028 audit chain entry for the document's "DocumentPosted" event. The verifier (offline mode, on-prem) reads the QR, queries the local DB for the document, recomputes the hash from the live record, and confirms or denies tampering. The verifier is exposed as a Blazor page at `/verify/{seal}` and as a CLI for the inspector.

**Rationale**:
- CBOR is more compact than JSON in a QR; matters because QR error correction degrades quickly with payload size.
- Hash-binding to the FR-028 audit chain means the QR's verifier and the chain verifier draw from the same source of truth.
- Offline verification (SC-013, < 1 s/doc) is met because the verifier only queries the local DB.
- Optional public verifier on a customer-controlled domain is a Roadmap item; not required for MVP.

**Alternatives considered**:
- Embed full document JSON in the QR: rejected (too large, fragile under print scaling).
- Cryptographic signature with private key: rejected for MVP (key management complexity; FR-028 hash chain is sufficient for tamper detection without full PKI).

---

## R-11 — Bilingual UI: Arabic ↔ English

**Decision**: Standard ASP.NET Core localization via `IStringLocalizer<T>`, RESX resource files per page/component (`Page.ar.resx`, `Page.en.resx`). RTL layout via a body-level `dir="rtl"` toggle bound to the user's preferred language; CSS uses logical properties (`margin-inline-start`, etc.) to avoid manual mirroring. The user's preferred language is stored on the User entity and can be overridden per session.

**Rationale**:
- Built-in framework support; no custom plumbing.
- Logical CSS properties remove the need for separate AR/EN stylesheets.
- Compatible with the bilingual entity strings (`ArabicEnglishText` value object) used for customer/supplier/item names.

**Alternatives considered**:
- Custom JSON translations: rejected (RESX is the framework default; no benefit to reinvention).
- i18next / react-intl: not applicable (no JS framework).

---

## R-12 — Arabic-words conversion for invoice totals

**Decision**: Implement a deterministic Arabic-words-from-amount converter inline (no external dependency). Vetted Arabic accounting test vectors (e.g., 1234.56 EGP → "ألف ومئتان وأربعة وثلاثون جنيهاً مصرياً وستة وخمسون قرشاً") covered by golden-file tests.

**Rationale**:
- The handful of available .NET libraries are unmaintained or English-centric.
- The rules are bounded (1 to ~999 trillion EGP, with currency suffix) and well-documented.
- Golden-file tests prevent drift.

**Alternatives considered**:
- `Humanizer`: English/some-locale only; doesn't ship Egyptian-Arabic accounting form.
- Third-party Arabic words library: surveyed npm/NuGet; nothing meets the licensing + accuracy bar.

---

## R-13 — Egyptian TIN validation

**Decision**: 9-digit numeric, regex `^\d{9}$`, with a checksum stub returning "valid format" only. Cross-check against the public ETA-published TIN registry is **deferred to Near-term** (Differentiator 1's "supplier-TIN periodic re-validation cron" — the cron infrastructure ships in MVP per FR-043; the actual remote-list integration ships when the published list is wired up).

**Rationale**:
- ETA does not publish a stable checksum algorithm; format-only validation is what incumbents do.
- Cron infrastructure ships in MVP so the live data source can be plugged in without re-architecture.

---

## R-14 — Approval workflow engine

**Decision**: Domain-modeled state machine inside the document aggregate (no external workflow engine). State transitions: `Draft → Submitted → (Approved | Rejected) → Posted`, with `Voided` as a separate terminal from non-Posted states (FR-026 / FR-027 / mid-round refinement). The per-document-type approval setting (FR-026) gates whether `Submitted/Approved` are required at all.

**Rationale**:
- The workflow is small, well-bounded, and tightly coupled to the document aggregates; an external engine (Elsa, MassTransit Saga) would be over-engineering.
- FR-027 mutability rules are state-dependent and live cleanly inside the aggregate.

**Alternatives considered**:
- Elsa Workflows: rejected (additional surface; the spec's workflow doesn't need branching/timers/visual designer).
- MassTransit Saga: rejected (no distributed messaging in MVP; single-process app).

---

## R-15 — Accountant-Firm Portal topology (FR-049)

**Decision**: Per-installation accounts created by invitation token (Round 5 clarification). Switcher implemented as a Blazor page that the firm user accesses via a per-installation cookie and an in-browser credential pool (HTTP-only secure cookies + `localStorage`-cached installation directory the firm user accepted). Each switch is a fresh authenticated request to a different installation URL using its own cookie. Optional Near-term IdP federation (Azure AD / Entra / Google Workspace) gated behind a feature flag, implemented via OpenIddict's relying-party flow.

**Rationale**:
- Preserves the on-prem / no-vendor-hub guarantee (Round 1 + Round 5).
- Sub-second switching achieved via per-installation session keep-alive (default 4-hour TTL); the switcher caches installation URLs but never exchanges data between installations.
- IdP federation is additive, not load-bearing, so the MVP ships without it.

**Alternatives considered**:
- Vendor-hosted hub: forbidden by Round-1 deployment assumption.
- Firm-hosted central server: forbidden by Round-5 reaffirmation; also creates a new SaaS that the firm doesn't want to operate.
- Browser extension: deferred — adds an install step beyond what an SME accountant tolerates.

---

## R-16 — Tax-Inspection Bundle (US9 / FR-048)

**Decision**: Background job (Hangfire) builds the bundle into a temp directory, generates each register PDF via QuestPDF, copies attachments by hard-link where possible (same volume) or copy otherwise, computes per-file SHA-256s, generates `MANIFEST.sha256`, packages into a ZIP with the included verification script (`verify-bundle.ps1`). The script independently re-hashes every file, recomputes the archive hash, and re-validates the audit-chain extract using a pure PowerShell + .NET-built-in implementation (no need to install the application on the inspector's machine).

**Rationale**:
- Hangfire provides progress reporting + retry on failure.
- Hard-linking attachments avoids doubling disk usage for large attachment sets.
- PowerShell verifier runs on every Windows 10+ machine without dependencies (SC-014 "clean Windows machine with no network access").
- ZIP format is universally inspector-friendly.

**Alternatives considered**:
- Stream straight to ZIP without temp dir: simpler but loses resumability and partial-progress reporting.
- Ship a .NET console verifier: rejected (requires .NET runtime install on inspector machine).

---

## R-17 — WHT computation (US7 / FR-045 / FR-046)

**Decision**: WHT category effective-from-dated table mirroring VAT category (FR-019). On payment voucher post (FR-051), compute WHT amount per the category in force on the **payment date** (not the invoice date — Egyptian WHT is event-dated to the payment). Form 41 (FR-046) generated quarterly by aggregating supplier-payment-voucher WHT lines; output is both QuestPDF-rendered PDF and JSON conforming to `contracts/form41.schema.json`. The JSON shape mirrors the spec's expected regulator format (documented internally; remappable when ETA publishes the official shape).

**Rationale**:
- Effective-from-dated rate model already proven for VAT; reusing it keeps the rule engine consistent.
- Payment-date effectivity matches Egyptian tax practice (WHT is on cash flow, not on invoice issuance).
- Dual output (PDF + JSON) satisfies FR-046 and prepares for live filing later.

---

## R-18 — Rule engine for Tax Risk Score (Differentiator 1)

**Decision**: Plain-C# rule classes implementing `IDocumentRiskRule`. Each rule produces zero or more `RiskFinding`s with severity `Info | Warning | MustFixBeforeFiling | Blocker`. Rules registered via DI; new rules added by adding a class. No external rule engine (Drools, Rules Engine NuGet) — the rule count is bounded (~15 rules in MVP), and the rules need access to typed domain objects and configuration.

**Rationale**:
- Type-safe, debuggable, refactorable; no DSL learning curve.
- Per-rule unit tests are trivial.
- Cockpit aggregation (Differentiator 2) and per-document badging (Differentiator 1) both consume the same `RiskFinding[]`.

**Alternatives considered**:
- NRules / RulesEngine: rejected (adds a DSL surface for ~15 rules; reflection cost on hot path; harder to unit-test against typed entities).

---

## R-19 — Localization for accounting/tax terminology

**Decision**: Curated bilingual terminology JSON file shipped with the product, used by both the UI (`IStringLocalizer`) and PDF templates. Editorial process: a designated terminology owner reviews changes before release. New tax-rule names get added in both languages from day one.

**Rationale**:
- Egyptian accounting Arabic has specific conventions (e.g., "إقرار ضريبي" not "إعلان ضريبي"; "خصم وإضافة" not "ضريبة خصم").
- Centralized terminology ensures the UI, PDFs, certificates (FR-045), and inspector bundle (FR-048) all use the same words.

---

## R-20 — Operational logging (distinct from audit log)

**Decision**: Serilog → SQL Server (`ops.application_log` table) for application diagnostics + Serilog → rolling file (`${INSTALL_ROOT}/logs/`) for high-volume request logs. Log levels: Information default, Debug toggleable per logger via the operator panel. **Strictly distinct** from the FR-028 audit log: operational logs are operator-readable, application-mutable, and shipped under any retention the operator chooses; audit logs are append-only, hash-chained, retained per FR-042.

**Rationale**:
- Mixing the two has been a frequent compliance failure in audited products; we keep them physically separated (different schemas, different storage rules).

---

## R-21 — Storage layout for attachments

**Decision**: `${INSTALL_ROOT}/attachments/{yyyy}/{mm}/{document_id}/{attachment_id}.{ext}`. The DB stores metadata (size, sha256, uploader, mime) but not the bytes. Backups must include this directory + the SQL DB to be coherent (documented in operator runbook).

**Rationale**:
- Filesystem is faster and cheaper than `varbinary(max)` for moderate file counts.
- Year/month sharding keeps directory-listing fast on Windows (NTFS slows past ~10k files in one directory).
- SHA-256 storage detects bit-rot on restore.

**Alternatives considered**:
- `varbinary(max)` in SQL: rejected (DB grows fast; backup window grows; harder to clean/rebuild attachments tier).
- FILESTREAM: rejected (SQL Server Express FILESTREAM has a 10 GB cap).

---

## R-22 — Notification mechanism (in-app vs SMTP)

**Decision**: In-app notifications via SignalR (built into Blazor Server) for the MVP. Optional SMTP delivery for the same notification stream gated by an operator-configured SMTP profile; if SMTP is not configured, the user sees only the in-app inbox. Email-based password reset (FR-038) requires SMTP; without SMTP, the password reset path falls back to admin-recovery (also FR-038).

**Rationale**:
- Many Egyptian SMEs don't have outbound SMTP — graceful degradation is essential.
- SignalR is already in-process for Blazor Server; zero added infra.
- Approval queue is the most common notification (Differentiator 3); in-app is sufficient for the daily flow.

---

## R-23 — Time source (FR-042)

**Decision**: Application reads server local time, treats it as authoritative. A startup-time NTP probe (using `System.Net.Sockets.UdpClient` against `pool.ntp.org` or operator-configured server) records skew; if skew > 60 s, an operator-visible alert fires (the application continues running per FR-042). Skew is re-checked every 6 hours by a Hangfire job.

**Rationale**:
- Windows Server time service handles synchronization at the OS level; we just verify and alert.
- FR-042 explicitly says "MUST raise an operator-visible alert without halting the application."

---

## R-24 — CI / build / test pipeline

**Decision**: GitHub Actions (or operator's choice) running on Windows Server agent: `dotnet restore`, `dotnet build -c Release`, `dotnet test` for all four test projects, `playwright test` for E2E with a Testcontainers-managed SQL Server, `wix build` for the MSI. Versioned with semantic versioning; release artifacts attached to GitHub Releases or a private feed.

**Rationale**:
- Windows agent is required for WiX MSI build.
- Testcontainers-managed SQL Server gives hermetic integration tests.
- Pipeline ordering matches Constitution Principle III (test-first).

---

## Open items / explicit non-decisions

These items are intentionally NOT resolved here; they belong to later phases or to Near-term post-MVP work:

- **OCR partner choice** (Differentiator 9 / Roadmap Group 10): deferred to the OCR feature spec round.
- **Live ETA SDK choice**: deferred to the Live ETA Future Advanced spec round.
- **WhatsApp Business API account topology**: deferred to the WhatsApp receipt capture Near-term spec round.
- **Banking API partners** for Egyptian banks: deferred; in MVP we ship the bank-statement parsers only as Near-term (Round 4 addition), no live banking API.

---

## Decision-to-requirement traceability

| Decision | Requirements anchored | Tests required (Test-First) |
| --- | --- | --- |
| R-01 Blazor Server | FR-039 sessions, all UX FRs | E2E: full login + invoice flow under SignalR |
| R-02 EF Core + selective Dapper | All persistence FRs; SC-002 perf | Integration: VAT report ≤ 5 s at 5 k docs |
| R-03 Hangfire | FR-036 retry queue, FR-028 checkpoint, FR-043 cron | Integration: ETA retry survives restart; checkpoint every 1k entries |
| R-04 QuestPDF | FR-033, FR-046, US9 bundle PDFs | Contract: SC-008 every required PDF field present; Arabic shaping golden file |
| R-05 Hash chain | FR-028, SC-010 | Integration: synthetic tamper detected for insert/edit/delete/reorder/tail-truncate within 30 s on 1 M entries |
| R-06 Numbering allocator | FR-011, SC-006 | Integration: 500 concurrent posts spanning fiscal-year boundary, 0 gaps / 0 duplicates |
| R-07 Otp.NET TOTP | FR-002 | Unit: enrollment + login validation; integration: MFA-required role cannot disable MFA |
| R-08 Argon2id | FR-002, FR-038 | Unit: hash format stable across runs; verify/upgrade path |
| R-09 ETA eInvoice schema | FR-034, SC-007 | Contract: 100 % of generated JSON validates against `eta-einvoice.schema.json` |
| R-10 QR seal | FR-044, SC-013 | Contract: tamper detection on synthetic mutation; offline verification < 1 s |
| R-11 Localization | bilingual coverage | E2E: every page renders in both AR and EN with correct RTL |
| R-12 Arabic words | edge case "Bilingual content" | Unit: golden-file vectors |
| R-13 TIN validation | FR-040, FR-041 | Unit: format pass/fail |
| R-14 Approval engine | FR-026, FR-027 | Unit + integration: every state transition + every blocked transition |
| R-15 Firm portal topology | FR-049, SC-014 firm-switch | Integration: invitation accept; switcher across two installations; revocation |
| R-16 Inspection bundle | FR-048, SC-014 | Integration: bundle for 5k docs in < 5 min; verifier in < 60 s on clean machine |
| R-17 WHT compute | FR-045, FR-046, SC-011 | Integration: WHT-payable balance ±1 EGP; Form 41 zero-dup zero-omission |
| R-18 Rule engine | Differentiator 1 | Unit: every rule fires on synthetic positives; never on synthetic negatives |
| R-19 Terminology | bilingual content quality | Unit: terminology coverage check; CI fails on missing AR/EN keys |
| R-20 Operational log | observability | Unit: log routing; integration: ops log does not pollute audit log |
| R-21 Attachment storage | FR-032 | Integration: round-trip; bit-rot detection |
| R-22 Notifications | Differentiator 3 + FR-038 | Integration: notification fires; SMTP fallback degrades gracefully |
| R-23 NTP | FR-042 | Integration: synthetic skew > 60 s triggers alert |
| R-24 CI/CD | Constitution III | Pipeline: every PR runs all four test projects + WiX build |

End of Phase 0.
