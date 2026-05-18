# Phase 1 Data Model: DaftarX Website + Customer Portal

**Plan**: [plan.md](./plan.md)
**Date**: 2026-05-18

Eight entities total — 1:1 mapping with the spec's Key Entities section. All live in the new `DaftarXPortal` database (SQL Server in prod, SQLite override for dev). No physical join to the on-prem product's `EgyptTax` database per FR-032.

Relationships at a glance:

```
CustomerOrganisation 1───* Subscription 1───* Licence
        │                    │
        │                    1───* Invoice
        │
        *────* TeamMember (via OrganisationMembership; many-to-many for accounting firms)
        │
        1───* SupportTicket
        │
        1───* AuditLogEntry

SalesLead (standalone, may convert into a CustomerOrganisation on signup)
```

---

## 1. CustomerOrganisation

**Purpose**: Top-level account that owns subscriptions, licences, invoices, tickets, and team-member memberships. Created on signup; one per real business customer.

**Storage**: `customer_organisations` table.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK. |
| `LegalNameAr` | `nvarchar(256)` | yes | Arabic legal name as it appears on Egyptian tax invoices. |
| `LegalNameEn` | `nvarchar(256)` | no | English variant for the invoice's en-US fallback. |
| `TaxRegistrationNumber` | `nvarchar(32)` | no | The customer's TRN; required before issuing the first paid invoice (per FR-016 Egyptian tax-line rules). |
| `BillingEmail` | `nvarchar(256)` | yes | Where invoices + payment receipts go. Distinct from any TeamMember email — survives team-member churn. |
| `BillingPhone` | `nvarchar(32)` | no | E.164 format; used for WhatsApp + Fawry SMS notifications. |
| `BillingAddressJson` | `nvarchar(max)` | no | JSON blob: line1, line2, city, governorate, postcode. Free-form to accommodate Egyptian address variations. |
| `CountryCode` | `char(2)` | yes | `"EG"` for v1 (spec assumption; multi-country defer). |
| `CreatedAtUtc` | `datetime2` | yes | First-touch timestamp. |
| `SoftDeletedAtUtc` | `datetime2` | no | Populated when the customer requests account deletion (FR-024); the nightly purge job hard-deletes 30 days later. |

**Validation**: `LegalNameAr` non-empty; `BillingEmail` is RFC 5322 valid; `CountryCode` ISO-3166 alpha-2; soft-delete is set-once.

**Lifecycle**: Created on signup → soft-deleted on user request → hard-deleted 30 days later (audit-log entries retained).

---

## 2. TeamMember

**Purpose**: A user authenticated against the portal. Belongs to one or more CustomerOrganisations via `OrganisationMembership`. Cross-organisation membership exists for accounting firms supporting multiple client organisations.

**Storage**: `team_members` table + a join table `organisation_memberships`. AspNetCore.Identity manages the auth columns.

**Fields** (TeamMember):

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK; matches AspNetCore.Identity's `IdentityUser.Id`. |
| `Email` | `nvarchar(256)` | yes | Unique across the portal (NOT scoped per organisation). |
| `EmailConfirmedAtUtc` | `datetime2` | no | Set when the confirmation link is clicked. Until then, paid actions are blocked (FR-010). |
| `PasswordHash` | `nvarchar(512)` | yes | PBKDF2 via AspNetCore.Identity. |
| `MfaEnabled` | `bit` | yes | TOTP enrollment flag per FR-011. |
| `MfaSharedSecret` | `nvarchar(64)` | no | Base32-encoded TOTP secret; encrypted at rest via DataProtection. |
| `DisplayName` | `nvarchar(128)` | no | "Ahmed Hassan"; surfaces in support ticket threads + audit log. |
| `LocalePreference` | `nvarchar(8)` | yes | `"ar-EG"` or `"en-US"`; defaults to `ar-EG`. |
| `CreatedAtUtc` | `datetime2` | yes | |
| `LastLoginAtUtc` | `datetime2` | no | |
| `SoftDeletedAtUtc` | `datetime2` | no | Same soft-delete pattern as CustomerOrganisation. |

