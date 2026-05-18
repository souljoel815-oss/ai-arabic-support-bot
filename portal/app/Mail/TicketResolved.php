<?php

namespace App\Mail;

use App\Models\SupportTicket;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;

/**
 * T115 — sent when a vendor-staff member marks the ticket as Resolved.
 * Customer can reply to reopen it.
 */
class TicketResolved extends LocalisedMailable implements ShouldQueue
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
        return "تم حل طلب الدعم — #{$this->ticket->id}";
    }

    protected function subjectEn(): string
    {
        return "Support ticket resolved — #{$this->ticket->id}";
    }

    protected function viewName(): string
    {
        return 'emails.support.ticket-resolved';
    }

    protected function templateData(): array
    {
        return [
            'ticket' => $this->ticket,
            'portalUrl' => $this->portalUrl,
        ];
    }
}
