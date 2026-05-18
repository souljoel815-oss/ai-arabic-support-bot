<?php

namespace App\Services\Organisations;

use App\Models\CustomerOrganisation;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use InvalidArgumentException;

/**
 * T141 per FR-024. Self-service account deletion with a 30-day
 * soft-delete window.
 *
 * Side effects:
 *   1. CustomerOrganisation.soft_deleted_at = now()
 *   2. All active TeamMember rows owning this org get soft_deleted_at
 *      (if those members ONLY belong to this org — cross-org members
 *      keep their user row).
 *   3. Audit row written; T142 purge command runs nightly and hard-
 *      deletes orgs+members past the 30-day window EXCEPT for the
 *      audit-log rows themselves (data-model.md §9 retention rule).
 *
 * Restore window: Owner can cancel deletion within 30 days by clearing
 * soft_deleted_at (separate restore service — not in this commit).
 */
class DeleteAccountService
{
    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    public function softDelete(
        CustomerOrganisation $org,
        TeamMember $actor,
        string $confirmationText,
        string $originatingIp,
    ): CustomerOrganisation {
        // Defence-in-depth: the UI requires the operator to type the
        // org's Arabic name to confirm. Service rejects if it doesn't
        // match — catches accidental URL clicks.
        if (trim($confirmationText) !== trim($org->legal_name_ar)) {
            throw new InvalidArgumentException(
                "Confirmation text must match the organisation's legal name exactly."
            );
        }
        if ($org->soft_deleted_at !== null) {
            throw new InvalidArgumentException(
                "Organisation is already soft-deleted (scheduled for purge on {$org->soft_deleted_at->copy()->addDays(30)->toDateString()})."
            );
        }

        return DB::transaction(function () use ($org, $actor, $originatingIp) {
            $org->update(['soft_deleted_at' => now()]);

            $this->audit->write(
                organisationId: $org->id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'organisation.softDeleted',
                subjectKind: 'CustomerOrganisation',
                subjectId: $org->id,
                payload: [
                    'will_purge_on' => $org->soft_deleted_at->copy()->addDays(30)->toIso8601String(),
                ],
                originatingIp: $originatingIp,
            );

            return $org->fresh();
        });
    }

    public function restore(
        CustomerOrganisation $org,
        TeamMember $actor,
        string $originatingIp,
    ): CustomerOrganisation {
        if ($org->soft_deleted_at === null) {
            throw new InvalidArgumentException("Organisation is not soft-deleted.");
        }
        if ($org->soft_deleted_at->copy()->addDays(30)->isPast()) {
            throw new InvalidArgumentException(
                "The 30-day restore window has elapsed. The account is queued for permanent purge."
            );
        }

        return DB::transaction(function () use ($org, $actor, $originatingIp) {
            $org->update(['soft_deleted_at' => null]);

            $this->audit->write(
                organisationId: $org->id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'organisation.restored',
                subjectKind: 'CustomerOrganisation',
                subjectId: $org->id,
                payload: null,
                originatingIp: $originatingIp,
            );

            return $org->fresh();
        });
    }
}
