# Phase 1 Data Model: DaftarX Website + Customer Portal

**Plan**: [plan.md](./plan.md)
**Date**: 2026-05-18 (stack-switched from .NET to Laravel 11 — replaces the .NET EF Core data-model from commit 6bf395e)

Thirteen Eloquent models — the 8 entities from `spec.md` Key Entities plus 5 supporting types (OrganisationMembership join, Invitation, SupportTicketReply, SupportTicketAttachment, DownloadArtifactVersion). All live in the new `daftarx_portal` MySQL database (SQLite override at `database/database.sqlite` for dev). No physical FK to the on-prem product's `EgyptTax` database per FR-032.

Relationships at a glance:

```
CustomerOrganisation 1───* Subscription 1───* Licence
        │                    │
        │                    1───* Invoice
        │
        *────* TeamMember (via OrganisationMembership; many-to-many for accounting firms)
        │
        1───* SupportTicket 1───* SupportTicketReply
        │                  1───* SupportTicketAttachment
        │
        1───* Invitation
        │
        1───* AuditLogEntry

SalesLead (standalone, may convert into a CustomerOrganisation on signup)
DownloadArtifactVersion (standalone — vendor-managed, per-artefact)
```

**Naming conventions** (Laravel defaults):

- Table names are plural snake_case (`customer_organisations`, `audit_log_entries`)
- Column names are snake_case (`legal_name_ar`, `created_at`, `soft_deleted_at`)
- Primary keys are `id` (`bigint unsigned` auto-increment by default, but we use `char(36)` UUIDs to avoid leaking customer-count info via incrementing IDs)
- Timestamps: Eloquent's automatic `created_at` + `updated_at` (Laravel convention). Soft-delete uses `deleted_at` (Laravel's standard for `SoftDeletes` trait); we use **`soft_deleted_at`** explicitly because some entities have BOTH a Laravel soft-delete (transient) AND a domain "soft-delete" (the FR-024 30-day deletion window) — keeping these distinct prevents confusion.

---

## 1. CustomerOrganisation

**Purpose**: Top-level account that owns subscriptions, licences, invoices, tickets, and team-member memberships. Created on signup; one per real business customer.

**Storage**: `customer_organisations` table. Eloquent model at `portal/app/Models/CustomerOrganisation.php`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `legal_name_ar` | `varchar(256)` | yes | Arabic legal name as it appears on Egyptian tax invoices. |
| `legal_name_en` | `varchar(256)` | no | English variant for the invoice's en-US fallback. |
| `tax_registration_number` | `varchar(32)` | no | The customer's TRN; required before issuing the first paid invoice (per FR-016 Egyptian tax-line rules). |
| `billing_email` | `varchar(256)` | yes | Where invoices + payment receipts go. Distinct from any TeamMember email — survives team-member churn. |
| `billing_phone` | `varchar(32)` | no | E.164 format; used for WhatsApp + Fawry SMS notifications. |
| `billing_address` | `json` | no | JSON column: `{line1, line2, city, governorate, postcode}`. Free-form to accommodate Egyptian address variations. |
| `country_code` | `char(2)` | yes | `"EG"` for v1. |
| `requires_mfa_for_owners` | `boolean` | yes | Default `false`. T155 / FR-011 org-policy MFA enforcement. |
| `created_at` | `timestamp` | yes | First-touch timestamp. |
| `updated_at` | `timestamp` | yes | |
| `soft_deleted_at` | `timestamp` | no | Populated when the customer requests account deletion (FR-024); the nightly purge job hard-deletes 30 days later. |

**Eloquent**:

- `protected $casts = ['billing_address' => 'array', 'requires_mfa_for_owners' => 'boolean', 'soft_deleted_at' => 'datetime'];`
- Relationships: `hasMany(Subscription::class)`, `hasMany(Invoice::class)`, `hasMany(SupportTicket::class)`, `hasMany(AuditLogEntry::class)`, `belongsToMany(TeamMember::class)->using(OrganisationMembership::class)`.

