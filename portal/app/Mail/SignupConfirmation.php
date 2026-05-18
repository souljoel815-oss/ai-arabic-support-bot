<?php

namespace App\Mail;

/**
 * Sent immediately after `POST /register` succeeds. Carries the email-
 * verification URL (signed, 60-minute TTL — handled by Laravel's
 * MustVerifyEmail trait). Synchronous send (no queue) so the user sees
 * the "check your inbox" flash message backed by a real delivery.
 */
class SignupConfirmation extends LocalisedMailable
{
    public function __construct(
        string $localePreference,
        public readonly string $displayName,
        public readonly string $verificationUrl,
    ) {
        parent::__construct($localePreference);
    }

    protected function subjectAr(): string
    {
        return 'فعّل حسابك في دفترx';
    }

    protected function subjectEn(): string
    {
        return 'Confirm your DaftarX account';
    }

    protected function viewName(): string
    {
        return 'emails.signup-confirmation';
    }

    protected function templateData(): array
    {
        return [
            'displayName' => $this->displayName,
            'verificationUrl' => $this->verificationUrl,
        ];
    }
}
