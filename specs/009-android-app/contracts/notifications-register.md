# Contract: `POST /api/v1/notifications/register` + `DELETE /api/v1/notifications/register`

**Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) (FR-005, FR-006)

The native shell calls `POST` on every successful sign-in and on every FCM token rotation, and `DELETE` on every explicit sign-out. The server persists / soft-deletes a [`MobilePushRegistration`](../data-model.md#5-mobilepushregistration) row.

---

## `POST /api/v1/notifications/register`

### Request

```
POST /api/v1/notifications/register
Cookie: AspNetCore.Cookies=…
Content-Type: application/json
```

```json
{
  "deviceId": "f4a9c1e0-2b8d-4e7a-9c1f-3e5a6b7d8c9e",
  "fcmToken": "dEr3X8q5RFa…",
  "platform": "android",
  "appVersion": "1.0.0"
}
```

**Fields**:

| Field | Type | Notes |
|-------|------|-------|
| `deviceId` | string (UUID) | Stable per install. App generates on first launch, persists in `EncryptedSharedPreferences`. |
| `fcmToken` | string | The current FCM device token (≤ 512 chars per FCM spec). |
| `platform` | string | `"android"` (only value accepted in v1; `"ios"` reserved). |
| `appVersion` | string | The app's `versionName` (e.g. `"1.0.0"`). |

**Auth**: Session cookie. Anonymous = `401`.

### Response — 201 Created (first registration) / 200 OK (refresh)

```json
{ "registrationId": "8e2a1b4c-9d6f-4e7a-b1c2-3d4e5f6a7b8c" }
```

### Response — 400 Bad Request

When `deviceId` isn't a valid UUID, `fcmToken` is empty, or `platform` isn't `"android"`:

```json
{ "error": "invalid_request", "details": "deviceId must be a UUID" }
```

### Response — 401 Unauthorized

```json
{ "error": "not_authenticated" }
```

### Behaviour

1. Lookup an existing `mobile_push_registrations` row by `(user_id, device_id)` where `revoked_at_utc IS NULL`.
2. If found: update `fcm_token`, `app_version`, `last_seen_at_utc`. Return 200.
3. If not found: insert a new row with `created_at_utc = now`. Return 201.
4. Write an audit-log row with kind `mobile.push.registered` (or `mobile.push.refreshed`), payload `{deviceId, platform, appVersion}` — token NOT logged.

---

## `DELETE /api/v1/notifications/register`

### Request

```
DELETE /api/v1/notifications/register?deviceId=f4a9c1e0-2b8d-4e7a-9c1f-3e5a6b7d8c9e
Cookie: AspNetCore.Cookies=…
```

### Response — 204 No Content

Soft-deletes the row (`revoked_at_utc = now`) and writes an audit-log row with kind `mobile.push.revoked`. Idempotent: deleting a non-existent or already-revoked registration also returns 204.

### Response — 401 Unauthorized

```json
{ "error": "not_authenticated" }
```

---

## Contract test

Server-side `NotificationRegisterEndpointTests.cs` asserts:

1. `POST` first time → 201, new row created, audit-log row written.
2. `POST` same `(user, device)` with different `fcmToken` → 200, row updated, `fcm_token` changed, `last_seen_at_utc` advanced.
3. `POST` with invalid `deviceId` → 400.
4. `POST` anonymous → 401.
5. `DELETE` after `POST` → 204, row's `revoked_at_utc` populated.
6. `DELETE` without prior `POST` → 204 (idempotent).
7. `POST` after `DELETE` re-activates the row (clears `revoked_at_utc`).
8. Audit-log entries NEVER contain the `fcm_token` value.

Test-first per Constitution III.
