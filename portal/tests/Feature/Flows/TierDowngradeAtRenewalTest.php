<?php

namespace Tests\Feature\Flows;

use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Subscriptions\ScheduleDowngradeService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

/**
 * T074 — FR-033 downgrade is scheduled, not immediate.
 *
 * ScheduleDowngradeService stores `pending_tier_change_to` on the
 * Subscription row. The renewal job (separate) picks it up next cycle
 * and flips the tier + issues the new lower-tier invoice. Customer
 * keeps the higher-tier features for the rest of the period they
 * already paid for. NO mid-period refund.
 */
class TierDowngradeAtRenewalTest extends TestCase
{
    use RefreshDatabase;

    public function test_downgrade_sets_pending_change_and_keeps_current_tier(): void
    {
        $actor = TeamMember::factory()->create();
        $subscription = Subscription::factory()->create([
            'tier' => Subscription::TIER_ENTERPRISE,
            'pending_tier_change_to' => null,
        ]);

        $updated = app(ScheduleDowngradeService::class)->schedule(
            $subscription,
            Subscription::TIER_SMB,
            $actor,
            '127.0.0.1',
        );

        // Current tier untouched — they keep Enterprise this period.
        $this->assertSame(Subscription::TIER_ENTERPRISE, $updated->tier);
        // Pending change recorded for the renewal job.
        $this->assertSame(Subscription::TIER_SMB, $updated->pending_tier_change_to);

        $this->assertDatabaseHas('audit_log_entries', ['verb' => 'subscription.downgrade_scheduled']);
    }

    public function test_downgrade_rejects_upgrade_attempt(): void
    {
        $actor = TeamMember::factory()->create();
        $subscription = Subscription::factory()->create([
            'tier' => Subscription::TIER_SOLO,
        ]);

        $this->expectException(\InvalidArgumentException::class);
        $this->expectExceptionMessage('not a downgrade');

        app(ScheduleDowngradeService::class)->schedule(
            $subscription,
            Subscription::TIER_SMB,
            $actor,
            '127.0.0.1',
        );
    }

    public function test_downgrade_overwrites_previous_pending_change(): void
    {
        $actor = TeamMember::factory()->create();
        $subscription = Subscription::factory()->create([
            'tier' => Subscription::TIER_ENTERPRISE,
            'pending_tier_change_to' => Subscription::TIER_SMB,
        ]);

        $updated = app(ScheduleDowngradeService::class)->schedule(
            $subscription,
            Subscription::TIER_SOLO,
            $actor,
            '127.0.0.1',
        );

        $this->assertSame(Subscription::TIER_SOLO, $updated->pending_tier_change_to);
    }
}