**Validation**: `legal_name_ar` non-empty; `billing_email` is RFC 5322 valid; `country_code` ISO-3166 alpha-2.

**Lifecycle**: Created on signup → `soft_deleted_at` set on user request → hard-deleted by `PurgeSoftDeletedAccountsJob` 30 days later (audit-log rows retained indefinitely per data-model.md §11).

---

## 2. TeamMember (extends Laravel's User)

**Purpose**: A user authenticated against the portal. Belongs to one or more CustomerOrganisations via `OrganisationMembership`. Cross-organisation membership exists for accounting firms supporting multiple client organisations. FR-032: completely separate from the on-prem product's user store.

**Storage**: `team_members` table (Laravel Breeze defaults to `users` but we rename for clarity). Model at `portal/app/Models/TeamMember.php` extending `Illuminate\Foundation\Auth\User`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK; matches Breeze's `users.id` when overridden to char(36). |
| `email` | `varchar(256)` | yes | Unique across the portal (NOT scoped per organisation). |
| `email_verified_at` | `timestamp` | no | Set when the confirmation link is clicked. Until then, paid actions are blocked (FR-010). |
| `password` | `varchar(255)` | yes | bcrypt via Laravel's `Hash` facade. |
| `mfa_secret` | `text` | no | Base32-encoded TOTP secret; encrypted at rest via Laravel's `encrypted` cast. |
| `mfa_enabled_at` | `timestamp` | no | TOTP enrollment timestamp per FR-011. NULL = not enrolled. |
| `display_name` | `varchar(128)` | no | "Ahmed Hassan"; surfaces in support ticket threads + audit log. |
| `locale_preference` | `varchar(8)` | yes | `"ar-EG"` or `"en-US"`; defaults to `ar-EG`. |
| `remember_token` | `varchar(100)` | no | Laravel's standard cookie remember token. |
| `created_at` | `timestamp` | yes | |
| `updated_at` | `timestamp` | yes | |
| `last_login_at` | `timestamp` | no | |
| `soft_deleted_at` | `timestamp` | no | Same soft-delete pattern as CustomerOrganisation. |

**Eloquent**:

- `protected $casts = ['email_verified_at' => 'datetime', 'mfa_enabled_at' => 'datetime', 'mfa_secret' => 'encrypted', 'last_login_at' => 'datetime', 'soft_deleted_at' => 'datetime'];`
- Uses `HasUuids` trait so Eloquent generates UUIDs on creation.
- Relationships: `belongsToMany(CustomerOrganisation::class)->using(OrganisationMembership::class)`.

**Validation**: Email RFC 5322; password ≥ 12 chars (Laravel rule `Password::min(12)->mixedCase()->numbers()`).

**Lifecycle**: signup → email-confirm (24h window) → soft-delete on user request → hard-delete 30 days later.

---

## 3. OrganisationMembership (join table)

**Purpose**: The many-to-many link between `team_members` and `customer_organisations`, carrying a role per row.

**Storage**: `organisation_memberships` table. Eloquent uses the `Pivot` model pattern via `belongsToMany(...)->using(OrganisationMembership::class)`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `customer_organisation_id` | `char(36)` | yes | FK → customer_organisations.id. |
| `team_member_id` | `char(36)` | yes | FK → team_members.id. |
| `role` | `enum('Owner','BillingAdmin','SupportAdmin','ReadOnly')` | yes | |
| `invited_at` | `timestamp` | yes | |
| `accepted_at` | `timestamp` | no | NULL until invitee clicks the link + sets password. |
| `revoked_at` | `timestamp` | no | FR-022 — set when an Owner removes the member. Session-kill cron picks this up within 5 min. |
| `security_stamp_version` | `int` | yes | Default 0; bumped on revoke so all existing sessions invalidate (the session middleware rejects any session with `security_stamp_version` < current row). |
| `created_at` | `timestamp` | yes | |
| `updated_at` | `timestamp` | yes | |

