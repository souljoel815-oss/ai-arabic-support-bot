# Phase 1 Data Model: Egyptian Tax Accounting MVP

**Date**: 2026-05-07 | **Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

This document formalizes every entity introduced by the spec into a typed data model with fields, relationships, invariants, and the user story that introduces it. Group ordering follows the source-code project structure in plan.md.

**Conventions**:
- IDs are `Guid` v7 unless noted (sortable by creation time, suitable as clustered index keys).
- Money fields use the `MoneyEgp` value object (`decimal(19,4)` underneath; rounded to 2 dp on render per the Banker's Rounding edge case).
- Bilingual text uses the `ArabicEnglishText` value object (two `nvarchar` columns + a derived display picker).
- Audit columns (`created_at_utc`, `created_by_user_id`, `updated_at_utc`, `updated_by_user_id`) are present on every persisted entity unless noted.
- Soft deletion is forbidden (FR-027). All "delete" operations are blocked at the application layer; entities are deactivated where applicable.

---

## Group A — Identity & Access

### A1. User
**Introduced by**: US1 (and every story). **FRs**: FR-001, FR-002, FR-038, FR-039, FR-042.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| email | string(254) | Unique, lowercased, trimmed |
| display_name | ArabicEnglishText | |
| password_hash | string(opaque) | Argon2id; format `{algo}${params}${salt}${hash}` |
| password_must_change | bool | Set true after admin reset (FR-038) |
| mfa_secret | string(opaque, nullable) | TOTP secret; encrypted at rest using DPAPI |
| mfa_enrolled_at_utc | DateTime? | |
| preferred_language | enum {ar, en} | |
| status | enum {Active, Disabled} | Disabled retained for audit |
| last_login_at_utc | DateTime? | |
| last_login_succeeded | bool | |
| failed_login_count | int | Reset on successful login |
| lockout_until_utc | DateTime? | |
| roles | many-to-many → Role | |

**Invariants**:
- Cannot be deleted once present in any audit-log entry (FR-001 via Audit Log immutability).
- If any role in `roles` has `requires_mfa = true`, `mfa_secret` MUST be non-null (FR-002).
- A user cannot approve a document they created or last edited (FR-004) — enforced at handler level, validated by integration test.

### A2. Role
**Introduced by**: US1. **FRs**: FR-001, FR-002, FR-003, FR-004.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| code | string(32) | One of: `Administrator`, `Accountant`, `Bookkeeper`, `Approver`, `Auditor` |
| name | ArabicEnglishText | |
| permissions | many-to-many → Permission | |
| requires_mfa | bool | True for Administrator + Approver |

**Seeded values**: the five roles above with default permission grants.

### A3. Permission
**Introduced by**: US1. **FRs**: FR-003.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| code | string(64) | E.g., `Invoice.Sales.PostDirect`, `Invoice.Sales.Approve`, `Wht.Form41.Generate`, `Audit.RunVerifier` |
| description | ArabicEnglishText | |

### A4. AccountantFirmUser *(extends User)*
**Introduced by**: US8. **FRs**: FR-049.

| Field | Type | Notes |
| --- | --- | --- |
| user_id | Guid (PK, FK→User) | |
| firm_name | string(200) | Captured at invitation; immutable except by Admin (audit-logged) |
| firm_external_identifier | string(200) | Email or IdP subject |
| invited_at_utc | DateTime | |
| invited_by_user_id | Guid (FK→User) | |
| accepted_at_utc | DateTime? | |
| revoked_at_utc | DateTime? | |
| revoked_by_user_id | Guid? (FK→User) | |

### A5. Session
**Introduced by**: US1. **FRs**: FR-039.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| user_id | Guid (FK→User) | |
| started_at_utc | DateTime | |
| last_activity_at_utc | DateTime | |
| absolute_expires_at_utc | DateTime | |
| inactivity_expires_at_utc | DateTime | Refreshed on activity |
| terminated_at_utc | DateTime? | |
| termination_reason | enum {Manual, InactivityTimeout, AbsoluteTimeout, AdminForce} | |

---

## Group B — Company & Master Data

### B1. Company
**Introduced by**: US1. **FRs**: FR-005.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | Single row in MVP (single-tenant) |
| legal_name | ArabicEnglishText | |
| tax_registration_number | EgyptianTin | 9 digits |
| commercial_registration_number | string(32) | |
| address | ArabicEnglishText | |
| logo_path | string? | Filesystem path within INSTALL_ROOT |
| fiscal_year_start_month | int (1–12) | |
| default_currency | string(3) | "EGP" only in MVP |
| default_language | enum {ar, en} | |

### B2. Customer
**Introduced by**: US1. **FRs**: FR-006, FR-007, FR-040.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| code | string(32) | Unique |
| name | ArabicEnglishText | |
| address | ArabicEnglishText | |
| phone | string(32) | |
| email | string(254) | |
| status | enum {Active, Inactive} | Cannot be deleted if referenced by any posted doc |
| tax_profile | embedded (B2a) | See below |

### B2a. CustomerTaxProfile *(value object embedded on Customer)*
**Introduced by**: Round 3. **FRs**: FR-040.

| Field | Type | Notes |
| --- | --- | --- |
| profile_type | enum {B2BRegistered, B2BUnregistered, B2CConsumer} | |
| tin | EgyptianTin? | Required iff B2BRegistered |
| customer_vat_exemption_flag | bool | |
| default_sales_vat_category_id | Guid? (FK→VatCategory) | Override of company default |

**Invariants**:
- `tin` non-null if and only if `profile_type = B2BRegistered`.
- `customer_vat_exemption_flag = true` forbids `default_sales_vat_category_id` referring to a non-exempt category.

### B3. Supplier
**Introduced by**: US2. **FRs**: FR-006, FR-007, FR-041.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| code | string(32) | Unique |
| name | ArabicEnglishText | |
| address | ArabicEnglishText | |
| phone | string(32) | |
| email | string(254) | |
| status | enum {Active, Inactive} | |
| tax_profile | embedded (B3a) | |

### B3a. SupplierTaxProfile *(value object)*
**Introduced by**: Round 3. **FRs**: FR-041.

| Field | Type | Notes |
| --- | --- | --- |
| profile_type | enum {RegisteredTaxpayer, Unregistered, ForeignSupplier} | |
| tin | EgyptianTin? | Required iff RegisteredTaxpayer |
| reverse_charge_flag | bool | True only when ForeignSupplier |
| default_purchase_vat_category_id | Guid? (FK→VatCategory) | |

**Invariants**:
- Input VAT recoverability requires `profile_type = RegisteredTaxpayer` (enforced in FR-020 / Tax Risk Score rule).

### B4. Item
**Introduced by**: US1. **FRs**: FR-006.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| code | string(32) | Unique |
| name | ArabicEnglishText | |
| default_vat_category_id | Guid (FK→VatCategory) | |
| default_revenue_account_id | Guid (FK→ChartOfAccount) | |
| default_expense_account_id | Guid? (FK→ChartOfAccount) | For inventory items used on the buy side (Future advanced) |
| status | enum {Active, Inactive} | |

### B5. ChartOfAccount
**Introduced by**: US5. **FRs**: FR-029 (mappings), report FRs.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| code | string(16) | Unique |
| name | ArabicEnglishText | |
| account_type | enum {Asset, Liability, Equity, Revenue, Expense} | |
| parent_account_id | Guid? (FK→ChartOfAccount) | Hierarchy |
| is_postable | bool | False on parent/header accounts |

**Seeded accounts** (subset, MVP):
- `1100` Cash, `1110` Bank — operating, `1200` Accounts Receivable, `1210` WHT Receivable
- `2100` Accounts Payable, `2110` Output VAT Payable, `2120` Input VAT Recoverable, `2130` WHT Payable
- `3000` Equity, `4000` Sales Revenue, `5000` Expenses, `5100` Depreciation Expense (Phase 4)

### B6. VatCategory
**Introduced by**: US5. **FRs**: FR-019, FR-022.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | Surrogate; one row per (code, effective_from) |
| code | string(32) | E.g., `Standard`, `Reduced`, `ZeroRated`, `Exempt` |
| name | ArabicEnglishText | |
| rate_percent | decimal(5,2) | 14.00 for standard at MVP seed |
| effective_from_date | date | |
| effective_to_date | date? | Null = open-ended |
| recoverable_input_vat | bool | False for Exempt |

**Invariants**: For any given `code`, ranges `[effective_from, effective_to)` MUST NOT overlap.

### B7. WhtCategory
**Introduced by**: US7. **FRs**: FR-045.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| code | string(32) | E.g., `Services`, `Goods`, `Professional` |
| name | ArabicEnglishText | |
| rate_percent | decimal(5,2) | |
| effective_from_date | date | |
| effective_to_date | date? | |
| applicable_to | enum {SuppliersServices, CustomersServices, Both} | |

### B8. DeductibleExpenseCategory
**Introduced by**: US2. **FRs**: FR-015.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| code | string(32) | |
| name | ArabicEnglishText | |
| default_deductible | bool | |
| default_account_id | Guid (FK→ChartOfAccount) | |
| status | enum {Active, Inactive} | |

---

## Group C — Documents

### Common base: Document (abstract)

All document aggregates share the following fields and the FR-026 / FR-027 state machine.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| document_type | enum | Discriminator: `SalesInvoice`, `CreditNote`, `PurchaseInvoice`, `Expense`, `JournalVoucher`, `SupplierPaymentVoucher`, `CustomerReceiptVoucher`, `FixedAsset` |
| document_series_id | Guid (FK→DocumentSeries) | |
| document_number | string(32) | Assigned at post; canonical format `<SERIES>-<YYYY>-<NNNNNN>` |
| document_date | date | Drives VAT rate (FR-022) and period assignment |
| state | enum {Draft, Submitted, Approved, Posted, Rejected, Voided} | |
| posted_at_utc | DateTime? | |
| posted_by_user_id | Guid? (FK→User) | |
| posting_mode | enum? {ApprovedThenPosted, UnapprovedDirect} | Captured at post (FR-026) |
| void_reason | string(500)? | |
| voided_at_utc | DateTime? | |
| voided_by_user_id | Guid? (FK→User) | |
| current_audit_chain_index | bigint? | Index of the most recent audit entry referencing this document |

**State machine** (FR-026 / FR-027 — also see R-14):

```
Draft  ── submit ──▶ Submitted ── approve ──▶ Approved ── post ──▶ Posted
  │                       │                       │
  │                    reject                   void
  │                       │                       │
  └─────── post* ─────▶ Posted   (only when      ▼
            (when approval        approval is   Voided
             not enabled)         enabled)
```

`Posted` is terminal; only credit-note (FR-013) or reversal voucher path corrects it. `Voided` is terminal; cannot transition back.

### C1. SalesInvoice / CreditNote
**Introduced by**: US1. **FRs**: FR-008, FR-010, FR-011, FR-012, FR-013, FR-033, FR-034, FR-040, FR-043, FR-044.

Header (extends Document):
- customer_id (FK→Customer)
- customer_tax_profile_snapshot (embedded — captured at post for audit immutability)
- currency = "EGP"
- header_reference (string, optional)
- subtotal, vat_total, invoice_level_discount_amount, invoice_level_discount_percent, grand_total (MoneyEgp)
- credit_note_of_invoice_id (FK→SalesInvoice, nullable; non-null iff document_type = CreditNote)

Lines:
- id, sales_invoice_id (FK→SalesInvoice), item_id (FK→Item), description (ArabicEnglishText, optional override)
- quantity (decimal(19,4)), unit_price (MoneyEgp)
- line_discount_amount, line_discount_percent (MoneyEgp)
- vat_category_id (FK→VatCategory)
- line_subtotal, line_vat, line_total (MoneyEgp, computed)

ETA submission status (1:1 with SalesInvoice/CreditNote when posted):
- eta_status (enum {Pending, Submitted, Failed})
- eta_submission_uuid (string?)
- eta_last_attempt_at_utc, eta_attempt_count
- eta_error_code, eta_error_message
- eta_submission_window_expires_at_utc

QR seal (1:1 when posted):
- seal_payload (string, base64url CBOR)
- seal_doc_hash (string, hex sha256) — same hash as the audit-chain entry for the post

### C2. PurchaseInvoice
**Introduced by**: US2. **FRs**: FR-009, FR-016, FR-020, FR-041.

Header:
- supplier_id (FK→Supplier), supplier_tax_profile_snapshot (embedded)
- supplier_invoice_number (string), date_received (date)
- subtotal, vat_total, grand_total

Lines:
- item_id (FK→Item, nullable for pure expense lines)
- expense_category_id (FK→DeductibleExpenseCategory, required if item_id null)
- quantity, unit_price, line_subtotal
- vat_category_id, line_vat, line_total
- deductible_flag (bool, default from category, override audit-logged)
- attachments (1:N → Attachment, ≥ 1 required if deductible_flag = true)

### C3. Expense
**Introduced by**: US2. **FRs**: FR-014, FR-015, FR-016.

| Field | Type | Notes |
| --- | --- | --- |
| date | date | |
| category_id | Guid (FK→DeductibleExpenseCategory) | |
| amount | MoneyEgp | |
| deductible_flag | bool | Default from category |
| description | ArabicEnglishText | |
| attachments | 1:N → Attachment | ≥ 1 if deductible |

**Invariant**: depreciation expenses are NOT entered via Expense (FR-014 update); they are produced by FR-017 in Phase 4.

### C4. FixedAsset *(Phase 4 — US6)*
**Introduced by**: US6. **FRs**: FR-017, FR-018.

| Field | Type | Notes |
| --- | --- | --- |
| code | string(32) | |
| description | ArabicEnglishText | |
| asset_category | string(32) | Configurable per asset class |
| cost | MoneyEgp | |
| in_service_date | date | |
| useful_life_months | int | OR rate_percent_per_year |
| depreciation_method | enum {StraightLine, DecliningBalance, ...} | StraightLine in Phase 4 minimum |
| salvage_value | MoneyEgp | |
| convention | enum {FullMonth, MidMonth, HalfYear} | |
| status | enum {InService, Disposed, WrittenOff} | |
| current_net_book_value | MoneyEgp | Computed |

Generates monthly depreciation Expense entries via the depreciation engine.

### C5. JournalVoucher
**Introduced by**: US4. **FRs**: FR-029, FR-030, FR-031.

Header:
- date, source_document_type (enum, nullable), source_document_id (Guid?, nullable)
- narration (ArabicEnglishText)
- is_auto_generated (bool)
- reverses_journal_id (Guid?, FK→JournalVoucher) for reversal vouchers

Lines (≥ 2; debits = credits):
- account_id (FK→ChartOfAccount), debit (MoneyEgp), credit (MoneyEgp), narration

**Invariant**: SUM(debit) = SUM(credit) per voucher; FR-030 refuses to post otherwise.

### C6. SupplierPaymentVoucher
**Introduced by**: Round 5 / FR-051.

Header:
- supplier_id (FK→Supplier), payment_date, payment_method (enum {Cash, BankTransfer}), payment_reference (string), note
- gross_payment_amount (MoneyEgp) — invoice total being paid
- wht_payable_amount (MoneyEgp, computed via WHT rules)
- net_cash_paid (MoneyEgp) — gross - wht_payable
- generated_wht_certificate_id (FK→WhtCertificate, nullable)

Allocations (1:N → PaymentAllocation):
- target_purchase_invoice_id, allocated_amount (MoneyEgp)

**Invariants**: SUM(allocated_amount) ≤ gross_payment_amount; no individual allocation exceeds the target invoice's open balance (FR-053 over-allocation rejection).

### C7. CustomerReceiptVoucher
**Introduced by**: Round 5 / FR-052.

Header:
- customer_id (FK→Customer), receipt_date, payment_method (enum), payment_reference (string), note
- gross_receipt_amount (MoneyEgp)
- customer_wht_certificate_id (FK→WhtCertificate, nullable) — when customer withheld
- wht_receivable_amount (MoneyEgp, from certificate)
- net_cash_received (MoneyEgp)

Allocations (1:N → PaymentAllocation):
- target_sales_invoice_id, allocated_amount (MoneyEgp)

### C8. PaymentAllocation
**Introduced by**: Round 5 / FR-053.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| voucher_id | Guid | Either supplier_payment_voucher_id or customer_receipt_voucher_id |
| voucher_kind | enum {SupplierPayment, CustomerReceipt} | |
| target_document_id | Guid | PurchaseInvoice or SalesInvoice id |
| allocated_amount | MoneyEgp | |

---

## Group D — Numbering

### D1. DocumentSeries
**Introduced by**: US1. **FRs**: FR-011.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| code | string(16) | E.g., `INV`, `CN`, `PI`, `EXP`, `JV`, `SPV`, `CRV`, `FA` |
| name | ArabicEnglishText | |
| document_type | enum (matches Document discriminator) | |

### D2. DocumentNumberAllocator
**Introduced by**: US1. **FRs**: FR-011, SC-006.

| Field | Type | Notes |
| --- | --- | --- |
| series_id | Guid (PK part) | |
| fiscal_year | int (PK part) | E.g., 2026 |
| next_number | int | Incremented under row lock; rolled back if post fails |

---

## Group E — Workflow

### E1. DocumentTypeApprovalSetting
**Introduced by**: Round 2 / Round 5. **FRs**: FR-026.

| Field | Type | Notes |
| --- | --- | --- |
| document_type | enum (PK) | |
| approval_required | bool | Default false in Phase 1 for SalesInvoice |
| eligible_approver_role_ids | many-to-many → Role | |

### E2. ApprovalRequest
**Introduced by**: US3. **FRs**: FR-026, FR-004.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| document_id | Guid | |
| document_type | enum | |
| submitted_by_user_id | Guid (FK→User) | |
| submitted_at_utc | DateTime | |
| approved_by_user_id | Guid? | |
| approved_at_utc | DateTime? | |
| rejected_by_user_id | Guid? | |
| rejected_at_utc | DateTime? | |
| rejection_reason | string(500)? | |

**Invariant**: `approved_by_user_id` and `rejected_by_user_id` MUST differ from `submitted_by_user_id` (FR-004); enforced in handler.

### E3. PeriodReviewLock
**Introduced by**: US8 / FR-050.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| period_year | int | |
| period_month | int | |
| locked_at_utc | DateTime | |
| locked_by_user_id | Guid (FK→User) | |
| locked_note | string(500)? | |
| released_at_utc | DateTime? | |
| released_by_user_id | Guid? (FK→User) | |
| accountant_actions_during_lock | int | Aggregated on release |

### E4. TaxPeriod
**Introduced by**: US3. **FRs**: FR-037.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| period_kind | enum {VatMonth, IncomeFiscalYear, WhtQuarter} | |
| year | int | |
| month_or_quarter | int | |
| status | enum {Open, Locked} | |
| locked_at_utc | DateTime? | |
| locked_by_user_id | Guid? | |
| locked_reason | string? | |

---

## Group F — Audit

### F1. AuditLogEntry
**Introduced by**: US3. **FRs**: FR-028, FR-042.

| Field | Type | Notes |
| --- | --- | --- |
| index | bigint (PK, monotonic) | Allocated by row-locked allocator (single-row table `audit_index`) |
| ts_utc | DateTime | |
| actor_user_id | Guid? | Null for system events |
| actor_firm_name | string(200)? | Captured for AccountantFirmUser actions |
| company_id | Guid | |
| kind | enum (~30 kinds) | DocumentPosted, FieldChanged, ConfigChanged, LoginSucceeded, LoginFailed, LogoutAutoTimeout, EtaSubmitted, EtaFailed, FirmUserInvited, etc. |
| payload_json | nvarchar(max) | Canonicalized event payload |
| prev_hash | binary(32) | |
| this_hash | binary(32) | SHA-256 of canonicalize(payload) ‖ prev_hash |

**Invariants**: append-only by application contract; UPDATE/DELETE attempted from app accounts blocked by SQL trigger; tamper-detected by FR-028 verifier.

### F2. AuditIntegrityCheckpoint
**Introduced by**: Round 2. **FRs**: FR-028.

Two storage modes (operator picks at install — see R-05):

**Mode A — file** (`${INSTALL_ROOT}/audit_checkpoints/checkpoint.json`):
```json
{
  "last_index": 12345,
  "last_hash": "<hex>",
  "ts_utc": "2026-05-07T10:23:00Z",
  "writer": "audit_checkpoint_emitter@localhost"
}
```
Atomic write (write to `.tmp`, fsync, rename).

**Mode B — table** (`audit_meta.checkpoint`, single row):
| Field | Type |
| --- | --- |
| last_index | bigint |
| last_hash | binary(32) |
| ts_utc | DateTime |

Same row updated each cadence; the single-row guarantee is enforced by a CHECK constraint.

---

## Group G — Tax (WHT, Form 41, Compliance)

### G1. WhtCertificate
**Introduced by**: US7. **FRs**: FR-045.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| direction | enum {OutboundToSupplier, InboundFromCustomer} | |
| date | date | Same as voucher date |
| counterparty_id | Guid | Supplier or Customer |
| source_voucher_id | Guid | SupplierPaymentVoucher or CustomerReceiptVoucher |
| source_invoice_id | Guid | The invoice being paid/settled |
| wht_category_id | Guid (FK→WhtCategory) | Captured for audit; rate frozen to certificate |
| rate_applied_percent | decimal(5,2) | |
| amount_withheld | MoneyEgp | |
| certificate_number | string | Generated (Outbound) or recorded from customer (Inbound) |
| issued_at_utc | DateTime | |

### G2. Form41Filing
**Introduced by**: US7. **FRs**: FR-046.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| fiscal_year | int | |
| quarter | int (1–4) | |
| status | enum {Unfiled, Filed, Overdue} | |
| generated_at_utc | DateTime | |
| filed_at_utc | DateTime? | |
| filed_by_user_id | Guid? | |
| pdf_path | string | Filesystem path |
| structured_json_path | string | Filesystem path |
| total_wht_payable | MoneyEgp | Reconciles to WHT-payable account balance |
| line_count | int | Distinct WHT-payable accruals included |

### G3. ComplianceRiskItem
**Introduced by**: Round 4 / Differentiator 1. **FRs**: FR-043, FR-047, Differentiator 1.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| document_id | Guid? | Null for non-document risks |
| document_type | enum? | |
| kind | enum (~20 kinds) | EtaWindowExpiring, EtaSubmissionFailed, MissingTin, MissingAttachment, BackdatedToClosedPeriod, DuplicateSupplierInvoice, WhtRequiredButMissing, NonRecoverableInputVat, etc. |
| severity | enum {Info, Warning, MustFixBeforeFiling, Blocker} | |
| reason_text | ArabicEnglishText | |
| how_to_fix_text | ArabicEnglishText | |
| estimated_egp_exposure | MoneyEgp? | E.g., projected fine on EtaWindowExpiring |
| detected_at_utc | DateTime | |
| resolved_at_utc | DateTime? | |
| resolved_by_user_id | Guid? | |

Stored as a derived/projected entity refreshed by the rule engine; UI consumes it through the Cockpit and per-document badges.

---

## Group H — ETA Integration (mock)

### H1. EtaSubmission
**Introduced by**: US1. **FRs**: FR-034, FR-035, FR-036, FR-043.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| document_id | Guid (FK) | |
| document_type | enum | |
| payload_json | nvarchar(max) | The generated eInvoice JSON |
| payload_schema_version | string | Tracks contract version |
| status | enum {Pending, Submitted, Failed} | |
| attempt_count | int | |
| last_attempt_at_utc | DateTime? | |
| simulated_uuid | string? | Returned by mock |
| error_code | string? | |
| error_message | string? | |
| submission_window_expires_at_utc | DateTime | Configurable look-back/forward |

---

## Group I — Verification

### I1. DocumentVerificationSeal (QR)
**Introduced by**: Round 4 / Differentiator FR-044.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| document_id | Guid | |
| document_type | enum | |
| seal_payload_b64 | string | base64url CBOR, embedded in QR |
| doc_hash | binary(32) | SHA-256, same as audit-chain entry for post |
| ver_url_relative | string | E.g., `/verify/{seal}` |

---

## Group J — Attachments

### J1. Attachment
**Introduced by**: US2. **FRs**: FR-032.

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| document_id | Guid | |
| document_type | enum | |
| filename_original | string(260) | |
| filename_storage | string(260) | `{guid}.{ext}` |
| relative_path | string | `attachments/yyyy/mm/{document_id}/...` |
| sha256 | binary(32) | For bit-rot detection |
| mime_type | string(100) | |
| size_bytes | bigint | |
| uploaded_by_user_id | Guid (FK→User) | |
| uploaded_at_utc | DateTime | |

**Invariants**:
- After parent document is Posted, attachments cannot be deleted (FR-027). Adding new attachments to posted docs is configurable (default off) and audit-logged.

---

## Group K — Compliance Surface (Cockpit, Document 360)

These entities are computed/projected, not first-class persisted; included for completeness.

### K1. MonthlyTaxClosingCockpit *(projection)*
**FRs**: Round 4 / Differentiator 2.

Inputs: TaxPeriod + ComplianceRiskItem aggregations + EtaSubmission status + ApprovalRequest queue + Form41Filing status + period close state. Recomputed on every page request and cached for 30 s.

### K2. Document360View *(projection)*
**FRs**: Round 4 / Differentiator 6.

Inputs: Document + Attachment + CustomerTaxProfile/SupplierTaxProfile snapshot + audit-chain extract for the document + EtaSubmission history + DocumentVerificationSeal status + ComplianceRiskItem history.

### K3. MissingDocumentRequest
**FRs**: Round 5 / Differentiator 8 (Near-term).

| Field | Type | Notes |
| --- | --- | --- |
| id | Guid (PK) | |
| target_document_id | Guid | Where uploaded files attach |
| requested_by_user_id | Guid | |
| recipient_email | string? | |
| recipient_phone | string? | |
| upload_token | string(64) | Single-use, expires in 7 days |
| status | enum {Sent, Opened, Uploaded, Expired} | |
| sent_at_utc | DateTime | |
| uploaded_at_utc | DateTime? | |
| uploaded_attachment_ids | Guid[] | |

---

## Cross-cutting invariants (enforced at handler / pipeline layer)

| ID | Invariant | Source |
| --- | --- | --- |
| INV-001 | All write operations are dispatched through MediatR; transaction commits AFTER audit-emit | R-02, FR-028 |
| INV-002 | Posted documents reject any field-level UPDATE; SQL row-version check + handler-level guard | FR-027 |
| INV-003 | Document numbers consumed only on successful end-to-end post (allocator inside same transaction) | FR-011 |
| INV-004 | Approver ≠ creator/last-editor | FR-004 |
| INV-005 | Mandatory MFA roles cannot disable MFA without first removing the role | FR-002 |
| INV-006 | Voiding is restricted to non-Posted states; posted corrections route through credit note / reversal voucher | FR-012, FR-027 |
| INV-007 | Audit log INSERT only; UPDATE/DELETE blocked by trigger (per app account) and by application code | FR-028 |
| INV-008 | Audit hash chain head index ≥ checkpoint index; verifier reports tail-truncation when violated | FR-028 |
| INV-009 | All NTP probes inside Hangfire job; on > 60 s skew, operator alert raised, application continues | FR-042 |
| INV-010 | Customer Tax Profile TIN required iff B2BRegistered | FR-040 |
| INV-011 | Supplier Tax Profile TIN required iff RegisteredTaxpayer; input VAT recoverability gated on this | FR-041 |
| INV-012 | Payment allocation cannot over-allocate voucher or invoice | FR-053 |
| INV-013 | WHT effective-from-dated rate applied per payment date, not invoice date | R-17 |
| INV-014 | Period close + period review lock are independent; review lock leaves Accountant adjusting entries open | FR-037, FR-050 |
| INV-015 | Firm-user actions audit-tagged with firm_name captured at invitation; firm_name change requires Admin action and is audit-logged | FR-049 |

---

## Story-to-entity map

| User Story | New / extended entities |
| --- | --- |
| US1 (P1) | Company, Customer, Item, VatCategory (seeded), DocumentSeries, DocumentNumberAllocator, SalesInvoice, EtaSubmission, DocumentVerificationSeal, Session, User (Administrator + Accountant seeded) |
| US2 (P1) | Supplier, DeductibleExpenseCategory, ChartOfAccount (seeded), PurchaseInvoice, Expense, Attachment, SupplierTaxProfile, CustomerTaxProfile (refined for sales) |
| US3 (P2) | Role, Permission, ApprovalRequest, DocumentTypeApprovalSetting, AuditLogEntry, AuditIntegrityCheckpoint |
| US4 (P2) | JournalVoucher (auto-generation logic), reversal vouchers |
| US5 (P3) | Effective-from-dated VatCategory + ChartOfAccount editing UI; WhtCategory configured (not yet used) |
| US6 (P3, Phase 4) | FixedAsset, depreciation engine producing Expense entries |
| US7 (P2, Phase 3) | WhtCertificate, Form41Filing, WHT extensions on SupplierPaymentVoucher / CustomerReceiptVoucher |
| US8 (P3, Phase 2 add-on) | AccountantFirmUser, PeriodReviewLock |
| US9 (P3, Phase 3) | (no new entities; consumes everything via projection) |
| Round 5 / payments | SupplierPaymentVoucher, CustomerReceiptVoucher, PaymentAllocation |
| Round 4 differentiators | ComplianceRiskItem (rule engine), MonthlyTaxClosingCockpit (projection), Document360View (projection), MissingDocumentRequest (Near-term) |

End of Phase 1 data model.
