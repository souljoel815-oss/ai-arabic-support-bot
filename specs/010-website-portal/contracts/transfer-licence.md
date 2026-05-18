# Contract: `POST /api/v1/portal/licences/{licenceId}/transfer`

**Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) (FR-014, User Story 2)

Retires an existing Licence's token, signs a fresh token bound to a new hardware id, and writes both events to the audit log. Implements the "I got a new laptop" self-service flow that SC-003 requires complete in under 5 minutes.

---

## Request

```
POST /api/v1/portal/licences/7a9b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d/transfer
Cookie: laravel_session=...
Content-Type: application/json
```

```json
{
  "newHwid": "B102-704C-5D98-6A20"
}
```

**Auth**: Cookie. The signed-in TeamMember MUST be an Owner of the Licence's Subscription's Organisation; otherwise 403.

---

## Response — 200 OK

```json
{
  "retiredLicenceId": "7a9b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d",
  "newLicenceId": "9c8b2a7d-3e5f-1a4b-6c8d-2e9f3a4b5c6d",
  "edition": "SMB",
  "expiresAtUtc": "2027-05-18T00:00:00Z",
  "downloadUrl": "/api/v1/portal/licences/9c8b2a7d-3e5f-1a4b-6c8d-2e9f3a4b5c6d/token.token"
}
```

---

## Response — 400 Bad Request

```json
{ "error": "invalid_hwid", "details": "..." }
```

OR

```json
{ "error": "same_hwid", "details": "New HWID matches the current binding; nothing to transfer." }
```

---

## Response — 403 Forbidden

```json
{ "error": "not_owner" }
```

---

## Response — 404 Not Found

```json
{ "error": "licence_not_found" }
```

The licence id doesn't exist or already belongs to a different (deleted) organisation.

---

## Response — 409 Conflict

```json
{
  "error": "hwid_collision",
  "details": "Target HWID is bound to an active licence under a different customer; contact support."
}
```

---

## Response — 422 Unprocessable

```json
{ "error": "already_retired", "details": "Licence was already retired on 2026-05-10." }
```

---

## Behaviour

1. Authn + organisation-scope check (FR-021); reject with 403 if not Owner.
2. Load the source `Licence` row; reject with 404 if not found within the signed-in member's organisations.
3. Reject with 422 if `RetiredAtUtc IS NOT NULL`.
4. Validate the new HWID format; reject with 400 on malformed.
5. Reject with 400 if `newHwid == source.Hwid`.
6. Check for an existing active `Licence` row with `newHwid` across the entire `licences` table; reject with 409 on collision.
7. In a single transaction:
   a. Set `source.RetiredAtUtc = now`, `source.RetiredReason = "Transferred"`.
   b. Call `ILicenceSigningService.SignAsync(...)` for the new HWID with the SAME `edition` + `expiresAtUtc` as the source.
   c. Insert the new `Licence` row.
   d. Write two `AuditLogEntry` rows: `licence.retired` (subject = source) and `licence.activated` (subject = new), both with payload referencing the other's id for traceability.
8. Return 200.

---

## Contract test

`portal/tests/Feature/Contracts/TransferLicenceEndpointTest.php` asserts:

1. Owner transferring a valid licence to a fresh HWID returns 200 + the source licence's `RetiredAtUtc` is populated + a new Licence row exists with the new HWID + same Edition + same expiry.
2. Two audit-log rows are written: `licence.retired` referencing the source, and `licence.activated` referencing the new, with cross-references in their payloads.
3. The source licence's `SignedTokenBase64` is UNCHANGED by the transfer (the original token still verifies on whatever machine has it cached — important so the customer doesn't get locked out during the brief window before they drop the new token).
4. Non-Owner returns 403.
5. Source licence id not found in the member's orgs returns 404.
6. Same HWID returns 400 with `error = "same_hwid"`.
7. Already-retired source returns 422.
8. Target HWID bound to a different customer returns 409.
9. Concurrent transfer attempts on the same source (two requests racing) result in exactly ONE 200 + ONE 422 (optimistic-concurrency via row-version check, not pessimistic lock).
10. The transaction rolls back ALL changes if `ILicenceSigningService.SignAsync` throws (no half-retired-half-issued state).

Test-first per Constitution III.