**Indexes**:
- `(customer_organisation_id, team_member_id)` UNIQUE — but partial: only rows WHERE `revoked_at IS NULL`. Implemented as a generated column + unique index since MySQL 8 doesn't support partial indexes natively. (SQLite override uses native partial index.)
- `(team_member_id, revoked_at)` for "list my orgs" queries.

**Lifecycle**: invited (no `accepted_at`) → accepted (`accepted_at` set) → revoked (`revoked_at` set; row kept for audit history).

---

## 4. Subscription

**Purpose**: A plan binding a CustomerOrganisation to a tier (Solo, SMB, Enterprise, Firm), a billing cadence (monthly, annual), a payment method, and a billing state. **NO trial state** — trial is owned client-side by the on-prem product per FR-030.

**Storage**: `subscriptions` table. Model at `portal/app/Models/Subscription.php`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `customer_organisation_id` | `char(36)` | yes | FK. |
| `tier` | `enum('Solo','SMB','Enterprise','Firm')` | yes | |
| `billing_cadence` | `enum('Monthly','Annual')` | yes | |
| `payment_method_id` | `char(36)` | no | FK → `payment_methods.id` (saved-card scenarios); NULL for bank-transfer subscriptions. |
| `status` | `enum('Active','PastDue','Cancelled','Paused')` | yes | **`Trial` intentionally absent** per FR-030. |
| `current_period_start_at` | `timestamp` | yes | |
| `current_period_end_at` | `timestamp` | yes | Next renewal date. |
| `pending_tier_change_to` | `enum('Solo','SMB','Enterprise','Firm')` | no | When set, the renewal job flips `tier` at `current_period_end_at` per FR-033 downgrade rule. |
| `cancelled_at` | `timestamp` | no | Set when the customer cancels; status flips to `Cancelled` at `current_period_end_at`. |
| `created_at` | `timestamp` | yes | |
| `updated_at` | `timestamp` | yes | |

**Indexes**: `(customer_organisation_id, status)` for the dashboard query.

**Lifecycle** (state transitions):

```
[Active] ──Owner upgrades tier──▶ [Active] (tier flips immediately, Invoice issued for proration)
[Active] ──Owner downgrades tier──▶ [Active + pending_tier_change_to set] ──renewal job──▶ [Active at new tier]
[Active] ──renewal payment fails──▶ [PastDue] ──N retries fail──▶ [Cancelled]
[Active] ──Owner cancels──▶ [Active until current_period_end_at] ──renewal job──▶ [Cancelled]
[Active] ──Owner pauses──▶ [Paused] (licences still valid until period end; no renewal charge)
[Cancelled] ──Owner resubscribes──▶ new [Active] row (old row retained)
```

**Validation**: `status == 'Active'` requires `current_period_end_at > now()`.

---

## 5. Licence

**Purpose**: A signed token bound to a hardware id, derived from a Subscription. Multiple licences per Subscription for the LAN-client + multi-device cases.

**Storage**: `licences` table. Model at `portal/app/Models/Licence.php`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `subscription_id` | `char(36)` | yes | FK → subscriptions.id. |
| `hwid` | `varchar(64)` | yes | Customer-provided hardware id, `XXXX-XXXX-XXXX-XXXX` format. |
| `edition` | `enum('Solo','SMB','Enterprise','Firm')` | yes | Snapshot of the Subscription's tier at issuance time (a tier upgrade reissues the token at the new edition). |
| `signed_token_base64` | `longtext` | yes | The Ed25519-signed JSON envelope per the on-prem `LicenseEnvelope` wire format (lowercase `payload` + `signature` keys). |
| `issued_at` | `timestamp` | yes | Signing timestamp. |
| `expires_at` | `timestamp` | yes | Matches the Subscription's `current_period_end_at` at issuance time. |
| `retired_at` | `timestamp` | no | Set when the licence is transferred or the Subscription is cancelled. |
| `retired_reason` | `enum('Transferred','SubscriptionCancelled','Refunded','TierUpgraded')` | no | |
| `created_at` | `timestamp` | yes | |
| `updated_at` | `timestamp` | yes | |

