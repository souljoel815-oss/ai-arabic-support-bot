<?php

namespace App\Services\Payments;

use App\Models\Invoice;
use App\Models\TeamMember;
use InvalidArgumentException;
use RuntimeException;

/**
 * T089 — high-level Paymob facade. Per FR-015 + research §3.
 *
 * Translates "checkout this Invoice for this customer with this payment
 * method" into the right sequence of `PaymobHttpClient` calls. Returns a
 * `PaymobCheckoutHandle` containing everything the controller needs to
 * render the next step:
 *
 *   - Card / Vodafone Cash / InstaPay → embed iframe URL with the
 *     payment-key token
 *   - Fawry → display the Fawry reference number returned by Paymob's
 *     direct-payment endpoint (the customer pays at a Fawry kiosk)
 *
 * Only routes through Paymob; Bank Transfer is reconciled by the vendor
 * via the `invoice:mark-paid` Artisan command (see MarkInvoicePaid.php)
 * and never touches this class.
 */
class PaymobAdapter
{
    public const METHOD_CARD = 'Card';
    public const METHOD_FAWRY = 'Fawry';
    public const METHOD_VODAFONE_CASH = 'VodafoneCash';
    public const METHOD_INSTAPAY = 'InstaPay';

    /**
     * Maps our internal payment_method enum value to the integration_id
     * config key. Bank Transfer is intentionally absent.
     */
    private const METHOD_TO_INTEGRATION_KEY = [
        self::METHOD_CARD => 'card',
        self::METHOD_FAWRY => 'fawry',
        self::METHOD_VODAFONE_CASH => 'vodafone_cash',
        self::METHOD_INSTAPAY => 'instapay',
    ];

    public function __construct(private readonly PaymobHttpClient $http)
    {
    }

    /**
     * Start a checkout for an Invoice. Re-runnable: if the same Invoice is
     * re-checked-out (e.g. customer abandoned the iframe), Paymob's
     * `merchant_order_id` de-dupe means we get the same order back. The
     * payment-key, however, is regenerated each call — Paymob expires
     * unused keys after `expirationSeconds`.
     */
    public function checkout(Invoice $invoice, TeamMember $payer): PaymobCheckoutHandle
    {
        if ($invoice->status !== Invoice::STATUS_PENDING) {
            throw new InvalidArgumentException(
                "Invoice {$invoice->invoice_number} is in status {$invoice->status}; only Pending invoices can be checked out."
            );
        }

        $method = $invoice->payment_method;
        $integrationKey = self::METHOD_TO_INTEGRATION_KEY[$method] ?? null;
        if ($integrationKey === null) {
            throw new InvalidArgumentException("Paymob does not route '{$method}' — Bank Transfer is reconciled manually via MarkInvoicePaid CLI.");
        }

        $integrationId = config("services.paymob.integration_ids.{$integrationKey}");
        if (!is_numeric($integrationId)) {
            throw new RuntimeException("Paymob integration_id not configured for '{$method}' (services.paymob.integration_ids.{$integrationKey}).");
        }

        $orderId = $this->http->registerOrder(
            merchantOrderId: $invoice->invoice_number,
            amountCents: (int) $invoice->amount_egp_minor,
            currency: 'EGP',
        );

        $paymentKey = $this->http->createPaymentKey(
            orderId: $orderId,
            amountCents: (int) $invoice->amount_egp_minor,
            integrationId: (int) $integrationId,
            billingData: $this->buildBillingData($payer),
        );

        return new PaymobCheckoutHandle(
            paymobOrderId: $orderId,
            paymentKey: $paymentKey,
            method: $method,
            iframeUrl: $method === self::METHOD_CARD
                ? "https://accept.paymob.com/api/acceptance/iframes/CARD_IFRAME_ID?payment_token={$paymentKey}"
                : null,
        );
    }

    /**
     * Issue a refund against a previously-cleared transaction. Webhook
     * will arrive later with `is_refunded: true` to finalise the Invoice
     * state — this just sends the refund request to Paymob.
     */
    public function refund(Invoice $invoice): void
    {
        if ($invoice->paymob_transaction_id === null) {
            throw new InvalidArgumentException(
                "Invoice {$invoice->invoice_number} has no paymob_transaction_id — cannot refund (was it a Bank Transfer?)."
            );
        }
        $this->http->refundTransaction(
            paymobTransactionId: (int) $invoice->paymob_transaction_id,
            amountCents: (int) $invoice->amount_egp_minor,
        );
    }

    /**
     * Paymob requires every billing-data field even when we don't have
     * the value. We default to "NA" for missing fields rather than
     * leaving them empty (Paymob rejects empty strings).
     *
     * @return array<string,string>
     */
    private function buildBillingData(TeamMember $payer): array
    {
        // display_name "Ahmed Mohamed" → first="Ahmed", last="Mohamed"
        $nameParts = preg_split('/\s+/', trim($payer->display_name ?? $payer->email), 2);
        $firstName = $nameParts[0] ?? 'NA';
        $lastName = $nameParts[1] ?? 'NA';

        return [
            'first_name' => $firstName,
            'last_name' => $lastName,
            'email' => $payer->email,
            'phone_number' => '+201000000000',
            'country' => 'EG',
            'city' => 'Cairo',
            'street' => 'NA',
            'building' => 'NA',
            'floor' => 'NA',
            'apartment' => 'NA',
            'postal_code' => 'NA',
            'state' => 'NA',
        ];
    }
}
