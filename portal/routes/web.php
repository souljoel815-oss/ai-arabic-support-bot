<?php

use App\Http\Controllers\Marketing\AboutController;
use App\Http\Controllers\Marketing\ContactController;
use App\Http\Controllers\Marketing\DownloadsController;
use App\Http\Controllers\Marketing\FeaturesController;
use App\Http\Controllers\Marketing\HomeController;
use App\Http\Controllers\Marketing\PricingController;
use App\Http\Controllers\Marketing\TermsController;
use App\Http\Controllers\Portal\BillingController;
use App\Http\Controllers\Portal\DashboardController;
use App\Http\Controllers\Portal\LicenceController;
use App\Http\Controllers\Portal\SubscriptionController;
use App\Http\Controllers\ProfileController;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Route;

/**
 * T035 + T052 — Two-surface routing with optional locale prefix.
 *   /*            → marketing (public Blade views).
 *   /ar/* + /en/* → same marketing pages, locale-aware.
 *   /portal/*     → authenticated portal (Blade + auth + organisation-scope).
 *   /api/*        → JSON endpoints (defined in routes/api.php).
 */

// --- T036: /health -------------------------------------------------------
Route::get('/health', function () {
    try {
        DB::connection()->getPdo();
    } catch (\Throwable $e) {
        return response()->json(['status' => 'degraded', 'db' => 'unreachable'], 503);
    }
    return response()->json([
        'status' => 'ok',
        'db' => 'ok',
        'app' => config('app.name', 'DaftarX Portal'),
        'time_utc' => now()->toIso8601String(),
    ]);
});

// --- T053: robots.txt + sitemap.xml --------------------------------------
Route::get('/robots.txt', function () {
    $body = "User-agent: *\n"
        . "Allow: /\n"
        . "Disallow: /portal/\n"
        . "Disallow: /api/\n"
        . "Disallow: /login\n"
        . "Disallow: /register\n"
        . "\n"
        . "Sitemap: " . url('/sitemap.xml') . "\n";
    return response($body)->header('Content-Type', 'text/plain; charset=utf-8');
});

Route::get('/sitemap.xml', function () {
    $base = rtrim(config('app.url') ?: url('/'), '/');
    $now = now()->toAtomString();
    $paths = ['/', '/features', '/pricing', '/downloads', '/about', '/contact', '/terms', '/refund', '/privacy', '/privacy/android'];
    $urls = [];
    foreach (['ar', 'en'] as $locale) {
        foreach ($paths as $p) {
            $loc = $p === '/' ? "{$base}/{$locale}" : "{$base}/{$locale}{$p}";
            $altAr = $p === '/' ? "{$base}/ar" : "{$base}/ar{$p}";
            $altEn = $p === '/' ? "{$base}/en" : "{$base}/en{$p}";
            $urls[] = "  <url>\n"
                . "    <loc>{$loc}</loc>\n"
                . "    <lastmod>{$now}</lastmod>\n"
                . "    <changefreq>weekly</changefreq>\n"
                . "    <xhtml:link rel=\"alternate\" hreflang=\"ar-EG\" href=\"{$altAr}\"/>\n"
                . "    <xhtml:link rel=\"alternate\" hreflang=\"en-US\" href=\"{$altEn}\"/>\n"
                . "  </url>";
        }
    }
    $body = '<?xml version="1.0" encoding="UTF-8"?>' . "\n"
        . '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" '
        . 'xmlns:xhtml="http://www.w3.org/1999/xhtml">' . "\n"
        . implode("\n", $urls) . "\n"
        . '</urlset>';
    return response($body)->header('Content-Type', 'application/xml; charset=utf-8');
});

// --- Marketing surface ---------------------------------------------------
// All routes wired through LocaleResolver. Same controller serves both
// the locale-prefixed and locale-less variants of each page; LocaleResolver
// reads the URL prefix and sets App::setLocale() accordingly.

