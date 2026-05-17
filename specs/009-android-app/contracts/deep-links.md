# Contract: Deep-link URI grammar

**Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) (FR-008, User Story 3)

The native shell registers two URI families with the OS so a tapped push notification (or any external link source: messaging app, email, Slack) opens DaftarX directly on the right page. The `DeepLinkRouter` in `web/DeepLinkRouter.kt` translates the incoming URI into a path the in-app web view loads.

---

## Custom scheme — works on every Android version, no server prerequisites

```
daftarx://<page>[/<id>][?<query>]
```

Registered via `<intent-filter>` on `MainActivity` with:

```xml
<intent-filter>
  <action android:name="android.intent.action.VIEW" />
  <category android:name="android.intent.category.DEFAULT" />
  <category android:name="android.intent.category.BROWSABLE" />
  <data android:scheme="daftarx" />
</intent-filter>
```

**Supported pages**:

| URI | Web route | Source |
|-----|-----------|--------|
| `daftarx://home` | `/` | Generic fallback. |
| `daftarx://approvals` | `/approvals` | Approval-pending push. |
| `daftarx://invoice/<guid>` | `/invoices/<guid>` | Tapped invoice link. |
| `daftarx://purchase-invoice/<guid>` | `/purchase-invoices/<guid>` | Tapped purchase link. |
| `daftarx://expense/<guid>` | `/expenses/<guid>` | Expense link. |
| `daftarx://compliance-calendar` | `/compliance/calendar` | Tax deadline push. |
| `daftarx://eta-inbox` | `/eta-inbox` | ETA failure push. |
| `daftarx://license` | `/settings?tab=license` | License expiry push. |
| `daftarx://scan-history` | `/scan-history` | Post-scan confirmation tap. |
| `daftarx://settings` | `/settings` | Native shell's "Open server settings" deep link. |

Unknown pages route to `daftarx://home` and log a warning — never crash.

---

## HTTPS App Link — only when the customer hosts publicly with verified Digital Asset Links

```
https://<server-host>:<port>/<route>
```

Registered via `<intent-filter android:autoVerify="true">` with `<data android:host="*">`. The verification only succeeds if the customer's server hosts `/.well-known/assetlinks.json` pointing at our package + signing cert. LAN-only customers skip the verification; their HTTPS links open in the browser as a fallback (acceptable per the spec — the custom scheme is the primary path).

When the verification succeeds, links like `https://daftarx.acme-co.com:443/invoices/7a9b…` open the app directly on the matching page.

---

## Authentication-deferred routing

When a deep link fires while no `CachedSession` is unlocked, the `DeepLinkRouter`:

1. Stores the target URI in a single-slot `MutableStateFlow<Uri?>` (the "pending destination").
2. Navigates to the lock screen → biometric prompt → web login as appropriate.
3. After authentication completes, consumes the pending destination and instructs the web view to load it.
4. If no authentication completes within five minutes, the pending destination is discarded.

The web view ALWAYS loads the configured server's origin first to establish the session, then navigates to the deep-linked path via `loadUrl()` — never opens the path as a fresh navigation (which would lose the cookie).

---

## Notification payload mapping

Each push notification from the server includes a `data` block with one of these shapes; the `NotificationRouter` translates it to a `daftarx://...` URI:

```json
// Approval-pending
{ "type": "approval_pending", "count": 3 }
// → daftarx://approvals
```

```json
// Tax deadline
{ "type": "tax_deadline", "deadline": "vat_monthly", "windowDays": 7 }
// → daftarx://compliance-calendar
```

```json
// ETA submission failure
{ "type": "eta_failure", "invoiceId": "7a9b..." }
// → daftarx://invoice/7a9b...
```

```json
// License expiry
{ "type": "license_expiry", "windowDays": 30 }
// → daftarx://license
```

Unknown `type` values route to `daftarx://home` and log a warning.

---

## Contract test

Android instrumentation test `DeepLinkRouterTest.kt` asserts:

1. Each known `daftarx://...` URI resolves to the expected web path.
2. Unknown URIs resolve to `daftarx://home` (the safe fallback).
3. A deep link fired while no `CachedSession` is unlocked is preserved through the biometric flow.
4. A deep link fired while `CachedSession` is unlocked navigates the web view directly without re-prompting.
5. A pending deep link older than five minutes is discarded.
6. Each notification `data` payload type maps to the expected URI.
7. App Link verification status is detected at runtime (`PackageManager.getDomainVerificationUserState`) and the HTTPS path is only used when verified.

Test-first per Constitution III.
