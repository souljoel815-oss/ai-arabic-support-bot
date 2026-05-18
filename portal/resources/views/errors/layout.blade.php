@php
    $isArabic = str_starts_with(app()->getLocale(), 'ar');
@endphp
<!DOCTYPE html>
<html lang="{{ app()->getLocale() }}" dir="{{ $isArabic ? 'rtl' : 'ltr' }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@yield('title') · {{ __('messages.app.name') }}</title>
    @vite(['resources/css/app.css'])
</head>
<body class="antialiased">
    <div class="auth-shell">
        <a href="/" class="mb-8"><x-application-logo size="lg" /></a>
        <div class="auth-card text-center">
            @yield('code-block')
            <h1 class="text-2xl font-bold text-ink-950 mb-3">@yield('headline')</h1>
            <p class="text-ink-600 mb-6">@yield('explanation')</p>
            <div class="flex flex-col sm:flex-row gap-3 justify-center">
                <a href="/" class="btn-primary">العودة للرئيسية</a>
                @yield('extra-cta')
            </div>
        </div>
        <p class="mt-6 text-xs text-ink-500 text-center max-w-md">
            لو المشكلة استمرّت، تواصل مع <a href="/contact" class="text-brand-700 hover:underline">الدعم</a>.
        </p>
    </div>
</body>
</html>