$marketingRoutes = function (): void {
    Route::get('/', [HomeController::class, 'show'])->name('marketing.home');
    Route::get('/features', [FeaturesController::class, 'show'])->name('marketing.features');
    Route::get('/pricing', [PricingController::class, 'show'])->name('marketing.pricing');
    Route::get('/downloads', [DownloadsController::class, 'show'])->name('marketing.downloads');
    Route::get('/about', [AboutController::class, 'show'])->name('marketing.about');
    Route::get('/contact', [ContactController::class, 'show'])->name('marketing.contact');
    Route::post('/contact', [ContactController::class, 'submit'])
        ->middleware('throttle:10,60')   // FR-005 + T147 — 10/hour/IP
        ->name('marketing.contact.submit');
    Route::get('/terms', [TermsController::class, 'terms'])->name('marketing.terms');
    Route::get('/refund', [TermsController::class, 'refund'])->name('marketing.refund');
    Route::get('/privacy', [TermsController::class, 'privacy'])->name('marketing.privacy');
    Route::get('/privacy/android', [TermsController::class, 'privacyAndroid'])
        ->name('marketing.privacy.android');
};

// /ar/* and /en/* — explicit locale prefix.
Route::prefix('{locale}')->where(['locale' => 'ar|en'])
    ->middleware(\App\Http\Middleware\LocaleResolver::class)
    ->group($marketingRoutes);

// /* — locale-less; LocaleResolver picks default.
Route::middleware(\App\Http\Middleware\LocaleResolver::class)
    ->group($marketingRoutes);

// --- Portal surface (authenticated) --------------------------------------
Route::prefix('portal')
    ->middleware(['auth', 'verified', \App\Http\Middleware\OrganisationScope::class])
    ->group(function () {
        // --- US3 dashboard (T098) ---
        Route::get('/', [DashboardController::class, 'show'])->name('portal.dashboard');

        // --- US3 subscription management (T099, T103, T153) ---
        Route::get('/subscription', [SubscriptionController::class, 'index'])
            ->name('portal.subscription');
        Route::get('/subscription/start', [SubscriptionController::class, 'showStart'])
            ->name('portal.subscription.start');
        Route::post('/subscription/start', [SubscriptionController::class, 'start'])
            ->name('portal.subscription.start.submit');
        Route::post('/subscription/{subscription}/cancel', [SubscriptionController::class, 'cancel'])
            ->name('portal.subscription.cancel');

        // --- US3 billing (T100) ---
        Route::get('/billing', [BillingController::class, 'index'])
            ->name('portal.billing');
        Route::get('/billing/{invoice}/pdf', [BillingController::class, 'downloadPdf'])
            ->name('portal.billing.pdf');
        Route::post('/billing/{invoice}/refund', [BillingController::class, 'refund'])
            ->name('portal.billing.refund');

        // --- US2 licence self-service (T065) ---
        Route::get('/licences', [LicenceController::class, 'index'])
            ->name('portal.licences');
        Route::get('/licences/activate-paid', [LicenceController::class, 'showActivatePaid'])
            ->name('portal.licences.activate-paid');
        Route::post('/licences/activate-paid', [LicenceController::class, 'activatePaid'])
            ->name('portal.licences.activate-paid.submit');
        Route::get('/licences/{licence}/transfer', [LicenceController::class, 'showTransfer'])
            ->name('portal.licences.transfer');
        Route::post('/licences/{licence}/transfer', [LicenceController::class, 'transfer'])
            ->name('portal.licences.transfer.submit');
        Route::get('/licences/{licence}/token', [LicenceController::class, 'downloadToken'])
            ->name('portal.licences.token');
        Route::view('/downloads', 'portal.placeholder')
            ->name('portal.downloads')->defaults('page', 'downloads');
        Route::view('/support', 'portal.placeholder')
            ->name('portal.support')->defaults('page', 'support');
        Route::view('/organisation', 'portal.placeholder')
            ->name('portal.organisation')->defaults('page', 'organisation');
        Route::view('/account/security', 'portal.placeholder')
            ->name('portal.account.security')->defaults('page', 'security');
    });

// --- Profile (Breeze default) --------------------------------------------
Route::middleware('auth')->group(function () {
    Route::get('/profile', [ProfileController::class, 'edit'])->name('profile.edit');
    Route::patch('/profile', [ProfileController::class, 'update'])->name('profile.update');
    Route::delete('/profile', [ProfileController::class, 'destroy'])->name('profile.destroy');
});

// Breeze's auth routes.
require __DIR__.'/auth.php';
