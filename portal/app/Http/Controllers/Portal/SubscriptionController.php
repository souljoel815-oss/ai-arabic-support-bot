<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\CustomerOrganisation;
use App\Models\Subscription;
use App\Services\Subscriptions\CancelSubscriptionService;
use App\Services\Subscriptions\ConvertTrialToPaidService;
use App\Services\Subscriptions\Pricing;
use Illuminate\Contracts\View\View;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Throwable;

/**
 * T099 + T103 + T153. Subscription management UI:
 *   GET  /portal/subscription                 → index (current state)
 *   GET  /portal/subscription/start           → tier + cadence + payment-method picker
 *   POST /portal/subscription/start           → ConvertTrialToPaidService
 *   POST /portal/subscription/{id}/cancel     → CancelSubscriptionService
 *
 * Upgrade + Downgrade pages land in Phase 5 follow-up (UpgradeTierService
 * + ScheduleDowngradeService — proration math). For MVP the customer
 * either starts a new subscription or cancels an existing one.
 */
class SubscriptionController extends Controller
{
    public function __construct(
        private readonly ConvertTrialToPaidService $convertToPaid,
        private readonly CancelSubscriptionService $canceller,
    ) {
    }

    public function index(): View
    {
        $org = $this->resolveActiveOrg();
        $subscriptions = Subscription::query()
            ->where('customer_organisation_id', $org->id)
            ->orderByDesc('created_at')
            ->get();

        return view('portal.subscription.index', compact('org', 'subscriptions'));
    }

    public function showStart(Request $request): View
    {
        $org = $this->resolveActiveOrg();
        $hasActive = Subscription::query()
            ->where('customer_organisation_id', $org->id)
            ->whereIn('status', [Subscription::STATUS_ACTIVE, Subscription::STATUS_PAST_DUE])
            ->exists();

        return view('portal.subscription.start', [
            'org' => $org,
            'hasActive' => $hasActive,
            'preferredTier' => $request->query('tier'),
            'priceTable' => Pricing::PRICE_TABLE,
        ]);
    }

    public function start(Request $request): RedirectResponse
    {
        $validated = $request->validate([
            'tier' => ['required', 'in:Solo,SMB,Enterprise,Firm'],
            'billing_cadence' => ['required', 'in:Monthly,Annual'],
            'payment_method' => ['required', 'in:Card,Fawry,InstaPay,VodafoneCash,BankTransfer'],
        ]);

        $org = $this->resolveActiveOrg();

        try {
            [$_subscription, $invoice] = $this->convertToPaid->start(
                org: $org,
                tier: $validated['tier'],
                billingCadence: $validated['billing_cadence'],
                paymentMethod: $validated['payment_method'],
                actor: Auth::user(),
                originatingIp: $request->ip() ?? '0.0.0.0',
            );
        } catch (Throwable $e) {
            return back()->withInput()->withErrors(['tier' => $e->getMessage()]);
        }

        return redirect()
            ->route('portal.billing')
            ->with('status', __('subscription.started_flash', ['number' => $invoice->invoice_number]));
    }

    public function cancel(Request $request, Subscription $subscription): RedirectResponse
    {
        $this->authoriseSubscription($subscription);
        try {
            $this->canceller->cancel($subscription, Auth::user(), $request->ip() ?? '0.0.0.0');
        } catch (Throwable $e) {
            return back()->withErrors(['subscription' => $e->getMessage()]);
        }
        return redirect()->route('portal.subscription')->with('status', __('subscription.cancelled_flash'));
    }

    // ----- helpers --------------------------------------------------------

    private function resolveActiveOrg(): CustomerOrganisation
    {
        $org = Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->first();
        if ($org === null) {
            abort(403, __('messages.errors.not_a_member'));
        }
        return $org;
    }

    private function authoriseSubscription(Subscription $subscription): void
    {
        $orgIds = Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->pluck('customer_organisations.id');
        if (! $orgIds->contains($subscription->customer_organisation_id)) {
            abort(403);
        }
    }
}
