<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\Subscription;
use Illuminate\Contracts\View\View;
use Illuminate\Support\Facades\Auth;

/**
 * T101 per FR-017. Authenticated downloads page. Shows the customer
 * the installers they're entitled to based on their Subscription tier:
 *   - Solo / SMB / Enterprise / Firm: full Desktop installer + APK
 *   - SMB+:                          + LAN-client installer
 *   - Trial / no subscription:        same as Solo (trial = full features
 *                                    per FR-029)
 *
 * Latest version data is hard-coded as an array on this controller for
 * v1. Future iteration: pull from the DownloadArtifactVersion entity
 * (T154 Phase 9) so ops can publish new builds via an admin command.
 */
class DownloadsController extends Controller
{
    public function show(): View
    {
        $user = Auth::user();
        $orgIds = $user
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->pluck('customer_organisations.id');

        $activeTier = Subscription::query()
            ->whereIn('customer_organisation_id', $orgIds)
            ->where('status', Subscription::STATUS_ACTIVE)
            ->value('tier');  // null when no active subscription (trial mode)

        $hasSubscription = $activeTier !== null;
        $isLanCapable = $hasSubscription && in_array($activeTier, [
            Subscription::TIER_SMB,
            Subscription::TIER_ENTERPRISE,
            Subscription::TIER_FIRM,
        ], true);

        // Static catalog for v1. Real version-history rotation lands
        // with the DownloadArtifactVersion entity in Phase 9 (T154).
        $artifacts = [
            'desktop' => [
                'name'         => 'DaftarX-Setup.msi',
                'version'      => '5.18.0',
                'released_on'  => '2026-05-15',
                'size_bytes'   => 182 * 1024 * 1024,
                'sha256'       => 'a1b2c3...',  // placeholder until real build pipeline writes the manifest
                'url'          => '/downloads/DaftarX-Setup.msi',
                'available'    => true,
            ],
            'lan' => [
                'name'         => 'DaftarX-Client-Setup.msi',
                'version'      => '5.18.0',
                'released_on'  => '2026-05-15',
                'size_bytes'   => 24 * 1024 * 1024,
                'sha256'       => 'd4e5f6...',
                'url'          => '/downloads/DaftarX-Client-Setup.msi',
                'available'    => $isLanCapable,
            ],
            'android' => [
                'name'         => 'DaftarX (Android)',
                'version'      => '1.0.0',
                'released_on'  => '2026-05-15',
                'size_bytes'   => 12 * 1024 * 1024,
                'sha256'       => 'g7h8i9...',
                'url'          => '/downloads/daftarx-android.apk',
                'play_url'     => 'https://play.google.com/store/apps/details?id=com.daftarx.mobile',
                'available'    => true,
            ],
        ];

        return view('portal.downloads', [
            'artifacts' => $artifacts,
            'activeTier' => $activeTier,
            'hasSubscription' => $hasSubscription,
            'isLanCapable' => $isLanCapable,
        ]);
    }
}
