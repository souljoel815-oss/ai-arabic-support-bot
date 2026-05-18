<?php

namespace App\Http\Controllers;

use App\Models\Invitation;
use App\Services\Organisations\AcceptInvitationService;
use Illuminate\Contracts\View\View;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Illuminate\Validation\Rules;
use RuntimeException;

/**
 * T128 per FR-020. Public route (no auth required) — the invitation
 * link in the email lands here.
 *
 * GET /invitations/accept?token=... → form (set password + accept)
 * POST /invitations/accept           → AcceptInvitationService → log in
 *                                       the new TeamMember → redirect
 *                                       to portal.dashboard.
 *
 * Distinguishes error modes per contracts/invite-member.md:
 *   - expired → renders a "this invitation expired" landing
 *   - already_used → renders a 410-equivalent "already accepted" page
 *   - invalid/unknown token → 404
 */
class AcceptInvitationController extends Controller
{
    public function __construct(private readonly AcceptInvitationService $accepter)
    {
    }

    public function show(Request $request): View
    {
        $token = $request->query('token');
        if (! is_string($token) || $token === '') {
            abort(404);
        }

        $tokenHash = Invitation::hashToken($token);
        $invitation = Invitation::query()
            ->where('token_hash', $tokenHash)
            ->first();

        if ($invitation === null) {
            abort(404);
        }
        if ($invitation->isAccepted()) {
            return view('invitations.already-used', compact('invitation'));
        }
        if ($invitation->isExpired()) {
            return view('invitations.expired', compact('invitation'));
        }

        // Show form. Pre-populate display_name from invitation if set.
        // Pre-existing TeamMember? Skip the password field — they
        // already have credentials.
        $alreadyTeamMember = \App\Models\TeamMember::query()
            ->where('email', $invitation->email)
            ->exists();

        return view('invitations.accept', [
            'invitation' => $invitation,
            'token' => $token,
            'alreadyTeamMember' => $alreadyTeamMember,
        ]);
    }

    public function accept(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'token' => ['required', 'string'],
            'display_name' => ['nullable', 'string', 'max:128'],
            'password' => ['nullable', 'string', Rules\Password::defaults()],
        ]);

        try {
            [$teamMember, $_membership] = $this->accepter->accept(
                rawToken: $data['token'],
                plainPassword: $data['password'] ?? '',  // ignored if user already exists
                displayName: $data['display_name'] ?? null,
                originatingIp: $request->ip() ?? '0.0.0.0',
            );
        } catch (RuntimeException $e) {
            return back()->withErrors(['token' => $e->getMessage()]);
        }

        Auth::login($teamMember);

        return redirect()
            ->route('portal.dashboard')
            ->with('status', __('organisation.accept_welcome_flash'));
    }
}
