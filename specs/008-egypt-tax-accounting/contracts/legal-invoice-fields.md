# Legally Required Fields for an Egyptian Tax Invoice (PDF)

**Source FRs**: FR-033, FR-040 (TIN gating), SC-008
**Source data-model entities**: C1 SalesInvoice / CreditNote, B1 Company, B2 Customer, I1 DocumentVerificationSeal

This contract is the canonical list of fields that the PDF renderer (T101 `QuestPdfInvoiceRenderer`) MUST emit on every sales-invoice and credit-note PDF. It is the source of truth for SC-008 ("Every legally required field for an Egyptian tax invoice ... is present on every generated PDF, validated against an automated PDF-content check"). The contract test in `tests/EgyptTax.ContractTests/Pdf/InvoicePdfFieldsTests.cs` (T079) MUST iterate this list and assert the presence of each required field on a randomized sample of generated PDFs.

The list is derived from Egyptian VAT Law 67/2016, the ETA invoice format, and the publicly documented Egyptian Accounting Standards (EAS) presentation conventions. When the regulator publishes a binding shape, this file remaps without changing the renderer's API.

---

## A. Invoice header (issuer + document identity)

| Field | Source | Required when | Bilingual? |
| --- | --- | --- | --- |
| Document type label (e.g., "فاتورة ضريبية / Tax Invoice", "إشعار خصم / Credit Note", "فاتورة ضريبية مبسطة / Simplified Tax Invoice") | derived from `document_type` + customer tax profile (per FR-033 + FR-040) | always | yes |
| Canonical document number (`<SERIES>-<YYYY>-<NNNNNN>`, e.g., `INV-2026-000123`) | `Document.document_number` | always | no (number is locale-neutral) |
| Document date | `Document.document_date` | always | dual numerals optional |
| Currency label (`EGP` / `جنيه مصري`) | constant | always | yes |

## B. Issuer (the company)

| Field | Source | Required when | Bilingual? |
| --- | --- | --- | --- |
| Legal name | `Company.legal_name` | always | yes |
| Tax registration number (TIN) — 9 digits | `Company.tax_registration_number` | always | no |
| Commercial registration number | `Company.commercial_registration_number` | always | no |
| Registered address | `Company.address` | always | yes |
| Company logo | `Company.logo_path` | when configured | n/a |

## C. Receiver (the customer)

| Field | Source | Required when | Bilingual? |
| --- | --- | --- | --- |
| Customer legal name | `Customer.name` | always | yes |
| Customer tax registration number (TIN) — 9 digits | `Customer.tax_profile.tin` | **only when `tax_profile.profile_type = B2BRegistered`** (FR-040) | no |
| Customer address | `Customer.address` | always | yes |
| Customer phone | `Customer.phone` | when present | no |

## D. Lines (one row per invoice line)

| Field | Source | Required when | Bilingual? |
| --- | --- | --- | --- |
| Line index (sequential) | derived | always | no |
| Item code | `Item.code` (or `expense_category_id`) | always | no |
| Item description | `Item.name` (or per-line override) | always | yes |
| Quantity | line | always | no |
| Unit price | line | always | no |
| Line discount amount or % | line | when non-zero | no |
| Line subtotal (qty × unit price − line discount) | computed | always | no |
| VAT category label | `VatCategory.name` | always | yes |
| VAT rate % | `VatCategory.rate_percent` (effective on `document_date` per FR-022) | always | no |
| Line VAT amount | computed | always | no |
| Line total (subtotal + VAT) | computed | always | no |

## E. Footer totals (above signature)

| Field | Source | Required when | Bilingual? |
| --- | --- | --- | --- |
| Invoice subtotal | aggregate of line subtotals | always | no |
| Invoice-level discount amount or % | header | when non-zero | no |
| Net before VAT | computed | when invoice-level discount applied | no |
| Total VAT (per category breakdown) | aggregate of line VAT | always | yes |
| Grand total (numeric, 2 dp, banker's rounding per spec edge case "Rounding") | computed | always | no |
| Grand total in Arabic words | `ArabicWordsConverter` per R-12 | always | yes (Arabic primary) |

## F. Compliance markings

| Field | Source | Required when | Bilingual? |
| --- | --- | --- | --- |
| Document Verification Seal QR (FR-044) bottom-right | `DocumentVerificationSeal.seal_payload` | always | n/a (image) |
| Rounding-rule disclosure | constant ("الأرقام مقربة لأقرب قرشين / amounts rounded to two decimal places") | always | yes |
| Posted-by user identity (small footnote) | `Document.posted_by_user_id` → display name | always | yes |
| Posted timestamp (UTC + Egypt time) | `Document.posted_at_utc` | always | dual representation |

## G. Credit note specifics (only when `document_type = CreditNote`)

| Field | Source | Required when | Bilingual? |
| --- | --- | --- | --- |
| Reference to original invoice number | `SalesInvoice.credit_note_of_invoice_id` → original `document_number` | always for credit notes | no |
| Reference to original invoice date | dereferenced | always for credit notes | no |
| Reason for credit note (free-text) | header | always for credit notes | yes |

---

## Required tests (Test-First per Constitution III)

| Test | Synthetic input | Expected output |
| --- | --- | --- |
| `Every_Required_Field_Present_On_B2BRegistered_Invoice` | A random posted sales invoice to a `B2BRegistered` customer | All A–F + customer TIN line render |
| `Customer_Tin_Omitted_On_B2BUnregistered` | Same with `B2BUnregistered` customer | A–F render; customer TIN line absent; document type label is "Tax Invoice" not "Simplified" |
| `Customer_Tin_Omitted_On_B2CConsumer` | Same with `B2CConsumer` | A–F render; customer TIN line absent; document type label is "Simplified Tax Invoice" |
| `Credit_Note_References_Original` | A posted credit note | All A–G render including credit-note-specific G fields |
| `Arabic_Words_Match_Total` | Random invoice | Arabic-words rendering matches `ArabicWordsConverter.Convert(grand_total)` |
| `Qr_Seal_Present_And_Verifies` | Random invoice | QR present bottom-right; verifier returns `VALID` for the rendered QR |

These tests are run by T079 (and extended cases) over a randomized sample of ≥ 100 invoices spanning all customer tax-profile types.
