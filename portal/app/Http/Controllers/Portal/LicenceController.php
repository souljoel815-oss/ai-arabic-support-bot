<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\Licence;
use App\Models\Subscription;
use App\Services\Licences\ActivatePaidLicenceService;
use App\Services\Licences\TransferLicenceService;
use Illuminate\Contracts\View\View;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Http\Response;
use Illuminate\Support\Facades\Auth;
use Throwable;

/**
 * T065-T068 per US2. UI for the customer-portal licence-management
 * surface: list / activate-paid / transfer / download-token.
 *
 * All actions are scoped to the signed-in user's active organisation via
 * the OrganisationScope middleware that runs in the /portal route
 * group. The controller itself enforces a SECOND scope on every
 * lookup as belt-and-braces against route-tampering.
 */
class LicenceController extends Controller
{
    public function __construct(
        private readonly ActivatePaidLicenceService $activator,
        private readonly TransferLicenceService $transferer,
    ) {
    }

    /**
     * T066. List active + retired licences with action buttons. Renders
     * the Priority-support badge inline next to the tier name when the
     * Subscription's tier ∈ {Enterprise, Firm} per US2 AS#3.
     */
    public function index(): View
    {
        $orgIds = Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->pluck('customer_organisations.id');

        $subscriptions = Subscription::query()
            ->whereIn('customer_organisation_id', $orgIds)
            ->with(['licences' => function ($q) {
                $q->orderByDesc('issued_at');
            }])
            ->get();

        return view('portal.licences.list', compact('subscriptions'));
    }

    public function showActivatePaid(Request $request): View
    {
        $subscriptionId = $request->query('subscription');
        $subscription = $this->loadOwnedSubscription($subscriptionId);

        return view('portal.licences.activate-paid', compact('subscription'));
    }

    public function activatePaid(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'subscription_id' => ['required', 'uuid'],
            'hwid' => ['required', 'string', 'max:64'],
        ]);

        $subscription = $this->loadOwnedSubscription($data['subscription_id']);

        try {
            $licence = $this->activator->activate(
                $subscription,
                $data['hwid'],
                Auth::user(),
                $request->ip() ?? '0.0.0.0',
            );
        } catch (Throwable $e) {
            return back()->withInput()->withErrors(['hwid' => $e->getMessage()]);
        }

        return redirect()
            ->route('portal.licences.token', ['licence' => $licence->id])
            ->with('status', __('licences.activated_flash'));
    }

    public function showTransfer(Licence $licence): View
    {
        $this->authoriseLicence($licence);
        return view('portal.licences.transfer', compact('licence'));
    }

    public function transfer(Request $request, Licence $licence): RedirectResponse
    {
        $this->authoriseLicence($licence);

        $data = $request->validate([
            'new_hwid' => ['required', 'string', 'max:64'],
        ]);

        try {
            $newLicence = $this->transferer->transfer(
                $licence,
                $data['new_hwid'],
                Auth::user(),
                $request->ip() ?? '0.0.0.0',
            );
        } catch (Throwable $e) {
            return back()->withInput()->withErrors(['new_hwid' => $e->getMessage()]);
        }

        return redirect()
            ->route('portal.licences.token', ['licence' => $newLicence->id])
            ->with('status', __('licences.transferred_flash'));
    }

    /**
     * Stream the signed-token JSON file for download. The customer
     * drops it at %PROGRAMDATA%\DaftarX\license\license.token on their
     * machine + restarts the on-prem service.
     */
    public function downloadToken(Licence $licence): Response
    {
        $this->authoriseLicence($licence);

        // signed_token_base64 stores the base64 of the JSON envelope;
        // decode back to JSON so the on-prem LicenseVerifier reads it
        // as the wire format it expects.
        $envelopeJson = base64_decode($licence->signed_token_base64, strict: true);

        return response($envelopeJson, 200, [
            'Content-Type' => 'application/json; charset=utf-8',
            'Content-Disposition' => 'attachment; filename="license.token"',
            'Cache-Control' => 'no-store',
        ]);
    }

    // ----- helpers --------------------------------------------------------

    private function loadOwnedSubscription(?string $subscriptionId): Subscription
    {
        if (! $subscriptionId) {
            abort(404);
        }
        $subscription = Subscription::query()
            ->with('customerOrganisation')
            ->findOrFail($subscriptionId);
        $this->authoriseOrganisation($subscription->customer_organisation_id);
        return $subscription;
    }

    private function authoriseLicence(Licence $licence): void
    {
        // Load subscription via FK so we can authorise on org id.
        $licence->loadMissing('subscription');
        $this->authoriseOrganisation($licence->subscription->customer_organisation_id);
    }

    private function authoriseOrganisation(string $organisationId): void
    {
        $belongs = Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->where('customer_organisations.id', $organisationId)
            ->exists();

        if (! $belongs) {
            abort(403, __('messages.errors.not_a_member'));
        }
    }
}
