<?php

namespace App\Mail;

use App\Models\CustomerOrganisation;
use App\Models\Invitation;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;

/**
 * T130 — invitation email containing the raw token-bearing accept link.
 * Locale falls back to the inviter's locale_preference because the
 * invitee likely doesn't have an account yet (no preference of their own).
 */
class OrganisationInvitation extends LocalisedMailable implements ShouldQueue
{
    use Queueable;

    public function __construct(
        string $localePreference,
        public readonly Invitation $invitation,
        public readonly CustomerOrganisation $organisation,
        public readonly string $acceptUrl,
        public readonly string $inviterName,
    ) {
        parent::__construct($localePreference);
    }

    protected function subjectAr(): string
    {
        return "دعوة للانضمام إلى {$this->organisation->legal_name_ar} على DaftarX";
    }

    protected function subjectEn(): string
    {
        return "Invitation to join {$this->organisation->legal_name_ar} on DaftarX";
    }

    protected function viewName(): string
    {
        return 'emails.organisation.invitation';
    }

    protected function templateData(): array
    {
        return [
            'invitation' => $this->invitation,
            'organisation' => $this->organisation,
            'acceptUrl' => $this->acceptUrl,
            'inviterName' => $this->inviterName,
        ];
    }
}
