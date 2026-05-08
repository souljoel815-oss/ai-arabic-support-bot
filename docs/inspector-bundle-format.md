# Tax-Inspection Bundle — Public Format Specification

**Audience**: Egyptian tax inspectors who receive a Tax-Inspection Bundle from a taxpayer running the EgyptTax accounting system, and any third party (auditor, consultant, court-appointed expert) who needs to independently verify the bundle's integrity on a clean Windows machine.

**Status**: Stable. Format version `1.0` — see [Versioning](#versioning) for the back-compat policy.

**Authoritative schema**: [`inspection-bundle-manifest.schema.json`](../specs/008-egypt-tax-accounting/contracts/inspection-bundle-manifest.schema.json) (JSON Schema 2020-12). This document is the human-readable companion; if the two ever disagree, the JSON Schema wins.

---

## 1. What this bundle is, and is not

A Tax-Inspection Bundle is a **self-contained, integrity-sealed ZIP archive** that captures every piece of evidence required to substantiate a taxpayer's filings for a specific period. It is generated on demand by an EgyptTax installation operator (typically the company's accountant) when the tax authority requests an inspection.

The bundle contains:

- **All registers** for the period — sales invoices, purchase invoices + expenses, credit notes, the general journal listing, the trial balance, the monthly VAT report, the quarterly Form 41 (if applicable).
- **All attachments** for posted purchase invoices and expenses (PDF / JPEG / PNG receipts).
- **An audit-trail extract** — every entry from the application's append-only, hash-chained audit log whose timestamp falls inside the bundle period.
- **An auditor verification report** that pre-runs the audit-chain verifier on the extract and bundles its result for inspector reference.
- **A SHA-256 manifest** (`MANIFEST.sha256`) that lists every file with its hash and size.
- **A self-contained verifier script** (`verify-bundle.ps1`) that re-validates the manifest on a clean Windows machine **without any EgyptTax software installed**.
- **A README** (`README-FOR-INSPECTOR.md`) that walks the inspector through verification.

What the bundle is **not**:

- It is **not** a regulator-mandated filing artifact. ETA eInvoice submissions and Form 41 quarterly filings are still made through their respective official channels; the bundle is a complementary evidence pack the inspector can request as part of an audit.
- It does **not** include the company's full database — only documents whose document date or audit-log timestamp falls inside the bundle period.
- It does **not** carry any cryptographic signature from the tax authority. Integrity is established by the audit-chain hash + the manifest hashes; **provenance** (i.e., proof that a specific company generated this exact bundle) requires the inspector to compare the company TIN, manifest `generatedByUserId`, and audit-chain head hash against records the company has independently filed elsewhere.

---

## 2. Top-level ZIP layout

```
EgyptTax-Bundle-{TIN}-{period}.zip
├── MANIFEST.sha256                       — JSON manifest (this spec)
├── README-FOR-INSPECTOR.md               — verification walkthrough
├── verify-bundle.ps1                     — PowerShell verifier (self-contained)
├── registers/
│   ├── sales-invoices.{csv,pdf}          — SalesInvoiceRegister
│   ├── purchases-and-expenses.{csv,pdf}  — PurchaseInvoiceAndExpenseRegister
│   ├── credit-notes-and-reversals.{csv,pdf}  — CreditNoteAndReversalRegister
│   ├── general-journal.{csv,pdf}         — GeneralJournalListing
│   ├── trial-balance.{csv,pdf}           — TrialBalance
│   ├── vat-monthly-{YYYY-MM}.pdf         — VatMonthlyReport (one per VAT month)
│   └── form41-{YYYY-Q}.{json,pdf}        — Form41Filing (one per WHT quarter)
├── attachments/
│   └── {document-id}/{original-filename} — Attachment (one per posted purchase / expense receipt)
└── audit-trail/
    ├── audit-trail-extract.jsonl         — AuditTrailExtract (newline-delimited JSON)
    └── verification-report.txt           — AuditorVerificationReport (verifier's at-generation result)
```

Filenames inside `attachments/` use the original filename the operator uploaded (UTF-8). The `{document-id}` directory is a Guid string in the canonical `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` form. The same Guid appears in the manifest's `linkedDocumentId` field for that file's row.

---

## 3. `MANIFEST.sha256` format

`MANIFEST.sha256` is **JSON, not the GNU `sha256sum` text format**. Its schema is normatively defined by [`inspection-bundle-manifest.schema.json`](../specs/008-egypt-tax-accounting/contracts/inspection-bundle-manifest.schema.json); the table below is a human-readable companion.

### 3.1 Top-level fields

