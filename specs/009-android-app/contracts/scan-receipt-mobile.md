# Contract: `POST /api/v1/ai/scan-receipt-mobile`

**Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) (User Story 2 + Story 4, FR-003, FR-004)

The native shell uploads a receipt image (from camera capture or share-target) to this endpoint. The server queues the OCR job and returns immediately with a queued-row identifier — the actual extraction result surfaces in `/scan-history` on the web UI within the SC-003 ten-second window.

This is the *mobile-optimised* sibling of the existing in-page `/scan-receipt` form. The existing form is multipart with WebView-local state (filename, language hint, vendor pre-pick); the mobile path is a simpler binary upload because the app already knows the user and device context.

---

## Request

```
POST /api/v1/ai/scan-receipt-mobile
Cookie: AspNetCore.Cookies=…
Content-Type: image/jpeg               (or image/png, image/heic, application/pdf)
X-DaftarX-Device-Id: f4a9c1e0-2b8d-4e7a-9c1f-3e5a6b7d8c9e
X-DaftarX-Capture-Source: camera       (or "share")
Content-Length: <bytes>

<binary body, max 8 MB>
```

**Headers**:

| Header | Required | Notes |
|--------|----------|-------|
| `Content-Type` | yes | One of `image/jpeg`, `image/png`, `image/heic`, `application/pdf`. Anything else → 415. |
| `X-DaftarX-Device-Id` | yes | The `deviceId` from `EncryptedSharedPreferences`. Bound to the audit-log row so a customer can trace "which device scanned this receipt". |
| `X-DaftarX-Capture-Source` | yes | `"camera"` or `"share"`. Powers the analytics rollup of camera vs share usage. |
| `Content-Length` | yes | Server caps at 8 MB; over-cap returns 413. |

**Auth**: Session cookie. Anonymous = 401. Edition gate: requires `Feature.ReceiptOcr` (i.e. SMB+) — Solo customers get 403.

---

## Response — 202 Accepted

```json
{
  "scanId": "7a9b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d",
  "viewUrl": "/scan-history?highlight=7a9b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d",
  "status": "queued"
}
```

**Fields**:

| Field | Type | Notes |
|-------|------|-------|
| `scanId` | string (UUID) | The new row's id in the existing `receipt_scans` table. |
| `viewUrl` | string | Relative URL the app can deep-link the user into the scan-history row on the web. |
| `status` | string | Always `"queued"` in v1; reserved for future `"processing"` / `"done"` polling. |

---

## Response — 401 Unauthorized

```json
{ "error": "not_authenticated" }
```

## Response — 403 Forbidden (edition gate)

```json
{
  "error": "feature_not_licensed",
  "feature": "receipt_ocr",
  "minimumEdition": "SMB",
  "currentEdition": "Solo"
}
```

The app responds by showing the same `/license-restricted` style upgrade panel as the web — the native UI shouldn't even surface the scan button to a Solo install, but the server enforces as a defence-in-depth.

## Response — 413 Payload Too Large

```json
{ "error": "image_too_large", "maxBytes": 8388608 }
```

## Response — 415 Unsupported Media Type

```json
{ "error": "unsupported_content_type", "accepted": ["image/jpeg", "image/png", "image/heic", "application/pdf"] }
```

## Response — 503 Service Unavailable

```json
{ "error": "ai_provider_unavailable", "retryAfterSeconds": 30 }
```

Returned when the configured Groq endpoint is down (the existing `OcrReceiptHandler` propagates the upstream error). The app shows a retry banner; the captured image stays in the local cache for re-submission.

---

## Contract test

Server-side `ScanReceiptMobileEndpointTests.cs` asserts:

1. Anonymous → 401.
2. Solo license → 403 with `feature_not_licensed` payload.
3. SMB+ license + valid JPEG ≤ 8 MB → 202 with a non-null `scanId` and a new `receipt_scans` row.
4. PDF body with `Content-Type: application/pdf` → 202.
5. `Content-Type: image/webp` → 415.
6. 9 MB body → 413.
7. Missing `X-DaftarX-Device-Id` → 400.
8. `X-DaftarX-Capture-Source` outside `{camera, share}` → 400.
9. The created `receipt_scans` row records `device_id` + `capture_source` in its payload metadata.
10. An audit-log entry is written with kind `ai.receipt_scan.mobile.queued`.

Test-first per Constitution III.
