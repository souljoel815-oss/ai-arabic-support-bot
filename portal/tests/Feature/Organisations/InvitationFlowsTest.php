<?php

namespace Tests\Feature\Organisations;

use App\Models\CustomerOrganisation;
use App\Models\Invitation;
use App\Models\OrganisationMembership;
use App\Models\TeamMember;
use App\Services\Organisations\AcceptInvitationService;
use App\Services\Organisations\InviteMemberService;
use App\Services\Organisations\RemoveMemberService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

/**
 * T117-T119 — Contract assertions for invitation flow per
 * contracts/invite-member.md + FR-020 + FR-022.
 */
class InvitationFlowsTest extends TestCase
{
    use RefreshDatabase;

    public function test_invite_persists_invitation_with_hashed_token_and_returns_raw_token_once(): void
    {
        [$owner, $org] = $this->seedOwner();
        $service = app(InviteMemberService::class);

        [$invitation, $rawToken] = $service->invite(
            org: $org,
            inviter: $owner,
            inviteeEmail: 'new@member.eg',
            role: OrganisationMembership::ROLE_BILLING_ADMIN,
            displayName: 'Mariam',
            localePreference: 'ar-EG',
            originatingIp: '127.0.0.1',
        );

        // T117 #11 — raw token NEVER stored in DB, only its SHA-256 hash.
        $this->assertNotEquals($rawToken, $invitation->token_hash);
        $this->assertSame(64, strlen($invitation->token_hash), 'token_hash is SHA-256 hex (64 chars)');
        $this->assertSame(Invitation::hashToken($rawToken), $invitation->token_hash);

        // Expiry default = 7 days; allow a 5-min delta for clock drift in tests.
        $expectedExpiry = now()->addDays(7);
        $this->assertTrue(
            abs($invitation->expires_at->diffInMinutes($expectedExpiry, absolute: true)) < 5,
            "expires_at should be ~7 days from now, got {$invitation->expires_at}"
        );

        // Audit row.
        $this->assertDatabaseHas('audit_log_entries', [
            'verb' => 'member.invited',
            'subject_id' => $invitation->id,
        ]);
    }

    public function test_invite_rejects_non_owner(): void
    {
        $org = CustomerOrganisation::factory()->create();
        $user = TeamMember::factory()->create();
        OrganisationMembership::create([
            'customer_organisation_id' => $org->id,
            'team_member_id' => $user->id,
            'role' => OrganisationMembership::ROLE_BILLING_ADMIN,
            'invited_at' => now(),
            'accepted_at' => now(),
        ]);

        $this->expectException(\RuntimeException::class);
        app(InviteMemberService::class)->invite(
            org: $org,
            inviter: $user,
            inviteeEmail: 'x@x.eg',
            role: OrganisationMembership::ROLE_READ_ONLY,
            displayName: null,
            localePreference: 'ar-EG',
            originatingIp: '127.0.0.1',
        );
    }

    public function test_invite_rejects_already_member_email(): void
    {
        [$owner, $org] = $this->seedOwner();

        // Seed an existing active member.
        $existing = TeamMember::factory()->create(['email' => 'dup@test.eg']);
        OrganisationMembership::create([
            'customer_organisation_id' => $org->id,
            'team_member_id' => $existing->id,
            'role' => OrganisationMembership::ROLE_READ_ONLY,
            'invited_at' => now(),
            'accepted_at' => now(),
        ]);

        $this->expectException(\RuntimeException::class);
        app(InviteMemberService::class)->invite(
            org: $org,
            inviter: $owner,
            inviteeEmail: 'dup@test.eg',
            role: OrganisationMembership::ROLE_BILLING_ADMIN,
            displayName: null,
            localePreference: 'ar-EG',
            originatingIp: '127.0.0.1',
        );
    }

    public function test_invite_rejects_pending_invitation_already_exists(): void
    {
        [$owner, $org] = $this->seedOwner();
        app(InviteMemberService::class)->invite(
            org: $org, inviter: $owner,
            inviteeEmail: 'pending@x.eg',
            role: OrganisationMembership::ROLE_READ_ONLY,
            displayName: null, localePreference: 'ar-EG', originatingIp: '127.0.0.1',
        );

        $this->expectException(\RuntimeException::class);
        app(InviteMemberService::class)->invite(
            org: $org, inviter: $owner,
            inviteeEmail: 'pending@x.eg',
            role: OrganisationMembership::ROLE_READ_ONLY,
            displayName: null, localePreference: 'ar-EG', originatingIp: '127.0.0.1',
        );
    }

