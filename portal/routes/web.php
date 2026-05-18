<?php

use App\Http\Controllers\ProfileController;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Route;

/**
 * T035 — Two-surface routing:
 *   /*           → marketing (Razor-Pages-equivalent — public Blade views).
 *   /portal/*    → authenticated portal (Blade + auth + organisation-scope).
 *   /api/*       → JSON endpoints (defined in routes/api.php).
 *
 * Marketing pages with the optional locale prefix /ar/* + /en/* resolve
 * to the same Blade view via the LocaleResolver middleware. Phase 3
 * (US1) replaces these placeholder closures with real Marketing\* controllers.
 */

// --- T036: /health ---------------------------------------------------
// JSON 200 when the DB is reachable + degraded JSON 503 otherwise.
// Cloudflare can use this for origin health checks; UptimeRobot pings
// from 3 geographies per research §15.
Route::get('/health', function () {
    try {
        DB::connection()->getPdo();
        $dbStatus = 'ok';
    } catch (\Throwable $e) {
        return response()->json(['status' => 'degraded', 'db' => 'unreachable'], 503);
    }
    return response()->json([
        'status' => 'ok',
        'db' => $dbStatus,
        'app' => config('app.name', 'DaftarX Portal'),
        'time_utc' => now()->toIso8601String(),
    ]);
});

// --- Marketing surface (public, no auth) -----------------------------
// Phase 3 (US1, T040-T055) fleshes these out with the real Marketing\*
// controllers + per-page Blade views. For Phase 2 we just route them
// through placeholder views that prove the layout composes.

$marketingRoutes = function (): void {
    Route::view('/', 'marketing.home')->name('marketing.home');
    Route::view('/features', 'marketing.placeholder')->name('marketing.features')
        ->defaults('page', 'features');
    Route::view('/pricing', 'marketing.placeholder')->name('marketing.pricing')
        ->defaults('page', 'pricing');
    Route::view('/downloads', 'marketing.placeholder')->name('marketing.downloads')
        ->defaults('page', 'downloads');
    Route::view('/about', 'marketing.placeholder')->name('marketing.about')
        ->defaults('page', 'about');
    Route::view('/contact', 'marketing.placeholder')->name('marketing.contact')
        ->defaults('page', 'contact');
    Route::view('/terms', 'marketing.placeholder')->name('marketing.terms')
        ->defaults('page', 'terms');
    Route::view('/refund', 'marketing.placeholder')->name('marketing.refund')
        ->defaults('page', 'refund');
    Route::view('/privacy', 'marketing.placeholder')->name('marketing.privacy')
        ->defaults('page', 'privacy');
    Route::view('/privacy/android', 'marketing.placeholder')
        ->name('marketing.privacy.android')
        ->defaults('page', 'privacy_android');
};

// Locale-prefixed variants: /ar/*, /en/*.
Route::prefix('{locale}')->where(['locale' => 'ar|en'])
    ->middleware(\App\Http\Middleware\LocaleResolver::class)
    ->group($marketingRoutes);

// Locale-less variants — LocaleResolver still runs to pick a default.
Route::middleware(\App\Http\Middleware\LocaleResolver::class)
    ->group($marketingRoutes);

// --- Portal surface (authenticated) ----------------------------------
// /portal/* requires login + email verification + active organisation
// membership (OrganisationScope middleware).
Route::prefix('portal')
    ->middleware(['auth', 'verified', \App\Http\Middleware\OrganisationScope::class])
    ->group(function () {
        Route::view('/', 'portal.dashboard')->name('portal.dashboard');
        // Phase 4 (US2) + Phase 5 (US3) + Phase 6-8 (US4-US6) flesh
        // these out. Placeholder routes prove the auth + sidebar pipeline.
        Route::view('/licences', 'portal.placeholder')
            ->name('portal.licences')
            ->defaults('page', 'licences');
        Route::view('/subscription', 'portal.placeholder')
            ->name('portal.subscription')
            ->defaults('page', 'subscription');
        Route::view('/billing', 'portal.placeholder')
            ->name('portal.billing')
            ->defaults('page', 'billing');
        Route::view('/downloads', 'portal.placeholder')
            ->name('portal.downloads')
            ->defaults('page', 'downloads');
        Route::view('/support', 'portal.placeholder')
            ->name('portal.support')
            ->defaults('page', 'support');
        Route::view('/organisation', 'portal.placeholder')
            ->name('portal.organisation')
            ->defaults('page', 'organisation');
        Route::view('/account/security', 'portal.placeholder')
            ->name('portal.account.security')
            ->defaults('page', 'security');
    });

// --- Profile (Breeze default — kept under /profile, not /portal) -----
// Breeze's profile editor doesn't need the OrganisationScope middleware
// (it's a per-user setting, not an org-scoped operation), so keep it
// at the top level for now. US5 may move it under /portal/account.
Route::middleware('auth')->group(function () {
    Route::get('/profile', [ProfileController::class, 'edit'])->name('profile.edit');
    Route::patch('/profile', [ProfileController::class, 'update'])->name('profile.update');
    Route::delete('/profile', [ProfileController::class, 'destroy'])->name('profile.destroy');
});

// Breeze's auth routes (login, register, verify-email, password reset).
require __DIR__.'/auth.php';
