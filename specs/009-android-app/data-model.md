# Phase 1 Data Model: DaftarX Android App

**Plan**: [plan.md](./plan.md)
**Date**: 2026-05-17

Five entities total, drawn from the spec's "Key Entities" section and the technology decisions in [research.md](./research.md). Three live on the device only (in-memory or encrypted-on-disk); one lives in encrypted preferences; one is the single new server-side table.

---

## Device-side entities

### 1. ServerConfiguration

**Purpose**: The single source of truth for which DaftarX server this install talks to. Set on first launch, edited from the settings screen, wiped on "reset configuration".

**Storage**: `EncryptedSharedPreferences` file `server_config.prefs` (per research §3).

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `host` | String | yes | Normalised hostname or IP — no scheme, no port, no path. Validated against a permissive regex (any printable ASCII, no whitespace) on save. |
| `port` | Int | yes | 1–65535. Defaults to 50063 in the configuration UI. |
| `useHttps` | Boolean | yes | `false` by default (matches on-prem LAN bind); set `true` when the user types an `https://...` URL or toggles in settings. |
| `lastValidatedAtUtc` | Instant? | no | Set after `ServerProbe.check()` returns OK; cleared on URL change. |
| `addedAtUtc` | Instant | yes | When the user first saved this config. Used by the audit log when registering an FCM token. |

**Lifecycle**:

```
[no config] ──first-run setup──▶ [pending probe] ──probe OK──▶ [validated]
                                                  │
                                                  └─probe fail──▶ [error screen, stays in pending probe until retry]

[validated] ──user resets──▶ [no config]
[validated] ──user changes URL──▶ [pending probe] (wipes lastValidatedAtUtc, cached session, biometric flag)
```

**Validation**:
- Reject blank host.
- Reject port outside 1–65535.
- Reject obviously-malformed input (whitespace, control chars, `..`, `/` inside host).
- The actual reachability test is `ServerProbe` (a `HEAD /` request with 5 s timeout); failure shows the error screen, doesn't block save.

---

### 2. CachedSession

**Purpose**: Persist the WebView session cookie across cold starts so returning users only re-type their web password once per session-lifetime. Released to the WebView only after a successful biometric prompt.

**Storage**: AES-GCM ciphertext (key wrapped by AndroidX `MasterKey`) in `EncryptedSharedPreferences` file `session_vault.prefs`. Separate file from `server_config.prefs` so a "reset configuration" wipe can drop the vault without touching anything else.

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `cookieBlob` | ByteArray | yes | AES-GCM(`Set-Cookie` payload). Plaintext format is the raw HTTP `Set-Cookie` string from the server's auth response (e.g. `AspNetCore.Cookies=…; expires=…; path=/; HttpOnly`). |
| `serverHost` | String | yes | Bound to the `ServerConfiguration.host` at the time the session was issued. Changing the configured server invalidates the vault. |
| `userEmail` | String | yes | The signed-in user's email, plaintext. Used only to render "Signed in as …" on the biometric prompt. Not a secret. |
| `issuedAtUtc` | Instant | yes | When the cookie was first cached. |
| `lastUnlockedAtUtc` | Instant | yes | Updated on each successful biometric unlock; used by the idle timer. |
| `biometricRequired` | Boolean | yes | Per-user toggle from the settings screen. Defaults to `true` when the device has at least one enrolled biometric. |

**Lifecycle**:

```
[empty] ──user logs in via WebView──▶ [populated, locked]
                                       │
[locked] ──biometric/PIN OK──▶ [unlocked for this session]
[unlocked] ──idle timeout──▶ [locked]
[unlocked] ──user signs out──▶ [empty]
[any] ──user resets config / changes server URL──▶ [empty]
[any] ──biometric enrolment changes on device──▶ [empty] (KeyPermanentlyInvalidatedException)
```

**Validation**:
- The decryption guard catches `KeyPermanentlyInvalidatedException` (raised when the user changes their device PIN, deletes their fingerprint, etc.) and treats it as an expected re-auth event, not a crash.

---

### 3. PendingShareIntake

**Purpose**: An in-memory holding pen for a file shared into the app while the user wasn't authenticated. Released to the upload pipeline once the user unlocks.

**Storage**: A single `MutableStateFlow<PendingShareIntake?>` on the `DaftarXApp` singleton. No persistence (per research §7 — over-engineered to persist).

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `cacheFileUri` | Uri | yes | A `file://` URI inside the app's cache directory — the share's source URI was copied here on intake because the originating app may have revoked its grant by the time we upload. |
| `mimeType` | String | yes | `image/jpeg`, `image/png`, `image/heic`, `application/pdf`. |
| `sizeBytes` | Long | yes | Used to short-circuit obviously oversized uploads. |
| `intakeAtUtc` | Instant | yes | Stale entries (> 1 hour) are dropped on app foreground. |

**Lifecycle**:

```
[null] ──ShareReceiverActivity intake──▶ [populated]
[populated] ──user unlocks + upload completes──▶ [null]
[populated] ──user dismisses share without uploading──▶ [null]
[populated] ──process death──▶ [null] (we accept the loss in v1)
```

