<?php

use App\Http\Controllers\AcceptInvitationController;
use App\Http\Controllers\Marketing\AboutController;
use App\Http\Controllers\Marketing\ContactController;
use App\Http\Controllers\Marketing\DownloadsController as MarketingDownloadsController;
use App\Http\Controllers\Marketing\FeaturesController;
use App\Http\Controllers\Marketing\HomeController;
use App\Http\Controllers\Marketing\PricingController;
use App\Http\Controllers\Marketing\TermsController;
use App\Http\Controllers\Portal\AccountController;
use App\Http\Controllers\Portal\AuditLogController;
use App\Http\Controllers\Portal\BillingController;
use App\Http\Controllers\Portal\DashboardController;
use App\Http\Controllers\Portal\DownloadsController as PortalDownloadsController;
use App\Http\Controllers\Portal\LicenceController;
use App\Http\Controllers\Portal\OrganisationController;
use App\Http\Controllers\Portal\SubscriptionController;
use App\Http\Controllers\Portal\SupportTicketController;
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
    Route::get('/downloads', [MarketingDownloadsController::class, 'show'])->name('marketing.downloads');
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
    // T136 — archived privacy-policy snapshots (one per amendment date).
    // 404 when no snapshot exists for the requested date so consent
    // claims chain cleanly.
    Route::get('/privacy/android/history/{date}', [TermsController::class, 'privacyAndroidHistory'])
        ->where('date', '\d{4}-\d{2}-\d{2}')
        ->name('marketing.privacy.android.history');
};

// /ar/* and /en/* — explicit locale prefix.
Route::prefix('{locale}')->where(['locale' => 'ar|en'])
    ->middleware(\App\Http\Middleware\LocaleResolver::class)
    ->group($marketingRoutes);

// /* — locale-less; LocaleResolver picks default.
Route::middleware(\App\Http\Middleware\LocaleResolver::class)
    ->group($marketingRoutes);

