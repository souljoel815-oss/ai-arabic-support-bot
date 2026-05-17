# Contract: `GET /api/v1/me/features`

**Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) (FR-007, FR-019, FR-020)

The native shell polls this endpoint on every app foreground to refresh the [`LicenseFeatureSnapshot`](../data-model.md#4-licensefeaturesnapshot) entity. The response drives three independent native behaviours:

1. **Edition gating** — `features[]` + `edition` decide which native shell entry points (camera scan, AI suggestions) are surfaced (FR-007).
2. **Bidirectional version gate** — `serverVersion` + `minAppVersion` decide whether to block the app on an outdated build OR an outdated server (FR-019).
3. **Side-load update banner** — `latestKnownAppVersion` decides whether to render the non-blocking "v1.2 is available" card (FR-020).

> **Refreshed 2026-05-17 after `/speckit-clarify` Session 2026-05-17** — added `serverVersion`, `minAppVersion`, `latestKnownAppVersion`.

---

## Request

```
GET /api/v1/me/features
Cookie: AspNetCore.Cookies=…
Accept: application/json
```

**Auth**: Standard session cookie (same one the WebView uses). Anonymous calls get `401`.

**Permissions**: Any authenticated role (Owner, Admin, Accountant, Bookkeeper, Cashier, SalesRep). The endpoint reports the install's license + the server's version triple; it does not vary per role.

---

## Response — 200 OK

```json
{
  "edition": "Enterprise",
  "features": [
    "bank_import",
    "bulk_invoice",
    "multi_cashbox",
    "closing_cockpit",
    "trial_balance",
    "chart_of_accounts_custom",
    "multi_user",
    "smtp_email",
    "auto_backup",
    "audit_log",
    "invoice_templates",
    "bilingual_invoices",
    "eta_item_code_suggester",
    "whatsapp_delivery",
    "receipt_ocr",
    "multi_company",
    "income_tax_return",
    "wht_certificate_mgmt",
    "compliance_health",
    "priority_support",
    "cloud_backup",
    "ai_assistant"
  ],
  "expiresAtUtc": "2027-05-16T00:00:00Z",
  "fetchedAtUtc": "2026-05-17T08:30:14.317Z",
  "serverVersion": "5.1.3",
  "minAppVersion": "1.0.0",
  "latestKnownAppVersion": "1.0.0"
}
```

**Fields**:

| Field | Type | Notes |
|-------|------|-------|
| `edition` | string | One of `"Solo"`, `"SMB"`, `"Enterprise"`, `"Firm"`, `"Trial"`. |
| `features` | string[] | The `Feature.*` keys included in the active license. Empty array for Solo + unlicensed installs. |
| `expiresAtUtc` | string (ISO-8601) | License expiry. `null` for unlicensed / trial-with-no-expiry. |
| `fetchedAtUtc` | string (ISO-8601) | Server's "now" — lets the app correct for clock skew when computing T-7 / T-30 banners. |
| `serverVersion` | string | The DaftarX server's running version. Source: assembly `AssemblyInformationalVersion`, cached at server startup. Format: flat `"MAJOR.MINOR.PATCH"`. |
| `minAppVersion` | string | The minimum app version this server will accept. Source: `appsettings.json` → `MobileVersionConfig:MinAppVersion`. Reload-on-change. Format: flat `"MAJOR.MINOR.PATCH"`. |
| `latestKnownAppVersion` | string | The most recent app build the vendor has published. Source: `appsettings.json` → `MobileVersionConfig:LatestKnownAppVersion`. Reload-on-change. Format: flat `"MAJOR.MINOR.PATCH"`. |

---

## Response — 401 Unauthorized

```json
{ "error": "not_authenticated" }
```

The app responds by clearing the cached session and routing to the login flow.

---

## Response — 503 Service Unavailable

```json
{ "error": "license_state_unknown" }
```

Returned when the server's boot-time license gate hasn't completed. The app keeps the previous snapshot (including the previous version triple — version-gate doesn't flap during transient 503s) and retries on next foreground.

---

## Contract test

Server-side test `MeFeaturesEndpointTests.cs` asserts:

**Edition + license fields (original)**

1. Anonymous request returns `401`.
2. Authenticated request returns 200 with the edition + features matching the currently activated license token on the test installation.
3. The `features` array shape exactly matches the keys defined in `src/EgyptTax.Web/Licensing/Feature.cs` — adding a new feature constant without updating the gate or the test fails CI.
4. `expiresAtUtc` is `null` when no token is activated (unlicensed install).
5. The response is JSON, not `text/html` (server doesn't fall through to the SPA fallback).

**Version triple (added 2026-05-17 per FR-019/FR-020)**

6. `serverVersion` is non-null and matches the regex `^\d+\.\d+\.\d+$`.
7. `serverVersion` equals the assembly's `AssemblyInformationalVersion` attribute (verified by reflection in the test).
8. `minAppVersion` and `latestKnownAppVersion` reflect the test's overridden `appsettings.json` values exactly (no transformation on the way out).
9. When the configured `MinAppVersion` is omitted from `appsettings.json`, the endpoint returns `"1.0.0"` (the documented default) — not null, not absent.
10. When the configured `LatestKnownAppVersion` is omitted, the endpoint returns the same value as `serverVersion` (sensible default — "the version that ships in lock-step with this server is the latest").
11. Changing `MobileVersionConfig:LatestKnownAppVersion` in `appsettings.json` and triggering a config reload is reflected in the very next response (proves reload-on-change works — no server restart needed).

All eleven assertions MUST be written first (test-first per Constitution III) and observed failing before the endpoint code is written.
