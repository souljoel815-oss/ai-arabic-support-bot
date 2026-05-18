<?php

namespace App\Services\Payments;

/**
 * Value object returned by `PaymobAdapter::checkout()`.
 *
 * `iframeUrl` is populated only for the Card flow (hosted iframe). For
 * Fawry / Vodafone Cash / InstaPay the caller redirects the customer to
 * Paymob's hosted payment page using `paymentKey` directly.
 */
final readonly class PaymobCheckoutHandle
{
    public function __construct(
        public int $paymobOrderId,
        public string $paymentKey,
        public string $method,
        public ?string $iframeUrl,
    ) {
    }
}
