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

    /**
     * T136 per US6 — version archival. When the privacy policy text
     * changes the operator publishes a snapshot at
     * /privacy/android/history/{YYYY-MM-DD}. Older snapshots remain
     * accessible so prior consent claims stay auditable for the
     * lifetime of the affected Play Store listings + tax-authority
     * inspections that reference them.
     *
     * Snapshots live as plain Blade views under
     * resources/views/marketing/privacy/history/{date}.blade.php. When
     * a snapshot doesn't exist for the requested date, this method
     * returns 404 (rather than silently falling back to the current
     * policy) so consent-claim chains don't get falsified by a missing
     * snapshot resolving to the latest version.
     */
    public function privacyAndroidHistory(string $date): View
    {
        // Strict ISO date format guard — prevents path-injection +
        // makes the snapshot key auditable.
        if (! preg_match('/^\d{4}-\d{2}-\d{2}$/', $date)) {
            abort(404);
        }

        $viewName = "marketing.privacy.history.{$date}";
        if (! view()->exists($viewName)) {
            abort(404);
        }

        return view($viewName);
    }
}