| Field | Type | Notes |
|---|---|---|
| `bundleVersion` | string | Always `"1.0"` for bundles produced by this format spec. See [Versioning](#versioning). |
| `company` | object | Issuing company identity — see §3.2. |
| `period` | object | Period covered by the bundle — see §3.3. |
| `generatedAt` | string (ISO-8601 date-time) | UTC timestamp when the bundle was built. |
| `generatedByUserId` | string (UUID) | The internal user-id of the operator who triggered the build. **The user-id is internal to the company's EgyptTax installation and is not by itself a public identity** — pair it with the company's user roster if you need the operator name. |
| `files` | array | One entry per file in the bundle (excluding `MANIFEST.sha256` itself). See §3.4. |
| `auditChainExtract` | object | Reference to the audit-trail extract — see §3.5. |
| `topLevelArchiveSha256` | string (64 hex chars) | SHA-256 over the canonical-JSON of `MANIFEST.sha256` itself, **excluding this very field**. The verifier omits this field, re-serializes, hashes, and compares. |
| `draftsExcluded` | boolean | `true` when the operator chose to generate the bundle despite some draft documents existing in the period. See §3.6. |
| `excludedDraftIds` | array of UUIDs (optional) | Present iff `draftsExcluded == true` — lists the document-ids that were drafts at generation time and so were NOT included in the registers. |

### 3.2 `company` object

| Field | Type | Notes |
|---|---|---|
| `companyId` | UUID | Internal id; useful for cross-referencing other artifacts the same installation produces. |
| `tin` | string (9 digits) | Egyptian Tax Identification Number. **This is the public anchor identity.** Inspectors should verify this against the company they expect the bundle to come from. |
| `legalName` | object `{ ar, en }` | Bilingual legal name. Both fields are required and must match the company's commercial registration. |

### 3.3 `period` object

| Field | Type | Notes |
|---|---|---|
| `fiscalYear` | integer | Egyptian fiscal year covered. |
| `kind` | enum | One of `"Quarter"`, `"FiscalYear"`, `"Custom"`. |
| `quarter` | integer (1-4) | Present only when `kind == "Quarter"`. |
| `start` | string (ISO date) | Inclusive lower bound (UTC). |
| `end` | string (ISO date) | Inclusive upper bound (UTC). |

### 3.4 `files[]` entries

| Field | Type | Notes |
|---|---|---|
| `relativePath` | string | Path inside the ZIP, with `/` as separator. Always relative to the ZIP root. |
| `sha256` | string (64 lowercase hex chars) | SHA-256 of the file's exact bytes. |
| `sizeBytes` | integer ≥ 0 | File size in bytes. |
| `category` | enum | One of the categories listed in §4. The verifier uses this to apply category-specific rules (e.g. an attachment must have a `linkedDocumentId`). |
| `linkedDocumentId` | UUID (optional) | Required for `Attachment` entries — references the source document the attachment belongs to. The same Guid is used as the directory name under `attachments/`. |

The manifest **does not** list itself in `files[]`. The manifest's own integrity is established via `topLevelArchiveSha256`.

### 3.5 `auditChainExtract` object

| Field | Type | Notes |
|---|---|---|
| `startIndex` | integer | First audit-log index in the extract (inclusive). |
| `endIndex` | integer | Last audit-log index in the extract (inclusive). |
| `extractSha256` | string (64 hex chars) | SHA-256 of the JSONL extract file's exact bytes. Cross-checks the same file's `files[]` entry under category `AuditTrailExtract`. |
| `verifiedAtGeneration` | boolean | `true` iff the in-app audit-chain verifier returned a clean result on the extract at generation time. **An inspector should still re-run the verifier after handoff** — `false` here means the bundle was produced from a tampered or broken chain and should be rejected. |

### 3.6 Drafts-excluded flag

By default, the bundle generator **rejects** the build if any draft documents exist whose document-date falls inside the period — drafts represent unposted business that an inspector cannot reconcile. The operator can override this by explicitly accepting a "drafts excluded" bundle, in which case:

- `draftsExcluded` is set to `true`,
- `excludedDraftIds` lists every excluded draft document-id,
- the bundle's registers contain only Posted documents.

**Inspector action when `draftsExcluded == true`**: ask the operator to either (a) post the listed drafts and regenerate the bundle, or (b) provide a written explanation for each excluded draft. The bundle itself remains cryptographically valid; the question is whether it is *complete enough* to substantiate the period's filings.

---

## 4. File categories (the `category` enum)

| Category | Meaning |
|---|---|
| `SalesInvoiceRegister` | Tabular listing of every posted sales invoice + credit note in the period, with totals reconciled to the VAT report. |
| `PurchaseInvoiceAndExpenseRegister` | Tabular listing of every posted purchase invoice + expense, with deductible / non-deductible split. |
| `CreditNoteAndReversalRegister` | Tabular listing of every credit note + journal-voucher reversal, with cross-reference to the original document. |
| `GeneralJournalListing` | Per-document journal entries (the GL-level view of every posted transaction). |
| `TrialBalance` | Period-end trial balance summary. |
| `VatMonthlyReport` | One per VAT month covered by the period — output VAT, recoverable input VAT, net payable. |
| `Form41Filing` | One per WHT quarter — the Form 41 JSON + PDF as filed (or as ready-to-file). |
| `Attachment` | One per receipt / supporting document for a posted purchase invoice or expense. `linkedDocumentId` REQUIRED. |
| `AuditTrailExtract` | The newline-delimited JSON extract of the audit log for the period. Exactly one per bundle. |
| `AuditorVerificationReport` | The in-app audit-chain verifier's at-generation result for the extract. Exactly one per bundle. |
| `VerifierScript` | `verify-bundle.ps1` — the PowerShell script that re-validates the manifest. Exactly one per bundle. |
| `ReadmeForInspector` | `README-FOR-INSPECTOR.md` — verification walkthrough. Exactly one per bundle. |

A new category cannot be added without bumping `bundleVersion` (see [Versioning](#versioning)) — the schema's `enum` constraint enforces this so an old verifier rejects an unknown category rather than silently passing over it.

---

## 5. Verification protocol

### 5.1 Quick verification (recommended)

1. Unzip the bundle to a working directory: `Expand-Archive -Path .\EgyptTax-Bundle-{TIN}-{period}.zip -DestinationPath .\bundle`
2. Run the verifier:
   ```powershell
   PS> .\bundle\verify-bundle.ps1 -BundleDirectory .\bundle
   ```
3. The script re-computes the SHA-256 of every file listed in `MANIFEST.sha256` and compares to the recorded value. Output is per-file `PASS` / `FAIL` plus an overall verdict.

**Exit codes**:

| Code | Meaning |
|---|---|
| 0 | All files match. The bundle has not been tampered with since generation. |
| 1 | One or more files have a hash or size mismatch, or a manifest-listed file is missing. The bundle has been tampered with or corrupted in transit. |
| 2 | Usage error (wrong arguments, missing manifest, etc). |

### 5.2 What the script intentionally does NOT do

The bundled `verify-bundle.ps1` is a **per-file integrity check**. It does NOT:

- **Re-run the audit-chain hash verification** inside `audit-trail/audit-trail-extract.jsonl`. That requires JCS (RFC 8785) JSON canonicalization which is non-trivial in pure PowerShell. Instead, the extract file's SHA-256 IS verified — so any post-handoff modification to the JSONL trips the script. For full chain replay, the inspector should run the C#-based verifier (shipped with the application — ask the operator to provide the `EgyptTax.Web verify-audit` CLI binary, or use any independent SHA-256-chain replayer that follows the [audit-chain-verifier contract](../specs/008-egypt-tax-accounting/contracts/audit-chain-verifier.md)).
- **Re-compute `topLevelArchiveSha256`**. That requires producing the exact same canonical JSON bytes the application's serializer produced (camelCase, indented, `UnsafeRelaxedJsonEscaping`), which is brittle to implement in pure PowerShell. The per-file hashes alone are sufficient to detect any tampering — `topLevelArchiveSha256` is a belt-and-braces signal that requires the C#-based verifier.

### 5.3 Independent re-verification (recommended for high-stakes inspections)

For an audit where the inspector needs to satisfy a third-party reviewer that the verification is independent of any tooling the company supplied, the inspector can:

1. Compute the SHA-256 of every file in the unzipped bundle using their own tool of choice (`sha256sum`, `Get-FileHash -Algorithm SHA256`, OpenSSL, etc).
2. Read `MANIFEST.sha256` as ordinary JSON.
3. For each entry in `files[]`, compare their independently computed hash to `sha256` and the file size to `sizeBytes`.

The bundle format is intentionally simple enough that this independent re-verification is straightforward — no proprietary tooling required.

---

## 6. Audit-trail extract format

`audit-trail/audit-trail-extract.jsonl` is **newline-delimited JSON** (RFC 8259 documents, one per line, separated by `\n`). Each line is one audit-log entry. The format and verification protocol are normatively defined by [`audit-chain-verifier.md`](../specs/008-egypt-tax-accounting/contracts/audit-chain-verifier.md); summary:

- Each entry has `index` (monotonic, starting from `auditChainExtract.startIndex`), `tsUtc`, `actorUserId` (nullable for system actions), `actorFirmName` (nullable; populated when the actor was an external accounting-firm user), `companyId`, `kind`, `payloadJson` (canonicalized via JCS / RFC 8785), `prevHash` (32 bytes hex), `thisHash` (32 bytes hex).
- `thisHash[i] == SHA-256(canonicalize(payloadJson[i]) || prevHash[i])` — a chain such that any insert / edit / delete / reorder produces a hash mismatch detectable by replaying the chain forward from `startIndex`.
- The first entry's `prevHash` chains back to the entry at `startIndex - 1` of the company's full audit log (or the genesis hash if `startIndex == 1`). The companion `verification-report.txt` (category `AuditorVerificationReport`) records what `prevHash` value was expected at `startIndex` so an inspector can spot a tampered prefix.

---

## 7. Versioning

`bundleVersion` follows semantic versioning at the **format** level:

- **Major** (e.g. `1.0` → `2.0`) — backwards-incompatible change to the manifest shape, the file layout, or the verification protocol. An old verifier MUST reject a new-major bundle (the schema's `const: "1.0"` constraint guarantees this).
- **Minor** (e.g. `1.0` → `1.1`) — additive only. New optional fields, new `category` enum values, new informational files. An old verifier MUST tolerate (ignore) the new additions; a new verifier MUST work on an old bundle.
- **Patch** — wording / typos / documentation. No format change.

Inspectors may safely use this spec at version `1.x` with any `1.0` bundle they receive.

---

## 8. Worked example

A minimal `MANIFEST.sha256` for a bundle covering Q1 2026 of a single sales-invoice-only company:

```json
{
  "bundleVersion": "1.0",
  "company": {
    "companyId": "11111111-1111-1111-1111-111111111111",
    "tin": "123456789",
    "legalName": { "ar": "شركة المثال", "en": "Example LLC" }
  },
  "period": {
    "fiscalYear": 2026,
    "kind": "Quarter",
    "quarter": 1,
    "start": "2026-01-01",
    "end": "2026-03-31"
  },
  "generatedAt": "2026-04-15T10:00:00Z",
  "generatedByUserId": "22222222-2222-2222-2222-222222222222",
  "files": [
    { "relativePath": "README-FOR-INSPECTOR.md", "sha256": "...", "sizeBytes": 4123, "category": "ReadmeForInspector" },
    { "relativePath": "verify-bundle.ps1", "sha256": "...", "sizeBytes": 5784, "category": "VerifierScript" },
    { "relativePath": "registers/sales-invoices.csv", "sha256": "...", "sizeBytes": 12031, "category": "SalesInvoiceRegister" },
    { "relativePath": "registers/general-journal.csv", "sha256": "...", "sizeBytes": 8920, "category": "GeneralJournalListing" },
    { "relativePath": "registers/trial-balance.pdf", "sha256": "...", "sizeBytes": 18540, "category": "TrialBalance" },
    { "relativePath": "registers/vat-monthly-2026-01.pdf", "sha256": "...", "sizeBytes": 14210, "category": "VatMonthlyReport" },
    { "relativePath": "registers/vat-monthly-2026-02.pdf", "sha256": "...", "sizeBytes": 14310, "category": "VatMonthlyReport" },
    { "relativePath": "registers/vat-monthly-2026-03.pdf", "sha256": "...", "sizeBytes": 14080, "category": "VatMonthlyReport" },
    { "relativePath": "audit-trail/audit-trail-extract.jsonl", "sha256": "...", "sizeBytes": 45612, "category": "AuditTrailExtract" },
    { "relativePath": "audit-trail/verification-report.txt", "sha256": "...", "sizeBytes": 612, "category": "AuditorVerificationReport" }
  ],
  "auditChainExtract": {
    "startIndex": 1024,
    "endIndex": 2867,
    "extractSha256": "...",
    "verifiedAtGeneration": true
  },
  "topLevelArchiveSha256": "...",
  "draftsExcluded": false
}
```

A bundle in the same format with attachments would add `attachments/{document-id}/{filename}` entries with category `Attachment` and a populated `linkedDocumentId`.

---

## 9. Reporting bundle integrity issues

If an inspector receives a bundle whose verification fails:

1. **Do not accept the bundle as evidence.** Document the specific failure (which file, expected vs actual hash) and request a fresh bundle from the company.
2. The company's operator can re-generate the bundle from the same period — the result will be byte-identical for the same period bounds, the same posted document set, and the same audit-chain head (subject to a refreshed `generatedAt` timestamp and `topLevelArchiveSha256`). A second consecutive verification failure on a freshly-generated bundle is a strong signal that the company's audit chain itself has been tampered with — escalate per the tax authority's incident-response protocol.
3. If the company refuses to regenerate or the second bundle also fails, the inspector should request the live database via the regular regulatory channels.

---

**Format spec maintained alongside [`inspection-bundle-manifest.schema.json`](../specs/008-egypt-tax-accounting/contracts/inspection-bundle-manifest.schema.json). Last reviewed: 2026-05-08.**
