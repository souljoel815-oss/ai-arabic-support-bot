<?php

namespace App\Mail;

use App\Models\Subscription;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;

/**
 * Sent by `PaymentWebhookHandler::applyFailed()` whenever a Renewal
 * invoice transitions to Failed and the Subscription moves Active →
 * PastDue. Tells the customer to update their payment method before
 * the renewal-job's retry budget runs out.
 */
class RenewalFailed extends LocalisedMailable implements ShouldQueue
{
    use Queueable;

    public function __construct(
        string $localePreference,
        public readonly string $displayName,
        public readonly Subscription $subscription,
        public readonly string $updatePaymentUrl,
    ) {
        parent::__construct($localePreference);
    }

    protected function subjectAr(): string
    {
        return 'فشل تجديد الاشتراك — يرجى تحديث بياناتك';
    }

    protected function subjectEn(): string
    {
        return 'Subscription renewal failed — please update your payment details';
    }

    protected function viewName(): string
    {
        return 'emails.renewal-failed';
    }

    protected function templateData(): array
    {
        return [
            'displayName' => $this->displayName,
            'subscription' => $this->subscription,
            'updatePaymentUrl' => $this->updatePaymentUrl,
        ];
    }
}