**Fields** (OrganisationMembership join):

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK. |
| `OrganisationId` | `uniqueidentifier` | yes | FK → CustomerOrganisation. |
| `TeamMemberId` | `uniqueidentifier` | yes | FK → TeamMember. |
| `Role` | `nvarchar(32)` | yes | One of `Owner`, `BillingAdmin`, `SupportAdmin`, `ReadOnly`. |
| `JoinedAtUtc` | `datetime2` | yes | When the invitation was accepted (NOT when it was sent). |
| `RevokedAtUtc` | `datetime2` | no | Set by FR-022 member-removal flow. |
| `InvitedByTeamMemberId` | `uniqueidentifier` | no | The Owner who sent the invitation (null for the founding member who self-signed-up). |

**Indexes**: `IX_team_members_email` UNIQUE; `IX_organisation_memberships_org_member` UNIQUE filtered on `RevokedAtUtc IS NULL`.

**Validation**: Email RFC 5322; Role must be one of the four enum values; an Organisation MUST have at least one active Owner at all times (the RemoveMemberHandler enforces this — FR-022).

**Lifecycle**:

```
[invited] ──invitation link clicked, password set──▶ [active]
[active] ──Owner removes member──▶ [revoked] (sessions terminated within 5 min per FR-022)
[revoked] ──re-invited──▶ [active] (new membership row, old one retained for audit)
```

---

## 3. Subscription

**Purpose**: A plan binding a CustomerOrganisation to a tier (Solo, SMB, Enterprise, Firm), a billing cadence (monthly, annual), a payment method, and a billing state. No trial state — trial is owned client-side by the on-prem product per FR-030.

**Storage**: `subscriptions` table.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK. |
| `OrganisationId` | `uniqueidentifier` | yes | FK → CustomerOrganisation. |
| `Tier` | `nvarchar(16)` | yes | One of `Solo`, `SMB`, `Enterprise`, `Firm`. |
| `BillingCadence` | `nvarchar(16)` | yes | `Monthly` or `Annual`. |
| `PaymentMethodId` | `uniqueidentifier` | no | FK → PaymentMethod (stored separately to support saved-card scenarios; OK to be null for bank-transfer subscriptions which reconcile manually). |
| `Status` | `nvarchar(16)` | yes | `Active`, `PastDue`, `Cancelled`, `Paused`. **`Trial` intentionally absent** per FR-030. |
| `CurrentPeriodStartUtc` | `datetime2` | yes | |
| `CurrentPeriodEndUtc` | `datetime2` | yes | Next renewal date. |
| `PendingTierChangeTo` | `nvarchar(16)` | no | When set, transitions to this tier at next renewal per FR-033 downgrade rule. |
| `CreatedAtUtc` | `datetime2` | yes | |
| `CancelledAtUtc` | `datetime2` | no | Set when customer cancels; Status flips to `Cancelled` at `CurrentPeriodEndUtc`. |

**Lifecycle**:

```
[Active] ──Owner upgrades tier──▶ [Active] (Tier flips immediately, Invoice issued for proration)
[Active] ──Owner downgrades tier──▶ [Active + PendingTierChangeTo set] ──renewal job──▶ [Active at new tier]
[Active] ──renewal payment fails──▶ [PastDue] ──N retries fail──▶ [Cancelled]
[Active] ──Owner cancels──▶ [Active until CurrentPeriodEndUtc] ──renewal job──▶ [Cancelled]
[Active] ──Owner pauses──▶ [Paused] (licences still valid until period end; no renewal charge)
[Cancelled] ──Owner resubscribes──▶ [Active] (new Subscription row, old one retained)
```

**Validation**: Tier must be one of the enum values; `Status == Active` requires `CurrentPeriodEndUtc > now`.

---

## 4. Licence

