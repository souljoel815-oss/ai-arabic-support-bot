# Contract: `POST /api/v1/portal/licences/activate`

**Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) (FR-031, FR-014, User Story 3)

Customer-side endpoint that signs a paid licence token bound to a specific hardware id. Called from the portal's "Activate paid licence" Blade page after payment clears. Uses Laravel's `LicenceSigningService` which wraps PHP's built-in `sodium_crypto_sign_detached()` against the `vendor-keys.json` keypair to produce envelopes byte-compatible with the on-prem `EgyptTax.Web.Licensing.LicenseVerifier` per research §10.

---

## Request

```
POST /api/v1/portal/licences/activate
Cookie: laravel_session=...
Content-Type: application/json
```

```json
{
  "subscriptionId": "8e2a1b4c-9d6f-4e7a-b1c2-3d4e5f6a7b8c",
  "hwid": "6A20-602B-7063-5D98"
}
```

**Auth**: Cookie auth. The signed-in TeamMember MUST be an Owner of the Subscription's Organisation; otherwise 403.

---

## Response — 200 OK

```json
{
  "licenceId": "7a9b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d",
  "edition": "SMB",
  "expiresAtUtc": "2027-05-18T00:00:00Z",
  "downloadUrl": "/api/v1/portal/licences/7a9b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d/token.token"
}
```

The token isn't returned in the response body — the UI offers a separate Download button that hits `downloadUrl` (which streams the file with `Content-Disposition: attachment; filename="license.token"`).

---

## Response — 400 Bad Request

```json
{ "error": "invalid_hwid", "details": "HWID format must match XXXX-XXXX-XXXX-XXXX." }
```

OR

```json
{ "error": "subscription_not_paid", "details": "Subscription is in trial or unpaid state." }
```

OR

```json
{ "error": "subscription_status", "details": "Subscription is Paused; resume billing before activating a new licence." }
```

---

## Response — 403 Forbidden

```json
{ "error": "not_owner" }
```

The signed-in TeamMember isn't an Owner of the Subscription's Organisation.

---

## Response — 409 Conflict

```json
{
  "error": "hwid_collision",
  "details": "This HWID is bound to an active licence under a different customer; contact support."
}
```

Cross-customer collision per FR-014. The collision is detected even across Subscriptions belonging to the SAME customer — if the same HWID already has an active licence under another Subscription, the customer must transfer (not activate) to avoid double-binding.

---

## Behaviour

1. Authn + organisation-scope check; reject with 403 if the signed-in TeamMember isn't an Owner of the target Subscription's Organisation.
2. Validate HWID format; reject with 400 on malformed.
3. Verify the Subscription's status is `Active` and the most-recent Invoice for the Subscription has `Status = "Paid"`; reject with 400 on `Trial` / `Pending` / `PastDue` / `Paused`.
4. Check for an existing active `Licence` row with the same HWID across the entire `licences` table (not just this Subscription); reject with 409 on collision.
5. Call `ILicenceSigningService.SignAsync(new SignRequest { Hwid = ..., Customer = org.LegalNameAr, Edition = sub.Tier, Expires = sub.CurrentPeriodEndUtc })` which wraps the existing `LicenseIssueHost` flow.
6. Insert the resulting `Licence` row with `SignedTokenBase64` populated, `IssuedAtUtc = now`, `ExpiresAtUtc = sub.CurrentPeriodEndUtc`.
7. Write an `AuditLogEntry` with `Verb = "licence.activated"`, `SubjectKind = "Licence"`, `SubjectId = licence.Id`, payload `{ hwid, edition }` — but NEVER include the signed token base64.
8. Return 200 with the licence id + edition + expiry + download URL.

The transaction wraps steps 6-7 as a single SaveChanges.

---

## Contract test

`portal/tests/Feature/Contracts/ActivatePaidLicenceEndpointTest.php` asserts:

1. Owner of a Paid SMB Subscription submitting a valid HWID returns 200 + a new Licence row whose `SignedTokenBase64` is a valid Ed25519-signed envelope (verifiable by the existing `LicenseVerifier`).
2. The returned `edition` matches the Subscription's current Tier.
3. The returned `expiresAtUtc` matches the Subscription's `CurrentPeriodEndUtc` exactly.
4. Malformed HWID returns 400 with `error = "invalid_hwid"`.
5. A non-Owner TeamMember submitting a valid request returns 403.
6. A Subscription in `PastDue` returns 400 with `error = "subscription_status"`.
7. A HWID already bound to a different customer's active Licence returns 409 with `error = "hwid_collision"`.
8. The audit-log row is written with payload `{ hwid, edition }` and DOES NOT include `signedTokenBase64`.
9. The download URL streams the token with `Content-Disposition: attachment; filename="license.token"` + `Content-Type: application/json`.
10. The signed token, when written to a file and opened by the existing on-prem `LicenseVerifier`, verifies successfully (end-to-end format compatibility check).

Test-first per Constitution III.
