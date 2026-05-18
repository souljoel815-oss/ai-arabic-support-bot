<?php

namespace App\Services\Organisations;

use App\Models\CustomerOrganisation;
use App\Models\Invitation;
use App\Models\OrganisationMembership;
use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Str;
use InvalidArgumentException;
use RuntimeException;

/**
 * T122 per contracts/invite-member.md + FR-020.
 *
 * Generates a 32-byte cryptographically random token, stores its
 * SHA-256 hash, builds the invitation URL using the RAW token
 * (base64url-encoded), and returns both objects so the caller can
 * dispatch the email (the raw token isn't kept after this method
 * returns).
 *
 * Pre-conditions enforced here:
 *   - Inviter must be an Owner of the target org.
 *   - Email must not already be an active member.
 *   - No pending (non-expired, non-accepted) invitation already exists.
 *
 * Rate limiting (10 invitations / hour / org) is enforced at the
 * route level via Laravel's throttle middleware (cleaner than
 * coupling it to the service).
 */
class InviteMemberService
{
    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    /**
     * @return array{0: Invitation, 1: string} the persisted Invitation
     *                                          + the RAW token (use it
     *                                          once for the email URL
     *                                          then discard).
     */
    public function invite(
        CustomerOrganisation $org,
        TeamMember $inviter,
        string $inviteeEmail,
        string $role,
        ?string $displayName,
        string $localePreference,
        string $originatingIp,
    ): array {
        if (! in_array($role, Invitation::ROLES, true)) {
            throw new InvalidArgumentException("Unknown role: {$role}");
        }
        if (! filter_var($inviteeEmail, FILTER_VALIDATE_EMAIL)) {
            throw new InvalidArgumentException("Email format invalid.");
        }
        if (! $this->isOwner($inviter, $org)) {
            throw new RuntimeException("Only Owners can invite members.");
        }

        $existingMember = OrganisationMembership::query()
            ->where('customer_organisation_id', $org->id)
            ->whereNull('revoked_at')
            ->whereNotNull('accepted_at')
            ->whereExists(function ($q) use ($inviteeEmail) {
                $q->select(DB::raw(1))
                    ->from('team_members')
                    ->whereColumn('team_members.id', 'organisation_memberships.team_member_id')
                    ->where('team_members.email', $inviteeEmail);
            })
            ->exists();

        if ($existingMember) {
            throw new RuntimeException("This email is already an active member of the organisation.");
        }

        $pendingInvitation = Invitation::query()
            ->where('customer_organisation_id', $org->id)
            ->where('email', $inviteeEmail)
            ->whereNull('accepted_at')
            ->where('expires_at', '>', now())
            ->exists();

        if ($pendingInvitation) {
            throw new RuntimeException("An open invitation for this email already exists; resend or revoke first.");
        }

        // Tier-cap guard — block the invitation if it would push the
        // organisation past its subscription's user limit (Subscription::MAX_USERS).
        // Counts distinct active TeamMembers across the org plus any
        // open invitations (those reserve a seat until accepted or
        // expired). Firm / Enterprise tiers have null caps and skip
        // the check.
        $activeSubscription = Subscription::query()
            ->where('customer_organisation_id', $org->id)
            ->where('status', Subscription::STATUS_ACTIVE)
            ->first();

        if ($activeSubscription !== null) {
            $cap = Subscription::userCapFor($activeSubscription->tier);
            if ($cap !== PHP_INT_MAX) {
                $activeMembers = OrganisationMembership::query()
                    ->where('customer_organisation_id', $org->id)
                    ->whereNull('revoked_at')
                    ->whereNotNull('accepted_at')
                    ->distinct('team_member_id')
                    ->count('team_member_id');
                $openInvitations = Invitation::query()
                    ->where('customer_organisation_id', $org->id)
                    ->whereNull('accepted_at')
                    ->where('expires_at', '>', now())
                    ->count();
                if ($activeMembers + $openInvitations >= $cap) {
                    throw new RuntimeException(
                        "User cap reached for tier {$activeSubscription->tier} ({$cap} users). "
                        . "Revoke an existing member or upgrade your subscription before inviting more."
                    );
                }
            }
        }

        $rawToken = $this->generateRawToken();
        $tokenHash = Invitation::hashToken($rawToken);

        return DB::transaction(function () use ($org, $inviter, $inviteeEmail, $role, $displayName, $localePreference, $originatingIp, $rawToken, $tokenHash) {
            $invitation = Invitation::create([
                'customer_organisation_id' => $org->id,
                'email' => $inviteeEmail,
                'role' => $role,
                'display_name' => $displayName,
                'locale_preference' => $localePreference,
                'token_hash' => $tokenHash,
                'invited_by_team_member_id' => $inviter->id,
                'expires_at' => now()->addDays(Invitation::DEFAULT_EXPIRY_DAYS),
            ]);

            $this->audit->write(
                organisationId: $org->id,
                actorTeamMemberId: $inviter->id,
                actorDisplayNameSnapshot: $inviter->display_name ?? $inviter->email,
                verb: 'member.invited',
                subjectKind: 'Invitation',
                subjectId: $invitation->id,
                payload: [
                    'email' => $inviteeEmail,
                    'role' => $role,
                ],
                originatingIp: $originatingIp,
            );

            return [$invitation, $rawToken];
        });
    }

    /**
     * Build the invitee-facing URL containing the RAW token. Email
     * Mailable composes this into the invitation message body.
     */
    public function buildAcceptUrl(string $rawToken): string
    {
        $base = rtrim(config('app.url') ?: url('/'), '/');
        return $base.'/invitations/accept?token='.rawurlencode($rawToken);
    }

    private function generateRawToken(): string
    {
        // 32 random bytes => base64url-encoded ~43 chars. Plenty of
        // entropy to make pre-image attacks pointless even against
        // a leaked DB.
        return rtrim(strtr(base64_encode(random_bytes(32)), '+/', '-_'), '=');
    }

    private function isOwner(TeamMember $user, CustomerOrganisation $org): bool
    {
        return OrganisationMembership::query()
            ->where('customer_organisation_id', $org->id)
            ->where('team_member_id', $user->id)
            ->where('role', OrganisationMembership::ROLE_OWNER)
            ->whereNull('revoked_at')
            ->whereNotNull('accepted_at')
            ->exists();
    }
}