**Purpose**: A signed token bound to a hardware id, derived from a Subscription. Multiple licences per Subscription for the LAN-client + multi-device cases.

**Storage**: `licences` table.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK. |
| `SubscriptionId` | `uniqueidentifier` | yes | FK → Subscription. |
| `Hwid` | `nvarchar(64)` | yes | Customer-provided hardware id (XXXX-XXXX-XXXX-XXXX format). |
| `Edition` | `nvarchar(16)` | yes | Snapshot of the Subscription's Tier at the time the token was signed (so a tier upgrade reissues the token with a new Edition value). |
| `SignedTokenBase64` | `nvarchar(max)` | yes | The Ed25519-signed JSON envelope per the existing on-prem product format (lowercase `payload` + `signature` keys). |
| `IssuedAtUtc` | `datetime2` | yes | Signing timestamp. |
| `ExpiresAtUtc` | `datetime2` | yes | Matches the Subscription's `CurrentPeriodEndUtc` at issuance time. |
| `RetiredAtUtc` | `datetime2` | no | Set when the licence is transferred to a different hardware id or the Subscription is cancelled. |
| `RetiredReason` | `nvarchar(32)` | no | `Transferred`, `SubscriptionCancelled`, `Refunded`, `TierUpgraded` (the old-tier token retires when a new one is issued at the new tier). |

**Indexes**: `IX_licences_subscription` on `SubscriptionId`; `IX_licences_hwid` on `Hwid` (used by FR-014 cross-customer collision check).

**Validation**: Hwid format matches `^[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}$`; ExpiresAtUtc > IssuedAtUtc; the FR-014 collision check rejects a Transfer attempt if the target Hwid is currently bound to a different Subscription's active Licence.

**Lifecycle**:

```
[active] ──Transfer──▶ [retired Transferred] + new [active] row issued
[active] ──Tier upgrade──▶ [retired TierUpgraded] + new [active] row at new Edition
[active] ──Subscription cancelled──▶ [retired SubscriptionCancelled]
[active] ──Refund within 7-day window──▶ [retired Refunded]
```

---

## 5. Invoice

**Purpose**: Cleared payment record bound to a Subscription, with a sequential per-organisation invoice number, the payment method, the amount in Egyptian pounds, and a downloadable PDF.

**Storage**: `invoices` table.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK. |
| `OrganisationId` | `uniqueidentifier` | yes | FK → CustomerOrganisation. |
| `SubscriptionId` | `uniqueidentifier` | yes | FK → Subscription. |
| `InvoiceNumber` | `nvarchar(32)` | yes | Sequential per organisation, format `INV-YYYY-NNNNN`. Required for Egyptian tax compliance per FR-016. |
| `Kind` | `nvarchar(16)` | yes | `FirstPeriod`, `Renewal`, `TierUpgrade`, `Addon`. Drives `RefundEligibility` per FR-034. |
| `AmountEgp` | `decimal(18,2)` | yes | Always EGP per spec assumption. |
| `TaxEgp` | `decimal(18,2)` | yes | Egyptian VAT line, 14% of subtotal in v1. |
| `PaymentMethod` | `nvarchar(16)` | yes | `Card`, `Fawry`, `VodafoneCash`, `InstaPay`, `BankTransfer`. |
| `PaymobTransactionId` | `nvarchar(64)` | no | Paymob's transaction reference; null for BankTransfer kind. |
| `Status` | `nvarchar(16)` | yes | `Pending`, `Paid`, `Refunded`, `Failed`. |
| `PaidAtUtc` | `datetime2` | no | Set when payment clears; null while Pending. Starts the 7-day refund window. |
| `RefundedAtUtc` | `datetime2` | no | Set on successful refund per FR-034. |
| `PdfBlobReference` | `nvarchar(256)` | no | Azure Blob URI; null until PDF is generated (async after Paid status). |

**Computed (not stored)**: `RefundEligibility` enum — `Eligible`, `NotEligible_OutsideWindow`, `NotEligible_Renewal`, `NotEligible_TierUpgrade`. Computed at read time per research §12.

