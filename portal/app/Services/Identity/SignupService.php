<?php

namespace App\Services\Identity;

use App\Models\CustomerOrganisation;
use App\Models\OrganisationMembership;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;

/**
 * T081 per contracts/signup.md. Atomic 3-step insert:
 *   1. Create the TeamMember (auth user) with email_verified_at = null
 *      so the FR-010 paid-action gate stays closed until email confirm.
 *   2. Create a CustomerOrganisation for this signup.
 *   3. Bind them via an OrganisationMembership with role = Owner.
 *
 * Wrapped in a DB.transaction so a failure on step 3 rolls back the
 * partial team_member+org rows. Writes an `organisation.signedUp`
 * audit-log entry.
 *
 * Email dispatch is fired separately by the caller (the
 * RegisteredUserController emits Laravel's Registered event, which
 * Breeze's MustVerifyEmail listener turns into a verification email).
 */
class SignupService
{
    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    /**
     * @return array{0: TeamMember, 1: CustomerOrganisation}
     */
    public function signUp(
        string $displayName,
        string $email,
        string $plainPassword,
        string $organisationLegalNameAr,
        string $localePreference,
        string $originatingIp,
    ): array {
        return DB::transaction(function () use (
            $displayName,
            $email,
            $plainPassword,
            $organisationLegalNameAr,
            $localePreference,
            $originatingIp,
        ) {
            $user = TeamMember::create([
                'display_name' => $displayName,
                'email' => $email,
                'password' => Hash::make($plainPassword),
                'locale_preference' => $localePreference,
            ]);

            $org = CustomerOrganisation::create([
                'legal_name_ar' => $organisationLegalNameAr,
                'billing_email' => $email,
                'country_code' => 'EG',
            ]);

            OrganisationMembership::create([
                'customer_organisation_id' => $org->id,
                'team_member_id' => $user->id,
                'role' => OrganisationMembership::ROLE_OWNER,
                'invited_at' => now(),
                // accepted_at is set immediately for self-signup —
                // the user is BOTH the inviter + invitee. Future
                // OrganisationMemberships for invited teammates
                // leave accepted_at null until they click their
                // invitation link (Phase 7, US5 T123).
                'accepted_at' => now(),
            ]);

            $this->audit->write(
                organisationId: $org->id,
                actorTeamMemberId: $user->id,
                actorDisplayNameSnapshot: $displayName,
                verb: 'organisation.signedUp',
                subjectKind: 'CustomerOrganisation',
                subjectId: $org->id,
                payload: [
                    'email' => $email,
                    'locale' => $localePreference,
                ],
                originatingIp: $originatingIp,
            );

            return [$user, $org];
        });
    }
}
