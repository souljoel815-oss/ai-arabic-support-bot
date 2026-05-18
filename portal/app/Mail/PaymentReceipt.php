<?php

namespace App\Mail;

use App\Models\Invoice;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Mail\Mailables\Attachment;

/**
 * Sent by `PaymentWebhookHandler::applyCleared()` once a payment clears.
 * Queueable because we attach the rendered invoice PDF — DOMPDF render
 * is ~1s and we don't want to block the webhook ack.
 */
class PaymentReceipt extends LocalisedMailable implements ShouldQueue
{
    use Queueable;

    public function __construct(
        string $localePreference,
        public readonly Invoice $invoice,
        public readonly string $pdfStoragePath,
    ) {
        parent::__construct($localePreference);
    }

    protected function subjectAr(): string
    {
        return "إيصال الدفع — فاتورة {$this->invoice->invoice_number}";
    }

    protected function subjectEn(): string
    {
        return "Payment receipt — invoice {$this->invoice->invoice_number}";
    }

    protected function viewName(): string
    {
        return 'emails.payment-receipt';
    }

    protected function templateData(): array
    {
        return [
            'invoice' => $this->invoice,
            'amountEgp' => number_format($this->invoice->amount_egp_minor / 100, 2),
        ];
    }

    /** @return array<int, Attachment> */
    public function attachments(): array
    {
        return [
            Attachment::fromStorageDisk('local', $this->pdfStoragePath)
                ->as("invoice-{$this->invoice->invoice_number}.pdf")
                ->withMime('application/pdf'),
        ];
    }
}