// --- Portal surface (authenticated) --------------------------------------
// Per FR-010: signup + email confirmation are decoupled — the visitor
// can browse the portal (dashboard / subscription / billing / licences
// listings) WITHOUT verifying their email; verification is only enforced
// before any PAID ACTION (creating a subscription, activating a paid
// licence, transferring a licence, requesting a refund). The `verified`
// middleware is attached per-route on the POST handlers that matter,
// not on the route group as a whole.
Route::prefix('portal')
    ->middleware(['auth', \App\Http\Middleware\OrganisationScope::class])
    ->group(function () {
        // --- US3 dashboard (T098) ---
        Route::get('/', [DashboardController::class, 'show'])->name('portal.dashboard');

        // --- US3 subscription management (T099, T103, T153) ---
        Route::get('/subscription', [SubscriptionController::class, 'index'])
            ->name('portal.subscription');
        Route::get('/subscription/start', [SubscriptionController::class, 'showStart'])
            ->name('portal.subscription.start');
        // Verified gate on the POST — submission attempts an actual paid
        // action (creates a Subscription + Invoice), so FR-010 kicks in.
        Route::post('/subscription/start', [SubscriptionController::class, 'start'])
            ->middleware('verified')
            ->name('portal.subscription.start.submit');
        Route::post('/subscription/{subscription}/cancel', [SubscriptionController::class, 'cancel'])
            ->middleware('verified')
            ->name('portal.subscription.cancel');

        // --- US3 billing (T100) ---
        Route::get('/billing', [BillingController::class, 'index'])
            ->name('portal.billing');
        Route::get('/billing/{invoice}/pdf', [BillingController::class, 'downloadPdf'])
            ->name('portal.billing.pdf');
        // Refund touches money + reverses a Paymob transaction → verified.
        Route::post('/billing/{invoice}/refund', [BillingController::class, 'refund'])
            ->middleware('verified')
            ->name('portal.billing.refund');

        // --- US2 licence self-service (T065) ---
        // GET routes (browsing licences + reading the activate form) are
        // open to unverified accounts; the POST handlers that sign a
        // token + the token-download endpoint are gated by `verified`
        // because they produce / hand over a paid-licence artefact.
        Route::get('/licences', [LicenceController::class, 'index'])
            ->name('portal.licences');
        Route::get('/licences/activate-paid', [LicenceController::class, 'showActivatePaid'])
            ->name('portal.licences.activate-paid');
        Route::post('/licences/activate-paid', [LicenceController::class, 'activatePaid'])
            ->middleware('verified')
            ->name('portal.licences.activate-paid.submit');
        Route::get('/licences/{licence}/transfer', [LicenceController::class, 'showTransfer'])
            ->name('portal.licences.transfer');
        Route::post('/licences/{licence}/transfer', [LicenceController::class, 'transfer'])
            ->middleware('verified')
            ->name('portal.licences.transfer.submit');
        Route::get('/licences/{licence}/token', [LicenceController::class, 'downloadToken'])
            ->middleware('verified')
            ->name('portal.licences.token');
        // --- US4 support tickets (T111-T114) ---
        Route::get('/support', [SupportTicketController::class, 'index'])
            ->name('portal.support');
        Route::get('/support/new', [SupportTicketController::class, 'showNew'])
            ->name('portal.support.new');
        Route::post('/support', [SupportTicketController::class, 'create'])
            ->name('portal.support.create');
        Route::get('/support/{ticket}', [SupportTicketController::class, 'show'])
            ->name('portal.support.detail');
        Route::post('/support/{ticket}/reply', [SupportTicketController::class, 'reply'])
            ->name('portal.support.reply');
        Route::get('/support/attachments/{attachment}', [SupportTicketController::class, 'downloadAttachment'])
            ->name('portal.support.attachment');

        // --- US3 tier-aware downloads (T101) ---
        Route::get('/downloads', [PortalDownloadsController::class, 'show'])
            ->name('portal.downloads');

        // --- US5 multi-user invitations (T125-T127) — Owner-only ---
        Route::get('/organisation', [OrganisationController::class, 'members'])
            ->name('portal.organisation');
        Route::get('/organisation/members', [OrganisationController::class, 'members'])
            ->name('portal.organisation.members');
        Route::post('/organisation/invitations', [OrganisationController::class, 'invite'])
            ->middleware(['verified', 'throttle:10,60'])  // FR-020 10/hour rate limit
            ->name('portal.organisation.invitations.send');
        Route::post('/organisation/memberships/{membership}/revoke', [OrganisationController::class, 'revoke'])
            ->middleware('verified')
            ->name('portal.organisation.memberships.revoke');
        // T143 — Audit log viewer (Owner-only).
        Route::get('/organisation/audit-log', [AuditLogController::class, 'show'])
            ->name('portal.organisation.audit-log');

        // --- T139 + T140 account + security pages ---
        Route::get('/account/security', [AccountController::class, 'security'])
            ->name('portal.account.security');
        Route::get('/account/delete', [AccountController::class, 'showDelete'])
            ->name('portal.account.delete');
        Route::post('/account/delete', [AccountController::class, 'delete'])
            ->middleware('verified')
            ->name('portal.account.delete.submit');
        Route::post('/account/restore', [AccountController::class, 'restore'])
            ->middleware('verified')
            ->name('portal.account.restore');
    });

// --- US5 public invitation-accept route (no auth) ----------------------
// The link sent in the invitation email lands here. The invitee may
// not have a portal account yet — they create their password on the
// accept form. Per FR-020.
Route::get('/invitations/accept', [AcceptInvitationController::class, 'show'])
    ->name('invitations.accept');
Route::post('/invitations/accept', [AcceptInvitationController::class, 'accept'])
    ->middleware('throttle:20,60')
    ->name('invitations.accept.submit');

// --- Profile (Breeze default) --------------------------------------------
Route::middleware('auth')->group(function () {
    Route::get('/profile', [ProfileController::class, 'edit'])->name('profile.edit');
    Route::patch('/profile', [ProfileController::class, 'update'])->name('profile.update');
    Route::delete('/profile', [ProfileController::class, 'destroy'])->name('profile.destroy');
});

// Breeze's auth routes.
require __DIR__.'/auth.php';