    public function test_accept_creates_team_member_and_membership_atomically(): void
    {
        [$owner, $org] = $this->seedOwner();
        [$invitation, $rawToken] = app(InviteMemberService::class)->invite(
            $org, $owner, 'fresh@x.eg', OrganisationMembership::ROLE_BILLING_ADMIN,
            null, 'ar-EG', '127.0.0.1',
        );

        [$user, $membership] = app(AcceptInvitationService::class)->accept(
            rawToken: $rawToken,
            plainPassword: 'BrandN3wPa$$word!',
            displayName: 'Mariam Saleh',
            originatingIp: '127.0.0.1',
        );

        $this->assertSame('fresh@x.eg', $user->email);
        $this->assertNotNull($user->email_verified_at, 'invitation link doubles as email verification');
        $this->assertSame(OrganisationMembership::ROLE_BILLING_ADMIN, $membership->role);
        $this->assertNotNull($membership->accepted_at);
        $this->assertSame($invitation->id, $invitation->fresh()->id);
        $this->assertNotNull($invitation->fresh()->accepted_at, 'invitation marked accepted');
    }

    public function test_accept_rejects_expired_invitation(): void
    {
        [$owner, $org] = $this->seedOwner();
        [$invitation, $rawToken] = app(InviteMemberService::class)->invite(
            $org, $owner, 'exp@x.eg', OrganisationMembership::ROLE_READ_ONLY,
            null, 'ar-EG', '127.0.0.1',
        );
        $invitation->update(['expires_at' => now()->subDay()]);

        $this->expectExceptionMessage(AcceptInvitationService::ERROR_EXPIRED);
        app(AcceptInvitationService::class)->accept($rawToken, 'WhateverPass1!', null, '127.0.0.1');
    }

    public function test_accept_rejects_second_use_of_same_token(): void
    {
        [$owner, $org] = $this->seedOwner();
        [$_invitation, $rawToken] = app(InviteMemberService::class)->invite(
            $org, $owner, 'twice@x.eg', OrganisationMembership::ROLE_READ_ONLY,
            null, 'ar-EG', '127.0.0.1',
        );
        app(AcceptInvitationService::class)->accept($rawToken, 'FirstPass123!', 'Name', '127.0.0.1');

        $this->expectExceptionMessage(AcceptInvitationService::ERROR_ALREADY_USED);
        app(AcceptInvitationService::class)->accept($rawToken, 'SecondPass123!', 'Other', '127.0.0.1');
    }

    public function test_remove_member_sets_revoked_at_and_bumps_security_stamp(): void
    {
        [$owner, $org] = $this->seedOwner();
        $target = TeamMember::factory()->create();
        $membership = OrganisationMembership::create([
            'customer_organisation_id' => $org->id,
            'team_member_id' => $target->id,
            'role' => OrganisationMembership::ROLE_BILLING_ADMIN,
            'invited_at' => now(),
            'accepted_at' => now(),
        ]);

        $oldStamp = $membership->security_stamp_version;
        $removed = app(RemoveMemberService::class)->remove($membership, $owner, '127.0.0.1');

        $this->assertNotNull($removed->revoked_at);
        $this->assertGreaterThan($oldStamp, $removed->security_stamp_version);
        $this->assertDatabaseHas('audit_log_entries', [
            'verb' => 'member.removed',
            'subject_id' => $membership->id,
        ]);
    }

    public function test_remove_member_refuses_to_remove_only_remaining_owner(): void
    {
        // T119 invariant.
        [$owner, $org] = $this->seedOwner();
        $ownerMembership = OrganisationMembership::query()
            ->where('customer_organisation_id', $org->id)
            ->where('team_member_id', $owner->id)
            ->where('role', OrganisationMembership::ROLE_OWNER)
            ->firstOrFail();

        $this->expectException(\RuntimeException::class);
        app(RemoveMemberService::class)->remove($ownerMembership, $owner, '127.0.0.1');
    }

    public function test_remove_member_allows_removing_owner_when_another_owner_remains(): void
    {
        [$owner1, $org] = $this->seedOwner();
        $owner2 = TeamMember::factory()->create();
        $owner2Membership = OrganisationMembership::create([
            'customer_organisation_id' => $org->id,
            'team_member_id' => $owner2->id,
            'role' => OrganisationMembership::ROLE_OWNER,
            'invited_at' => now(),
            'accepted_at' => now(),
        ]);

        $removed = app(RemoveMemberService::class)->remove($owner2Membership, $owner1, '127.0.0.1');
        $this->assertNotNull($removed->revoked_at);
    }

    /**
     * @return array{0: TeamMember, 1: CustomerOrganisation}
     */
    private function seedOwner(): array
    {
        $user = TeamMember::factory()->create();
        $org = CustomerOrganisation::factory()->create();
        OrganisationMembership::create([
            'customer_organisation_id' => $org->id,
            'team_member_id' => $user->id,
            'role' => OrganisationMembership::ROLE_OWNER,
            'invited_at' => now(),
            'accepted_at' => now(),
        ]);
        return [$user, $org];
    }
}
