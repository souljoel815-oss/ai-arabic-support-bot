<?php

namespace App\Mail;

use App\Models\Invoice;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;

/**
 * Sent by `RefundFirstPeriodService` once the refund-row Invoice is
 * written. The webhook side (`applyRefunded`) also fires this when a
 * Paymob refund-cleared callback arrives — both paths converge on the
 * same email so the customer sees one canonical confirmation.
 */
class RefundConfirmation extends LocalisedMailable implements ShouldQueue
{
    use Queueable;

    public function __construct(
        string $localePreference,
        public readonly Invoice $originalInvoice,
    ) {
        parent::__construct($localePreference);
    }

    protected function subjectAr(): string
    {
        return "تأكيد استرداد — فاتورة {$this->originalInvoice->invoice_number}";
    }

    protected function subjectEn(): string
    {
        return "Refund confirmation — invoice {$this->originalInvoice->invoice_number}";
    }

    protected function viewName(): string
    {
        return 'emails.refund-confirmation';
    }

    protected function templateData(): array
    {
        return [
            'invoice' => $this->originalInvoice,
            'amountEgp' => number_format($this->originalInvoice->amount_egp_minor / 100, 2),
        ];
    }
}
