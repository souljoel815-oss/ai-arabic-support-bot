<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\App;
use Symfony\Component\HttpFoundation\Response;

/**
 * T030 per FR-008 + research §8. Sets the active locale based on the
 * first URL path segment when it's `/ar/...` or `/en/...`. Falls back
 * to `ar-EG` (the spec's default) when no prefix is present.
 *
 * Sets two things:
 *   1. App::setLocale() — affects __('…') translations + carbon dates
 *      + validation messages.
 *   2. session('app.locale') — so the same locale persists across
 *      requests that don't carry the prefix (e.g. portal interior
 *      routes that don't bother with /ar prefix since they're
 *      authenticated).
 *
 * URL-prefix routing for marketing pages is wired in routes/web.php
 * via Route::prefix('{locale}')->where('locale', 'ar|en'); this
 * middleware just maps the prefix to a Laravel locale.
 */
class LocaleResolver
{
    private const SUPPORTED = ['ar' => 'ar-EG', 'en' => 'en-US'];
    private const DEFAULT_LOCALE = 'ar-EG';

    public function handle(Request $request, Closure $next): Response
    {
        $path = $request->path();
        $firstSegment = strtolower(strtok($path, '/'));

        $locale = self::SUPPORTED[$firstSegment]
            ?? session('app.locale')
            ?? self::DEFAULT_LOCALE;

        App::setLocale($locale);
        session(['app.locale' => $locale]);

        return $next($request);
    }
}
