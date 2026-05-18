<?php

namespace App\Http\Controllers\Marketing;

use App\Http\Controllers\Controller;
use Illuminate\Contracts\View\View;

/**
 * T042 per FR-003. Pricing page — 4 tier cards (Solo/SMB/Enterprise/Firm)
 * with monthly/annual EGP prices, feature checklist, primary CTA per tier.
 * SC-001: prospect reaches "Start trial" CTA in ≤ 3 clicks from homepage.
 */
class PricingController extends Controller
{
    public function show(): View
    {
        return view('marketing.pricing');
    }
}
