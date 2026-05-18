<?php

namespace App\Services\Organisations;

use App\Models\OrganisationMembership;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use RuntimeException;

/**
 * T124 per FR-022. Removes a team member from an organisation.
 *
 * Atomic:
 *   1. Refuse if the membership being removed is the only remaining
 *      active Owner (T119 invariant — prevents the org from becoming
 *      inaccessible).
 *   2. Set revoked_at = now() on the OrganisationMembership row.
 *   3. Bump security_stamp_version so any cached session token that
 *      pre-dates the bump becomes invalid on next check (the
 *      OrganisationScope middleware already calls
 *      "user has ANY active membership?" — once we revoke this one
 *      AND the user has no other active membership, that check
 *      fails → bounces to login. Effective session-kill within
 *      5 min per FR-022 since OrganisationScope runs on every
 *      authenticated request).
 *   4. Write member.removed audit row.
 *
 * Note: this does NOT delete the TeamMember row itself — the user
 * can still log in if they belong to other organisations (cross-org
 * membership is supported by the data-model for accounting firms).
 */
class RemoveMemberService
{
    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    public function remove(
        OrganisationMembership $membershipToRemove,
        TeamMember $actor,
        string $originatingIp,
    ): OrganisationMembership {
        if ($membershipToRemove->revoked_at !== null) {
            throw new RuntimeException("Membership is already revoked.");
        }

        // T119 invariant: don't remove the only remaining active Owner.
        if ($membershipToRemove->role === OrganisationMembership::ROLE_OWNER) {
            $otherActiveOwnerCount = OrganisationMembership::query()
                ->where('customer_organisation_id', $membershipToRemove->customer_organisation_id)
                ->where('role', OrganisationMembership::ROLE_OWNER)
                ->whereNull('revoked_at')
                ->whereNotNull('accepted_at')
                ->where('id', '!=', $membershipToRemove->id)
                ->count();

            if ($otherActiveOwnerCount === 0) {
                throw new RuntimeException(
                    "Cannot remove the only remaining Owner. Promote another member to Owner first."
                );
            }
        }

        return DB::transaction(function () use ($membershipToRemove, $actor, $originatingIp) {
            $membershipToRemove->update([
                'revoked_at' => now(),
                'security_stamp_version' => $membershipToRemove->security_stamp_version + 1,
            ]);

            $this->audit->write(
                organisationId: $membershipToRemove->customer_organisation_id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'member.removed',
                subjectKind: 'OrganisationMembership',
                subjectId: $membershipToRemove->id,
                payload: [
                    'removed_team_member_id' => $membershipToRemove->team_member_id,
                    'role_at_removal' => $membershipToRemove->role,
                ],
                originatingIp: $originatingIp,
            );

            return $membershipToRemove->fresh();
        });
    }
}
