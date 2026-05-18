<?php

namespace Tests\Feature\Flows;

use App\Models\CustomerOrganisation;
use App\Models\Invoice;
use App\Models\Licence;
use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Licences\LicenceSigningService;
use App\Services\Subscriptions\UpgradeTierService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Mockery;
use Tests\TestCase;

/**
 * T073 — FR-033 instant proration on upgrade.
 *
 * The UpgradeTierService must:
 *   - flip the tier immediately
 *   - retire all active licences with retired_reason = TierUpgraded
 *   - re-issue fresh licences at the new edition for the same HWIDs
 *   - create an Upgrade Invoice for the prorated remainder
 *   - reject "upgrades" to a lower or equal tier
 */
class TierUpgradeProrationTest extends TestCase
{
    use RefreshDatabase;

    protected function setUp(): void
    {
        parent::setUp();
        // Stub the signing service so the test doesn't read vendor-keys.json.
        $stub = Mockery::mock(LicenceSigningService::class);
        $stub->shouldReceive('sign')->andReturn([
            'payload' => ['fake' => 'payload'],
            'signature' => base64_encode(random_bytes(64)),
        ]);
        $this->app->instance(LicenceSigningService::class, $stub);
    }

    public function test_upgrade_flips_tier_and_creates_proration_invoice(): void
    {
        $actor = TeamMember::factory()->create();
        $org = CustomerOrganisation::factory()->create();
        $subscription = Subscription::factory()->create([
            'customer_organisation_id' => $org->id,
            'tier' => Subscription::TIER_SOLO,
            'billing_cadence' => 'Monthly',
            'current_period_start_at' => now()->subDays(10),
            'current_period_end_at' => now()->addDays(20),
        ]);
        $existingLicence = Licence::factory()->create([
            'subscription_id' => $subscription->id,
            'edition' => Subscription::TIER_SOLO,
        ]);

        [$updated, $invoice] = app(UpgradeTierService::class)->upgrade(
            $subscription,
            Subscription::TIER_SMB,
            $actor,
            '127.0.0.1',
        );

        $this->assertSame(Subscription::TIER_SMB, $updated->tier);
        $this->assertSame(Invoice::KIND_UPGRADE, $invoice->kind);
        $this->assertSame(Invoice::STATUS_PENDING, $invoice->status);
        $this->assertGreaterThan(0, $invoice->amount_egp_minor);

        // Old licence retired with TierUpgraded.
        $existingLicence->refresh();
        $this->assertNotNull($existingLicence->retired_at);
        $this->assertSame(Licence::REASON_TIER_UPGRADED, $existingLicence->retired_reason);

        // Fresh licence issued at new tier for same HWID.
        $reissued = Licence::query()
            ->where('subscription_id', $subscription->id)
            ->where('hwid', $existingLicence->hwid)
            ->whereNull('retired_at')
            ->first();
        $this->assertNotNull($reissued);
        $this->assertSame(Subscription::TIER_SMB, $reissued->edition);

        $this->assertDatabaseHas('audit_log_entries', ['verb' => 'subscription.upgraded']);
    }

    public function test_upgrade_rejects_downgrade_attempt(): void
    {
        $actor = TeamMember::factory()->create();
        $subscription = Subscription::factory()->create([
            'tier' => Subscription::TIER_ENTERPRISE,
        ]);

        $this->expectException(\InvalidArgumentException::class);
        $this->expectExceptionMessage('not an upgrade');

        app(UpgradeTierService::class)->upgrade(
            $subscription,
            Subscription::TIER_SMB,
            $actor,
            '127.0.0.1',
        );
    }

    public function test_upgrade_rejects_non_active_subscription(): void
    {
        $actor = TeamMember::factory()->create();
        $subscription = Subscription::factory()->create([
            'tier' => Subscription::TIER_SOLO,
            'status' => Subscription::STATUS_PAST_DUE,
        ]);

        $this->expectException(\InvalidArgumentException::class);

        app(UpgradeTierService::class)->upgrade(
            $subscription,
            Subscription::TIER_SMB,
            $actor,
            '127.0.0.1',
        );
    }
}
