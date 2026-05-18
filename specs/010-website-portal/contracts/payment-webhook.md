# Contract: `POST /api/v1/portal/payments/webhook` (Paymob callback)

**Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) (FR-015, User Story 3 scenario 3)

Receives Paymob's transaction-status callbacks. Idempotent state machine that transitions the matching `Invoice` row to `Paid` / `Failed` / `Refunded` and triggers downstream effects (issue licences, generate PDF receipt, email the customer).

---

## Request

Paymob posts a signed payload to this endpoint with a HMAC-SHA512 header for authenticity verification. The body shape matches Paymob's documented v3 webhook schema:

```
POST /api/v1/portal/payments/webhook
Content-Type: application/json
HMAC: <hex-encoded HMAC-SHA512 of the concatenated key fields, signed with our shared secret>
```

```json
{
  "type": "TRANSACTION",
  "obj": {
    "id": 18293847,
    "amount_cents": 49900,
    "currency": "EGP",
    "success": true,
    "is_voided": false,
    "is_refunded": false,
    "is_3d_secure": false,
    "integration_id": 12345,
    "order": {
      "id": 9876543,
      "merchant_order_id": "INV-2026-00042"
    },
    "source_data": {
      "type": "card",
      "pan": "xxxx xxxx xxxx 1234",
      "sub_type": "MasterCard"
    },
    "created_at": "2026-05-18T13:45:21Z"
  }
}
```

**Auth**: HMAC header verification using the shared secret stored in `appsettings.json` (the value is rotated whenever Paymob credentials change). Requests with invalid HMAC return 401 immediately without further processing.

---

## Response — 200 OK

```json
{ "received": true }
```

Returned for ALL successfully-processed callbacks (success transactions, failed transactions, refund notifications). Paymob retries on non-200 responses, so the server returns 200 even for "we already processed this" idempotency hits.

---

## Response — 401 Unauthorized

Returned (with no body) when the HMAC header doesn't validate against the shared secret. Paymob's retry policy treats this as a permanent failure after N retries.

---

## Response — 400 Bad Request

```json
{ "error": "unknown_merchant_order_id" }
```

Returned when `obj.order.merchant_order_id` doesn't match any Invoice we have on file. This is a permanent failure — Paymob retries are pointless.

---

## Behaviour

1. Verify the HMAC header against the shared secret over the documented Paymob field concatenation. Reject with 401 on mismatch.
2. Look up the Invoice by `merchant_order_id` (which we set to the Invoice's `InvoiceNumber` at order creation). Reject with 400 if not found.
3. **Idempotency check**: if the Invoice's `PaymobTransactionId` already matches `obj.id`, return 200 immediately — this is a Paymob retry of a callback we already processed.
4. Begin a transaction:
   a. Set `Invoice.PaymobTransactionId = obj.id`.
   b. If `obj.success && !obj.is_voided`:
      - Set `Invoice.Status = "Paid"`, `Invoice.PaidAtUtc = obj.created_at`.
      - If `Invoice.Kind == "FirstPeriod"` or `"Renewal"` or `"TierUpgrade"`: transition the parent Subscription to `Active`, extend `CurrentPeriodEndUtc` to the new period.
      - Enqueue the PDF-generation job (QuestPDF, async).
      - Enqueue the "payment receipt" email (Resend, async).
      - Write `AuditLogEntry` with `Verb = "payment.cleared"`, `SubjectKind = "Invoice"`, `SubjectId = invoice.Id`, payload `{ amountEgp, paymentMethod, paymobTransactionId }`.
   c. Else if `obj.is_refunded`:
      - Set `Invoice.Status = "Refunded"`, `Invoice.RefundedAtUtc = now`.
      - Retire all `Licence` rows derived from the Subscription with `RetiredReason = "Refunded"`.
      - Write `AuditLogEntry` with `Verb = "payment.refunded"`.
   d. Else (failure):
      - Set `Invoice.Status = "Failed"`.
      - If the parent Subscription was `Active`, transition to `PastDue`.
      - Write `AuditLogEntry` with `Verb = "payment.failed"`.
5. Commit. Return 200.

The PDF generation and email dispatch happen OUTSIDE the transaction via a background job queue so a slow PDF render doesn't block the webhook reply (Paymob times out after 30 seconds).

---

## Contract test

`tests/EgyptTax.Portal.IntegrationTests/Contracts/PaymentWebhookEndpointTests.cs` asserts:

1. A valid `success: true` payload for a known InvoiceNumber transitions the Invoice to `Paid`, extends the Subscription's period, and enqueues both the PDF + email jobs (verified by a fake background-queue adapter).
2. Same payload received twice returns 200 both times but only emits ONE `payment.cleared` audit-log row (idempotency).
3. Invalid HMAC returns 401.
4. Unknown `merchant_order_id` returns 400.
5. `success: false` payload transitions the Invoice to `Failed` and the parent Subscription to `PastDue` (if it was `Active`).
6. `is_refunded: true` payload transitions the Invoice to `Refunded` AND retires all derived Licences with `RetiredReason = "Refunded"`.
7. The PDF generation + email dispatch are NEVER attempted on a `success: false` callback.
8. The audit-log entry NEVER includes the cardholder PAN or Paymob's raw `source_data` (regulatory + PCI scope minimisation).
9. A callback for a FirstPeriod invoice triggers Subscription.CreatedAtUtc population (the Subscription was Pending before payment cleared).
10. The endpoint completes within 5 seconds at p95 even with a fake PDF job that intentionally hangs (proves the background-queue dispatch is non-blocking).

Test-first per Constitution III.
