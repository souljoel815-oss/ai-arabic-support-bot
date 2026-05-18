<?php

namespace App\Mail;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;

/**
 * "Your trial ends tomorrow" final reminder. T-1 sibling of TrialEndingT7
 * — same scheduling mechanism (daily Artisan, +29 days window).
 */
class TrialEndingT1 extends LocalisedMailable implements ShouldQueue
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
        return 'فترة التجربة تنتهي غدًا';
    }

    protected function subjectEn(): string
    {
        return 'Your DaftarX trial ends tomorrow';
    }

    protected function viewName(): string
    {
        return 'emails.trial-ending-T-1';
    }

    protected function templateData(): array
    {
        return [
            'displayName' => $this->displayName,
            'convertUrl' => $this->convertUrl,
            'daysLeft' => 1,
        ];
    }
}
