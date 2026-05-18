@php
    $isArabic = str_starts_with(app()->getLocale(), 'ar');
    $dir = $isArabic ? 'rtl' : 'ltr';
    $altLocale = $isArabic ? 'en' : 'ar';
    $currentPath = request()->path();
    // Strip leading /ar or /en from the path so we can build the alt-locale URL.
    $pathNoLocale = preg_replace('#^(ar|en)(/|$)#', '', $currentPath);
    $altUrl = '/'.$altLocale.($pathNoLocale === '' ? '' : '/'.$pathNoLocale);
@endphp
<!DOCTYPE html>
<html lang="{{ app()->getLocale() }}" dir="{{ $dir }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@yield('title', __('messages.app.name'))</title>
    <meta name="description" content="@yield('description', __('messages.app.tagline'))" />
    <link rel="alternate" hreflang="ar-EG" href="{{ url('/ar'.$pathNoLocale) }}" />
    <link rel="alternate" hreflang="en-US" href="{{ url('/en'.$pathNoLocale) }}" />
    @vite(['resources/css/app.css', 'resources/js/app.js'])
</head>
<body class="min-h-screen bg-amber-50 text-stone-900 antialiased">

    {{-- T033 — Mission Control marketing header. Sidebar is portal-only;
         the public marketing surface gets a simple top nav. --}}
    <header class="border-b border-stone-200 bg-white shadow-sm">
        <nav class="mx-auto flex max-w-6xl items-center justify-between gap-6 px-4 py-4">
            <a href="/" class="text-xl font-bold text-stone-900">{{ __('messages.app.name') }}</a>
            <div class="flex items-center gap-4 text-sm">
                <a href="/features" class="hover:text-amber-600">{{ __('messages.nav.features') }}</a>
                <a href="/pricing" class="hover:text-amber-600">{{ __('messages.nav.pricing') }}</a>
                <a href="/downloads" class="hover:text-amber-600">{{ __('messages.nav.downloads') }}</a>
                <a href="/about" class="hover:text-amber-600">{{ __('messages.nav.about') }}</a>
                <a href="/contact" class="hover:text-amber-600">{{ __('messages.nav.contact') }}</a>
                <a href="/login" class="rounded border border-stone-300 px-3 py-1 hover:bg-stone-100">{{ __('messages.nav.login') }}</a>
                <a href="/register" class="rounded bg-amber-600 px-3 py-1 font-semibold text-white hover:bg-amber-700">{{ __('messages.nav.register') }}</a>
                <a href="{{ $altUrl }}" class="rounded border border-stone-200 px-2 py-1 text-xs text-stone-600 hover:bg-stone-100" aria-label="Switch language">
                    {{ $isArabic ? 'EN' : 'ع' }}
                </a>
            </div>
        </nav>
    </header>

    <main class="mx-auto max-w-6xl px-4 py-12">
        @yield('content')
    </main>

    {{-- T051 — footer with legal links per FR-007. --}}
    <footer class="mt-16 border-t border-stone-200 bg-white">
        <div class="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-4 py-6 text-xs text-stone-500">
            <span>© {{ date('Y') }} {{ __('messages.app.name') }}. {{ __('messages.footer.tagline') }}</span>
            <div class="flex flex-wrap gap-4">
                <a href="/terms" class="hover:text-amber-600">{{ __('messages.footer.terms') }}</a>
                <a href="/refund" class="hover:text-amber-600">{{ __('messages.footer.refund') }}</a>
                <a href="/privacy" class="hover:text-amber-600">{{ __('messages.footer.privacy') }}</a>
                <a href="/privacy/android" class="hover:text-amber-600">{{ __('messages.footer.privacy_android') }}</a>
            </div>
        </div>
    </footer>

</body>
</html>
