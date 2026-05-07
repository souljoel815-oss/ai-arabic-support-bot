# Document Verification Seal (QR) Contract

**Source FRs**: FR-044, SC-013
**Source data-model entity**: I1 DocumentVerificationSeal

This contract defines the on-the-wire shape of the QR-encoded verification seal embedded in every sales-invoice and credit-note PDF (FR-033 + FR-044). It is the source of truth for offline verification (SC-013, < 1 s/document) and the canonical link between a printed PDF and its FR-028 audit-chain entry.

---

## QR payload

The QR encodes a single string: `EGT1.<base64url-cbor>`.

- `EGT1.` — protocol prefix + version. `EGT` for "Egyptian Tax", version `1`. Future protocol revisions bump to `EGT2.` etc.
- `<base64url-cbor>` — base64url encoding (no padding) of the CBOR-encoded payload object below.

CBOR is preferred over JSON to maximize the payload that fits inside an L-error-corrected QR at typical print scaling (≤ 300 bytes total payload).

## Payload object (CBOR map)

| Key | CBOR type | Description |
| --- | --- | --- |
| `1` | uint | Document type code: `1` = SalesInvoice, `2` = CreditNote |
| `2` | text (≤ 32) | Canonical document number (e.g., `INV-2026-000123`) |
| `3` | text (UUID) | Document ID (server-side) |
| `4` | uint | Total grand-total in piastres (i.e., EGP × 100) — integer to keep payload compact |
| `5` | byte string (32) | SHA-256 hash of the FR-028 audit-chain entry that records the post |
| `6` | uint | Audit-chain entry index (bigint serialized as CBOR uint) |
| `7` | text | Verification-page relative URL (e.g., `/verify`) |
| `8` | text (TIN) | Issuer (company) TIN — optional disambiguator when verifying off-network |

CBOR is canonicalized using RFC 8949 §4.2.1 deterministic encoding so the resulting bytes are reproducible.

## Verification protocol

1. **Reader** scans the QR, gets the string, splits at the first `.`. Validates the prefix is `EGT1`.
2. **Reader** base64url-decodes the remainder and CBOR-decodes the payload.
3. **Reader** opens `${baseUrl}${payload[7]}/{base64url(payload as bytes)}` (or runs the offline CLI verifier with the same input).
4. **Verifier**:
   a. Looks up the document by `payload[3]` (document ID).
   b. Asserts document number matches `payload[2]`.
   c. Asserts grand-total in piastres matches `payload[4]`.
   d. Asserts the audit-chain entry at index `payload[6]` exists and its `this_hash` equals `payload[5]`.
   e. Recomputes the audit-chain entry hash from the live data — if it differs, returns `TAMPERED` with the mismatched fields.
5. **Verifier** returns one of:
   - `VALID` — all checks pass.
   - `TAMPERED` — at least one mismatch; details listed.
   - `UNKNOWN` — document not found in this installation.
   - `MALFORMED` — payload could not be decoded.

## Performance requirement (SC-013)

Offline verification on the on-prem deployment MUST complete in under 1 second per document on a database of up to 50,000 posted documents. Implementation: indexed lookup on `(document_id)` plus a single audit-log row read by index — both are O(log n) on a B-tree index.

## Tamper detection coverage

| Attack | Detected by |
| --- | --- |
| User edits PDF total | Step 4c (recomputed total mismatch) |
| User edits document number on PDF | Step 4b |
| Database UPDATE to document fields | Step 4e (recomputed audit-chain hash mismatch) |
| Database DELETE of audit-log row | Step 4d (audit-chain entry missing or hash mismatch) |
| Forged QR for a non-existent document | Step 4a (UNKNOWN) |
| Forged QR with edited hash | Step 4d / 4e (chain integrity fails) |

## Required tests (Test-First per Constitution III)

- **Contract test**: 100 random posted documents → encode QR → decode → verify → all `VALID`.
- **Tamper test**: For each of 100 documents, mutate one field at a time (total, number, date, line item) and assert verifier returns `TAMPERED` with the correct mismatch.
- **Performance test**: 1,000 verifications on a 50,000-document seeded database; p95 < 1 s.
- **Print-fidelity test**: Render PDF, OCR-read the QR back at 300 DPI, decode — all 50 random samples decode cleanly.
