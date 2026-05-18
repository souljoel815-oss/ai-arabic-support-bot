@php
    $isArabic = str_starts_with(app()->getLocale(), 'ar');
@endphp
<!DOCTYPE html>
<html lang="{{ app()->getLocale() }}" dir="{{ $isArabic ? 'rtl' : 'ltr' }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <link rel="icon" href="/favicon.ico" sizes="any">
    <link rel="icon" href="/images/daftarx-logo.png" type="image/png">
    <link rel="apple-touch-icon" href="/images/daftarx-logo.png">
    <title>@yield('title') · {{ __('messages.app.name') }}</title>
    @vite(['resources/css/app.css'])
</head>
<body class="antialiased">
    <div class="min-h-screen flex flex-col items-center justify-center px-4 py-12 relative overflow-hidden bg-brand-50">

        {{-- Decorative orbs --}}
        <span class="pointer-events-none absolute -top-40 -end-40 h-96 w-96 rounded-full opacity-25"
              style="background: radial-gradient(circle, #d68a1f 0%, transparent 70%);"></span>
        <span class="pointer-events-none absolute -bottom-40 -start-40 h-96 w-96 rounded-full opacity-20"
              style="background: radial-gradient(circle, #f5d691 0%, transparent 70%);"></span>

        <a href="/" class="relative z-10 mb-10">
            <x-application-logo size="lg" />
        </a>

        <div class="relative z-10 card-padded text-center max-w-md w-full shadow-elevation-3 animate-fade-in-up">
            @yield('code-block')
            <h1 class="display-2 mb-3">@yield('headline')</h1>
            <p class="text-ink-600 mb-7 leading-relaxed">@yield('explanation')</p>
            <div class="flex flex-col sm:flex-row gap-3 justify-center">
                <a href="/" class="btn-primary !py-3">العودة للرئيسية</a>
                @yield('extra-cta')
            </div>
        </div>

        <p class="relative z-10 mt-7 text-xs text-ink-500 text-center max-w-md">
            لو المشكلة استمرّت، تواصل مع
            <a href="/contact" class="text-brand-700 font-semibold hover:underline">الدعم</a>.
        </p>
    </div>
</body>
</html>
