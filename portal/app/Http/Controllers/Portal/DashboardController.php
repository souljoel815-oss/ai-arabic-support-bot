<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\Invoice;
use App\Models\Licence;
use App\Models\Subscription;
use Illuminate\Contracts\View\View;
use Illuminate\Support\Facades\Auth;

/**
 * T098 per FR-012. Real dashboard showing subscription status, active
 * licences, next renewal, open ticket count, and recent downloads
 * (the latter two are stubbed at zero until US4 + the
 * DownloadArtifactVersion polish task land).
 *
 * Per FR-029/FR-030, when the customer has NO active Subscription the
 * dashboard renders the "Subscribe to keep going" CTA (T103) — trial
 * is owned client-side; the portal just nudges the customer to convert.
 */
class DashboardController extends Controller
{
    public function show(): View
    {
        $user = Auth::user();
        $orgIds = $user->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->pluck('customer_organisations.id');

        $subscriptions = Subscription::query()
            ->whereIn('customer_organisation_id', $orgIds)
            ->whereIn('status', [Subscription::STATUS_ACTIVE, Subscription::STATUS_PAST_DUE])
            ->with('customerOrganisation')
            ->get();

        $activeLicenceCount = Licence::query()
            ->whereIn('subscription_id', $subscriptions->pluck('id'))
            ->whereNull('retired_at')
            ->count();

        $recentInvoices = Invoice::query()
            ->whereIn('customer_organisation_id', $orgIds)
            ->orderByDesc('created_at')
            ->limit(5)
            ->get();

        return view('portal.dashboard', [
            'user' => $user,
            'subscriptions' => $subscriptions,
            'activeLicenceCount' => $activeLicenceCount,
            'recentInvoices' => $recentInvoices,
            'hasActiveSubscription' => $subscriptions->isNotEmpty(),
        ]);
    }
}
