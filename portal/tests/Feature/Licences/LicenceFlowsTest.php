<?php

namespace Tests\Feature\Licences;

use App\Models\AuditLogEntry;
use App\Models\CustomerOrganisation;
use App\Models\Licence;
use App\Models\OrganisationMembership;
use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Licences\ActivatePaidLicenceService;
use App\Services\Licences\TransferLicenceService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\Concerns\WithTestVendorKeys;
use Tests\TestCase;

/**
 * T056-T058 (integration). Activate-paid + transfer flow tests against
 * the real services + the in-memory SQLite DB. Full contract tests
 * (per contracts/activate-paid-licence.md + transfer-licence.md) land
 * here too — the markdown contracts enumerate exact assertions.
 */
class LicenceFlowsTest extends TestCase
{
    use RefreshDatabase;
    use WithTestVendorKeys;

    protected function setUp(): void
    {
        parent::setUp();
        $this->setUpTestVendorKeys();
    }

    protected function tearDown(): void
    {
        $this->tearDownTestVendorKeys();
        parent::tearDown();
    }

    public function test_activate_signs_token_and_writes_audit_row(): void
    {
        $service = app(ActivatePaidLicenceService::class);
        [$user, $subscription] = $this->seedOrgWithOwner();

        $licence = $service->activate(
            $subscription,
            'A1B2-C3D4-E5F6-7890',
            $user,
            '127.0.0.1',
        );

        $this->assertNotNull($licence->id);
        $this->assertSame('A1B2-C3D4-E5F6-7890', $licence->hwid);
        $this->assertSame($subscription->tier, $licence->edition);
        $this->assertNull($licence->retired_at);
        $this->assertTrue(strlen($licence->signed_token_base64) > 100);

        $this->assertDatabaseHas('audit_log_entries', [
            'customer_organisation_id' => $subscription->customer_organisation_id,
            'verb' => 'licence.activated',
            'subject_id' => $licence->id,
        ]);
    }

    public function test_activate_rejects_malformed_hwid(): void
    {
        $service = app(ActivatePaidLicenceService::class);
        [$user, $subscription] = $this->seedOrgWithOwner();

        $this->expectException(\InvalidArgumentException::class);
        $service->activate($subscription, 'not-a-hwid', $user, '127.0.0.1');
    }

    public function test_activate_rejects_collision_with_another_active_licence(): void
    {
        $service = app(ActivatePaidLicenceService::class);
        [$userA, $subA] = $this->seedOrgWithOwner();
        [$userB, $subB] = $this->seedOrgWithOwner();

        // Customer A activates the hwid first.
        $service->activate($subA, 'CAFE-BABE-DEAD-BEEF', $userA, '1.1.1.1');

        // Customer B tries the same hwid → 409-equivalent error.
        $this->expectException(\RuntimeException::class);
        $service->activate($subB, 'CAFE-BABE-DEAD-BEEF', $userB, '2.2.2.2');
    }

    public function test_transfer_retires_old_signs_new_and_writes_two_audit_rows(): void
    {
        $activator = app(ActivatePaidLicenceService::class);
        $transferer = app(TransferLicenceService::class);
        [$user, $subscription] = $this->seedOrgWithOwner();

        $original = $activator->activate($subscription, 'AAAA-BBBB-CCCC-DDDD', $user, '127.0.0.1');

        $replacement = $transferer->transfer(
            $original,
            'EEEE-FFFF-1111-2222',
            $user,
            '127.0.0.1',
        );

        $original->refresh();
        $this->assertNotNull($original->retired_at, 'old licence should be retired');
        $this->assertSame(Licence::REASON_TRANSFERRED, $original->retired_reason);

        $this->assertSame('EEEE-FFFF-1111-2222', $replacement->hwid);
        $this->assertSame($original->edition, $replacement->edition);
        $this->assertNull($replacement->retired_at);

        // Two cross-referenced audit rows (one retired + one activated).
        $rows = AuditLogEntry::query()
            ->where('customer_organisation_id', $subscription->customer_organisation_id)
            ->whereIn('subject_id', [$original->id, $replacement->id])
            ->orderBy('occurred_at')
            ->get();

        $this->assertSame(3, $rows->count(), 'expected the initial activate + retire + new activate audit rows');
        $verbs = $rows->pluck('verb')->all();
        $this->assertSame(['licence.activated', 'licence.retired', 'licence.activated'], $verbs);
    }

    public function test_transfer_rejects_collision_with_another_active_licence(): void
    {
        $activator = app(ActivatePaidLicenceService::class);
        $transferer = app(TransferLicenceService::class);
        [$userA, $subA] = $this->seedOrgWithOwner();
        [$userB, $subB] = $this->seedOrgWithOwner();

        // Customer A activates A1.
        $licenceA = $activator->activate($subA, 'A111-A111-A111-A111', $userA, '1.1.1.1');
        // Customer B activates B1.
        $licenceB = $activator->activate($subB, 'B222-B222-B222-B222', $userB, '2.2.2.2');

        // Customer A tries to transfer their licence to B1 (collision).
        $this->expectException(\RuntimeException::class);
        $transferer->transfer($licenceA, 'B222-B222-B222-B222', $userA, '1.1.1.1');
    }

    public function test_transfer_rejects_same_hwid(): void
    {
        $activator = app(ActivatePaidLicenceService::class);
        $transferer = app(TransferLicenceService::class);
        [$user, $subscription] = $this->seedOrgWithOwner();

        $licence = $activator->activate($subscription, 'C0DE-C0DE-C0DE-C0DE', $user, '127.0.0.1');

        $this->expectException(\InvalidArgumentException::class);
        $transferer->transfer($licence, 'C0DE-C0DE-C0DE-C0DE', $user, '127.0.0.1');
    }

    public function test_signed_envelope_round_trips_with_the_verifier(): void
    {
        // Sign here, verify here — proves the on-prem-compatible
        // sodium signing path works end-to-end against the same
        // canonical-bytes serialization.
        $service = app(ActivatePaidLicenceService::class);
        $signer = app(\App\Services\Licences\LicenceSigningService::class);
        [$user, $subscription] = $this->seedOrgWithOwner();

        $licence = $service->activate($subscription, '1234-5678-90AB-CDEF', $user, '127.0.0.1');

        // Decode the saved envelope + re-verify the signature.
        $envelopeJson = base64_decode($licence->signed_token_base64, strict: true);
        $envelope = json_decode($envelopeJson, true);

        $this->assertTrue($signer->verify($envelope), 'signed envelope must verify against the same vendor public key');
    }

    /**
     * @return array{0: TeamMember, 1: Subscription}
     */
    private function seedOrgWithOwner(): array
    {
        $org = CustomerOrganisation::factory()->create();
        $user = TeamMember::factory()->create();
        OrganisationMembership::create([
            'customer_organisation_id' => $org->id,
            'team_member_id' => $user->id,
            'role' => OrganisationMembership::ROLE_OWNER,
            'invited_at' => now(),
            'accepted_at' => now(),
        ]);
        $subscription = Subscription::factory()->for($org, 'customerOrganisation')->create();
        return [$user, $subscription];
    }
}
