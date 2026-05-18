<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\CustomerOrganisation;
use App\Models\OrganisationMembership;
use App\Services\Organisations\DeleteAccountService;
use Illuminate\Contracts\View\View;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Throwable;

/**
 * T139 + T140 per FR-024 + FR-011. Account + security UI.
 * GET  /portal/account/security  → MFA + sessions placeholder
 * GET  /portal/account/delete    → soft-delete-with-confirmation form (Owner-only)
 * POST /portal/account/delete    → DeleteAccountService.softDelete
 * POST /portal/account/restore   → DeleteAccountService.restore
 */
class AccountController extends Controller
{
    public function __construct(private readonly DeleteAccountService $deleter)
    {
    }

    public function security(): View
    {
        return view('portal.account.security', [
            'user' => Auth::user(),
        ]);
    }

    public function showDelete(): View
    {
        $org = $this->resolveOwnedOrg();
        return view('portal.account.delete', compact('org'));
    }

    public function delete(Request $request): RedirectResponse
    {
        $org = $this->resolveOwnedOrg();
        $data = $request->validate([
            'confirmation_text' => ['required', 'string'],
        ]);

        try {
            $this->deleter->softDelete(
                org: $org,
                actor: Auth::user(),
                confirmationText: $data['confirmation_text'],
                originatingIp: $request->ip() ?? '0.0.0.0',
            );
        } catch (Throwable $e) {
            return back()->withErrors(['confirmation_text' => $e->getMessage()]);
        }

        return redirect()
            ->route('portal.dashboard')
            ->with('status', 'تم جدولة حذف الحساب. هتفضل ال beانات متاحة 30 يوم، تقدر تلغي الحذف من نفس الصفحة.');
    }

    public function restore(Request $request): RedirectResponse
    {
        $org = $this->resolveOwnedOrg();
        try {
            $this->deleter->restore(
                org: $org,
                actor: Auth::user(),
                originatingIp: $request->ip() ?? '0.0.0.0',
            );
        } catch (Throwable $e) {
            return back()->withErrors(['restore' => $e->getMessage()]);
        }

        return redirect()
            ->route('portal.dashboard')
            ->with('status', 'تم إلغاء طلب الحذف. حسابك نشط مرة أخرى.');
    }

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
