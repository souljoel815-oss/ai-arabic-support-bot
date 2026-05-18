@php
    $isArabic = str_starts_with(app()->getLocale(), 'ar');
    $dir = $isArabic ? 'rtl' : 'ltr';
    $altLocale = $isArabic ? 'en' : 'ar';
    $currentPath = request()->path();
    // Strip leading /ar or /en from the path + normalise the result so we
    // never produce double slashes when the original path was just "/".
    $pathNoLocale = preg_replace('#^(ar|en)(/|$)#', '', $currentPath);
    $pathNoLocale = trim($pathNoLocale, '/');
    $altUrl = $pathNoLocale === '' ? '/'.$altLocale : '/'.$altLocale.'/'.$pathNoLocale;
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
    <link rel="canonical" href="{{ url($currentPath === '/' ? '/' : '/'.$currentPath) }}" />

    {{-- T054 — Open Graph + Twitter Card meta for social previews. --}}
    <meta property="og:type" content="website" />
    <meta property="og:site_name" content="{{ __('messages.app.name') }}" />
    <meta property="og:title" content="@yield('title', __('messages.app.name'))" />
    <meta property="og:description" content="@yield('description', __('messages.app.tagline'))" />
    <meta property="og:url" content="{{ url($currentPath === '/' ? '/' : '/'.$currentPath) }}" />
    <meta property="og:locale" content="{{ $isArabic ? 'ar_EG' : 'en_US' }}" />
    <meta property="og:locale:alternate" content="{{ $isArabic ? 'en_US' : 'ar_EG' }}" />
    <meta name="twitter:card" content="summary_large_image" />
    <meta name="twitter:title" content="@yield('title', __('messages.app.name'))" />
    <meta name="twitter:description" content="@yield('description', __('messages.app.tagline'))" />

    {{-- T053 — JSON-LD Organization + SoftwareApplication structured data. --}}
    <script type="application/ld+json">
    {
        "@context": "https://schema.org",
        "@type": "Organization",
        "name": "{{ __('messages.app.name') }}",
        "url": "{{ url('/') }}",
        "logo": "{{ url('/images/daftarx-logo.png') }}",
        "description": "{{ __('messages.app.tagline') }}",
        "contactPoint": {
            "@type": "ContactPoint",
            "email": "{{ __('marketing.contact.channels.sales') }}",
            "contactType": "sales",
            "areaServed": "EG",
            "availableLanguage": ["Arabic", "English"]
        }
    }
    </script>
    <script type="application/ld+json">
    {
        "@context": "https://schema.org",
        "@type": "SoftwareApplication",
        "name": "{{ __('messages.app.name') }}",
        "applicationCategory": "BusinessApplication",
        "operatingSystem": "Windows, Android",
        "offers": {
            "@type": "AggregateOffer",
            "priceCurrency": "EGP",
            "lowPrice": "{{ __('marketing.pricing.tiers.solo.monthly') }}",
            "highPrice": "{{ __('marketing.pricing.tiers.firm.monthly') }}",
            "offerCount": 4
        }
    }
    </script>

    @vite(['resources/css/app.css', 'resources/js/app.js'])
