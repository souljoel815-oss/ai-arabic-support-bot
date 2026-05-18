<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Log;
use Symfony\Component\HttpFoundation\Response;

/**
 * Verifies the HMAC-SHA512 header Paymob attaches to every webhook
 * delivery per
 *   specs/010-website-portal/contracts/payment-webhook.md "Auth".
 *
 * The signature is computed over the concatenation of a specific subset
 * of `obj` fields (in fixed order, per Paymob's docs). We rebuild the
 * canonical string from the request body, HMAC-SHA512 it with the shared
 * secret, and compare in constant time with `hash_equals` to defeat
 * timing attacks.
 *
 * Reject → 401 with no body so Paymob's retry policy treats it as a
 * permanent failure after N attempts (preventing log spam from a
 * misconfigured secret).
 */
class PaymobHmacMiddleware
{
    /**
     * The Paymob v3 documented HMAC field order. DO NOT reorder — this is
     * matched exactly against Paymob's own concatenation rule. If Paymob
     * publishes a v4 change, update here AND the contract doc.
     */
    private const HMAC_FIELDS = [
        'amount_cents',
        'created_at',
        'currency',
        'error_occured',
        'has_parent_transaction',
        'id',
        'integration_id',
        'is_3d_secure',
        'is_auth',
        'is_capture',
        'is_refunded',
        'is_standalone_payment',
        'is_voided',
        'order.id',
        'owner',
        'pending',
        'source_data.pan',
        'source_data.sub_type',
        'source_data.type',
        'success',
    ];

    public function handle(Request $request, Closure $next): Response
    {
        $providedHmac = $request->header('hmac', $request->query('hmac'));
        if (!is_string($providedHmac) || $providedHmac === '') {
            Log::warning('paymob:hmac no header on '.$request->fullUrl());
            return response('', 401);
        }

        $secret = config('services.paymob.hmac_secret');
        if (!is_string($secret) || $secret === '') {
            Log::error('paymob:hmac shared secret not configured (services.paymob.hmac_secret).');
            return response('', 401);
        }

        $obj = $request->input('obj', []);
        if (!is_array($obj) || $obj === []) {
            Log::warning('paymob:hmac payload missing obj');
            return response('', 401);
        }

        $canonical = '';
        foreach (self::HMAC_FIELDS as $field) {
            $canonical .= $this->stringifyField(data_get($obj, $field));
        }

        $expectedHmac = hash_hmac('sha512', $canonical, $secret);

        if (!hash_equals($expectedHmac, $providedHmac)) {
            Log::warning('paymob:hmac mismatch on '.$request->fullUrl());
            return response('', 401);
        }

        return $next($request);
    }

    /**
     * Paymob stringifies bool as the literal `"true"` / `"false"` and
     * leaves numbers / strings as-is. Nulls become empty strings.
     */
    private function stringifyField(mixed $value): string
    {
        if (is_bool($value)) {
            return $value ? 'true' : 'false';
        }
        if ($value === null) {
            return '';
        }
        return (string) $value;
    }
}
