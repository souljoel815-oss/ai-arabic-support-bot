# Feature Specification: Egyptian Tax Accounting MVP

**Feature Branch**: `008-egypt-tax-accounting`
**Created**: 2026-05-07
**Status**: Draft
**Input**: User description: "a production-grade Egyptian tax accounting MVP for legal tax optimization and compliance: ASP.NET Core 8 + Blazor + SQL Server, clean architecture, users/roles, company settings, customers, suppliers, items, sales invoices, purchase invoices, real business expenses, deductible vs non-deductible expense classification, input VAT/output VAT, configurable tax rules, profit/loss taxable income report, VAT monthly report, expense attachments/supporting documents, approval workflow, immutable posted invoices, automatic balanced journal entries, audit logs, PDF invoices, ETA eInvoice JSON/mock integration only, clear phases, database schema, tests, and acceptance criteria. The system must help reduce taxable profit through documented expenses, purchase invoices, depreciation rules, deductible expense categories, and proper VAT input deduction."

## Clarifications

### Session 2026-05-07

- Q: Deployment / hosting model → A: Self-hosted on-premises only
- Q: Authentication strength → A: Local password + TOTP MFA, mandatory for Administrator and Approver, optional for others
- Q: Document numbering reset cadence → A: Annual reset per series, fiscal year embedded (e.g., INV-2026-000123); gap-free within a year
- Q: Audit log tamper-protection model → A: Append-only with SHA-256 hash chain; verification routine detects any insert/edit/delete (even by a DBA)
- Q: Concurrency & data-volume sizing target → A: Small-to-mid SME tier — up to 25 concurrent users, 50,000 documents/year, 5 years online with older years archived but queryable
- Q: Phasing of fixed assets & depreciation → A: Phase 4 only — removed from P1/P2 acceptance scenarios; FR-017 and FR-018 marked Phase 4; new P3 user story (US6) carries the asset/depreciation flow
- Q: Posting vs approval workflow per phase → A: Approval is configurable per document type. In Phase 1, sales invoices post directly by Accountant/Administrator (no approval required) and the audit log records the post as "unapproved-direct" with the actor's role. From Phase 2 onward, document types with approval enabled follow Draft → Submitted → Approved → Posted; FR-004 (no self-approval) applies whenever approval is enabled.
- Q: Voiding vs corrections on posted documents → A: Voiding is permitted only for documents in Draft / Submitted / Approved states (with audit + reason). Once a document has consumed a numbered slot via posting, it cannot be voided. Corrections to posted **tax-impacting** documents (sales invoices, credit notes, purchase invoices) MUST use a credit note linked to the original. Corrections to posted **non-tax-impacting** documents (e.g., manual journal vouchers) MUST use a reversal voucher linked to the original.
- Q: Numbering invariant phrasing → A: Numbers are consumed only when posting succeeds end-to-end; failed/rolled-back attempts MUST NOT consume a number, and a subsequent successful post MUST receive the same number the failed attempt would have received. Mechanism (transaction boundary, lease, sequence + reconciliation) is left to planning.
- Q: Audit hash-chain checkpoint storage and cadence → A: Maintain a periodic external checkpoint storing (last entry index, last entry hash, last entry timestamp). Storage location is one of: (a) a dedicated `audit_checkpoint` table in a separate database schema whose write privileges are limited to a service account distinct from the SQL `sysadmin` account, or (b) a file under a write-restricted directory owned by that same service account. Operator picks one at install time. Cadence: every 1,000 new audit entries OR every 15 minutes, whichever fires first. Verification routine MUST validate both the chain and the latest checkpoint and report tail-truncation. No HSM, no external trusted timestamp authority in the MVP.
- Q: Mutability rules per state (FR-027) → A: Draft = editable by authorized users with full field-level audit logging; Submitted/Approved = no field edits, only workflow actions (Approve, Reject-with-reason, Post, Void-with-reason) gated by FR-003 permissions and FR-004 (no self-approval); Posted = fully immutable (no edit, no delete, no void), corrected via FR-013 credit note or reversal voucher; physical deletion is never permitted in any state, voiding is a logical state transition only and is always audit-logged.
- Q: Roadmap & maturity classification → A: A new `## Roadmap & Phasing` section was added covering 17 capability groups, classifying every line item as MVP / Near-term post-MVP / Future advanced / Out of current implementation. WHT and corporate-income-tax estimate are explicitly Future modules. Customer/supplier tax profiles (B2B/B2C and registered/non-registered) were added as MVP because they are required by ETA submission and PDF legal-field rules. Customer/supplier payments, opening balances, AR/AP aging, statements, bank/cash modules, multi-branch, and POS are Near-term or Future per the user's mapping. New MVP FRs added: FR-038 password reset, FR-039 session timeout, FR-040 customer tax profile, FR-041 supplier tax profile, FR-042 audit-log retention. FR-008 expanded to cover invoice-level discounts; FR-033 expanded to gate TIN rendering on customer tax profile. The cash-management OOS assumption was softened from "out of scope" to "out of scope for the MVP; planned near-term post-MVP per the Roadmap."
- Q: Strategic differentiators (Round 4) → A: Six market-differentiating capabilities encoded into the spec to position the product as the leading Egyptian tax-accounting product, not a me-too. **Three are promoted into the MVP**: (1) **WHT lifecycle** — moved from Future advanced to MVP Phase 3 with new US7, FR-045/FR-046/FR-047, addressing الخصم والإضافة and Form 41; (2) **Active ETA compliance dashboard** — new MVP Phase 1 capability via FR-043 (deadline countdown, fine exposure estimate, supplier-TIN periodic re-validation); (3) **Tamper-evident invoice QR seal** — new MVP Phase 1 capability via FR-044, layering on the existing FR-028 hash chain so any third party can verify a PDF invoice. **Two more land as MVP add-ons in Phase 2/3**: (4) **Accountant-firm portal with multi-company access and lock-for-review handoff** — new US8 + FR-049/FR-050, addressing the in-house bookkeeper ↔ external accountant divide; (5) **Tax-inspection bundle** — new US9 + FR-048, one-click regulator-ready archive sealed by the audit hash chain. **Smaller bets land in the Roadmap as Near-term**: pre-built parsers for the top 5 Egyptian banks (CIB, NBE, Banque Misr, QNB Egypt, ADIB), cash-business / Z-report mode, late-fee predictor, embedded last-updated Arabic explainers per tax form, audit-log external write-once mirror, and WhatsApp receipt capture (separated from full OCR which remains Future). **Sixth differentiator (Egyptian-Arabic OCR + receipt-to-expense matching)** stays Future advanced because accuracy depends on a third-party OCR partner; WhatsApp capture itself is promoted to Near-term so the ingestion path exists before OCR lands.
- Q: Resolve scope contradiction — WHT requires payment recording → A: Keep WHT in MVP Phase 3, and add a **minimal Phase 3 Payments & Settlement capability** sized strictly to support WHT and basic AR/AP balance closure: FR-051 supplier payment voucher (with WHT-payable split + payment method/date/reference), FR-052 customer receipt voucher (with customer-issued WHT certificate split + payment method/date/reference), FR-053 payment allocation against invoices + AR/AP balance settlement. Full cash management (cashboxes, bank accounts, internal transfers, deposits/withdrawals, statement import, bank reconciliation, cheques, post-dated cheques, multi-account cash/bank ledgers) remains **Near-term post-MVP** or **Future advanced** per the Roadmap — those features are NOT pulled into the MVP. The Assumptions cash-management bullet was updated to acknowledge the carve-out and forbid scope creep.
- Q: Resolve scope contradiction — Accountant-Firm Portal vs on-prem assumption → A: Accountant-Firm Portal stays as **MVP Phase 2 add-on** because a topology compatible with the on-prem / no-vendor-hosted-data assumption exists. The MVP topology: each client installation runs independently and exposes its own login URL; Accountant-Firm Users are scoped accounts on each installation, created via per-installation invitation tokens (FR-049). The accountant's "company switcher" is a **client-side credential pool** maintained in the user's browser session or in a dedicated thin desktop app — it is just a list of installations the accountant has accepted invitations into. **No vendor-hosted hub exists, no firm-hosted central server is required, and no client data leaves the client installation.** Switching companies makes a fresh authenticated request to a different installation URL using the cached credential; sub-second switching is achieved via session keep-alive within a configured per-installation TTL. The audit-tagging-with-firm-name guarantee is implemented per-installation: the invitation token records the firm name, and every action by the firm user is tagged with that firm name in the local audit log. **Optional federated-identity enhancement** (where the firm runs its own Azure AD / Entra / Google Workspace IdP and each client installation trusts that IdP for the firm user's identity) is recorded as a Near-term enhancement rather than MVP. This preserves on-prem semantics while still solving the bookkeeper ↔ external accountant divide.
- Q: Differentiators / Egypt-First Product Strategy section → A: A new normative `## Differentiators / Egypt-First Product Strategy` section was added enumerating the 9 user-proposed differentiators (Tax Risk Score per document, Monthly Tax Closing Cockpit, Accountant Review Mode, Owner Mode, ETA Reconciliation, Evidence Vault, Smart Migration Assistant, Missing Document Collection Flow, Egypt Tax Copilot) with explicit maturity tagging per item. Per my prior opinion: items composing on already-spec'd data (#1 Tax Risk Score, #5 ETA Reconciliation outbound, #6 Evidence Vault) land in MVP. The composing dashboard (#2 Cockpit) is MVP Phase 3 once VAT/WHT data exists. #3 Accountant Review Mode is folded into the existing US8 firm-portal home page rather than treated as a separate concept (avoids dashboard sprawl). #4 Owner Mode and #7 Smart Migration land Near-term post-MVP. #8 Missing Document Collection Flow lands Near-term post-MVP, paired with #5 inbound supplier eInvoice import. #9 Egypt Tax Copilot remains Future advanced and is **explicitly bounded as a rule-based assistant only — it MUST NOT be an LLM-powered tax-advice engine and MUST NOT silently change accounting data**, with auditor-runnable provenance for every explanation it produces. The section also pins a cross-cutting compliance-surface architecture: Tax Risk Score, ETA Compliance Dashboard, WHT Lifecycle Dashboard, Closing Cockpit, Owner Mode, and Accountant Review Mode are **one compliance surface with multiple lenses**, not six separate screens.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Issue compliant sales invoice and capture output VAT (Priority: P1)

An accountant at a small Egyptian company issues a sales invoice to a registered customer for goods or services. The invoice automatically calculates output VAT at the configured rate, generates a printable PDF in Arabic and English, and produces an ETA-format eInvoice JSON document (delivered through a mock submission endpoint, not a live ETA call). In Phase 1 the accountant posts the invoice directly (no approval workflow yet — that arrives in Phase 2 / US3); the audit log records the post as "unapproved-direct" with the actor's role. Once posted, the document and its computed taxes become immutable and contribute to the monthly VAT return.

**Why this priority**: Sales invoicing is the revenue and output-VAT source of truth. Without it the company cannot bill customers, cannot file VAT, and cannot demonstrate compliance — every other report depends on this data existing.

**Independent Test**: A user can log in as an accountant, configure one VAT rate and one item, create a draft sales invoice, post it, download the PDF, view the generated eInvoice JSON, and see the invoice appear in the VAT monthly report — all without any other module being implemented.

**Acceptance Scenarios**:

1. **Given** a draft sales invoice with one line item priced 1,000 EGP at 14% VAT, **When** the accountant posts the invoice, **Then** the system records subtotal 1,000, output VAT 140, total 1,140, generates an eInvoice JSON conforming to the documented ETA schema, and locks all invoice fields from further edit.
2. **Given** a posted sales invoice, **When** any user attempts to edit a line, change the customer, or change the date, **Then** the system rejects the change and instructs the user to issue a credit note instead.
3. **Given** a posted sales invoice, **When** the user opens the PDF, **Then** the document shows the company tax registration number, customer details, line items, VAT breakdown, total in numbers and Arabic words, and a unique invoice reference.

---

### User Story 2 - Record purchase invoices and deductible expenses to reduce taxable profit (Priority: P1)

A bookkeeper enters supplier purchase invoices and other business expenses (rent, utilities, salaries, etc.), attaches scanned supporting documents, and classifies each line as deductible or non-deductible per Egyptian tax rules. Deductible expenses with valid supplier tax invoices contribute input VAT (recoverable) and reduce taxable profit on the annual income tax report; non-deductible items still appear in management P&L but are added back when computing taxable income. Fixed-asset capitalization and depreciation arrive later (US6, Phase 4) and are out of scope for this story.

**Why this priority**: This is the legal-tax-optimization core of the product. Posting documented expenses against revenue is what reduces the corporate tax base; without it the system would only inflate VAT obligations without delivering the stated user benefit.

**Independent Test**: A user can create a supplier, enter one purchase invoice with attached PDF, mark it as deductible with input VAT, run the taxable income report for the period, and confirm that taxable profit equals (sales subtotal − deductible expense subtotal) and that recoverable input VAT appears on the VAT monthly report.

**Acceptance Scenarios**:

1. **Given** a posted sales invoice of 10,000 EGP and a posted deductible purchase invoice of 4,000 EGP (both subtotals, both at 14% VAT), **When** the user runs the monthly VAT report, **Then** output VAT shows 1,400, input VAT shows 560, and net VAT payable shows 840.
2. **Given** an expense classified as non-deductible (e.g., personal entertainment) for 500 EGP, **When** the user runs the taxable income report, **Then** the 500 EGP appears in management P&L expenses but is added back in the "Non-deductible adjustments" line so it does not reduce taxable profit.
3. **Given** a purchase invoice without an attached supporting document, **When** the user attempts to mark it as deductible and submit it for approval, **Then** the system blocks submission and requires at least one attachment.

---

### User Story 3 - Approval workflow with immutable posting and audit trail (Priority: P2)

An Administrator switches on approval for one or more document types (e.g., purchase invoices, expenses, manual journal vouchers). For those types, a junior bookkeeper drafts the document and submits it; a senior accountant or controller reviews, approves, or rejects with a reason; posting only occurs after approval. Document types where approval is **not** enabled (e.g., sales invoices in the default Phase 1 configuration) continue to support direct posting by users with the Accountant or Administrator role, with the post recorded as "unapproved-direct" in the audit log. Every state change (created, edited, submitted, approved, rejected, posted, voided) is captured in the audit log showing who did what, when, and what changed. Posted documents cannot be deleted or edited; tax-impacting posted documents are corrected via credit notes (FR-013), and non-tax-impacting posted documents (e.g., manual journal vouchers) are corrected via reversal vouchers linked to the original.

**Why this priority**: Egyptian tax authority audits require a clear chain of evidence and unbroken document numbering. Without approval and immutability, the books cannot be relied on for compliance — but the core invoicing in P1 must work first before adding governance on top.

**Independent Test**: A user logs in as bookkeeper, drafts a purchase invoice and submits it; logs in as approver, approves it; the invoice posts, becomes immutable, and the audit log shows both users, their actions, and the timestamp sequence.

**Acceptance Scenarios**:

1. **Given** a draft invoice submitted by user A, **When** user B with the Approver role approves it, **Then** the invoice transitions to Posted, journal entries are generated automatically, and the audit log captures both submission and approval events with usernames and timestamps.
2. **Given** a draft expense submitted for approval, **When** the approver rejects with a reason, **Then** the document returns to Draft state, the rejection reason is visible to the original creator, and the audit log records the rejection.
3. **Given** any posted document, **When** any user (including Administrators) attempts to delete, edit, or void it, **Then** the system rejects the action, logs the attempt, and (for tax-impacting documents) instructs the user to issue a credit note via FR-013, or (for non-tax-impacting documents) to enter a reversal voucher linked to the original.
4. **Given** a document type with approval enabled, **When** the document's creator (or last editor) attempts to approve their own submission, **Then** the system rejects the approval per FR-004 regardless of the user's role.

---

### User Story 4 - Automatic balanced double-entry journal generation (Priority: P2)

When any business document is posted (sales invoice, purchase invoice, expense, payment, credit note), the system automatically generates a corresponding double-entry journal voucher with debits equal to credits. Account mappings are configurable per document type and per VAT category. Users can view the generated journal from the source document and drill back from the journal to the source.

**Why this priority**: Without automatic balanced journals, every report (P&L, VAT, taxable income) would require manual reconciliation. This story enables reporting accuracy but only becomes useful once invoicing (P1) exists.

**Independent Test**: Posting one sales invoice and one purchase invoice produces two journal vouchers, each with debits = credits, and a trial-balance query for the period returns total debits = total credits.

**Acceptance Scenarios**:

1. **Given** a posted sales invoice for 1,000 EGP subtotal + 140 VAT, **When** the system generates the journal, **Then** Accounts Receivable is debited 1,140, Sales Revenue is credited 1,000, and Output VAT Payable is credited 140.
2. **Given** a posted deductible purchase invoice for 500 EGP subtotal + 70 VAT, **When** the system generates the journal, **Then** Expense (or Inventory) is debited 500, Input VAT Recoverable is debited 70, and Accounts Payable is credited 570.
3. **Given** any auto-generated journal, **When** the user queries the trial balance, **Then** total debits equal total credits to the cent.

---

### User Story 5 - Configurable tax rules and chart of accounts (Priority: P3)

An administrator configures the system's tax and accounting rules at runtime — without requiring code changes or a developer. The scope of US5 covers VAT categories (standard, reduced, zero-rated, exempt), deductible expense categories, the chart of accounts, fiscal year settings, and the company's tax/commercial registration data. Depreciation methods and rates per asset class are out of scope here; that configuration is delivered together with the depreciation engine in Phase 4 (US6). New rules apply only to documents created after the change; historical postings remain valid under the rules in force at the time.

**Why this priority**: Tax law changes (rate changes, new categories) must not require redeployment, but the MVP can launch with sane Egyptian defaults baked in. This story formalizes configurability after the core flows work.

**Independent Test**: An admin changes the standard VAT rate from 14% to 15% effective a future date; invoices dated before that date use 14%, invoices dated on or after use 15%, and the audit log shows which user changed the rule.

**Acceptance Scenarios**:

1. **Given** an admin updates a deductible expense category's tax treatment, **When** a bookkeeper creates a new expense, **Then** the new treatment is offered; pre-existing posted expenses retain their original classification.
2. **Given** the fiscal year is configured as Jan–Dec, **When** the user runs the annual taxable income report, **Then** the period defaults to the current fiscal year and groups expenses, depreciation, and revenue accordingly.

---

### User Story 6 - Fixed-asset capitalization and depreciation (Priority: P3, Phase 4)

A bookkeeper capitalizes a newly purchased fixed asset (machinery, vehicle, IT equipment, fit-out) instead of expensing it directly. The asset is recorded with cost, in-service date, useful life or rate, depreciation method, salvage value, and at least one supporting document. The system generates the periodic depreciation expense automatically using the configured rate and convention, posts it as a deductible expense to the taxable income report, and tracks the asset's net book value over time.

**Why this priority**: Depreciation is the second-largest legal lever for reducing taxable profit (after deductible operating expenses), but its rules and conventions add meaningful complexity (mid-period conventions, asset disposal, asset class configuration). It is correctly deferred to Phase 4 so the MVP can ship with the higher-impact P1/P2 flows first.

**Independent Test**: An Administrator capitalizes one asset on the first day of the fiscal year with a 5-year straight-line life and 0 salvage; running the taxable income report at year end shows depreciation expense equal to (cost / 5) and the asset's net book value equal to (cost × 4 / 5).

**Acceptance Scenarios**:

1. **Given** a fixed asset with cost 100,000 EGP, in-service date = first day of fiscal year, useful life 5 years straight-line, salvage 0, **When** the user runs the taxable income report at the end of that fiscal year, **Then** the system records depreciation expense of 20,000 EGP for the year as a deductible item and reports the asset's net book value as 80,000 EGP.
2. **Given** a fixed asset placed in service on the 15th of a month using the configured mid-month convention, **When** the user runs the taxable income report for that fiscal year, **Then** the system depreciates the asset pro-rata using the mid-month convention (per the depreciation in stub year edge case) rather than for the full year.
3. **Given** a fixed asset entered without a supporting document attached, **When** the user attempts to submit the asset for approval (or to post it directly where permitted), **Then** the system blocks the action and requires at least one attachment.

---

### User Story 7 - Withholding tax (الخصم والإضافة) lifecycle and Form 41 (Priority: P2, Phase 3)

A bookkeeper records a supplier services invoice; on the supplier-payment voucher (or directly on the invoice when paying-as-you-post), the system computes the applicable withholding-tax (WHT) rate per the configured WHT category for that service type, deducts the WHT from the cash paid to the supplier, accrues a "WHT to remit to authority" liability, and prints a WHT certificate (شهادة خصم) for the supplier. Each fiscal quarter, the system aggregates the company's WHT activity and generates Form 41 (نموذج 41) ready for filing. On the receivables side, when a customer withholds tax from a payment to the company, the bookkeeper records the customer-issued certificate against the invoice, the system credits the company's WHT-receivable, and reconciles it against the income-tax computation when corporate-tax reporting arrives. A dedicated WHT dashboard surfaces three states: WHT we owe to remit (by quarter), WHT certificates we expect to receive (by customer × invoice), and Form 41 filings (filed / unfiled / overdue).

**Why this priority**: WHT is the single most painful manual workflow Egyptian accountants currently run in Excel. Encoding it natively — including Form 41 and certificate tracking — eliminates that Excel and is the largest single time-saver the product can deliver. Phase 3 placement aligns with the broader tax-reporting work and avoids dragging Phase 1 / Phase 2 scope.

**Independent Test**: A user records one supplier services invoice for 10,000 EGP at the configured 5 % WHT rate and posts a payment of 9,500 EGP cash + 500 EGP WHT-payable; the system prints a WHT certificate, accrues a 500 EGP liability, and the WHT dashboard shows 500 EGP owed for the current quarter; running Form 41 generation for the quarter produces a filing artifact listing exactly that 500 EGP entry against the supplier.

**Acceptance Scenarios**:

1. **Given** a supplier-services invoice for 10,000 EGP and a configured WHT rate of 5 % for that service category, **When** the user posts a payment voucher against the invoice, **Then** the system splits the cash leg into 9,500 EGP paid to the supplier and 500 EGP credited to the "WHT payable" liability account, prints a WHT certificate (Arabic + English) referencing the supplier and the invoice, and updates the WHT dashboard's "owed to remit" total for the quarter.
2. **Given** an outbound sales invoice on which a customer subsequently withholds 1 % WHT, **When** the bookkeeper records the customer-issued WHT certificate against the invoice, **Then** the system credits a "WHT receivable" asset account, marks the residual invoice balance as settled, and the WHT dashboard's "certificates expected" view removes the entry once the certificate is received and recorded.
3. **Given** a closing fiscal quarter, **When** the user runs Form 41 generation, **Then** the system produces a filing artifact (PDF + structured machine-readable file) listing every WHT-payable accrued during the quarter, grouped by supplier and by service category, totaling exactly the WHT-payable balance for that quarter, and marks the entries as "filed" once the user confirms submission.
4. **Given** a quarter in which Form 41 has not been generated by the regulator-defined deadline, **When** any user opens the WHT dashboard, **Then** the dashboard surfaces a red "overdue" indicator with the elapsed days and an estimated late-filing penalty per the configured rule.

---

### User Story 8 - Accountant-firm portal with multi-company access and lock-for-review handoff (Priority: P3, Phase 2 add-on)

An external accountant (محاسب قانوني) supports several SME clients. Each client invites the accountant via a single email-or-link, granting access to *that* company's books. The accountant logs in once and sees a switcher listing every client they have access to, jumping between companies without re-authenticating. At month-end the in-house bookkeeper clicks "Lock for accountant review"; the company's books for the period freeze (no edits to drafts in that period; only the accountant or Administrator can post adjusting entries); the accountant reviews, posts adjustments where needed, and clicks "Release"; the period unlocks and the bookkeeper resumes normal operations. The accountant's actions are tagged in the audit log with both their identity and the firm they belong to.

**Why this priority**: The single biggest workflow gap in Egyptian SME accounting is the divide between in-house bookkeepers and external accounting firms — today they run in separate tools and re-key data manually. Solving this both reduces errors and creates a powerful distribution channel: accounting firms recommend the product to all their clients. Phase 2 placement aligns with introducing the per-doc-type approval framework from FR-026, which the firm-portal extends.

**Independent Test**: A user invites an accountant via email; the accountant accepts and logs in; the accountant sees the client company in their switcher, switches in, posts an adjusting journal voucher, and the audit log shows both the accountant's identity and their firm; the bookkeeper toggles "Lock for review" and the accountant's adjusting entry remains valid while bookkeeper-side edits are blocked for that period.

**Acceptance Scenarios**:

1. **Given** an Administrator at company A invites an external accountant by email, **When** the accountant accepts the invitation, **Then** the accountant gains a scoped account at company A with permissions configured by the Administrator (default: Accountant role + read-only on master data), the audit log records the invitation grant, and the accountant's company switcher shows company A alongside any other client companies they already serve.
2. **Given** an accountant logged into one client's books, **When** the accountant uses the company switcher to jump to a different client, **Then** the system completes the switch in under 1 second without re-authentication, every action thereafter is tagged with the new company context, and a visible UI indicator shows which client company the accountant is currently in.
3. **Given** a bookkeeper clicks "Lock for accountant review" for the current month, **When** any other bookkeeper attempts to edit a draft in that locked period, **Then** the system blocks the edit and surfaces "Locked for accountant review by [user] on [date]"; the accountant retains the ability to post adjusting journal vouchers in the locked period; the audit log records the lock with who/when.
4. **Given** an accountant has completed their review and clicks "Release", **When** the bookkeeper next opens the application, **Then** the period is unlocked, normal draft edits resume, and the audit log records the release with who/when plus a count of adjustments posted by the accountant during the locked window.

---

### User Story 9 - Tax-inspection bundle (Priority: P3, Phase 3)

The tax authority schedules an inspection (فحص ضريبي) and requests documentation for a specific period. The Administrator selects the period and clicks "Generate inspection bundle." The system produces a single archive containing: invoice register (sales + purchase + credit notes), expense register, journal listing, trial balance, monthly VAT reports, quarterly Form 41 filings, all attachments referenced by any document in the period, an audit-trail extract for the period, and an Auditor verification report from the FR-028 hash chain proving the bundle's source records were not altered after posting. The archive is sealed with a SHA-256 manifest hash that the inspector can verify independently.

**Why this priority**: A 30-day inspection request typically consumes weeks of accountant time today; one-click bundle generation collapses that to an afternoon and demonstrates compliance discipline that many SMEs cannot today. Phase 3 placement aligns with the report and journal infrastructure being delivered.

**Independent Test**: After running US1 + US2 + US7 to populate a fiscal year of activity, the user generates an inspection bundle for one quarter; the archive contains every required register and attachment for that quarter, the manifest hash matches a manually computed control hash, and an external script can re-verify the archive's integrity in under 60 seconds.

**Acceptance Scenarios**:

1. **Given** a fiscal quarter populated with sales invoices, purchase invoices, expenses, journals, VAT returns, and WHT activity, **When** an Administrator generates an inspection bundle for that quarter, **Then** the system produces a single ZIP/sealed-folder archive containing all of: invoice register PDF, expense register PDF, journal listing PDF, trial balance PDF, monthly VAT report PDF, Form 41 filing PDF, every attachment file referenced by any included document, an audit-trail extract for the period, an Auditor verification report from FR-028, and a `MANIFEST.sha256` listing every file with its hash plus a top-level archive hash.
2. **Given** a generated inspection bundle, **When** an inspector runs the bundled verification script (provided in the archive) on a clean machine, **Then** the script computes hashes for every included file, compares them against the manifest, recomputes the archive hash, validates the FR-028 hash-chain extract, and reports "valid" within 60 seconds for a quarter containing up to 5,000 documents.
3. **Given** an attempt to generate an inspection bundle for a period that includes any unposted draft documents, **When** the user proceeds, **Then** the system warns that drafts will be excluded, lists the affected drafts, requires explicit acknowledgement, and labels the bundle "drafts excluded" in the manifest.

---

### Edge Cases

- **Currency**: All values are stored and reported in EGP for the MVP. Foreign-currency invoices are out of scope.
- **VAT-exempt vs zero-rated**: Both must be distinguishable on invoices and reports; exempt sales do not allow input VAT recovery on related purchases, while zero-rated do.
- **Credit notes / refunds**: Posted invoices cannot be edited but must support credit notes that reverse output VAT and revenue, and reduce the VAT payable on the next return.
- **Period close**: Once a VAT return is filed for a period, the system must prevent backdating new invoices into that closed period (or require an explicit reopen action by an admin, which is logged).
- **Rounding**: VAT and totals are rounded to 2 decimals using banker's rounding; the rounding rule must be documented on every invoice PDF.
- **Bilingual content**: Invoices and key reports must render correctly in Arabic (right-to-left) and English; numeric totals must show Arabic words for the total amount.
- **Attachment integrity**: Once a document is posted, its attachments cannot be deleted; new attachments may only be added if explicitly allowed by configuration and are tracked in the audit log.
- **Approver self-approval**: See FR-004 (no separate edge-case rule; FR-004 is the canonical statement).
- **Concurrent posting**: Two users posting different documents at the same time must each receive sequential, gap-free document numbers within their (series, fiscal year) window per FR-011 — including the boundary case where one post lands on the last day of one fiscal year and another on the first day of the next.
- **Mock ETA submission failure**: If the mock ETA endpoint returns an error, the invoice remains posted but is flagged "ETA submission pending" and queued for retry; it must never silently fail.
- **Depreciation in stub year (Phase 4 / US6)**: Assets placed in service mid-period must depreciate pro-rata using the configured convention (full-month, half-year, etc.). Applies only once US6 is delivered in Phase 4.
- **Negative-margin transactions**: Discounts that drive a line below zero must be rejected; whole-invoice discounts that result in a negative invoice total must be rejected.

## Requirements *(mandatory)*

### Functional Requirements

#### Identity & access

- **FR-001**: System MUST support user accounts with role-based permissions covering at minimum the roles: Administrator, Accountant, Bookkeeper, Approver, and Auditor (read-only).
- **FR-002**: System MUST authenticate users via username/email and password with the following minimum complexity rules: password length ≥ 12 characters; MUST contain at least one uppercase letter, at least one lowercase letter, at least one digit, and at least one symbol from `!@#$%^&*()_-+=[]{}|;:,.<>?`. The system MUST reject the user's last 5 passwords on change. Passwords expire after 365 days unless the operator extends or disables expiry by configuration. Account lockout after 5 consecutive failed login attempts within a 15-minute rolling window; lockout duration 15 minutes (Administrator-resettable). All thresholds are configurable; the values above are the seeded defaults. System MUST additionally enforce a second factor (TOTP authenticator app) on every login for users holding the Administrator or Approver role; users in other roles MAY enable TOTP optionally. Disabling MFA for an Administrator or Approver MUST be impossible without first removing that role from the user, and the role/MFA change MUST be recorded in the audit log.
- **FR-003**: System MUST enforce per-role authorization on every action (create draft, edit draft, submit, approve, post, void, configure rules, view reports).
- **FR-004**: System MUST prevent any user from approving a document they themselves created or last edited.
- **FR-038**: System MUST provide a password-reset path that does NOT require external email infrastructure to function. At minimum, an Administrator MUST be able to reset another user's password from within the application; the reset MUST mark the password as expired so the user is forced to choose a new one on next login, and the reset action MUST be recorded in the audit log. Optional self-service password reset via SMTP MAY be enabled by an Administrator if outbound email is configured. To prevent a single locked-out Administrator from bricking the install, the application MUST support an out-of-band administrator-password recovery procedure (e.g., a server-local CLI run by the operator) that is itself audit-logged once the application restarts.
- **FR-039**: System MUST enforce a configurable session inactivity timeout (default 30 minutes) and a configurable absolute session lifetime (default 12 hours), after which the user MUST re-authenticate. Re-authentication MUST re-trigger MFA where FR-002 requires it. Logout (manual, timeout-driven, or absolute-lifetime-driven) MUST be recorded in the audit log.
- **FR-049**: System MUST support an **Accountant-Firm User**: a user identity that belongs to an accounting firm and may be granted scoped access to multiple companies (each company being a separate on-prem installation). On the per-installation side, an Administrator MUST be able to invite an Accountant-Firm User by email or by a single-use invitation token; the invitation MUST capture the firm name and the firm user's external identifier (email or external IdP subject claim). The invited user MAY accept the invitation and link the company into their existing firm-side credential pool. Once accepted, the firm user sees a company switcher listing every client company they have accepted invitations into; switching MUST complete in under 1 second of perceived latency, achieved via session keep-alive within a configured per-installation TTL (default 4 hours). Every action performed by the firm user MUST be tagged in the per-installation audit log with both the user identity and the firm name captured at invitation time. The Administrator at each installation MUST retain the ability to revoke firm-user access at any time; revocation MUST take effect within seconds, MUST be audit-logged, and MUST NOT delete or alter any record the firm user previously created.

  **On-prem topology (preserves the Round-1 deployment assumption — no vendor-hosted hub, no firm-hosted central server, no client data leaves any client installation):**
  - **Identity location** — Each installation owns its Accountant-Firm User identities locally. The MVP topology uses **per-installation accounts created by invitation token**; an optional Near-term enhancement allows each installation to trust an externally-provided IdP (Azure AD / Entra / Google Workspace) operated by the firm or by the customer, but no IdP dependency is required for MVP.
  - **Switcher location** — The company switcher is a **client-side credential pool** maintained either in the user's browser session (cookies + local storage) or in a dedicated thin desktop application that stores the pool in the OS keychain. The pool stores per-installation URL, accepted invitation reference, and a session token. The switcher renders this list. Switching opens a new authenticated request to a different installation URL; no central registry mediates the switch.
  - **Data flow** — Each request the firm user makes goes directly from their device to one client installation. **No client data is ever transmitted to any vendor system, to any firm-hosted hub, or to any other client installation.** The "firm" is a logical attribute of the user's account on each installation, not a connecting service.
  - **Cross-installation discovery** — There is none. Installations do NOT know about each other and MUST NOT communicate with each other. A firm user only sees an installation in their switcher because they personally accepted an invitation to it.
  - **Revocation** — Each installation revokes independently. Revoking firm-user access at installation A has no effect on the same firm user's access to installation B; this is by design and matches the on-prem isolation guarantee.
  - **Firm-name claim integrity** — Because the firm name is captured at invitation time and stored locally per installation, a firm user cannot "rename" their firm to evade audit attribution. Changing the firm name on an established account requires Administrator action on each installation and is audit-logged.
- **FR-050**: System MUST support a **"Lock for Accountant Review"** workflow on a per-fiscal-period basis. When a Bookkeeper or Administrator locks a period for review, (a) all draft documents dated within that period freeze (no edits, no submissions, no posts by Bookkeeper-role users); (b) the lock MUST be marked with the locking user's identity, timestamp, and an optional note; (c) users with the Accountant role (whether internal or firm-portal users from FR-049) RETAIN the ability to post adjusting journal vouchers in the locked period; (d) the lock screen MUST surface a clear "Locked for accountant review by [user] on [date]" indicator on every blocked action; (e) the lock MUST be released only by the locking user, an Administrator, or a participating Accountant; release MUST be audit-logged with a count of adjusting entries posted by the accountant during the locked window. This lock is distinct from FR-037's filing period close: review-lock is a soft handoff mechanism, while FR-037's close is a hard tax-filing boundary.

#### Company & master data

- **FR-005**: System MUST allow an Administrator to record company settings: legal name (Arabic + English), tax registration number, commercial registration number, address, fiscal year start, default currency (EGP), and logo.
- **FR-006**: System MUST allow management of customers, suppliers, and items as independent master-data entities, each with a unique code, name (Arabic + English), tax registration number where applicable, and active/inactive status.
- **FR-007**: System MUST prevent deletion of master-data records that are referenced by any posted document; deactivation is offered instead.
- **FR-040**: System MUST capture a **Customer Tax Profile** for every customer, indicating at minimum: (a) profile type — `B2B-Registered` (taxable taxpayer with an Egyptian tax registration number / TIN), `B2B-Unregistered` (business without a TIN), or `B2C-Consumer` (individual end customer); (b) the customer's TIN, mandatory and validated for `B2B-Registered`, optional/forbidden otherwise; (c) any customer-specific VAT exemption flag; (d) default sales VAT category if it differs from the company default. The profile drives FR-033 PDF legal-field rules (TIN line is rendered only for `B2B-Registered`), drives FR-034 eInvoice JSON document-type selection, and is the basis on which the monthly VAT report (FR-021) classifies receivables.
- **FR-041**: System MUST capture a **Supplier Tax Profile** for every supplier, indicating at minimum: (a) profile type — `Registered-Taxpayer` (Egyptian taxable supplier with TIN), `Unregistered`, or `Foreign-Supplier`; (b) the supplier's TIN, mandatory and validated for `Registered-Taxpayer`; (c) any supplier-specific reverse-charge flag (foreign suppliers); (d) the deductibility implication of input VAT from this supplier (input VAT is recoverable only when supplier is `Registered-Taxpayer` and the line is also marked deductible per FR-015 / FR-020). The profile drives input-VAT recoverability in FR-020 and the documentation requirements for FR-016 (e.g., `Unregistered` suppliers cannot produce a tax invoice and so deductible classification is rejected).

#### Sales & purchase invoices

- **FR-008**: System MUST support sales invoices with header (customer, date, currency, reference, optional **invoice-level discount** as either a percentage or a fixed EGP amount) and one or more lines (item, quantity, unit price, line-level discount, VAT category). Invoice-level discounts MUST be applied after line-level totals are computed and MUST be apportioned across taxable lines for VAT recalculation. Invoice-level discounts that drive the invoice grand total below zero MUST be rejected per the existing negative-margin edge case.
- **FR-009**: System MUST support purchase invoices with header (supplier, supplier invoice number, date received, currency) and one or more lines (item or expense category, quantity, unit price, VAT category, deductible flag).
- **FR-010**: System MUST automatically compute line subtotal, line VAT, invoice subtotal, invoice VAT total, and invoice grand total per the configured VAT category for each line.
- **FR-011**: System MUST assign a sequential, gap-free document number per document series (e.g., one series for sales invoices, one for credit notes, one for purchase entries) the moment posting succeeds end-to-end. Numbers MUST be considered consumed only by a successful post: a failed or rolled-back posting attempt MUST NOT consume a number, and a subsequent successful post MUST receive the same number that the failed attempt would have received. The system MUST guarantee gap-free numbering within each (series, fiscal year) window under concurrent posting load (mechanism — transaction boundary, lease/lock, sequence with reconciliation — is left to planning). Numbering MUST reset to 1 at the start of each fiscal year per series, and the assigned document number MUST embed the fiscal year (canonical format: `<SERIES>-<YYYY>-<NNNNNN>`, e.g., `INV-2026-000123`); the number printed on the PDF and embedded in the eInvoice JSON MUST use this canonical format.
- **FR-012**: System MUST treat posted invoices as immutable: no edits, no deletion, no voiding. Voiding is permitted **only** for documents in Draft, Submitted, or Approved states (not yet posted), and every void MUST capture a reason in the audit log. Corrections to a posted **tax-impacting** document (sales invoice, credit note, purchase invoice) MUST be performed via a credit note linked to the original (FR-013). Corrections to a posted **non-tax-impacting** document (e.g., a manual journal voucher) MUST be performed via a reversal voucher that references the original entry. Under no circumstance does a posted document release its document number; the original numbered slot remains permanently allocated.
- **FR-013**: System MUST support credit notes that reference the original invoice and reverse its revenue, VAT, and journal entries.

#### Expenses & depreciation

- **FR-014**: System MUST support recording of business expenses outside of supplier invoices (e.g., petty cash, payroll, rent, utilities), with date, category, amount, deductible flag, and at least one attachment. Depreciation expense is **not** entered through this requirement; it is generated automatically by the depreciation engine in Phase 4 (FR-017, US6).
- **FR-015**: System MUST classify each expense line as Deductible or Non-Deductible based on the selected category's default, allowing override only by users with appropriate permission and recording the override in the audit log.
- **FR-016**: System MUST require at least one attachment on any deductible expense and on any deductible purchase invoice line before it can be submitted for approval.
- **FR-017** *(Phase 4 — see US6)*: System MUST support fixed assets with cost, in-service date, useful life or rate, depreciation method (straight-line minimum; others if configured), and salvage value, and MUST generate periodic depreciation expense automatically using the configured rate and convention. Not required for the Phase 1 / Phase 2 / Phase 3 deliverables.
- **FR-018** *(Phase 4 — see US6)*: System MUST track the net book value of each asset at any point in time. Not required before Phase 4.

#### VAT handling

- **FR-019**: System MUST support multiple VAT categories (standard, reduced, zero-rated, exempt) configurable by Administrator, each with an effective-from date.
- **FR-020**: System MUST compute Output VAT from sales documents and Input VAT from deductible purchase documents, and MUST exclude non-deductible purchases from input VAT recovery even if a tax invoice exists.
- **FR-021**: System MUST produce a monthly VAT report listing total taxable sales, output VAT, total deductible purchases, recoverable input VAT, net VAT payable (or refundable), and the underlying document list.
- **FR-022**: System MUST apply the VAT rate that was in force on the document date, not the rate in force at posting time.

#### Withholding tax (الخصم والإضافة)

- **FR-045**: System MUST support configurable WHT categories (per service / payment type) with effective-from-dated rates, mirroring the VAT-category model in FR-019. On any supplier-services invoice or its payment voucher, the system MUST compute the applicable WHT amount, deduct it from the cash leg paid to the supplier, accrue a "WHT payable" liability to the tax authority, and produce a printable WHT certificate (شهادة خصم) in Arabic and English referencing the supplier, the source invoice, the WHT rate applied, and the amount withheld. WHT MUST also be supported on the receivables side: when a customer withholds tax from a payment to the company, the system MUST record the customer-issued certificate against the invoice and credit a "WHT receivable" asset.
- **FR-046**: System MUST generate **Form 41 (نموذج 41)** for any selected fiscal quarter, listing every WHT-payable accrual booked during the quarter, grouped by supplier and by service category, with quarter-total reconciliation to the WHT-payable account balance. The output MUST be produced both as a printable PDF and as a structured machine-readable file in the regulator-published shape (or, until the regulator publishes one, in a documented internal shape ready to be remapped). After the user marks Form 41 as filed, the included WHT entries MUST be flagged immutable for any further inclusion.
- **FR-047**: System MUST surface a **WHT lifecycle dashboard** with three views: (a) WHT we owe to remit, broken down by quarter with overdue indicators based on configurable filing deadlines and an estimated penalty using configurable late-fee rules; (b) WHT certificates we expect to receive from customers (per outbound invoice with WHT-receivable accrued but no certificate yet recorded), aged by days overdue; (c) Form 41 filings, with status (filed / unfiled / overdue) per quarter. The dashboard MUST drill down to the underlying invoices, payment vouchers, and certificates per FR-025.

#### Minimal Payments & Settlement *(MVP Phase 3 — sized to support WHT and AR/AP closure)*

The following three requirements deliver the smallest payments capability needed to make WHT (FR-045 / US7) work end-to-end and to close AR/AP balances against invoices. They deliberately do NOT include cashboxes, bank accounts, internal transfers, deposits/withdrawals, statement import, bank reconciliation, cheques, or multi-account cash/bank ledgers — those remain Near-term post-MVP or Future advanced per the Roadmap.

- **FR-051**: System MUST support a **Supplier Payment Voucher** as a posted document recording payment to a supplier against one or more of that supplier's posted purchase invoices. Header fields MUST include payment date, payment method (configurable list — default seed: `Cash`, `Bank Transfer`), payment reference (free text — typically a transaction reference, cheque number, or bank-confirmation number), and an optional note. The voucher MUST split the cash leg into (a) cash actually paid to the supplier and (b) WHT-payable accrued per FR-045 when the supplier-services rules apply, with the WHT-payable amount calculated automatically from the configured WHT category in force on the payment date. Posting a Supplier Payment Voucher MUST generate the corresponding balanced journal voucher per FR-029 (debit Accounts Payable, credit Cash, credit WHT Payable) and, when WHT is split, MUST automatically generate the supplier WHT certificate per FR-045. Supplier Payment Vouchers obey FR-026 / FR-027 immutability, audit, and approval rules in the same way as other posted documents.
- **FR-052**: System MUST support a **Customer Receipt Voucher** as a posted document recording receipt from a customer against one or more of that customer's posted sales invoices. Header fields MUST include receipt date, payment method (same configurable list as FR-051), payment reference, and an optional note. When the customer has withheld tax from the payment and provided a customer-issued WHT certificate per FR-045, the voucher MUST split the cash leg into (a) cash actually received and (b) WHT-receivable evidenced by the certificate, with the certificate identifier captured on the voucher line. Posting a Customer Receipt Voucher MUST generate the corresponding balanced journal voucher per FR-029 (debit Cash, debit WHT Receivable when applicable, credit Accounts Receivable). Customer Receipt Vouchers obey FR-026 / FR-027 immutability, audit, and approval rules.
- **FR-053**: System MUST allow **payment allocation** between any Supplier Payment Voucher and one or more posted purchase invoices for the same supplier (and symmetrically between Customer Receipt Vouchers and posted sales invoices for the same customer). Allocation MAY be full or partial; partial allocations MUST decrement the invoice's open balance by the allocated amount and update the AR/AP open-balance ledger. The system MUST surface, per customer and per supplier, an open-balance summary listing each invoice's original amount, allocated amount, and remaining open balance. The system MUST prevent over-allocation (allocating more than the voucher's net cash leg or more than the invoice's remaining open balance) and MUST surface a clear error in such cases. Cashboxes, bank accounts, and the broader ledger architecture remain out of scope for this requirement and are scheduled per the Roadmap; for the MVP, payment vouchers post directly against a single seeded "Cash" GL account and a single seeded "Bank — operating" GL account, distinguished only by the payment-method field.

#### Reports

- **FR-023**: System MUST produce a Profit & Loss (Taxable Income) report for any user-selected period showing: revenue, deductible expenses (by category), depreciation, non-deductible adjustments (added back), and resulting taxable profit.
- **FR-024**: System MUST produce a Trial Balance and General Journal listing for any user-selected period.
- **FR-025**: System MUST allow drill-down from any report figure to the underlying journal entries and source documents.
- **FR-048**: System MUST generate, on demand, a **Tax-Inspection Bundle** for any user-selected fiscal period. The bundle MUST be a single sealed archive containing: (a) sales-invoice register, (b) purchase-invoice and expense register, (c) credit-note and reversal register, (d) general-journal listing, (e) trial balance, (f) monthly VAT reports for each month in the period, (g) Form 41 filings for each quarter in the period, (h) every attachment file referenced by any included document, (i) an audit-trail extract for the period (including the FR-028 hash-chain segment that covers the period), (j) an Auditor verification report from FR-028 confirming chain integrity at bundle-generation time, and (k) a `MANIFEST.sha256` listing every file with its SHA-256 hash plus a top-level archive hash. The bundle MUST include a portable verification script that recomputes every file hash, validates the archive hash, and re-validates the audit-chain extract — runnable on a clean Windows machine without the application installed. Bundles that include any unposted draft documents MUST be rejected by default; the user MAY explicitly request a "drafts excluded" bundle, which MUST be labeled as such in the manifest.

#### Workflow & immutability

- **FR-026**: System MUST implement document states Draft, Submitted, Approved, Posted, with Rejected and Voided as additional terminal states for non-posted documents. Approval is **configurable per document type** by the Administrator. When approval is enabled for a document type, valid transitions are Draft → Submitted → Approved → Posted (with Rejected as a terminal state from Submitted, returning the document to Draft, and Voided as a terminal state from Draft / Submitted / Approved). When approval is **not** enabled for a document type, an authorized user (Accountant or Administrator) MAY post directly from Draft; in that case the audit log MUST record the post as `unapproved-direct` and capture the actor's role at the time of posting. The Phase 1 default configuration MUST have approval **disabled** for sales invoices (so US1 ships demoable). FR-004 (no self-approval) applies whenever approval is enabled for a document type.
- **FR-027**: Mutability and deletion rules are state-dependent:
  - **Draft**: a document MAY be edited by users authorized to edit drafts of that document type, until it is submitted or (where direct posting is permitted under FR-026) posted. Every field change on a draft MUST be captured in the audit log with username, timestamp, and before/after values for each changed field.
  - **Submitted / Approved**: field edits are NOT permitted. Only workflow actions are allowed, subject to per-role permission checks under FR-003 — namely Approve, Reject (with reason), Post, and Void (with reason). FR-004 (no self-approval) applies to the Approve action.
  - **Posted**: the document is fully immutable. No edit, no delete, no void. Corrections MUST flow through FR-013 (credit note) for tax-impacting documents or through a reversal voucher for non-tax-impacting documents (per FR-012).
  - **Across all states**: a document MUST NEVER be physically deleted from storage after creation. Cancellation is expressed solely as a logical state transition to Voided (where permitted by the rules above), MUST capture a reason, and MUST be recorded in the audit log; the document and its audit trail are preserved for the full retention period regardless of state.
- **FR-028**: System MUST capture an immutable audit log entry for every state transition, every field change on draft documents, every configuration change, and every login/logout, including username, timestamp, and before/after values where applicable. Audit log entries MUST be append-only and cryptographically chained: each entry MUST store the SHA-256 hash of (its own canonicalized payload + the previous entry's hash), and the system MUST expose an Auditor-runnable verification routine that walks the chain and reports the entry index of any inserted, modified, deleted, or reordered row. Verification MUST detect tampering even when performed by a database administrator with direct SQL access. To detect **tail truncation** (which a hash chain alone cannot detect), the system MUST additionally maintain a periodic external **integrity checkpoint** containing at minimum (a) the index of the latest committed audit entry, (b) the SHA-256 hash of that entry, and (c) its timestamp. The checkpoint MUST be written every 1,000 new audit entries OR every 15 minutes, whichever fires first. Checkpoint storage MUST be one of: (i) a dedicated `audit_checkpoint` table in a separate database schema whose write privileges are limited to a service account distinct from the SQL `sysadmin` account; or (ii) a file under a write-restricted operating-system directory owned by that same service account. The operator MUST select one of these two storage modes at install time. The verification routine MUST validate **both** the chain and the latest checkpoint, and MUST report a tail-truncation finding if the chain head index is less than the checkpoint index, or if the chain head hash does not match the checkpoint hash. The MVP MUST NOT require an HSM and MUST NOT require an external trusted timestamp authority.
- **FR-042**: System MUST retain audit log entries (and their corresponding integrity checkpoints) for **no less** than the document retention period defined in the Assumptions section (the longer of seven years or the period required by Egyptian tax law). Audit log entries MUST NEVER be deleted before that retention horizon by any user, role, or administrative action, including period close, fiscal-year close, or archive operations. When older fiscal years are moved to the archive tier (per the sizing assumption), the audit log MUST remain queryable by the Auditor role for the full retention horizon, and the integrity checkpoint MUST continue to validate across the archive boundary. Server time used for audit timestamps MUST be NTP-synchronized; an NTP failure MUST raise an operator-visible alert without halting the application.

#### Journal entries

- **FR-029**: System MUST automatically generate a balanced double-entry journal voucher (debits = credits) the moment any source document is posted, using configurable account mappings.
- **FR-030**: System MUST refuse to post any document for which the resulting journal would be unbalanced, and MUST surface a clear error to the user.
- **FR-031**: System MUST allow manual journal vouchers (e.g., adjusting entries) only for users with the appropriate role, and only in balanced form.

#### Attachments & PDF

- **FR-032**: System MUST allow attachments (PDF, JPG, PNG) with the following seeded size caps (all Administrator-configurable): per-file ≤ 25 MB, per-document aggregate ≤ 100 MB, per-fiscal-period aggregate ≤ 5 GB. Files exceeding the per-file cap MUST be rejected at upload with a clear error. Documents whose new attachment would push the per-document or per-period cap over MUST be rejected with the actor offered a path to compress or split. The system MUST store attachments durably on the operator-configured filesystem path, MUST persist a SHA-256 of each file for bit-rot detection, and MUST associate each attachment with a single source document.
- **FR-033**: System MUST generate a printable PDF for every sales invoice and credit note, in Arabic and English, including all legally required fields for an Egyptian tax invoice. The customer's tax registration number (TIN) line MUST be rendered if and only if the customer's tax profile (FR-040) is `B2B-Registered`; for `B2B-Unregistered` and `B2C-Consumer` profiles the TIN line MUST be omitted (and the document type label adjusted accordingly to "Simplified tax invoice" where applicable). The system company's TIN, commercial registration, address, and logo are always rendered.

#### ETA eInvoice integration (mock only)

- **FR-034**: System MUST generate, for every posted sales invoice and credit note, an ETA-format eInvoice JSON document conforming to the schema documented in this feature's contracts.
- **FR-035**: System MUST submit each generated eInvoice JSON to a configurable mock endpoint, record the simulated response, and surface submission status (Pending, Submitted, Failed) on the invoice; no live ETA endpoint is called in the MVP.
- **FR-036**: System MUST allow retry of failed mock submissions without changing the underlying invoice data.
- **FR-043**: System MUST surface an **Active ETA Compliance Dashboard** showing, for every posted sales invoice and credit note in a configurable look-back window, (a) ETA submission status (Pending / Submitted / Failed), (b) hours/days remaining until the regulator-defined submission window expires (configurable, with a sane Egyptian default), (c) estimated late-submission fine exposure if the window expires unsubmitted (using a configurable fine-rate table), and (d) recent supplier-TIN validation failures from a periodic supplier-TIN re-validation cron. Documents within configurable thresholds (e.g., < 24 h to deadline) MUST raise a visible badge for users with the Accountant or Administrator role and trigger an in-app notification. The dashboard data MUST refresh on every login and at least every 15 minutes while a user session is active.
- **FR-044**: System MUST embed a **tamper-evident QR seal** on every generated sales-invoice and credit-note PDF (FR-033). The QR encodes (a) a stable document identifier, (b) the document number, (c) the document grand total, (d) the document hash (SHA-256 of canonicalized payload at posting time), and (e) a verification URL that, when opened on a verifier site or scanned by the system's verification utility, recomputes the document hash from the live record and confirms (or denies) tampering since posting. The verification path MUST work offline within the on-prem deployment via a built-in verifier and MAY also work via a public-internet verifier when the operator opts in. Verification results MUST be reproducible with no dependency on the verifying user's role; the auditor verification routine in FR-028 MUST cross-check that the QR's embedded hash matches the audit-chain entry for the same document.

#### Period control

- **FR-037**: System MUST allow an Administrator to lock a fiscal period (e.g., once VAT is filed); after lock, no new documents may be posted with a date inside the locked period unless the period is explicitly reopened, and reopening is logged.

### Key Entities

- **User**: An authenticated person with one or more roles; cannot be deleted once they have created or approved any document — only deactivated.
- **Role**: A named permission bundle (Administrator, Accountant, Bookkeeper, Approver, Auditor).
- **Company**: The single legal entity for which books are kept in the MVP; one per installation; holds tax/commercial registration and fiscal-year configuration.
- **Customer / Supplier**: External parties with name (Arabic + English), tax registration number where applicable, contact details, and a Tax Profile (see below) that governs VAT, ETA, and PDF behavior.
- **Customer Tax Profile** *(MVP — FR-040)*: Per-customer record with profile type (`B2B-Registered`, `B2B-Unregistered`, `B2C-Consumer`), TIN (mandatory only for `B2B-Registered`), customer-level VAT exemption flag, and default sales VAT category override. Drives PDF TIN rendering, eInvoice JSON document type, and VAT report classification.
- **Supplier Tax Profile** *(MVP — FR-041)*: Per-supplier record with profile type (`Registered-Taxpayer`, `Unregistered`, `Foreign-Supplier`), TIN (mandatory only for `Registered-Taxpayer`), reverse-charge flag, and input-VAT recoverability implication. Drives input-VAT recoverability and deductible-classification eligibility.
- **Item**: A sold or purchased good or service, with code, name, default VAT category, and default account mapping.
- **Sales Invoice / Credit Note**: A revenue (or revenue-reversal) document with one or more lines, header customer reference, computed totals, status, and an associated eInvoice JSON document and PDF rendering.
- **Purchase Invoice**: A supplier-issued document recorded by the company, with deductible/non-deductible classification per line and at least one attachment when deductible.
- **Expense**: A non-supplier-invoice business cost (payroll, petty cash, depreciation), classified by category and deductible flag.
- **Fixed Asset** *(Phase 4 / US6)*: A capitalized item with cost, in-service date, depreciation method/rate, and tracked net book value. Not modeled in Phase 1 / 2 / 3.
- **VAT Category**: A configurable tax treatment with rate and effective-from date (standard, reduced, zero, exempt).
- **Deductible Expense Category**: A configurable expense classification with default deductible flag and default account mapping.
- **Chart of Account**: The configurable account list used for journal entries and reports.
- **Journal Voucher**: An automatically generated or manually entered double-entry transaction with header (date, source document reference) and balanced lines (debit/credit per account).
- **Document Series**: A configurable numbering series ensuring sequential, gap-free document numbers per document type.
- **Approval Request**: A queued submission tying a document to its required approver(s) with status and reason on rejection. Created only for document types whose Document Type Approval Setting is enabled.
- **Audit Log Entry**: An append-only record of any state-changing or configuration-changing action; cryptographically chained via SHA-256.
- **Audit Integrity Checkpoint**: An external (table-on-separate-schema or write-restricted file) record of the latest audit entry index, hash, and timestamp; written every 1,000 entries or every 15 minutes; consulted by the verification routine to detect tail truncation.
- **Document Type Approval Setting**: A per-document-type configuration toggle controlling whether documents of that type require approval before posting; when off, Accountants/Administrators may post directly and the audit log records `unapproved-direct`.
- **Attachment**: A binary supporting document linked to one source document.
- **Tax Period**: A fiscal period (typically calendar month for VAT, fiscal year for income tax) with open/locked status.
- **WHT Category** *(MVP — FR-045)*: A configurable withholding-tax classification per service / payment type with effective-from-dated rate.
- **WHT Certificate (شهادة خصم)** *(MVP — FR-045)*: A printable bilingual certificate evidencing tax withheld by one party from a payment to another. Issued outbound (to suppliers when the company withholds) and recorded inbound (from customers when the company is withheld upon).
- **Form 41 Filing (نموذج 41)** *(MVP — FR-046)*: A quarterly filing artifact (PDF + machine-readable file) listing all WHT-payable accruals for the quarter, with file/unfile/overdue lifecycle status.
- **Accountant-Firm User** *(MVP — FR-049)*: A user identity that belongs to an external accounting firm, granted scoped multi-company access via per-installation invitation. All actions audit-logged with both the user identity and the firm name.
- **Period Review Lock** *(MVP — FR-050)*: A soft, audit-logged lock placed by a Bookkeeper or Administrator that freezes draft edits in a fiscal period while leaving accountant-role adjusting entries possible. Distinct from FR-037's hard tax-filing close.
- **Tax-Inspection Bundle** *(MVP — FR-048)*: A sealed archive (registers + reports + attachments + audit-trail extract + verification report + manifest) generated on demand for any fiscal period; verifiable by an inspector on a clean machine.
- **Compliance Risk Item** *(MVP — FR-043, FR-047)*: A surfaced risk row on the Active ETA / WHT compliance dashboard, with a kind (e.g., "ETA submission window expires in 12 h", "Form 41 Q2 unfiled, +18 days"), an estimated EGP exposure, and a drill-down link to the underlying source.
- **Document Verification Seal (QR)** *(MVP — FR-044)*: A QR encoding embedded in invoice PDFs that ties the rendered document back to its FR-028 audit-chain entry; verifiable offline within the on-prem deployment and optionally online via a public verifier.
- **Supplier Payment Voucher** *(MVP — FR-051)*: A posted document recording payment to a supplier with payment date, payment method, payment reference, and an automatic WHT-payable split when configured WHT rules apply. Generates the supplier WHT certificate and the balanced journal voucher.
- **Customer Receipt Voucher** *(MVP — FR-052)*: A posted document recording receipt from a customer with receipt date, payment method, payment reference, and an optional customer-issued WHT-receivable split when the customer provides a WHT certificate. Generates the balanced journal voucher.
- **Payment Allocation** *(MVP — FR-053)*: A link between a Supplier Payment Voucher (or Customer Receipt Voucher) and one or more posted invoices, recording the amount applied to each invoice. Drives AR/AP open-balance settlement.
- **Tax Risk Score** *(MVP — Differentiator 1)*: A per-document score and ordered list of risk findings produced by the rule engine; visible in-context on every document edit/view screen and aggregated by the cockpit, ETA dashboard, WHT dashboard, Owner Mode, and Accountant Review Mode.
- **Monthly Tax Closing Cockpit** *(MVP Phase 3 — Differentiator 2)*: The canonical compliance surface for a fiscal period. Aggregates VAT readiness, missing documents, failed ETA submissions, unapproved deductible expenses, non-recoverable VAT, Form 10 / VAT pack readiness, and the Period Lock checklist. Owner Mode and Accountant Review Mode are role-filtered lenses on this entity.
- **Document 360 View (Evidence Vault)** *(MVP — Differentiator 6)*: The unified per-document view aggregating attachments, tax-profile snapshots, deductibility reason, audit trail extract, ETA submission history, audit integrity status, and Tax Risk Score history.
- **Missing Document Request** *(Near-term — Differentiator 8)*: A scoped single-use upload link delivered by email or copy-link to a designated responder, attached on upload directly to the originating document, with the request and response steps audit-logged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An accountant new to the system can complete the end-to-end flow (configure company → create one customer → create one item → issue one posted sales invoice with PDF and eInvoice JSON) in under 15 minutes without consulting external help.
- **SC-002**: For any fiscal period containing up to 5,000 online documents, the monthly VAT report and the taxable income report each render in under 5 seconds at the 95th percentile under a load of 25 concurrent active users; under the same load, all interactive screens (invoice list, draft edit, posting) respond in under 1 second at the 95th percentile.
- **SC-003**: 100% of automatically generated journal vouchers balance to the cent (debits = credits) across a randomized sample of at least 1,000 posted documents.
- **SC-004**: 100% of posted documents survive an attempted edit, attempted delete, and attempted backdate without data corruption; every attempt is recorded in the audit log.
- **SC-005**: For a representative sample company with 200 sales invoices and 200 purchase invoices in a year, the taxable income computed by the system matches a manually computed control figure to within ±1 EGP.
- **SC-006**: Zero gaps and zero duplicates in document numbering within any (series, fiscal year) window, validated against a stress run of 500 concurrent posting attempts spanning a fiscal-year boundary; numbering correctly resets to 1 on the first posting of the new fiscal year.
- **SC-007**: 100% of generated eInvoice JSON documents validate against the documented ETA-format schema in the contracts directory.
- **SC-008**: Every legally required field for an Egyptian tax invoice (per the field list in the contracts) is present on every generated PDF, validated against an automated PDF-content check.
- **SC-009**: After completing user-facing training material, a non-technical user can correctly classify deductible vs non-deductible expenses on at least 9 of 10 sample scenarios drawn from common Egyptian SME categories.
- **SC-010**: 100% of state changes, configuration changes, and login events appear in the audit log; an auditor can reconstruct the full life cycle of any randomly selected posted document end-to-end from the log alone. The hash-chain verification routine returns "valid" on a clean log and correctly identifies the affected entry index in 100% of synthetic tamper cases (insert, edit, delete, reorder, **tail truncation**) within 30 seconds for a log containing up to 1,000,000 entries; tail-truncation cases are detected via the integrity checkpoint described in FR-028.
- **SC-011**: For a representative sample company with 50 supplier-services invoices in a quarter at varying WHT rates, the WHT-payable balance computed by the system matches a manually computed control figure to within ±1 EGP; Form 41 generated from that data lists every WHT entry exactly once (zero duplicates, zero omissions); and the WHT lifecycle dashboard's "owed to remit" total reconciles to the WHT-payable account balance to the cent at all times.
- **SC-012**: The Active ETA Compliance Dashboard correctly identifies 100% of posted invoices whose ETA submission window will expire within the next 24 hours, in under 2 seconds, on a database containing up to 50,000 posted documents; the dashboard's fine-exposure estimate matches a manually computed control figure to within ±1 EGP per document.
- **SC-013**: 100% of generated invoice-PDF QR seals successfully verify against their source document immediately after posting and continue to verify after the document's audit-chain entry has been written; 100% of seals correctly fail verification when the underlying document is tampered with (synthetic tamper test); offline verification (no internet access) completes in under 1 second per document.
- **SC-014**: A Tax-Inspection Bundle generated for any fiscal quarter containing up to 5,000 documents is produced in under 5 minutes, contains every artifact required by FR-048, and is independently re-verifiable by the bundled verification script in under 60 seconds on a clean Windows machine with no network access. An Accountant-Firm User can switch between two client companies in under 1 second of perceived latency; every action they take is correctly audit-logged with both their user identity and their firm name; revoking firm-user access at one company takes effect within 5 seconds and does not alter their access to other client companies.

## Assumptions

- **Deployment**: self-hosted on-premises only. The customer installs the product on their own Windows server / VM and owns the database, file store, and backups end-to-end. No vendor-hosted SaaS offering, no vendor-side data, no hybrid mode in the MVP.
- Single-tenant, single-company per installation in the MVP; multi-company / multi-tenant is out of scope.
- All amounts in Egyptian Pounds (EGP); foreign-currency invoicing and FX revaluation are out of scope.
- Bilingual UI and document output limited to Arabic and English.
- ETA integration is **mock-only**: a configurable local endpoint receives the JSON, records it, and returns a simulated response. No production ETA credentials, signing certificates, or live submissions in the MVP.
- Cash & bank functionality in the MVP is restricted to a **minimal Phase 3 Payments & Settlement capability** — supplier payment vouchers, customer receipt vouchers, payment allocation against invoices, AR/AP balance settlement, and the WHT split on payment vouchers (FR-051, FR-052, FR-053). The minimal capability uses two seeded GL accounts (`Cash`, `Bank — operating`) distinguished by the payment-method field. **Everything else is Near-term post-MVP or Future advanced per the Roadmap and MUST NOT be pulled into the MVP**: cashboxes / safes, multiple bank accounts, internal cash↔bank transfers, deposits / withdrawals, multi-account cash & bank ledgers, AR / AP statements and aging reports, full payment-method workflows (cheques, post-dated cheques), payroll calculation, and inventory beyond simple cost capture remain Near-term; bank-statement import, bank reconciliation, FIFO / weighted-average valuation, automatic COGS journal generation, and POS remain Future advanced. Payroll totals continue to enter as expense entries from an external source until a payroll module is delivered.
- Default VAT rate seeded at 14% (Egyptian standard rate as of 2026); the rate is configurable so this can be updated when law changes.
- Default depreciation method seeded as straight-line with categorized rates; declining-balance and other methods are configurable but not all UI-managed in the MVP.
- Statutory income-tax rate computation (e.g., applying the corporate tax rate to taxable profit to compute tax due) is **out of scope** for the MVP — the system reports taxable income, not the tax liability itself.
- **Sizing target (MVP)**: small-to-mid Egyptian SME — up to 25 concurrent active users, up to 50,000 documents posted per year, with 5 fiscal years kept fully online (live database, fully queryable from the UI). Older fiscal years remain retained per the retention assumption below but may be moved to an archive tier where queries are still possible but may run slower; archive design itself is a planning-phase concern.
- Document and attachment retention follows the longer of seven years or the period required by Egyptian tax law (5 years online + at least 2 archived); physical/offsite backup policy is the operator's responsibility.
- Users have stable internal-network connectivity to the application; full offline operation is out of scope.
- The MVP is delivered in clearly demarcated phases: (1) identity + master data + sales invoices + PDF + mock eInvoice + **Active ETA Compliance Dashboard** + **tamper-evident invoice QR seal**; (2) purchase invoices + expenses + attachments + approval workflow + immutability + audit log + **Accountant-Firm Portal with multi-company access and lock-for-review handoff**; (3) automatic journals + VAT monthly report + taxable income report + period close + **WHT lifecycle (capture, certificates, Form 41, dashboard)** + **Tax-Inspection Bundle generator**; (4) fixed assets + depreciation + configurable rules UI. Each phase is independently demoable. Capabilities outside the MVP are scheduled in the Roadmap & Phasing section below. The Phase 1 + Phase 3 differentiator additions (Active ETA dashboard, QR seal, WHT, Inspection Bundle) and the Phase 2 firm portal were added in Round 4 specifically to position the MVP as the leading Egyptian tax-accounting product rather than a feature-parity me-too.

## Roadmap & Phasing

This section enumerates all planned capability groups and classifies every line item by maturity bucket. It is the authoritative scoping document; if a capability is not listed here, it has not been considered for any current or planned phase.

### Maturity legend

| Bucket | Meaning |
| --- | --- |
| **MVP** | Already in this spec or being added in this clarification round; delivered in Phases 1–4. |
| **Near-term post-MVP** | Planned for the first major release after the MVP. Not in the MVP but committed direction. |
| **Future advanced** | Committed direction with no near-term delivery commitment; will require its own spec round when scheduled. |
| **OOS** | Out of current implementation. Explicitly excluded from all currently planned releases. |

### 1. Accounting Core

| Capability | Maturity | Notes |
| --- | --- | --- |
| Chart of accounts | MVP | US5; configurable per FR-019 ecosystem and seeded at install. |
| Journal vouchers | MVP | FR-029, FR-031. |
| Auto-generated journals from source documents | MVP | FR-029, US4 (Phase 3). |
| Manual adjusting journals | MVP | FR-031, role-restricted. |
| Trial balance | MVP | FR-024. |
| General ledger | MVP | Implied by FR-024 + FR-025 (drill-down); explicitly delivered in Phase 3. |
| Account statement | Near-term | Per-account ledger statement view; drilled by FR-025 in MVP but no dedicated statement screen until post-MVP. |
| Period close / reopen | MVP | FR-037. |
| Fiscal-year close (closing entries) | Near-term | Retained-earnings rollover at fiscal-year boundary. Not in MVP. |
| Opening balances | Near-term | Required to onboard existing businesses; covered jointly with import/export group. |
| Reversal entries | MVP | FR-012, FR-027 introduce reversal voucher mechanic for posted non-tax-impacting docs. |
| Audit trail | MVP | FR-028, FR-042. |

### 2. Sales / AR

| Capability | Maturity | Notes |
| --- | --- | --- |
| Sales invoices | MVP | US1, FR-008. |
| Credit notes | MVP | FR-013. |
| **Customer receipt voucher (minimal)** | **MVP (Phase 3)** | Round 5 — FR-052. Sized to support WHT and AR closure; uses 2 seeded GL accounts. |
| Partial payments | **MVP (Phase 3)** | Round 5 — covered by FR-053 partial allocation. |
| Overpayments | Near-term | Posted to a customer-credit balance; not in MVP scope. |
| Customer credit balances | Near-term | Carry-forward credit applied automatically on next invoice. |
| Customer statements | Near-term | Period-bound statement of account PDF. |
| Receivable aging | Near-term | Buckets: current / 30 / 60 / 90+. |
| Overdue invoices | Near-term | Driven by payment terms (also Near-term). |
| **Payment allocation against invoices (minimal)** | **MVP (Phase 3)** | Round 5 — FR-053. Manual allocation in MVP; FIFO default deferred to Near-term. |
| Sales returns | Near-term | Distinct from credit notes for non-tax reversals; uses reversal voucher. |
| Sales discounts | MVP | Line-level (FR-008) and invoice-level (FR-008 expanded in Round 3). |
| Price lists | Near-term | Per-customer or per-channel pricing. |
| Customer tax profile | MVP | FR-040 — `B2B-Registered` / `B2B-Unregistered` / `B2C-Consumer`. |

### 3. Purchases / AP

| Capability | Maturity | Notes |
| --- | --- | --- |
| Purchase invoices | MVP | US2, FR-009. |
| **Supplier payment voucher (minimal, with WHT split)** | **MVP (Phase 3)** | Round 5 — FR-051. Sized to support WHT and AP closure; uses 2 seeded GL accounts. |
| Partial supplier payments | **MVP (Phase 3)** | Round 5 — covered by FR-053 partial allocation. |
| Supplier statements | Near-term | Period-bound statement of account PDF. |
| Payable aging | Near-term | Buckets: current / 30 / 60 / 90+. |
| Supplier invoice duplicate detection | Near-term | Match on (supplier, supplier-invoice-number, date, amount). |
| Purchase returns / debit notes | Near-term | Symmetric to credit notes. |
| Expense classification | MVP | FR-015. |
| Deductible vs non-deductible handling | MVP | FR-015, FR-020. |
| Attachment requirements | MVP | FR-016, FR-032. |
| Supplier tax profile | MVP | FR-041 — `Registered-Taxpayer` / `Unregistered` / `Foreign-Supplier`. |

### 4. Cash & Bank

| Capability | Maturity | Notes |
| --- | --- | --- |
| **Minimal payment vouchers (cash + single bank, no separate cashbox/bank account model)** | **MVP (Phase 3)** | Round 5 — FR-051 / FR-052 / FR-053. Two seeded GL accounts only (`Cash`, `Bank — operating`), distinguished by payment-method field. |
| **Payment methods (configurable list)** | **MVP (Phase 3)** | Round 5 — seeded with `Cash`, `Bank Transfer`; configurable in MVP for plain types only. Cheque-related methods deferred. |
| Cashboxes / safes | Near-term | Multiple cashboxes per company; replaces single `Cash` GL with cashbox-keyed ledgers. |
| Bank accounts | Near-term | Multiple bank accounts; per-account ledger; replaces single `Bank — operating` GL with account-keyed ledgers. |
| Cash receipts (full, beyond minimal voucher) | Near-term | |
| Cash payments (full, beyond minimal voucher) | Near-term | |
| Internal transfers | Near-term | Cashbox ↔ cashbox, cashbox ↔ bank, bank ↔ bank. |
| Bank deposits | Near-term | Cashbox → bank with bank-fee tracking. |
| Bank withdrawals | Near-term | Bank → cashbox with bank-fee tracking. |
| Bank statement import | Future advanced | CSV / OFX / MT940 ingestion. |
| Bank reconciliation | Future advanced | Match imported statement lines to internal ledger. |
| **Egyptian bank-statement parsers (top 5)** | **Near-term** | Round 4 addition. Pre-built parsers for CIB, NBE, Banque Misr, QNB Egypt, ADIB statement formats so reconciliation works out of the box for the bulk of Egyptian SMEs. |
| Cash/bank ledger | Near-term | Per-account ledger view. |
| Payment methods (full configurable list with cheque/card) | Near-term | Extends the minimal MVP list above with cheque, card, and other types. |
| Cheques / post-dated cheques | Future advanced | Distinct lifecycle (issued / deposited / cleared / bounced). |

### 5. VAT / Tax Compliance

| Capability | Maturity | Notes |
| --- | --- | --- |
| Configurable VAT categories | MVP | FR-019. |
| Input VAT / output VAT | MVP | FR-020. |
| Recoverable vs non-recoverable VAT | MVP | FR-020 + FR-041 (supplier profile gates recoverability). |
| VAT monthly report | MVP | FR-021. |
| Taxable income report | MVP | FR-023. |
| Non-deductible adjustments | MVP | FR-023 add-back line. |
| Withholding tax (الخصم والإضافة) | **MVP (Phase 3)** | Promoted from Future advanced in Round 4 — Egyptian SMEs' biggest manual workflow. Delivered via US7 + FR-045 (capture + certificate), FR-046 (Form 41), FR-047 (lifecycle dashboard). |
| WHT certificate generation (شهادة خصم) | MVP (Phase 3) | Bilingual certificate per FR-045. |
| Form 41 (نموذج 41) filing artifact | MVP (Phase 3) | Quarterly filing per FR-046. |
| WHT lifecycle dashboard | MVP (Phase 3) | Three views (owed / expected / Form 41 filings) per FR-047. |
| Late-fee / penalty predictor | Near-term | Configurable late-fee rules; layered on FR-043 (ETA) and FR-047 (WHT). Round 4 addition. |
| Corporate income-tax estimate | Future advanced | Apply statutory rate to taxable income to compute tax due (currently OOS per Assumptions). Future module. |
| Tax adjustment schedule | Near-term | Year-end schedule of book-to-tax adjustments. |
| Accountant review checklist | Near-term | Pre-filing checklist run by Accountant role. |
| Tax return support pack | Near-term | Bundle of VAT return data + supporting documents in regulator-acceptable shape. Distinct from FR-021 management report. |
| Embedded last-updated Arabic tax explainers | Near-term | Short Arabic explainer per regulator form, version-stamped, editable by an internal editorial process. Round 4 addition. |

### 6. Egyptian ETA Integration

| Capability | Maturity | Notes |
| --- | --- | --- |
| Mock eInvoice JSON | MVP | FR-034, FR-035. |
| ETA eInvoice schema validation | MVP | Generated JSON validated against schema in `contracts/`. |
| **Active ETA Compliance Dashboard** | **MVP (Phase 1)** | Round 4 — FR-043. Deadline countdown per invoice, fine-exposure estimate, supplier-TIN re-validation cron. The product's signature compliance feature. |
| **Tamper-evident invoice QR seal** | **MVP (Phase 1)** | Round 4 — FR-044. Public-verifiable proof that a posted invoice has not been altered. Differentiator no incumbent has. |
| Live eInvoice submission | Future advanced | Replaces mock endpoint with real ETA API. |
| Digital signing | Future advanced | Required by live ETA; uses signing certificate / smart card. |
| Token / credential management | Future advanced | OAuth/PKI token lifecycle for ETA. |
| Submission status polling | Near-term | Polled status updates from the mock endpoint; designed to extend to live in future. |
| Retry queue | MVP | FR-036. |
| ETA error mapping | Near-term | Translate ETA error codes into actionable user messages. |
| Import received supplier eInvoices | Future advanced | Pull inbound eInvoices addressed to the company TIN. |
| Document package download | Near-term | Bundle (eInvoice JSON + PDF + receipt) per document. |
| Audit of ETA submissions | MVP | All submissions / retries / responses captured by FR-028 audit log. |

### 7. POS / eReceipt

A dedicated full POS product is Future advanced (own spec round). However, **Cash-business mode** — daily Z-report aggregation + eReceipt batch submission for SMEs that don't issue per-transaction tax invoices — is promoted to Near-term in Round 4 because the regulator's eReceipt mandate is rolling out and few competitors will be ready.

| Capability | Maturity | Notes |
| --- | --- | --- |
| **Cash-business mode (Z-report + eReceipt batch)** | **Near-term** | Round 4 promotion. Aggregate daily takings into a Z-report, generate eReceipt batch JSON. Distinct from full POS terminal infra. |
| POS terminals | Future advanced | |
| Cashier users | Future advanced | |
| Shifts | Future advanced | |
| Sales receipts (per-transaction) | Future advanced | |
| Receipt returns | Future advanced | |
| Cash drawer summary | Future advanced | |
| Daily Z report (interactive POS) | Future advanced | Distinct from the standalone Z-report in Cash-business mode above. |
| Mock eReceipt JSON | Near-term | Shared with Cash-business mode. |
| Live ETA eReceipt integration | Future advanced | |
| Offline POS mode | Future advanced | Queue receipts locally, sync when online. |

### 8. Inventory / COGS

The entire group is **Future advanced** for the MVP. Simple cost capture (line-level cost on purchase invoices) remains in MVP; quantity tracking, valuation, and COGS automation are not in MVP or Near-term.

| Capability | Maturity | Notes |
| --- | --- | --- |
| Warehouses | Future advanced | |
| Stock movements | Future advanced | |
| Stock balances | Future advanced | |
| Purchase receiving | Future advanced | |
| Sales delivery | Future advanced | |
| Stock adjustment | Future advanced | |
| Stock transfer | Future advanced | |
| Inventory valuation | Future advanced | |
| Weighted average / FIFO | Future advanced | Per user direction. |
| Automatic COGS journal entry | Future advanced | |
| Low stock alerts | Future advanced | |
| Item batches / expiry | Future advanced | Per user direction. |

### 9. Fixed Assets *(Phase 4 of MVP — see US6)*

| Capability | Maturity | Notes |
| --- | --- | --- |
| Asset capitalization | MVP (Phase 4) | FR-017, US6. |
| Asset categories | MVP (Phase 4) | Configurable rates per class. |
| Depreciation methods | MVP (Phase 4) | FR-017 — straight-line minimum, others configurable. |
| Depreciation schedules | MVP (Phase 4) | Periodic schedule generation. |
| Net book value | MVP (Phase 4) | FR-018. |
| Asset disposal | MVP (Phase 4) | Removal from depreciation; gain/loss calc. |
| Asset sale / write-off | Near-term | Distinguished from disposal; revenue / write-off journal entry. |
| Asset attachments | MVP (Phase 4) | FR-032 + US6 scenario 3. |
| Depreciation journal entries | MVP (Phase 4) | Generated by FR-029 once US6 is delivered. |

### 10. Documents / Attachments / OCR

| Capability | Maturity | Notes |
| --- | --- | --- |
| PDF / JPG / PNG attachments | MVP | FR-032. |
| Attachment integrity after posting | MVP | Edge case "Attachment integrity"; FR-032 + audit log. |
| Receipt photo upload | MVP | Treated as a regular attachment. |
| OCR extraction | Future advanced | Per user direction; depends on third-party Egyptian-Arabic-tuned OCR partner. |
| Match receipt to expense or purchase invoice | Future advanced | Depends on OCR. |
| Mobile-friendly upload page | Near-term | Lightweight upload-only UI for receipt photos. |
| **WhatsApp receipt capture** | **Near-term** | Round 4 promotion. Bookkeeper sends a photo to the company's WhatsApp Business number; the system attaches it as a draft expense awaiting approval. Independent of OCR — works even before OCR ships, just creating drafts with the photo and metadata. |
| Document retention policy | MVP | Per Assumptions and FR-042. |

### 11. Workflow / Controls

| Capability | Maturity | Notes |
| --- | --- | --- |
| Role-based access control | MVP | FR-001, FR-003. |
| Approval workflow by document type | MVP | FR-026 — per-doc-type configurable. |
| No self-approval | MVP | FR-004. |
| Configurable approval thresholds | Near-term | E.g., > 50,000 EGP requires Controller-level approver. |
| Document state machine | MVP | FR-026, FR-027. |
| Immutable posted documents | MVP | FR-012, FR-027. |
| Credit-note / reversal correction flow | MVP | FR-013, FR-027. |
| Audit hash-chain | MVP | FR-028. |
| Audit integrity checkpoint | MVP | FR-028 (extended). |
| Auditor verification report | MVP | FR-028 verification routine, SC-010. |
| **Accountant-Firm Portal (multi-company access)** | **MVP (Phase 2 add-on)** | Round 4 — US8, FR-049. External accounting firm sees all client companies in one switcher. The product's strategic distribution wedge. |
| **"Lock for accountant review" handoff** | **MVP (Phase 2 add-on)** | Round 4 — US8, FR-050. Soft per-period lock distinct from FR-037 filing close. Replaces the WhatsApp-the-Excel ritual. |
| **Tax-Inspection Bundle generator** | **MVP (Phase 3)** | Round 4 — US9, FR-048. One-click sealed regulator-ready archive built on the FR-028 hash chain. |

### 12. Reporting & Dashboards

| Capability | Maturity | Notes |
| --- | --- | --- |
| Dashboard KPIs | Near-term | Sales / purchases / cash / VAT-due headline tiles. |
| Sales summary | Near-term | Period sales by customer / item. |
| Purchases summary | Near-term | Period purchases by supplier / category. |
| Expense summary | Near-term | Period expenses by category. |
| VAT report | MVP | FR-021. |
| Taxable income report | MVP | FR-023. |
| Profit & Loss | MVP | Subset of FR-023; presented as standalone view in Phase 3. |
| Balance sheet | Future advanced | Per user direction; depends on cash & bank module. |
| Cash flow | Future advanced | Per user direction; depends on cash & bank module. |
| AR aging | Near-term | Depends on payment recording (group 2). |
| AP aging | Near-term | Depends on payment recording (group 3). |
| Inventory valuation report | Future advanced | Depends on inventory module. |
| Audit integrity report | MVP | FR-028 verification output. |
| **Compliance dashboard (ETA + WHT + period close)** | **MVP** | Round 4 — composes FR-043 (ETA) + FR-047 (WHT) + FR-037 (period). Single "are we compliant?" landing page. |
| **Late-fee / penalty predictor** | Near-term | Round 4 — projected fines for the next 30 days given current state, based on configurable rules. |
| Export reports to Excel / PDF | Near-term | Common requirement; explicitly out of MVP. |
| Drill-down from reports to source documents | MVP | FR-025. |

### 13. Migration / Import / Export

The MVP ships with **manual data entry only**. All bulk import paths are Near-term post-MVP — they are required to onboard existing businesses but do not block MVP demoability for green-field installs.

| Capability | Maturity | Notes |
| --- | --- | --- |
| Import customers from Excel / CSV | Near-term | |
| Import suppliers | Near-term | |
| Import items | Near-term | |
| Import chart of accounts | Near-term | |
| Import opening balances | Near-term | Trial balance + AR/AP carry-forward + fixed-asset NBV. |
| Import historical unpaid invoices | Near-term | For receivable / payable continuity. |
| Export all major reports | Near-term | Excel + PDF. |
| Data migration checklist | Near-term | Operator-facing runbook. |
| Validation errors for import files | Near-term | Per-row error reporting; partial commits forbidden. |

### 14. On-Prem Operations

| Capability | Maturity | Notes |
| --- | --- | --- |
| Windows Server / VM deployment | MVP | Per Assumptions. |
| Installer | MVP | Required to deliver an on-prem product. |
| Upgrade path | Near-term | In-place upgrade from version N to N+1; schema migration tooling. |
| Database backup schedule | Near-term | Operator-configurable schedule via the application. |
| Restore procedure | Near-term | Documented + dry-runnable. |
| Attachment backup | Near-term | File-store backup integrated with database backup. |
| Audit checkpoint backup | MVP | Implied by FR-028 + FR-042 retention requirement. |
| Health / status screen | Near-term | NTP status, DB free space, last backup, last checkpoint, MFA enrollment. |
| Storage usage screen | Near-term | DB size, attachment size, growth trend. |
| License / activation | Future advanced | Commercial-licensing module; out of MVP. |
| Operator maintenance checklist | Near-term | Daily / weekly / monthly checklist surfaced in-app. |

### 15. Security

| Capability | Maturity | Notes |
| --- | --- | --- |
| Password policy | MVP | FR-002. |
| Lockout | MVP | FR-002. |
| TOTP MFA for Administrator and Approver | MVP | FR-002. |
| Optional MFA for other users | MVP | FR-002. |
| Session timeout | MVP | FR-039. |
| Password reset / admin recovery | MVP | FR-038. |
| Permission matrix | MVP | Implied by FR-003; full grid produced in plan.md. |
| Security audit log | MVP | FR-028; auth events included. |
| Secrets / configuration protection | MVP | Connection strings, MFA seeds, signing keys (when added) protected at rest. |
| Backup encryption | Future advanced | Per user direction. |
| **Audit-log external write-once mirror** | Near-term | Round 4 addition. Optional paid add-on: stream the FR-028 audit chain to a write-once external store (file-system WORM, S3 Object Lock, etc.). Compounds the tamper-evidence story for banks/lenders/large customers. |

### 16. Localization / Usability

| Capability | Maturity | Notes |
| --- | --- | --- |
| Arabic + English UI | MVP | Full bilingual. |
| Arabic RTL support | MVP | UI + invoice rendering. |
| English LTR support | MVP | |
| Arabic invoice totals in words | MVP | Edge case "Bilingual content". |
| Egyptian tax terminology | MVP | Glossary embedded in UI. |
| Printable invoice templates | MVP | FR-033. |
| Company logo | MVP | FR-005. |
| Branch-aware numbering | Future advanced | Belongs with Group 17 multi-branch. |

### 17. Multi-Branch / Multi-Company

The entire group is **Future advanced**. The MVP is single-company, single-installation per the Assumptions.

| Capability | Maturity | Notes |
| --- | --- | --- |
| Branches | Future advanced | |
| Branch-specific document series | Future advanced | Affects FR-011 numbering when delivered. |
| Branch-level reports | Future advanced | |
| Multi-company | Future advanced | Per user direction. |
| Consolidated reports | Future advanced | Per user direction. |

## Differentiators / Egypt-First Product Strategy

This section names the strategic capabilities that position the product as the leading Egyptian tax-accounting product rather than feature-parity with incumbents (Onyx, Smacc, Al-Ameen, Almisry, Odoo-localized, ERPNext-localized). It is normative for product strategy and roadmap planning. Implementation specifics are scheduled per each item's maturity bucket and elaborated in plan.md / tasks.md when the relevant phase is reached.

### Compliance surface architecture (cross-cutting)

The spec introduces multiple compliance-oriented surfaces (Tax Risk Score per document, Active ETA Compliance Dashboard FR-043, WHT Lifecycle Dashboard FR-047, Monthly Tax Closing Cockpit, Owner Mode, Accountant Review Mode). To prevent dashboard sprawl, **these are treated as one logical compliance surface with role-filtered and context-filtered lenses**, not as six independent screens. Implementation MUST share the underlying Compliance Risk Item entity, the underlying period-scoped data, and the underlying drill-down paths; differences between the lenses are presentation, role filtering, and aggregation level.

### Differentiator 1 — Tax Risk Score per document *(MVP)*

Every document (sales invoice, purchase invoice, expense, payment voucher, fixed-asset entry) MUST be evaluated against a configurable rule set producing a per-document Tax Risk Score and a list of risk findings. The MVP rule set MUST detect at minimum: missing customer / supplier TIN where the tax profile requires one (FR-040 / FR-041); missing required attachment on a deductible line (FR-016); invalid VAT category vs the customer/supplier tax profile (FR-040 / FR-041 / FR-019); missing ETA document-type code (FR-034); duplicate supplier-invoice fingerprint (supplier × supplier invoice number × date × amount); WHT category likely required but not configured on the line (FR-045 hint); attempted backdate into a closed period (FR-037); and ETA submission failure or stale-pending state (FR-035 / FR-043). Each finding MUST present a clear human-readable reason in the user's register (Arabic / English) and a one-click "How to fix" hint pointing to the specific field or workflow. The score and findings MUST be visible in-context on every document edit / view screen, not only on the cockpit. The score and findings drive the badging and prioritization in differentiators #2, #3, and #4.

### Differentiator 2 — Monthly Tax Closing Cockpit *(MVP Phase 3)*

A single landing surface used during the month-close ritual. MUST aggregate, for the selected period: (a) VAT readiness score (% of VAT-relevant documents with no Risk findings of severity ≥ "must fix before filing"); (b) missing documents list (deductible lines without attachments, posted invoices without ETA submission, supplier invoices without supplier-side WHT certificate where applicable); (c) failed ETA submissions (per FR-035); (d) unapproved deductible expenses (per FR-026 — drafts that need to post before filing); (e) non-recoverable VAT summary (per FR-020); (f) Form 10 / VAT support pack readiness checklist (each input the support pack needs, with current status); (g) Period Lock checklist with prerequisite items that MUST clear before FR-037 period close is permitted. The cockpit MUST link directly to the affected documents and to the Risk Score detail per document. The cockpit IS the underlying compliance surface; the dashboards in differentiators #3 and #4 are role-filtered lenses on it.

### Differentiator 3 — Accountant Review Mode *(MVP Phase 2 add-on)*

A role-filtered lens of the cockpit (Differentiator 2) presented as the home page for Accountant-Firm Users (US8 / FR-049). Adds: a queue of risky documents the accountant must review, deductible-expense approvals awaiting accountant sign-off, VAT pack and WHT pack readiness specific to the upcoming filing, taxable-income pack progress, threaded comments per document, and a Missing Document Request button (Differentiator 8) per row. Folded into the existing US8 firm portal — NOT a separate concept — to avoid dashboard sprawl. Permission-checked per FR-003 so that Accountant-Firm User actions are scoped to the client they are currently in.

### Differentiator 4 — Owner Mode *(Near-term post-MVP)*

A simplified Arabic-first dashboard for the company owner (typically holding an Administrator role but not an accounting one). MUST surface: estimated tax exposure for the period (linked to the Form 10 readiness in #2 and the WHT exposure in FR-047); current legal-deduction opportunities surfaced by the rule engine (e.g., "you have 4 deductible expenses unposted that would reduce taxable income by X EGP if posted before period end"); customer receivables summary (depends on FR-053 minimal allocation in MVP, full aging in Near-term); supplier payables summary; missing paperwork count with one-tap escalation to Differentiator 8; pending accountant actions (counts of items awaiting the accountant per FR-050 review lock). Owner Mode is the marketing weapon — its primary purpose is to make the product's ROI legible to the buyer, not to drive accountant workflow. Maturity is Near-term because several of its tiles depend on Release 2 features (full receivables / payables aging).

### Differentiator 5 — ETA Reconciliation *(MVP outbound; Near-term inbound)*

**Outbound (MVP Phase 1)**: For every posted sales invoice and credit note, the system MUST track and reconcile the document's ETA submission lifecycle (per FR-035 + FR-043) and surface any local-vs-ETA discrepancies on the cockpit and the per-document Risk Score. The MVP MUST detect: never-submitted documents past their submission window; submitted documents with no ETA acknowledgment after a configurable timeout; rejected documents with their ETA error code mapped to a human-readable cause.

**Inbound (Near-term post-MVP)**: Import received supplier eInvoices (eInvoices issued *to* the company's TIN) from the ETA inbox; auto-match each inbound eInvoice against the company's posted supplier-invoice records; flag every inbound eInvoice for which no matching local purchase record exists (a "missing local purchase record" finding routed through Differentiator 8). The inbound flow is gated on live ETA integration which itself is Future advanced; the Near-term release ships the matching engine and the flagging UI on top of mock inbound data so the workflow is demonstrable before live ETA lands.

### Differentiator 6 — Evidence Vault *(MVP — Document 360 view)*

For every deductible expense or tax-impacting document (sales invoice, purchase invoice, expense, fixed asset, credit note, payment voucher with WHT), the system MUST surface a single unified "Document 360" view containing: every attachment (FR-032), the relevant customer / supplier tax profile snapshot (FR-040 / FR-041), the deductibility reason and category (FR-015), the full approval / posting / amendment audit trail extracted from the FR-028 hash chain, the ETA submission status and history (FR-035), the audit integrity status for the document (FR-028 chain segment validity at view time), and the Tax Risk Score history (Differentiator 1). The data already exists across other requirements; this differentiator delivers the **unified per-document view** that pulls them together. It is the single-document analogue of the period-scoped Tax-Inspection Bundle (US9 / FR-048).

### Differentiator 7 — Smart Migration Assistant *(Near-term post-MVP)*

Upgrades Roadmap Group 13 (Migration / Import / Export) with row-level validation, partial-success-forbidden semantics (per the Roadmap), and a stepped go-live checklist UI. Imports MUST be supported for trial balance, opening customer balances, opening supplier balances, unpaid invoices (with original document number, date, customer/supplier, balance), items, suppliers, customers, chart of accounts, and Excel/CSV templates per import type. The assistant MUST validate each row against the active configuration (TIN format, VAT category existence, account existence, etc.), present a per-row error report, and refuse to commit any rows until all blocking errors are resolved or explicitly waived. The go-live checklist MUST step the operator through: configure company → seed VAT / WHT / chart of accounts → import customers → import suppliers → import items → import opening balances → import unpaid AR/AP → run reconciliation → declare go-live.

### Differentiator 8 — Missing Document Collection Flow *(Near-term post-MVP)*

When the Risk Score (Differentiator 1) flags a missing attachment, missing certificate, or missing source document on any line, the accountant or bookkeeper MUST be able to request the document from a designated staff member, supplier, or the owner via a **secure single-use upload link** delivered by email or copy-link (with optional WhatsApp delivery once Roadmap Group 10's WhatsApp receipt capture lands). The upload link MUST scope the recipient to uploading only the document(s) requested, MUST enforce file-type and size limits per FR-032, and MUST attach uploaded files directly to the originating expense / purchase invoice / fixed asset record without further user action. Every step (request issued, link clicked, file uploaded, file attached) MUST appear in the FR-028 audit log with both the requester's identity and the responder's email / phone.

### Differentiator 9 — Egypt Tax Copilot *(Future advanced — strict guard rails)*

A rule-based assistant that explains the system's tax decisions (VAT category selection, deductibility classification, WHT applicability, ETA validation results, missing-compliance-step diagnoses) in Arabic and English. **Hard guard rails — non-negotiable:**

- The Copilot MUST be a rule-based engine over the system's own configuration (VAT categories per FR-019, WHT categories per FR-045, deductible expense categories per FR-015, customer/supplier tax profiles per FR-040 / FR-041, period state per FR-037, etc.). It MUST NOT be a general LLM that gives Egyptian tax advice from training data.
- Every Copilot explanation MUST cite the specific internal rule, configuration entry, or KB entry it drew from. Citations MUST be machine-checkable; an Auditor MUST be able to re-derive the same explanation from the cited inputs at any time.
- The Copilot MUST NOT silently change accounting data, configuration, or any document. It MAY *suggest* a change with a one-click apply that records the apply as an explicit user action in the audit log, attributed to the user (not to the Copilot).
- The Copilot MUST refuse to answer Egyptian-tax-law questions that fall outside the cited rule set; it MUST NOT improvise or hallucinate. A configurable disclaimer is shown when refusing.
- The Copilot's input/output is itself audit-logged.

The Copilot is recorded as Future advanced because it adds non-trivial editorial-ops cost (rule maintenance as Egyptian law changes) and a non-trivial liability surface; both deserve their own scoping round before delivery.

### Differentiator maturity summary

| # | Capability | Maturity | Anchored on |
| --- | --- | --- | --- |
| 1 | Tax Risk Score per document | **MVP** | FR-040, FR-041, FR-043 + new rule engine |
| 2 | Monthly Tax Closing Cockpit | **MVP (Phase 3)** | FR-021, FR-023, FR-037, FR-043, FR-047 |
| 3 | Accountant Review Mode | **MVP (Phase 2 add-on)** | US8 / FR-049 home page (folded in, not separate) |
| 4 | Owner Mode | Near-term | Depends on full receivables / payables aging |
| 5 | ETA Reconciliation (outbound) | **MVP (Phase 1)** | FR-035, FR-043 |
| 5 | ETA Reconciliation (inbound supplier eInvoice import) | Near-term | Mock inbound first, live ETA inbound is Future |
| 6 | Evidence Vault (Document 360) | **MVP** | FR-016, FR-028, FR-032, FR-040, FR-041, FR-044 — unified view |
| 7 | Smart Migration Assistant | Near-term | Upgrades Roadmap Group 13 |
| 8 | Missing Document Collection Flow | Near-term | Pairs with Differentiators 1 + 5 (inbound) |
| 9 | Egypt Tax Copilot | Future advanced | Strict rule-engine + citation + no-silent-mutation guards |