**Indexes**:
- `(subscription_id)` for "list my licences".
- `(hwid)` with partial index WHERE `retired_at IS NULL` for the FR-014 cross-customer collision check (same generated-column trick as OrganisationMembership).

**Validation**: `hwid` matches `^[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}$`; `expires_at > issued_at`.

**Lifecycle**:

```
[active]      ──Transfer──▶ [retired Transferred]      + new [active] row issued
[active]      ──Tier upgrade──▶ [retired TierUpgraded] + new [active] row at new edition
[active]      ──Subscription cancelled──▶ [retired SubscriptionCancelled]
[active]      ──Refund within 7-day window──▶ [retired Refunded]
```

---

## 6. Invoice

**Purpose**: Cleared payment record bound to a Subscription, with a sequential per-organisation invoice number, the payment method, the amount in Egyptian pounds, and a downloadable PDF.

**Storage**: `invoices` table. Model at `portal/app/Models/Invoice.php`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `customer_organisation_id` | `char(36)` | yes | FK. |
| `subscription_id` | `char(36)` | yes | FK. |
| `invoice_number` | `varchar(32)` | yes | Per-org sequence, e.g. `INV-2026-00001`. UNIQUE per org. |
| `kind` | `enum('FirstPeriod','Renewal','Upgrade','Refund')` | yes | Drives `refund_eligibility` calc. |
| `payment_method` | `enum('Card','Fawry','InstaPay','VodafoneCash','BankTransfer')` | yes | |
| `amount_egp_minor` | `bigint unsigned` | yes | Stored as piasters (×100 EGP) to avoid float rounding. |
| `vat_egp_minor` | `bigint unsigned` | yes | Egyptian VAT line per FR-016. |
| `paymob_transaction_id` | `varchar(64)` | no | Paymob's idempotency key. UNIQUE for non-NULL values. |
| `status` | `enum('Pending','Paid','Refunded','Failed')` | yes | |
| `pdf_storage_path` | `varchar(512)` | no | Relative path under `storage/app/private/` once rendered. |
| `paid_at` | `timestamp` | no | |
| `refunded_at` | `timestamp` | no | |
| `created_at` | `timestamp` | yes | |
| `updated_at` | `timestamp` | yes | |

**Indexes**:
- `(customer_organisation_id, invoice_number)` UNIQUE.
- `(paymob_transaction_id)` UNIQUE where NOT NULL (filtered) — payment-webhook idempotency.
- `(customer_organisation_id, status, created_at desc)` for the billing list.

**Computed (model accessor — NOT a column)**:
- `refund_eligibility` returns one of `eligible` / `not_eligible_renewal` / `not_eligible_window_expired` / `not_eligible_tier_change` per FR-034. Rule: `kind == FirstPeriod` AND `status == Paid` AND `paid_at >= now() - 7 days` AND no refund exists for this org's first FirstPeriod invoice.

---

## 7. SalesLead

**Purpose**: A pre-signup record created by the contact form (FR-005), with prospect contact details, the page they came from, the tier they expressed interest in, and the date the vendor's sales team last contacted them.

**Storage**: `sales_leads` table. Model at `portal/app/Models/SalesLead.php`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `name` | `varchar(128)` | yes | |
| `email` | `varchar(256)` | yes | |
| `phone` | `varchar(32)` | no | |
| `interested_tier` | `enum('Solo','SMB','Enterprise','Firm')` | no | |
| `referrer_page` | `varchar(256)` | no | The marketing page they came from. |
| `message` | `text` | no | Free-text from the contact form. |
| `last_contacted_at` | `timestamp` | no | Vendor staff updates this. |
| `converted_to_organisation_id` | `char(36)` | no | FK → customer_organisations.id once they sign up. |
| `created_at` | `timestamp` | yes | |
| `updated_at` | `timestamp` | yes | |

