# Audit Chain Verifier Contract

**Source FRs**: FR-028, FR-042, SC-010
**Source data-model entities**: F1 AuditLogEntry, F2 AuditIntegrityCheckpoint

This contract specifies the protocol the Auditor-runnable verification routine implements. It is the source of truth for the SC-010 guarantee ("auditor can reconstruct the full life cycle of any randomly selected posted document end-to-end from the log alone" + "100 % of synthetic tamper cases ... within 30 s for a log containing up to 1,000,000 entries").

---

## Inputs

The verifier accepts:

1. A **read-only connection** to the application database (or a SQL Server export of `audit.audit_log` and `audit_meta.checkpoint`).
2. The **integrity checkpoint** from one of:
   - `${INSTALL_ROOT}/audit_checkpoints/checkpoint.json` (file-mode operator)
   - `audit_meta.checkpoint` table (table-mode operator)
3. Optional `--start-index` and `--end-index` to verify a window only (used by the inspection-bundle verifier).

The verifier MUST NOT require write access to either store.

## Algorithm

```
load checkpoint -> (cp.last_index, cp.last_hash, cp.ts_utc)

read all audit entries in order by index ASC, optionally bounded by start/end
  for each entry e:
    if e.index != prev.index + 1:
      report MISSING_INDEX(e.index, prev.index + 1)
    expected_prev_hash = prev.this_hash (or zero-hash if e.index == 1)
    if e.prev_hash != expected_prev_hash:
      report PREV_HASH_MISMATCH(e.index)
    recomputed = sha256(canonicalize(e.payload_json) || e.prev_hash)
    if recomputed != e.this_hash:
      report THIS_HASH_MISMATCH(e.index)
    prev = e

after loop:
  if max_observed_index < cp.last_index:
    report TAIL_TRUNCATION(cp.last_index, max_observed_index)
  if max_observed_index == cp.last_index and last_observed.this_hash != cp.last_hash:
    report CHECKPOINT_MISMATCH

emit final result: { valid: boolean, findings: Finding[] }
```

## Canonicalization

`canonicalize(payload_json)` is RFC 8785-style JSON Canonicalization Scheme (JCS):

- Object keys sorted lexicographically (codepoint order).
- All numbers serialized in the JSON-prescribed shortest form.
- Strings escaped per RFC 8259 with no extraneous whitespace.
- All datetimes in UTC, ISO 8601 Z suffix.

Both the application's audit-emitter and the verifier MUST use the same canonicalization implementation. The implementation is shipped in `EgyptTax.Domain` and reused by both the writer (Hangfire job) and the standalone verifier console.

## Performance requirement (SC-010)

Verification of a 1,000,000-entry chain MUST complete in under 30 seconds on the on-prem reference hardware (operator runbook lists the reference: 8-core CPU, 16 GB RAM, NVMe SSD).

Implementation notes:
- Streaming read with a single `ORDER BY index ASC` query (server-side cursor).
- SHA-256 in batches of 10,000 entries; reuse `IncrementalHash` across the batch.
- O(n) memory: only `prev.this_hash` and the current entry held at a time.

## Output: Finding

```json
{
  "kind": "MISSING_INDEX | PREV_HASH_MISMATCH | THIS_HASH_MISMATCH | TAIL_TRUNCATION | CHECKPOINT_MISMATCH",
  "atIndex": <bigint>,
  "expected": <hex|bigint>,
  "actual": <hex|bigint>,
  "notes": <string>
}
```

A clean log returns `{ valid: true, findings: [] }`. Any finding sets `valid` to `false`.

## Required tests (Test-First per Constitution III)

| Test | Synthetic input | Expected output | Time budget |
| --- | --- | --- | --- |
| `Clean_Log_Returns_Valid` | 1,000,000 entries, untampered, checkpoint matches | `valid=true, findings=[]` | < 30 s |
| `Single_Insert_At_Mid` | 500,000 untampered + insert at index 500,001 | `findings: [PREV_HASH_MISMATCH(500001)]` | < 30 s |
| `Single_Edit_At_Mid` | mutate `payload_json` at random index | `findings: [THIS_HASH_MISMATCH(idx)]` | < 30 s |
| `Single_Delete_At_Mid` | delete entry at index N | `findings: [MISSING_INDEX(N)]` | < 30 s |
| `Reorder_Two_Entries` | swap entries at i and i+1 | `findings: [PREV_HASH_MISMATCH(i+1), ...]` | < 30 s |
| `Tail_Truncation_100_Rows` | delete last 100; checkpoint still references original head | `findings: [TAIL_TRUNCATION(...)]` | < 30 s |
| `Checkpoint_Stale_But_Tail_Intact` | checkpoint hash mismatches actual head (e.g., last entry rewritten) | `findings: [THIS_HASH_MISMATCH, CHECKPOINT_MISMATCH]` | < 30 s |
| `Tamper_With_Checkpoint_File_Only` | edit checkpoint file but leave chain intact | `findings: [CHECKPOINT_MISMATCH]` | — |

## Operator runbook integration

The verifier is exposed as a console verb on the existing `EgyptTax.Web` host (no separate executable project — keeps the install footprint minimal and reuses the same DI container, configuration loader, and DPAPI keys as the running service). Operators run:

```powershell
EgyptTax.Web.exe verify-audit `
  --connection "Server=.;Database=EgyptTax;Trusted_Connection=Yes;" `
  --checkpoint-file "C:\EgyptTax\audit_checkpoints\checkpoint.json" `
  --output report.json
```

Exit code: `0` for valid, `1` for any finding, `2` for unrecoverable error (DB down, file unreadable).

The verifier engine itself lives in `src/EgyptTax.Domain/Audit/AuditChainVerifier.cs` (pure domain logic — depends only on `IAuditLogStore` and `IAuditCheckpointStore` port interfaces from the Application layer); the `verify-audit` console verb is implemented in `src/EgyptTax.Web/Tools/VerifyAuditCommand.cs`. The Tax-Inspection Bundle (FR-048) ships a slimmed PowerShell port of the same engine so an inspector can run it without installing the application.
