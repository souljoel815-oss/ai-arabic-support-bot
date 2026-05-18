<?php

namespace App\Providers;

use Illuminate\Cache\RateLimiting\Limit;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\RateLimiter;
use Illuminate\Support\ServiceProvider;

class AppServiceProvider extends ServiceProvider
{
    public function register(): void
    {
        //
    }

    public function boot(): void
    {
        $this->configureRateLimiting();
    }

    /**
     * T147 — public-endpoint rate limits per FR-005 + research §9.
     *
     * Named limiters resolved in routes via `throttle:signup` / etc. The
     * existing `throttle:10,60` literal on the contact-form route in
     * routes/web.php continues to work; new endpoints (signup, password-
     * reset link) consume the named limiters here so all public-facing
     * quotas live in one auditable place.
     */
    protected function configureRateLimiting(): void
    {
        // Signup — 5/hour/IP. Aligns with FR-005 (abuse mitigation on the
        // /register endpoint, where each successful POST writes a row to
        // `team_members` + sends a Resend-billed verification email).
        RateLimiter::for('signup', function (Request $request) {
            return Limit::perHour(5)->by($request->ip());
        });

        // Contact form — 10/hour/IP. Matches the inline `throttle:10,60`
        // currently on the contact route; promoted here so we can swap to
        // an IP+email composite key later without touching the route file.
        RateLimiter::for('contact-form', function (Request $request) {
            return Limit::perHour(10)->by($request->ip());
        });

        // Invitation accept — 20/hour/IP. Higher because legitimate
        // invitees may click the link from a shared NAT; we still want
        // some bound to prevent enumeration of token-hash space.
        RateLimiter::for('invitation-accept', function (Request $request) {
            return Limit::perHour(20)->by($request->ip());
        });
    }
}