**Indexes**: `IX_invoices_org_number` UNIQUE on `(OrganisationId, InvoiceNumber)`; `IX_invoices_paymob_tx` UNIQUE filtered on `PaymobTransactionId IS NOT NULL` (idempotency for webhook retries).

**Validation**: AmountEgp > 0; InvoiceNumber matches the sequence pattern; Kind transitions are immutable (an Invoice's Kind never changes after creation).

---

## 6. SupportTicket

**Purpose**: Customer-submitted issue with category, priority, description, attachments, status, assigned vendor staff, and a reply thread.

**Storage**: `support_tickets` table + child `support_ticket_replies` table + child `support_ticket_attachments` table.

**Fields** (SupportTicket):

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK. |
| `OrganisationId` | `uniqueidentifier` | yes | FK → CustomerOrganisation. |
| `OpenedByTeamMemberId` | `uniqueidentifier` | yes | FK → TeamMember. |
| `Category` | `nvarchar(32)` | yes | `Billing`, `Bug`, `FeatureRequest`, `AccountingQuestion`, `Urgent`. |
| `Priority` | `nvarchar(16)` | yes | `Low`, `Normal`, `High` (High disabled for Solo per FR-018). |
| `Subject` | `nvarchar(256)` | yes | One-line summary. |
| `Status` | `nvarchar(16)` | yes | `Open`, `InProgress`, `Resolved`, `Closed`. |
| `AssignedToStaffId` | `uniqueidentifier` | no | FK → VendorStaff (separate table outside this model; vendor-side identity). |
| `OpenedAtUtc` | `datetime2` | yes | |
| `FirstReplyAtUtc` | `datetime2` | no | Used to measure SC-005 (95% acknowledged within 24 hrs). |
| `ResolvedAtUtc` | `datetime2` | no | |
| `SlaTargetHours` | `int` | yes | 24 for Solo/SMB, 4 for Enterprise/Firm per FR-019. |

**Fields** (SupportTicketReply):

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK. |
| `TicketId` | `uniqueidentifier` | yes | FK → SupportTicket. |
| `AuthorKind` | `nvarchar(16)` | yes | `Customer` or `VendorStaff`. |
| `AuthorTeamMemberId` | `uniqueidentifier` | no | Set when AuthorKind=Customer. |
| `AuthorStaffId` | `uniqueidentifier` | no | Set when AuthorKind=VendorStaff. |
| `BodyMarkdown` | `nvarchar(max)` | yes | Plain markdown; rendered safely server-side. |
| `PostedAtUtc` | `datetime2` | yes | |

**Fields** (SupportTicketAttachment):

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK. |
| `TicketId` | `uniqueidentifier` | yes | FK. |
| `ReplyId` | `uniqueidentifier` | no | Null when attached to the original ticket; populated for reply attachments. |
| `FileName` | `nvarchar(256)` | yes | |
| `MimeType` | `nvarchar(64)` | yes | Whitelisted: `image/jpeg`, `image/png`, `image/webp`, `application/pdf`, `text/plain`. |
| `SizeBytes` | `bigint` | yes | Server validates ≤ 5,242,880 (5 MB). |
| `BlobReference` | `nvarchar(256)` | yes | Azure Blob URI. |

**Indexes**: `IX_support_tickets_org_status` on `(OrganisationId, Status)`; `IX_support_tickets_assigned` on `AssignedToStaffId`.

**Validation**: Subject non-empty; max 3 attachments per ticket-or-reply; total attachment size cap of 15 MB per ticket.

---

## 7. SalesLead

**Purpose**: A pre-signup record created by the marketing site's contact form, with prospect contact details + the page they came from + the tier they expressed interest in.

**Storage**: `sales_leads` table.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `uniqueidentifier` | yes | PK. |
| `ContactName` | `nvarchar(128)` | yes | |
| `ContactEmail` | `nvarchar(256)` | yes | |
| `ContactPhone` | `nvarchar(32)` | no | E.164 format if provided. |
| `OrganisationName` | `nvarchar(256)` | no | Free-form, not validated. |
| `Message` | `nvarchar(2000)` | no | Free text from the contact form. |
| `LandingPageUrl` | `nvarchar(512)` | no | The page they were on when they clicked Contact. |
| `InterestedTier` | `nvarchar(16)` | no | One of the tier names if the form captured it. |
| `Locale` | `nvarchar(8)` | yes | The locale they submitted from. |
| `CreatedAtUtc` | `datetime2` | yes | |
| `LastContactedAtUtc` | `datetime2` | no | Set by vendor sales team after first outreach. |
| `ConvertedToOrganisationId` | `uniqueidentifier` | no | FK → CustomerOrganisation if the lead later signs up; used for attribution. |

**Indexes**: `IX_sales_leads_email` on `ContactEmail`.

**Lifecycle**: `[new] → [contacted] → [converted | lost]`. Conversion linkage via `ConvertedToOrganisationId`.

---

## 8. AuditLogEntry

**Purpose**: Immutable record of every customer-visible state change. Owners can view a paginated list per FR-023.

**Storage**: `audit_log_entries` table.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `bigint identity` | yes | PK; sequential for chronological order. |
| `OrganisationId` | `uniqueidentifier` | yes | FK → CustomerOrganisation (every audit entry is scoped to an org). |
| `ActorKind` | `nvarchar(16)` | yes | `TeamMember`, `VendorStaff`, `System` (webhook-triggered events). |
| `ActorTeamMemberId` | `uniqueidentifier` | no | Set when ActorKind=TeamMember. |
| `ActorStaffId` | `uniqueidentifier` | no | Set when ActorKind=VendorStaff. |
| `Verb` | `nvarchar(64)` | yes | Dot-separated: `licence.transferred`, `member.invited`, `subscription.upgraded`, `payment.cleared`, `ticket.statusChanged`, `member.removed`, `account.softDeleted`, etc. |
| `SubjectKind` | `nvarchar(32)` | no | `Licence`, `Subscription`, `Invoice`, `Ticket`, `TeamMember`, etc. |
| `SubjectId` | `uniqueidentifier` | no | The id of the affected entity. |
| `PayloadJson` | `nvarchar(max)` | no | Verb-specific structured payload. NEVER contains FCM tokens, signed-token contents, PII beyond what's already exposed by other surfaces (e.g. licence-transfer records both Hwid values but NOT the signed token base64). |
| `OccurredAtUtc` | `datetime2` | yes | |
| `OriginatingIp` | `nvarchar(64)` | no | For TeamMember-actor entries (auth events per FR-028 + audit per FR-023). |

**Indexes**: `IX_audit_log_org_time` on `(OrganisationId, OccurredAtUtc DESC)`; `IX_audit_log_subject` on `(SubjectKind, SubjectId)`.

**Validation**: Immutable — no update or delete allowed. Retention is the regulatory floor (longer than the 30-day account-deletion window per FR-024).

---

## Cross-entity rules

- **Organisation scoping (FR-021)**: Every query that returns or modifies data for any entity except SalesLead MUST filter on `OrganisationId = currentTeamMember.MembershipOrganisationIds`. A repository base class enforces this; per-entity handlers MUST NOT bypass it.
- **Soft-delete cascade**: When `CustomerOrganisation.SoftDeletedAtUtc` is set, all child entities (Subscription, Licence, Invoice, SupportTicket, TeamMember-Membership) are flagged for purge at the same +30-day boundary. AuditLogEntry rows are NEVER purged (regulatory).
- **Identity isolation (FR-032)**: TeamMember has NO foreign key to the on-prem product's user table. The two identity stores live in different databases; no SQL Server linked-server or replication links them.
- **Trial absence (FR-030)**: Subscription has no `Trial` status. Period. The portal must never store, infer, or report on trial state.
