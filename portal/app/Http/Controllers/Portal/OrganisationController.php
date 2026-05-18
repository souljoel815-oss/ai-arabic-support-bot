<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\CustomerOrganisation;
use App\Models\Invitation;
use App\Models\OrganisationMembership;
use App\Services\Organisations\InviteMemberService;
use App\Services\Organisations\RemoveMemberService;
use Illuminate\Contracts\View\View;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\Log;
use Illuminate\Support\Facades\Mail;
use Throwable;

/**
 * T127 + T129 per US5. Owner-only UI for managing the organisation's
 * team members: list current members + roles, invite new ones, revoke
 * existing.
 */
class OrganisationController extends Controller
{
    public function __construct(
        private readonly InviteMemberService $inviter,
        private readonly RemoveMemberService $remover,
    ) {
    }

    public function members(): View
    {
        $org = $this->resolveOwnedOrg();

        $memberships = OrganisationMembership::query()
            ->where('customer_organisation_id', $org->id)
            ->orderByRaw("CASE role WHEN 'Owner' THEN 1 WHEN 'BillingAdmin' THEN 2 WHEN 'SupportAdmin' THEN 3 ELSE 4 END")
            ->get();

        // Lazy-load team_member emails inline (the pivot model isn't
        // wired with belongsTo; cheaper to fetch by id-list here).
        $userIds = $memberships->pluck('team_member_id')->all();
        $users = \App\Models\TeamMember::query()->whereIn('id', $userIds)->get()->keyBy('id');

        $pendingInvitations = Invitation::query()
            ->where('customer_organisation_id', $org->id)
            ->whereNull('accepted_at')
            ->where('expires_at', '>', now())
            ->orderByDesc('created_at')
            ->get();

        return view('portal.organisation.members', compact('org', 'memberships', 'users', 'pendingInvitations'));
    }

    public function invite(Request $request): RedirectResponse
    {
        $org = $this->resolveOwnedOrg();
        $data = $request->validate([
            'email' => ['required', 'email:rfc', 'max:256'],
            'role' => ['required', 'in:Owner,BillingAdmin,SupportAdmin,ReadOnly'],
            'display_name' => ['nullable', 'string', 'max:128'],
            'locale_preference' => ['nullable', 'in:ar-EG,en-US'],
        ]);

        try {
            [$invitation, $rawToken] = $this->inviter->invite(
                org: $org,
                inviter: Auth::user(),
                inviteeEmail: $data['email'],
                role: $data['role'],
                displayName: $data['display_name'] ?? null,
                localePreference: $data['locale_preference'] ?? 'ar-EG',
                originatingIp: $request->ip() ?? '0.0.0.0',
            );
        } catch (Throwable $e) {
            return back()->withInput()->withErrors(['email' => $e->getMessage()]);
        }

        // Dispatch invitation email. In dev (mail driver = log) this
        // lands in storage/logs/laravel.log. Mailable rendering (with
        // MJML) lands in Phase 9 polish (T130) — for now we just log
        // the URL so the operator can copy it into a browser.
        $acceptUrl = $this->inviter->buildAcceptUrl($rawToken);
        try {
            Mail::raw(
                "You've been invited to join {$org->legal_name_ar} on DaftarX.\n\n"
                ."Click to accept:\n{$acceptUrl}\n\n"
                ."(Expires in 7 days.)\n",
                function ($message) use ($data) {
                    $message->to($data['email'])->subject('DaftarX invitation');
                }
            );
        } catch (Throwable $e) {
            // Don't fail the whole flow if email dispatch hiccups —
            // log it and proceed; the operator can resend.
            Log::error("Invitation email dispatch failed: {$e->getMessage()}");
        }

        Log::info("Invitation issued: {$data['email']} role={$data['role']} → accept URL: {$acceptUrl}");

        return redirect()
            ->route('portal.organisation.members')
            ->with('status', __('organisation.invitation_sent_flash', ['email' => $data['email']]));
    }

    public function revoke(Request $request, OrganisationMembership $membership): RedirectResponse
    {
        $org = $this->resolveOwnedOrg();
        if ($membership->customer_organisation_id !== $org->id) {
            abort(403);
        }
        try {
            $this->remover->remove($membership, Auth::user(), $request->ip() ?? '0.0.0.0');
        } catch (Throwable $e) {
            return back()->withErrors(['membership' => $e->getMessage()]);
        }
        return redirect()
            ->route('portal.organisation.members')
            ->with('status', __('organisation.member_removed_flash'));
    }

    /**
     * Returns the org the signed-in user OWNS (vs. just belongs to as
     * BillingAdmin etc.). Only Owners reach this controller.
     */
    private function resolveOwnedOrg(): CustomerOrganisation
    {
        $org = Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->wherePivot('role', OrganisationMembership::ROLE_OWNER)
            ->first();
        if ($org === null) {
            abort(403, __('messages.errors.not_a_member'));
        }
        return $org;
    }
}