---

### 4. LicenseFeatureSnapshot

**Purpose**: A short-lived copy of the active license's feature list AND the server's reported version triple, polled from the server on every app foreground so the native shell can (a) hide/show entry points within one polling cycle of a license change, (b) enforce the bidirectional version gate (FR-019), and (c) drive the side-load update-available banner (FR-020).

**Storage**: A single `MutableStateFlow<LicenseFeatureSnapshot>` on the `DaftarXApp` singleton; *not* persisted across cold starts (refetched on launch).

**Fields**:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `features` | Set<String> | yes | The `Feature.*` keys the active license includes (e.g. `"receipt_ocr"`, `"bulk_invoice"`). Empty set for Solo + unlicensed. |
| `edition` | String | yes | The wire-format edition name (`"Solo"`, `"SMB"`, `"Enterprise"`, `"Firm"`, `"Trial"`). |
| `fetchedAtUtc` | Instant | yes | When the snapshot was last refreshed. |
| `expiresAtUtc` | Instant? | no | The license's expiry per the server payload. Used to show the T-30 / T-7 warning banner natively. |
| `serverVersion` | String | yes | The DaftarX server's running version, e.g. `"5.1.3"`. Read from the server's assembly attribute. Compared against the app's compile-time `MIN_SERVER_VERSION` constant by `VersionGate.kt` (FR-019). |
| `minAppVersion` | String | yes | The minimum app version this server accepts, e.g. `"1.0.0"`. Configured on the server side in `appsettings.json`. When `BuildConfig.VERSION_NAME < minAppVersion`, the app navigates to `AppOutdatedScreen` and blocks native shell flows (FR-019). |
| `latestKnownAppVersion` | String | yes | The most recent app build the vendor has published, e.g. `"1.2.0"`. Configured on the server side. When `BuildConfig.VERSION_NAME < latestKnownAppVersion`, the app renders the dismissable `UpdateAvailableBanner` (FR-020). |

**Lifecycle**: Re-fetched on `ON_RESUME` of the host activity. A failed fetch keeps the previous snapshot (fail-closed only if the snapshot is older than 1 hour, in which case we hide all premium features). On 503 (`license_state_unknown`), the version fields are kept from the previous snapshot — version-gate doesn't flap.

**Validation**:
- All three version fields must match the regex `^\d+\.\d+\.\d+$` (flat MAJOR.MINOR.PATCH per research §17).
- Comparison is component-wise integer compare (not lexicographic) — `"1.10.0"` > `"1.9.0"`.
- A missing or malformed field is treated as "unknown"; the corresponding gate/banner is suppressed (fail-open for version checks, not fail-closed, since blocking the user over a parser bug would be worse than briefly showing a missing screen).

---

## Server-side entity

### 5. MobilePushRegistration

**Purpose**: Bind an FCM device token to a (user, device) pair so the server's notification publishers can fan out to the right phones.

**Storage**: New table `mobile_push_registrations` added via EF Core migration `<timestamp>_MobilePushRegistrations`. Lives in the existing portable SQLite database; replicated to SQL Server for the on-prem enterprise installs by the same EF Core configuration.

**Schema**:

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | uniqueidentifier | PK, NEWID() default | Standard primary key per the existing convention. |
| `user_id` | uniqueidentifier | NOT NULL, FK → `users.id`, indexed | The DaftarX user this device is registered for. |
| `device_id` | nvarchar(64) | NOT NULL | A stable UUID the app generates on first launch and persists in `EncryptedSharedPreferences`. Survives the FCM token rotating. |
| `fcm_token` | nvarchar(512) | NOT NULL | The current FCM device token. |
| `platform` | nvarchar(16) | NOT NULL | `"android"` for now; `"ios"` reserved. |
| `app_version` | nvarchar(32) | NOT NULL | Sent by the app on every register call so the server knows when a customer is on an old version. |
| `created_at_utc` | datetime2 | NOT NULL | First-time registration timestamp. |
| `last_seen_at_utc` | datetime2 | NOT NULL | Updated on every register or token-refresh call. |
| `revoked_at_utc` | datetime2 | NULL | Soft-delete on logout; row kept for the audit log. |

**Indexes**:
- `IX_mobile_push_registrations_user_id` on `(user_id)` — every notifier queries by user.
- `UQ_mobile_push_registrations_user_device` UNIQUE on `(user_id, device_id) WHERE revoked_at_utc IS NULL` — a user can have multiple devices but only one active registration per device.

**Lifecycle**:

```
[no row] ──app POSTs /register──▶ [active]
[active] ──app POSTs /register with new fcm_token──▶ [active, fcm_token + last_seen updated]
[active] ──app POSTs /unregister on logout──▶ [revoked]
[revoked] ──user logs in again on same device──▶ [active, revoked_at_utc nulled]
```

**Audit trail**: Every state transition writes an `audit_log` row (per the existing FR-028 audit pattern) with kind `mobile.push.registered` / `mobile.push.refreshed` / `mobile.push.revoked` and a payload containing `device_id` (token NOT logged — secret).