</head>
<body class="min-h-screen antialiased">

    {{-- Brand top-nav. Sticky, blurred white surface, gold underline on hover. --}}
    <header class="sticky top-0 z-30 backdrop-blur-md bg-white/80 border-b border-ink-100/60">
        <nav class="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3.5">
            <a href="/" class="flex items-center gap-2">
                <x-application-logo size="md" />
            </a>
            @php
                $navItems = [
                    ['url' => '/features',  'label' => __('messages.nav.features')],
                    ['url' => '/pricing',   'label' => __('messages.nav.pricing')],
                    ['url' => '/downloads', 'label' => __('messages.nav.downloads')],
                    ['url' => '/about',     'label' => __('messages.nav.about')],
                    ['url' => '/contact',   'label' => __('messages.nav.contact')],
                ];
            @endphp
            <div class="hidden md:flex items-center gap-1 text-sm">
                @foreach ($navItems as $item)
                    @php
                        $isActive = str_starts_with('/'.trim(request()->path(), '/'), $item['url']);
                    @endphp
                    <a href="{{ $item['url'] }}"
                       class="relative px-3 py-2 transition {{ $isActive ? 'text-brand-700 font-semibold' : 'text-ink-700 hover:text-brand-700' }}">
                        {{ $item['label'] }}
                        @if ($isActive)
                            <span class="absolute inset-x-3 -bottom-[15px] h-0.5 bg-brand-600 rounded-full"></span>
                        @endif
                    </a>
                @endforeach
            </div>
            <div class="flex items-center gap-2 text-sm">
                <a href="/login" class="btn-ghost">{{ __('messages.nav.login') }}</a>
                <a href="/register" class="btn-primary !py-2 !px-4">{{ __('messages.nav.register') }}</a>
                <a href="{{ $altUrl }}" class="rounded-full border border-ink-200 px-2.5 py-1 text-xs font-bold text-ink-600 hover:bg-ink-50 hover:text-ink-900 hover:border-ink-300 transition" aria-label="Switch language">
                    {{ $isArabic ? 'EN' : 'ع' }}
                </a>
            </div>
        </nav>
    </header>

    <main class="mx-auto max-w-6xl px-4 py-12">
        @yield('content')
    </main>

    <footer class="mt-24 border-t border-ink-100 bg-white">
        <div class="mx-auto max-w-6xl px-4 py-12 grid grid-cols-1 md:grid-cols-4 gap-10">
            <div class="md:col-span-2">
                <x-application-logo size="md" />
                <p class="mt-4 text-sm text-ink-600 max-w-sm leading-relaxed">{{ __('messages.app.tagline') }}</p>
            </div>
            <div>
                <p class="eyebrow-muted mb-4">{{ __('messages.nav.features') }}</p>
                <ul class="space-y-2.5 text-sm">
                    <li><a href="/features" class="text-ink-600 hover:text-brand-700 transition">{{ __('messages.nav.features') }}</a></li>
                    <li><a href="/pricing" class="text-ink-600 hover:text-brand-700 transition">{{ __('messages.nav.pricing') }}</a></li>
                    <li><a href="/downloads" class="text-ink-600 hover:text-brand-700 transition">{{ __('messages.nav.downloads') }}</a></li>
                </ul>
            </div>
            <div>
                <p class="eyebrow-muted mb-4">المؤسسة</p>
                <ul class="space-y-2.5 text-sm">
                    <li><a href="/about" class="text-ink-600 hover:text-brand-700 transition">{{ __('messages.nav.about') }}</a></li>
                    <li><a href="/contact" class="text-ink-600 hover:text-brand-700 transition">{{ __('messages.nav.contact') }}</a></li>
                    <li><a href="/terms" class="text-ink-600 hover:text-brand-700 transition">{{ __('messages.footer.terms') }}</a></li>
                    <li><a href="/refund" class="text-ink-600 hover:text-brand-700 transition">{{ __('messages.footer.refund') }}</a></li>
                    <li><a href="/privacy" class="text-ink-600 hover:text-brand-700 transition">{{ __('messages.footer.privacy') }}</a></li>
                    <li><a href="/privacy/android" class="text-ink-600 hover:text-brand-700 transition">{{ __('messages.footer.privacy_android') }}</a></li>
                </ul>
            </div>
        </div>
        <div class="border-t border-ink-100/70">
            <div class="mx-auto max-w-6xl px-4 py-5 flex flex-wrap items-center justify-between gap-3 text-xs text-ink-500">
                <p>© {{ date('Y') }} {{ __('messages.app.name') }} · {{ __('messages.footer.tagline') }}</p>
                <p class="flex items-center gap-2">
                    <span class="flex h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse-soft"></span>
                    <span>كل الأنظمة شغّالة</span>
                </p>
            </div>
        </div>
    </footer>

</body>
</html>
