<?php

namespace App\Mail;

use App\Models\SupportTicket;
use App\Models\SupportTicketReply;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;

/**
 * T115 — sent to the customer whenever a vendor-staff member adds a
 * reply to one of their tickets (per ReplyTicketService).
 */
class TicketReplied extends LocalisedMailable implements ShouldQueue
{
    use Queueable;

    public function __construct(
        string $localePreference,
        public readonly SupportTicket $ticket,
        public readonly SupportTicketReply $reply,
        public readonly string $portalUrl,
    ) {
        parent::__construct($localePreference);
    }

    protected function subjectAr(): string
    {
        return "رد جديد على طلب الدعم — #{$this->ticket->id}";
    }

    protected function subjectEn(): string
    {
        return "New reply on support ticket — #{$this->ticket->id}";
    }

    protected function viewName(): string
    {
        return 'emails.support.ticket-replied';
    }

    protected function templateData(): array
    {
        return [
            'ticket' => $this->ticket,
            'reply' => $this->reply,
            'portalUrl' => $this->portalUrl,
        ];
    }
}
