<?php

namespace App\Mail;

use App\Models\CustomerOrganisation;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;

/**
 * T130 — sent to a member after their membership is revoked by the
 * organisation owner. Tells them the access has been removed and gives
 * an "appeal via support" path if it was unintended.
 */
class MemberRemoved extends LocalisedMailable implements ShouldQueue
{
    use Queueable;

    public function __construct(
        string $localePreference,
        public readonly string $memberName,
        public readonly CustomerOrganisation $organisation,
        public readonly string $supportUrl,
    ) {
        parent::__construct($localePreference);
    }

    protected function subjectAr(): string
    {
        return "تم إلغاء عضويتك في {$this->organisation->legal_name_ar}";
    }

    protected function subjectEn(): string
    {
        return "Your access to {$this->organisation->legal_name_ar} was revoked";
    }

    protected function viewName(): string
    {
        return 'emails.organisation.member-removed';
    }

    protected function templateData(): array
    {
        return [
            'memberName' => $this->memberName,
            'organisation' => $this->organisation,
            'supportUrl' => $this->supportUrl,
        ];
    }
}
