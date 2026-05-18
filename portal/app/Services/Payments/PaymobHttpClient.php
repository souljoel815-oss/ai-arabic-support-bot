<?php

namespace App\Services\Payments;

use Illuminate\Http\Client\Factory as HttpFactory;
use Illuminate\Http\Client\Response;
use Illuminate\Support\Facades\Cache;
use Illuminate\Support\Facades\Log;
use RuntimeException;

/**
 * T088 — raw Paymob v3 HTTP adapter.
 *
 * Wraps Laravel's `Illuminate\Http\Client` with Paymob-specific concerns:
 *
 *   1. Auth-token exchange (`POST /auth/tokens`) — Paymob's auth token is
 *      valid for ~60 minutes. Cached in the database `cache` store so
 *      bursts of webhook + order calls don't re-auth on every request.
 *      Expiry is set to 55 minutes so we never serve a stale one.
 *   2. Idempotent `register-order` + `payment-key` sequence for the
 *      hosted-iframe flow (card / Vodafone Cash / InstaPay) and the
 *      direct-payment flow (Fawry — different integration_id).
 *   3. Surfaces clean exceptions: every non-2xx response becomes a
 *      `RuntimeException` with the Paymob error body verbatim so the
 *      caller can log it for ops investigation. NEVER swallow errors —
 *      payments failing silently is worse than failing loudly.
 *
 * This class has no business logic — see `PaymobAdapter` for that. The
 * split exists so we can swap in a fake HTTP factory in tests without
 * mocking the higher-level adapter contract.
 */
class PaymobHttpClient
{
    private const AUTH_CACHE_KEY = 'paymob:auth_token';
    private const AUTH_CACHE_TTL_SECONDS = 60 * 55;

    public function __construct(private readonly HttpFactory $http)
    {
    }

    /**
     * Step 1 of the Paymob flow. Exchanges the long-lived API key for a
     * short-lived JWT used to authorise all subsequent calls.
     *
     * @return string Bearer JWT (no `Bearer ` prefix).
     */
    public function getAuthToken(): string
    {
        return Cache::remember(
            self::AUTH_CACHE_KEY,
            self::AUTH_CACHE_TTL_SECONDS,
            fn (): string => $this->fetchAuthToken(),
        );
    }

    /**
     * Step 2. Registers the Invoice as a Paymob "order" so subsequent
     * payment-key generation can reference it. Idempotent on
     * `merchant_order_id` — Paymob de-duplicates by that field, so we
     * can safely re-call for retries.
     *
     * @return int Paymob's internal `order.id` (used for support lookup).
     */
    public function registerOrder(string $merchantOrderId, int $amountCents, string $currency = 'EGP'): int
    {
        $body = [
            'auth_token' => $this->getAuthToken(),
            'delivery_needed' => 'false',
            'amount_cents' => $amountCents,
            'currency' => $currency,
            'merchant_order_id' => $merchantOrderId,
            'items' => [],
        ];

        $response = $this->http
            ->acceptJson()
            ->asJson()
            ->post($this->endpoint('/ecommerce/orders'), $body);

        $this->assertOk($response, 'paymob:register-order');

        $orderId = $response->json('id');
        if (!is_int($orderId) || $orderId <= 0) {
            throw new RuntimeException("paymob:register-order returned no integer id: {$response->body()}");
        }

        return $orderId;
    }

    /**
     * Step 3. Generates a payment key tied to (order × integration_id).
     * The returned token is the iframe URL parameter for hosted-card or
     * the redirect target for Fawry / Vodafone Cash / InstaPay.
     *
     * @param array<string,mixed> $billingData required by Paymob (first/last name, phone, email).
     */
    public function createPaymentKey(
        int $orderId,
        int $amountCents,
        int $integrationId,
        array $billingData,
        string $currency = 'EGP',
        int $expirationSeconds = 3600,
    ): string {
        $body = [
            'auth_token' => $this->getAuthToken(),
            'amount_cents' => $amountCents,
            'expiration' => $expirationSeconds,
            'order_id' => $orderId,
            'billing_data' => $billingData,
            'currency' => $currency,
            'integration_id' => $integrationId,
        ];

        $response = $this->http
            ->acceptJson()
            ->asJson()
            ->post($this->endpoint('/acceptance/payment_keys'), $body);

        $this->assertOk($response, 'paymob:create-payment-key');

        $token = $response->json('token');
        if (!is_string($token) || $token === '') {
            throw new RuntimeException("paymob:create-payment-key returned no token: {$response->body()}");
        }

        return $token;
    }

    /**
     * Refund a previously-cleared transaction. Used by
     * RefundFirstPeriodService when the customer requests a refund
     * within the FR-034 7-day window. Paymob refunds asynchronously —
     * the actual refund-cleared confirmation arrives via a subsequent
     * webhook callback (`is_refunded: true`).
     */
    public function refundTransaction(int $paymobTransactionId, int $amountCents): void
    {
        $body = [
            'auth_token' => $this->getAuthToken(),
            'transaction_id' => $paymobTransactionId,
            'amount_cents' => $amountCents,
        ];

        $response = $this->http
            ->acceptJson()
            ->asJson()
            ->post($this->endpoint('/acceptance/void_refund/refund'), $body);

        $this->assertOk($response, 'paymob:refund');
    }

    private function fetchAuthToken(): string
    {
        $apiKey = config('services.paymob.api_key');
        if (!is_string($apiKey) || $apiKey === '') {
            throw new RuntimeException('Paymob API key not configured (services.paymob.api_key).');
        }

        $response = $this->http
            ->acceptJson()
            ->asJson()
            ->post($this->endpoint('/auth/tokens'), ['api_key' => $apiKey]);

        $this->assertOk($response, 'paymob:auth');

        $token = $response->json('token');
        if (!is_string($token) || $token === '') {
            throw new RuntimeException("paymob:auth returned no token: {$response->body()}");
        }

        Log::info('paymob:auth-token refreshed');
        return $token;
    }

    private function endpoint(string $path): string
    {
        $base = rtrim((string) config('services.paymob.api_base', 'https://accept.paymob.com/api'), '/');
        return $base . '/' . ltrim($path, '/');
    }

    private function assertOk(Response $response, string $op): void
    {
        if ($response->successful()) {
            return;
        }
        $msg = "{$op} HTTP {$response->status()}: {$response->body()}";
        Log::error($msg);
        throw new RuntimeException($msg);
    }
}
