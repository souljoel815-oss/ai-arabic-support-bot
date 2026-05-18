<?php

namespace App\Mail;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;

/**
 * "Your trial ends in 7 days" reminder. Scheduled by a daily Artisan
 * command that walks every TeamMember whose
 *   email_verified_at + 23 days <= today < email_verified_at + 24 days
 * (the trial spans 30 days from verification per FR-030, and T-7 is
 * one day-window ahead — duplicate-send guard via an audit-log entry).
 */
class TrialEndingT7 extends LocalisedMailable implements ShouldQueue
{
    use Queueable;

    public function __construct(
        string $localePreference,
        public readonly string $displayName,
        public readonly string $convertUrl,
    ) {
        parent::__construct($localePreference);
    }

    protected function subjectAr(): string
    {
        return 'تبقى 7 أيام على انتهاء فترة التجربة';
    }

    protected function subjectEn(): string
    {
        return 'Your DaftarX trial ends in 7 days';
    }

    protected function viewName(): string
    {
        return 'emails.trial-ending-T-7';
    }

    protected function templateData(): array
    {
        return [
            'displayName' => $this->displayName,
            'convertUrl' => $this->convertUrl,
            'daysLeft' => 7,
        ];
    }
}
