<?php

namespace App\Services\Organisations;

use App\Models\Invitation;
use App\Models\OrganisationMembership;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;
use RuntimeException;

/**
 * T123 per contracts/invite-member.md "accept" sibling endpoint.
 *
 * Validates the raw token (hash + lookup + expiry + single-use),
 * creates a fresh TeamMember if the email isn't already a portal
 * user, marks the Invitation accepted, and inserts the
 * OrganisationMembership with the invitation's role. All in a single
 * DB transaction so a half-created state can never exist.
 *
 * The two error modes the contract distinguishes:
 *   400 expired — expires_at < now()
 *   410 already_used — accepted_at already populated
 * Throws RuntimeException with a distinguishing message either way;
 * the caller maps it to the HTTP status.
 */
class AcceptInvitationService
{
    public const ERROR_EXPIRED = 'expired';
    public const ERROR_ALREADY_USED = 'already_used';
    public const ERROR_INVALID = 'invalid';
    public const ERROR_EMAIL_TAKEN = 'email_taken_other_org';

    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    /**
     * @return array{0: TeamMember, 1: OrganisationMembership}
     */
    public function accept(
        string $rawToken,
        string $plainPassword,
        ?string $displayName,
        string $originatingIp,
    ): array {
        $tokenHash = Invitation::hashToken($rawToken);
        $invitation = Invitation::query()
            ->where('token_hash', $tokenHash)
            ->first();

        if ($invitation === null) {
            throw new RuntimeException(self::ERROR_INVALID);
        }
        if ($invitation->isAccepted()) {
            throw new RuntimeException(self::ERROR_ALREADY_USED);
        }
        if ($invitation->isExpired()) {
            throw new RuntimeException(self::ERROR_EXPIRED);
        }

        return DB::transaction(function () use ($invitation, $plainPassword, $displayName, $originatingIp) {
            // Find OR create the TeamMember by email.
            $teamMember = TeamMember::query()->where('email', $invitation->email)->first();

            if ($teamMember === null) {
                $teamMember = TeamMember::create([
                    'email' => $invitation->email,
                    'display_name' => $displayName ?? $invitation->display_name,
                    'password' => Hash::make($plainPassword),
                    'locale_preference' => $invitation->locale_preference,
                ]);
                // The invitation click IS the email verification — the
                // invitee proved they own the email by following the
                // emailed link. markEmailAsVerified() bypasses the
                // $fillable contract for this protected column.
                $teamMember->markEmailAsVerified();
            }

            $membership = OrganisationMembership::create([
                'customer_organisation_id' => $invitation->customer_organisation_id,
                'team_member_id' => $teamMember->id,
                'role' => $invitation->role,
                'invited_at' => $invitation->created_at,
                'accepted_at' => now(),
            ]);

            $invitation->update(['accepted_at' => now()]);

            $this->audit->write(
                organisationId: $invitation->customer_organisation_id,
                actorTeamMemberId: $teamMember->id,
                actorDisplayNameSnapshot: $teamMember->display_name ?? $teamMember->email,
                verb: 'member.accepted',
                subjectKind: 'Invitation',
                subjectId: $invitation->id,
                payload: [
                    'email' => $invitation->email,
                    'role' => $invitation->role,
                    'is_new_team_member' => $teamMember->wasRecentlyCreated,
                ],
                originatingIp: $originatingIp,
            );

            return [$teamMember, $membership];
        });
    }
}
