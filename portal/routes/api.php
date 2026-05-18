<?php

use App\Http\Controllers\Api\PaymentWebhookController;
use App\Http\Middleware\PaymobHmacMiddleware;
use Illuminate\Support\Facades\Route;

/**
 * Portal JSON endpoints. Mounted under the `/api` URI prefix by
 * bootstrap/app.php → withRouting(api: ...).
 *
 * Today the only consumer is Paymob's webhook delivery. The route is
 * versioned (`/api/v1/...`) so we can ship a v2 payload shape without
 * breaking Paymob's configured callback URL.
 */

Route::prefix('v1/portal')->group(function () {
    Route::post('payments/webhook', PaymentWebhookController::class)
        ->middleware(PaymobHmacMiddleware::class)
        ->name('api.payments.webhook');
});
