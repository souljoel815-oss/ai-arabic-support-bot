# Contract: `POST /api/v1/portal/organisations/{organisationId}/invitations`

**Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) (FR-020, User Story 5)

Owner invites a new TeamMember to share their CustomerOrganisation. Generates a single-use, 7-day-expiry invitation token; emails the invitee a link; the invitee sets their password on click and lands in the organisation.

---

## Request

```
POST /api/v1/portal/organisations/8e2a1b4c-9d6f-4e7a-b1c2-3d4e5f6a7b8c/invitations
Cookie: .AspNetCore.Identity.Application=...
Content-Type: application/json
```

```json
{
  "email": "bookkeeper@my-business.eg",
  "role": "BillingAdmin",
  "displayName": "Mariam Saleh",
  "localePreference": "ar-EG"
}
```

**Auth**: Cookie. The signed-in TeamMember MUST be an Owner of the target Organisation; otherwise 403.

---

## Response — 201 Created

```json
{
  "invitationId": "9c8b2a7d-3e5f-1a4b-6c8d-2e9f3a4b5c6d",
  "email": "bookkeeper@my-business.eg",
  "role": "BillingAdmin",
  "expiresAtUtc": "2026-05-25T13:45:21Z",
  "emailSent": true
}
```

---

## Response — 400 Bad Request

```json
{
  "error": "invalid_request",
  "details": { "email": "Email format invalid.", "role": "Must be one of Owner, BillingAdmin, SupportAdmin, ReadOnly." }
}
```

---

## Response — 403 Forbidden

```json
{ "error": "not_owner" }
```

---

## Response — 409 Conflict

```json
{ "error": "already_member" }
```

The email is already an active member of this organisation. (Cross-organisation membership is allowed — the email being a member of a DIFFERENT organisation is NOT a conflict.)

OR

```json
{ "error": "pending_invitation_exists", "details": "An open invitation for this email already exists; resend or revoke first." }
```

---

## Response — 429 Too Many Requests

```json
{ "error": "rate_limited", "retryAfterSeconds": 3600 }
```

Per organisation, max 10 new invitations per hour to deter abuse.

---

## Behaviour

1. Authn + Owner-role check; reject with 403 if not Owner of the target Organisation.
2. Validate the request body shape; reject with 400 on any error.
3. Look up an existing active `OrganisationMembership` for this email in this Organisation; reject with 409 `already_member` if found.
4. Look up an existing non-expired `Invitation` row for this email in this Organisation; reject with 409 `pending_invitation_exists` if found.
5. Generate a cryptographically random 32-byte token; store its hash + an `ExpiresAtUtc = now + 7 days` in a new `Invitation` row.
6. Build the invitation URL: `https://daftarx.app/portal/invitations/accept?token=<base64url-encoded raw token>`.
7. Send the email via Resend with the bilingual `invitation` template (renders in the invitee's `localePreference`, fallback to the inviter's locale if absent).
8. Write an `AuditLogEntry` with `Verb = "member.invited"`, `SubjectKind = "Invitation"`, `SubjectId = invitation.Id`, payload `{ email, role }`.
9. Return 201.

---

## Sibling endpoint: `POST /api/v1/portal/invitations/accept`

Body: `{ "token": "<base64url>", "password": "<min 12 chars>" }`. Verifies the token hash, finds the matching Invitation row (rejecting if expired or already used), creates a TeamMember if the email isn't already a portal user, and inserts the `OrganisationMembership` row with the invitation's `Role`. Marks the Invitation row as `AcceptedAtUtc = now` so re-clicks return a friendly "this invitation has already been used" page instead of a 404.

---

## Contract test

`tests/EgyptTax.Portal.IntegrationTests/Contracts/InviteMemberEndpointTests.cs` asserts:

1. Owner inviting a fresh email returns 201 + an `Invitation` row + a `member.invited` audit-log entry + a dispatched email (verified via fake Resend adapter).
2. Non-Owner returns 403.
3. Invalid email returns 400.
4. Invalid role returns 400 with the four valid values listed.
5. Re-inviting an email that's already an active member returns 409 `already_member`.
6. Re-inviting an email with a pending invitation returns 409 `pending_invitation_exists`.
7. The 11th invitation in the same org within an hour returns 429.
8. The dispatched email's invitation URL contains a URL-safe encoded token + works in the accept sibling endpoint.
9. Accepting an expired invitation returns 400 with `error = "expired"`.
10. Accepting an invitation a second time returns 410 Gone with `error = "already_used"`.
11. The Invitation row stores a HASH of the token, NEVER the raw token (so a DB compromise doesn't allow token reuse).
12. The accept endpoint's first call for an email not yet a TeamMember CREATES the TeamMember row + the OrganisationMembership row in a single transaction (no half-created state).

Test-first per Constitution III.