---

## 8. SupportTicket + SupportTicketReply + SupportTicketAttachment

**Purpose**: FR-018 customer-submitted support ticket with category, priority, description, attachments, status, assigned vendor staff, and a reply thread visible to both sides.

**Storage**: `support_tickets`, `support_ticket_replies`, `support_ticket_attachments` tables. Models at `portal/app/Models/SupportTicket.php`, etc.

**Ticket fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `customer_organisation_id` | `char(36)` | yes | FK. |
| `opened_by_team_member_id` | `char(36)` | yes | FK → team_members.id. |
| `category` | `enum('Billing','Bug','FeatureRequest','AccountingQuestion','Urgent')` | yes | |
| `priority` | `enum('Low','Normal','High')` | yes | `High` blocked for Solo per FR-018. |
| `subject` | `varchar(256)` | yes | |
| `body` | `text` | yes | |
| `status` | `enum('Open','InProgress','Resolved','Closed')` | yes | |
| `assigned_vendor_staff_id` | `char(36)` | no | Internal vendor staff id (not a TeamMember). |
| `opened_at` | `timestamp` | yes | |
| `first_reply_at` | `timestamp` | no | T156 / SC-005 — set on the first vendor reply. |
| `resolved_at` | `timestamp` | no | |
| `closed_at` | `timestamp` | no | |
| `created_at` | `timestamp` | yes | |
| `updated_at` | `timestamp` | yes | |

**Reply fields**: `id`, `support_ticket_id`, `author_kind` (`Customer`|`VendorStaff`), `author_team_member_id` or `author_vendor_staff_id`, `body`, `created_at`, `updated_at`.

**Attachment fields**: `id`, `support_ticket_id`, `original_filename`, `mime_type`, `size_bytes`, `storage_path` (under `storage/app/private/tickets/`), `created_at`, `updated_at`. Max 3 per ticket × 5 MB each + total 15 MB cap per FR-018, enforced by `CreateTicketService`.

---

## 9. AuditLogEntry

**Purpose**: Immutable record of every customer-visible state change (FR-023). One row per verb. Scoped to `customer_organisation_id` so Owners only see their own org's history. Payload is a free-form JSON blob — MUST NOT contain tokens, password hashes, or PII beyond what is already visible in the portal UI. Retained indefinitely — never purged by the soft-delete job (FR-024).

**Storage**: `audit_log_entries` table. Model at `portal/app/Models/AuditLogEntry.php`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `customer_organisation_id` | `char(36)` | yes | FK. |
| `actor_team_member_id` | `char(36)` | no | NULL = system actor (webhook, cron). |
| `actor_display_name_snapshot` | `varchar(128)` | yes | Snapshot — keeps audit readable after a member is removed. |
| `verb` | `varchar(64)` | yes | Dotted-namespace form: `licence.activated`, `subscription.cancelled`, `member.invited`, etc. |
| `subject_kind` | `varchar(32)` | yes | `Subscription`, `Licence`, `Invitation`, etc. |
| `subject_id` | `varchar(64)` | yes | Usually a UUID; NEVER a token value. |
| `payload_json` | `json` | no | Verb-specific context. NEVER tokens, PII, password hashes. |
| `originating_ip` | `varchar(45)` | yes | IPv6 max length. |
| `occurred_at` | `timestamp` | yes | |

**Indexes**: `(customer_organisation_id, occurred_at desc)`, `(verb)`.

---

## 10. Invitation (US5)

**Purpose**: 7-day single-use invitation token for the multi-user flow (FR-020).

