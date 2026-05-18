<?php

namespace App\Mail;

use Illuminate\Mail\Mailable;

/**
 * T095 base class for every portal-issued email. Per FR-015 + research §4:
 *
 *   1. Bilingual via the recipient's `locale_preference`. Subclasses
 *      provide `subjectAr()` / `subjectEn()` + a single Blade view that
 *      switches on `$locale` to render the right copy.
 *   2. Queueable so a slow mail submission doesn't block the request
 *      (`ShouldQueue` is opt-in per subclass — heavier emails like
 *      receipts attach a PDF and want the queue; transactional sends
 *      like signup verification stay synchronous so the user sees the
 *      flow complete).
 *   3. From-address read from `config('mail.from')` so non-prod
 *      environments don't accidentally email customers from
 *      hello@example.com.
 */
abstract class LocalisedMailable extends Mailable
{
    public function __construct(
        public readonly string $localePreference = 'ar-EG',
    ) {
    }

    abstract protected function subjectAr(): string;
    abstract protected function subjectEn(): string;
    abstract protected function viewName(): string;

    /**
     * @return array<string,mixed>  Extra view data merged on top of the
     *                              defaults provided by this class.
     */
    protected function templateData(): array
    {
        return [];
    }

    public function build(): self
    {
        $isArabic = str_starts_with($this->localePreference, 'ar');

        return $this->subject($isArabic ? $this->subjectAr() : $this->subjectEn())
            ->view($this->viewName(), array_merge(
                [
                    'isArabic' => $isArabic,
                    'dir' => $isArabic ? 'rtl' : 'ltr',
                    'lang' => $isArabic ? 'ar-EG' : 'en-US',
                ],
                $this->templateData(),
            ));
    }
}
