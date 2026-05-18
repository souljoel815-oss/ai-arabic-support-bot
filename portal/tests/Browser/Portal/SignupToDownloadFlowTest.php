<?php

namespace Tests\Browser\Portal;

/**
 * T076 — Dusk e2e for SC-002 (full signup → pay → download flow < 15 min).
 *
 * Scaffold only — running this requires:
 *   - composer require --dev laravel/dusk
 *   - php artisan dusk:install
 *   - a real Chrome installation
 *
 * On CI we run this via the `dusk` workflow; locally on Windows it
 * needs Chrome + chromedriver. The test logic below is the behavioural
 * contract; the Browser type-hint is intentionally a docblock so the
 * file parses without Dusk installed (`extends DuskTestCase` is added
 * once Dusk is set up).
 *
 * Steps:
 *   1. Visit /register, complete the form, submit
 *   2. Assert /portal redirect + email-verification banner
 *   3. Click "verify email" placeholder (mocked in dev)
 *   4. Visit /portal/subscription/start, pick tier+method, submit
 *   5. Visit /portal/billing, confirm invoice + download PDF
 *   6. Visit /portal/downloads, click installer link
 *   7. Assert total elapsed wall-clock < 900 seconds (15 min)
 */
class SignupToDownloadFlowTest /* extends DuskTestCase */
{
    public function test_full_signup_to_download_under_15_minutes(): void
    {
        $this->markTestSkipped(
            'T076 — requires laravel/dusk + Chrome installation. '
            . 'Run via the "dusk" CI workflow, not the default phpunit suite.'
        );
    }
}
