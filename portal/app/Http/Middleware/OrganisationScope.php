<?php

namespace App\Http\Middleware;

use App\Models\OrganisationMembership;
use App\Models\TeamMember;
use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

/**
 * T029 per FR-021 + FR-022. Two responsibilities:
 *
 * 1. Cross-organisation URL-tampering protection: if a route carries
 *    an `organisation` route parameter AND the authenticated user has
 *    no ACTIVE membership in that org, return 403. Prevents a member
 *    of org A from reading org B's data via a URL like
 *    /portal/organisations/{orgB-id}/...
 *
 * 2. Membership invalidation propagation: if the user's active
 *    OrganisationMembership has been revoked (FR-022 — Owner removed
 *    them), bounce them to the login page within 5 minutes of removal.
 *    Achieved by checking on every authenticated request.
 *
 * Applied to all routes inside the `/portal` route group. Unauthenticated
 * requests pass through — Laravel's auth middleware redirects to login
 * before this runs.
 */
class OrganisationScope
{
    public function handle(Request $request, Closure $next): Response
    {
        /** @var TeamMember|null $user */
        $user = $request->user();
        if ($user === null) {
            // Not authenticated — let the auth middleware handle it.
            return $next($request);
        }

        if ($user->soft_deleted_at !== null) {
            auth()->logout();
            return redirect()->route('login')->with('status', __('messages.auth.account_deleted'));
        }

        // If the route explicitly carries an organisation id, verify
        // the user belongs to that org. Used by Firm-tier members who
        // belong to multiple orgs.
        $requestedOrgId = $request->route('organisation');
        if (is_string($requestedOrgId) && $requestedOrgId !== '') {
            $hasAccess = OrganisationMembership::query()
                ->where('team_member_id', $user->id)
                ->where('customer_organisation_id', $requestedOrgId)
                ->whereNull('revoked_at')
                ->whereNotNull('accepted_at')
                ->exists();

            if (! $hasAccess) {
                abort(403, __('messages.errors.not_a_member'));
            }
        } else {
            // No explicit org in the route — verify the user has AT
            // LEAST ONE active membership somewhere (catches the
            // FR-022 "your only membership was revoked" case).
            $hasAnyMembership = OrganisationMembership::query()
                ->where('team_member_id', $user->id)
                ->whereNull('revoked_at')
                ->whereNotNull('accepted_at')
                ->exists();

            if (! $hasAnyMembership) {
                auth()->logout();
                return redirect()->route('login')->with('status', __('messages.auth.no_active_membership'));
            }
        }

        return $next($request);
    }
}
