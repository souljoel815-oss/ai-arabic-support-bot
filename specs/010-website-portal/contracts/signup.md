# Contract: `POST /api/v1/portal/signup`

**Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) (User Story 3, FR-010, FR-032)

Creates a new CustomerOrganisation + a founding-Owner TeamMember + sends an email-confirmation link. No payment method captured at signup per FR-029 (the trial requires nothing more than installing the on-prem product); this endpoint exists for the small subset of prospects who want a portal account before installing.

---

## Request

```
POST /api/v1/portal/signup
Content-Type: application/json
Accept-Language: ar-EG
```

```json
{
  "email": "owner@my-business.eg",
  "password": "<min 12 chars; UI enforces complexity>",
  "organisationLegalNameAr": "شركة الأمل للتجارة",
  "organisationLegalNameEn": null,
  "displayName": "Ahmed Hassan",
  "localePreference": "ar-EG",
  "acceptedTermsAt": "2026-05-18T12:34:56Z",
  "acceptedPrivacyPolicyAt": "2026-05-18T12:34:56Z"
}
```

**Auth**: Anonymous. Rate-limited to 5 requests per IP per hour to deter signup-spam.

---

## Response — 201 Created

```json
{
  "organisationId": "8e2a1b4c-9d6f-4e7a-b1c2-3d4e5f6a7b8c",
  "teamMemberId": "7a9b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d",
  "emailConfirmationSent": true
}
```

The response body intentionally does NOT include an auth token — the user must confirm their email before signing in.

---

## Response — 400 Bad Request

```json
{
  "error": "invalid_request",
  "details": {
    "email": "Email format invalid.",
    "password": "Password too short (min 12 chars).",
    "organisationLegalNameAr": "Required."
  }
}
```

Field-level validation errors. The UI surfaces each one inline.

---

## Response — 409 Conflict

```json
{ "error": "email_already_registered" }
```

The email already exists as an active TeamMember. The response does NOT confirm whether the email belongs to this CustomerOrganisation or another — the UI surface tells the user "either sign in or use the password-reset flow".

---

## Response — 429 Too Many Requests

```json
{ "error": "rate_limited", "retryAfterSeconds": 600 }
```

---

## Behaviour

1. Validate the request body shape; reject with 400 on any error.
2. Hash the password via AspNetCore.Identity's default PBKDF2 hasher.
3. Insert a `CustomerOrganisation` row (`LegalNameAr` = `organisationLegalNameAr`, `CountryCode = "EG"`, `BillingEmail` = `email`).
4. Insert a `TeamMember` row with the supplied email + display name + locale, `EmailConfirmedAtUtc` left null.
5. Insert an `OrganisationMembership` row binding the TeamMember to the Organisation with `Role = "Owner"`.
6. Generate an email-confirmation token via AspNetCore.Identity, dispatch via Resend with the bilingual template.
7. Write an `AuditLogEntry` with `Verb = "organisation.signedUp"`, `ActorKind = "TeamMember"`, `ActorTeamMemberId = <new id>`.
8. Return 201.

The transaction wraps steps 3-7 as a single EF Core SaveChanges (with the email dispatch outside the transaction to prevent slow SMTP from holding the DB connection).

---

## Contract test

`tests/EgyptTax.Portal.IntegrationTests/Contracts/SignupEndpointTests.cs` asserts:

1. Valid request returns 201 + a `CustomerOrganisation` row + a founding-Owner `TeamMember` row + a single `OrganisationMembership` with `Role = "Owner"`.
2. Missing `organisationLegalNameAr` returns 400 with `details.organisationLegalNameAr` populated.
3. Password shorter than 12 chars returns 400 with `details.password` populated.
4. Submitting the same email twice returns 409 on the second attempt (and does NOT create a duplicate TeamMember).
5. The response NEVER includes an auth cookie or token (the user must confirm their email first).
6. An audit-log row is written with `Verb = "organisation.signedUp"`.
7. The 6th request from the same IP within an hour returns 429.
8. The email-confirmation email is dispatched via a fake Resend adapter (verified by asserting the fake's outbox).
9. The TeamMember's `LocalePreference` matches the supplied value.
10. The endpoint REJECTS requests where `acceptedTermsAt` or `acceptedPrivacyPolicyAt` is null (legal requirement: explicit consent).

Test-first per Constitution III.
