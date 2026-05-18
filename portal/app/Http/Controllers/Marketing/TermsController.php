<?php

namespace App\Http\Controllers\Marketing;

use App\Http\Controllers\Controller;
use Illuminate\Contracts\View\View;

/**
 * T047 per FR-007. Terms of service + refund policy + privacy policy
 * pages — required to be linked from every page's footer (handled in
 * the marketing layout).
 */
class TermsController extends Controller
{
    public function terms(): View
    {
        return view('marketing.terms');
    }

    public function refund(): View
    {
        return view('marketing.refund');
    }

    public function privacy(): View
    {
        return view('marketing.privacy.index');
    }

    /**
     * T133 + US6 per FR-006 + SC-004. STABLE URL: must always return
     * 200 with the Android-specific privacy disclosure. Linked from the
     * Play Store Data Safety form (feature 009 FR-018). Any URL change
     * MUST be coordinated with a Play-listing update.
     */
    public function privacyAndroid(): View
    {
        return view('marketing.privacy.android');
    }
}
