<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Symfony\Component\HttpFoundation\Response;

/**
 * T155 — FR-011 enforcement.
 *
 * If the signed-in member is an Owner of an organisation whose
 * `requires_mfa_for_owners` flag is set, AND that member doesn't have
 * MFA enrolled (`mfa_enabled_at IS NULL`), redirect them to
 * `/portal/account/security` with an `mfa_required` flash message.
 * They can't access any other portal route until they enrol TOTP.
 *
 * Safe-list: the security page itself + logout are always reachable
 * (otherwise we'd lock them out of enrolling).
 *
 * Matrix the unit test should cover:
 *   - Owner ON + policy ON + MFA absent  → redirect
 *   - Owner ON + policy ON + MFA present → pass-through
 *   - Owner ON + policy OFF              → pass-through
 *   - Non-owner role + policy ON         → pass-through
 */
class OrganisationMfaPolicy
{
    private const SAFE_PATH_PREFIXES = [
        '/portal/account/security',
        '/logout',
    ];

    public function handle(Request $request, Closure $next): Response
    {
        $user = Auth::user();
        if ($user === null) {
            return $next($request);
        }

        $path = '/' . trim($request->path(), '/');
        foreach (self::SAFE_PATH_PREFIXES as $safe) {
            if (str_starts_with($path, $safe)) {
                return $next($request);
            }
        }

        // Already enrolled — nothing to enforce.
        if ($user->mfa_enabled_at !== null) {
            return $next($request);
        }

        // Look at the active (non-revoked, accepted) Owner membership
        // for any organisation that requires MFA for owners.
        $needsMfa = $user->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->wherePivot('role', 'Owner')
            ->where('requires_mfa_for_owners', true)
            ->exists();

        if ($needsMfa) {
            return redirect()
                ->route('portal.account.security')
                ->with('mfa_required', true);
        }

        return $next($request);
    }
}
