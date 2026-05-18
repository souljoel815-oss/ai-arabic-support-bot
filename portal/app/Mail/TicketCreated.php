<?php

namespace App\Mail;

use App\Models\SupportTicket;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;

/**
 * T115 — confirmation sent to the customer immediately after they
 * create a ticket via `POST /portal/support`. Also fires a sibling
 * notification to vendor-staff inbox (handled separately by ops).
 */
class TicketCreated extends LocalisedMailable implements ShouldQueue
{
    use Queueable;

    public function __construct(
        string $localePreference,
        public readonly SupportTicket $ticket,
        public readonly string $portalUrl,
    ) {
        parent::__construct($localePreference);
    }

    protected function subjectAr(): string
    {
        return "تم استلام طلب الدعم — #{$this->ticket->id}";
    }

    protected function subjectEn(): string
    {
        return "Support ticket received — #{$this->ticket->id}";
    }

    protected function viewName(): string
    {
        return 'emails.support.ticket-created';
    }

    protected function templateData(): array
    {
        return [
            'ticket' => $this->ticket,
            'portalUrl' => $this->portalUrl,
        ];
    }
}
