<?php

namespace App\Services\Email;

use Illuminate\Contracts\Mail\Mailable;
use Illuminate\Mail\Mailer;
use Illuminate\Support\Facades\Log;
use Throwable;

/**
 * T031 per FR-015 + research §4. Thin wrapper around Laravel's Mail
 * facade that adds retry-on-transient-failure for the Resend SMTP path.
 * Laravel doesn't ship with Polly equivalents so we use a simple
 * exponential-backoff loop — 3 attempts at 1s, 2s, 4s. Anything that
 * still fails after the third attempt bubbles up.
 *
 * Production routes through Resend's SMTP submission server
 * (`smtp.resend.com:587`) per .env. Dev routes through the `log`
 * driver so emails land in `storage/logs/laravel-{date}.log` for
 * inspection without API keys.
 */
class ResendMailer
{
    private const MAX_ATTEMPTS = 3;

    public function __construct(private readonly Mailer $mailer)
    {
    }

    /**
     * Send a Mailable to a single recipient. The Mailable carries its
     * own to/cc/bcc/subject/body via its build() method; this wrapper
     * only adds the retry policy.
     */
    public function send(Mailable $mailable, string $toAddress, ?string $toName = null): void
    {
        $attempt = 0;
        $lastError = null;

        while ($attempt < self::MAX_ATTEMPTS) {
            $attempt++;
            try {
                $this->mailer->to($toAddress, $toName)->send($mailable);

                if ($attempt > 1) {
                    Log::info("ResendMailer succeeded for {$toAddress} on attempt {$attempt}");
                }
                return;
            } catch (Throwable $e) {
                $lastError = $e;
                Log::warning(
                    "ResendMailer attempt {$attempt}/".self::MAX_ATTEMPTS." failed for {$toAddress}: {$e->getMessage()}"
                );
                if ($attempt < self::MAX_ATTEMPTS) {
                    // Exponential backoff: 1s, 2s, 4s.
                    usleep(pow(2, $attempt - 1) * 1_000_000);
                }
            }
        }

        Log::error("ResendMailer giving up on {$toAddress} after ".self::MAX_ATTEMPTS." attempts");
        throw $lastError;
    }
}
