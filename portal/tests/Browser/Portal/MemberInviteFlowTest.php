<?php

namespace Tests\Browser\Portal;

/**
 * T120 — Dusk e2e for SC-007 (invite-click-to-dashboard < 3 min).
 *
 * Scaffold only — see SignupToDownloadFlowTest for Dusk setup notes.
 *
 * Steps:
 *   1. Owner visits /portal/organisation, submits invite form
 *   2. Extract raw token from the test-mode mail queue
 *   3. Open invitation accept URL in a second browser context
 *   4. Fill display_name + password, submit
 *   5. Assert redirect to /portal dashboard within 180 seconds
 */
class MemberInviteFlowTest /* extends DuskTestCase */
{
    public function test_invite_to_dashboard_under_3_minutes(): void
    {
        $this->markTestSkipped(
            'T120 — requires laravel/dusk + Chrome installation.'
        );
    }
}
