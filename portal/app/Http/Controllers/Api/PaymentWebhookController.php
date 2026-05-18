<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Services\Payments\PaymentWebhookHandler;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Throwable;
use Illuminate\Support\Facades\Log;

/**
 * T091 + contracts/payment-webhook.md.
 *
 * Thin endpoint — auth is done by `PaymobHmacMiddleware`, business
 * logic is owned by `PaymentWebhookHandler`. This controller only
 * shapes the HTTP response:
 *
 *   200 { "received": true }                 — processed OR idempotent retry
 *   400 { "error": "unknown_merchant_order_id" } — unknown invoice
 *   500 { "error": "handler_exception" }     — bubbled exception (rare)
 */
class PaymentWebhookController extends Controller
{
    public function __construct(private readonly PaymentWebhookHandler $handler)
    {
    }

    public function __invoke(Request $request): JsonResponse
    {
        $obj = $request->input('obj', []);
        if (!is_array($obj)) {
            return response()->json(['error' => 'malformed_payload'], 400);
        }

        try {
            $result = $this->handler->handle($obj, $request->ip() ?? '0.0.0.0');
        } catch (Throwable $e) {
            Log::error('paymob:webhook handler exception: '.$e->getMessage(), ['exception' => $e]);
            return response()->json(['error' => 'handler_exception'], 500);
        }

        return match ($result) {
            PaymentWebhookHandler::UNKNOWN_ORDER => response()->json(['error' => 'unknown_merchant_order_id'], 400),
            PaymentWebhookHandler::PROCESSED, PaymentWebhookHandler::ALREADY_PROCESSED
                => response()->json(['received' => true]),
            default => response()->json(['error' => 'unknown_handler_result'], 500),
        };
    }
}