**Storage**: `invitations` table. Model at `portal/app/Models/Invitation.php`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `customer_organisation_id` | `char(36)` | yes | FK. |
| `email` | `varchar(256)` | yes | The invitee email. |
| `role` | `enum('Owner','BillingAdmin','SupportAdmin','ReadOnly')` | yes | |
| `display_name` | `varchar(128)` | no | Hint for the invitee. |
| `locale_preference` | `varchar(8)` | yes | `ar-EG` or `en-US` — drives the invitation email language. |
| `token_hash` | `char(64)` | yes | SHA-256 of the raw token. **NEVER store the raw token.** |
| `invited_by_team_member_id` | `char(36)` | yes | FK. |
| `expires_at` | `timestamp` | yes | Default `now() + 7 days`. |
| `accepted_at` | `timestamp` | no | Single-use: once set, future clicks return 410 Gone. |
| `created_at` | `timestamp` | yes | |
| `updated_at` | `timestamp` | yes | |

**Indexes**: `(token_hash)` UNIQUE; `(customer_organisation_id, email, accepted_at)` for "pending invitations exist?" checks.

---

## 11. DownloadArtifactVersion (T154 / FR-017)

**Purpose**: Tracks all published versions of the downloadable artifacts so Owners can roll back to the 3 most-recent prior versions.

**Storage**: `download_artifact_versions` table. Model at `portal/app/Models/DownloadArtifactVersion.php`.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `char(36)` | yes | UUID PK. |
| `artifact_kind` | `enum('DesktopInstaller','LanClientInstaller','AndroidApk')` | yes | |
| `version_string` | `varchar(32)` | yes | Flat `MAJOR.MINOR.PATCH`. |
| `released_at` | `timestamp` | yes | |
| `blob_uri` | `varchar(512)` | yes | Path under `storage/app/private/downloads/` OR a Cloudflare R2 / Backblaze URL once we migrate large binaries off local disk. |
| `sha256_checksum` | `char(64)` | yes | For integrity verification by the customer. |
| `retired_at` | `timestamp` | no | If a version is recalled (security CVE etc.), set this so the rollback dropdown skips it. |
| `release_notes_ar` | `text` | no | |
| `release_notes_en` | `text` | no | |
| `created_at` | `timestamp` | yes | |
| `updated_at` | `timestamp` | yes | |

**Indexes**: `(artifact_kind, released_at desc)` for the "latest + prior 3" query.

---

## Cross-entity rules

- **Organisation scoping** (FR-021): every query for a customer-visible entity MUST filter by `customer_organisation_id`. Enforced by a global Eloquent scope `BelongsToCurrentOrganisation` applied automatically by the `OrganisationScope` middleware (the middleware sets the active org id on a request-scoped service that the scope reads).
- **Identity isolation** (FR-032): the portal's `team_members` table has no FK, no view, and no network reachability to the on-prem product's user store. The two systems share only the `vendor-keys.json` Ed25519 keypair.
- **Audit-log retention**: never purged. `PurgeSoftDeletedAccountsJob` (T142) explicitly skips `audit_log_entries` rows even when their owning `CustomerOrganisation` is hard-deleted.
- **PII hygiene**: `AuditLogWriter` (T027 — `app/Services/Audit/AuditLogWriter.php`) rejects payloads containing forbidden substrings (`password`, `secret`, `token`, `api_key`, etc.) as a defence-in-depth check.

---

## Migration ordering

EF migrations were one big `00_Initial` in the .NET design. Laravel's convention is one migration per logical entity, ordered by timestamp. Phase 2 foundational migrations are:

1. `2026_05_18_000001_create_customer_organisations_table.php`
2. `2026_05_18_000002_create_team_members_table.php` (overrides Breeze's default `users` migration)
3. `2026_05_18_000003_create_organisation_memberships_table.php`
4. `2026_05_18_000004_create_audit_log_entries_table.php`

Per-user-story migrations land later: Subscriptions + Licences + Invoices + SalesLeads (US2 + US3), SupportTickets (US4), Invitations (US5), DownloadArtifactVersions (Polish).

The Breeze install scaffolds default `password_reset_tokens` + `sessions` tables which we keep as-is.
