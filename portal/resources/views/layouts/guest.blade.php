@php
    $isArabic = str_starts_with(app()->getLocale(), 'ar');
@endphp
<!DOCTYPE html>
<html lang="{{ app()->getLocale() }}" dir="{{ $isArabic ? 'rtl' : 'ltr' }}">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="csrf-token" content="{{ csrf_token() }}">
    <title>@yield('title', __('messages.app.name'))</title>
    @vite(['resources/css/app.css', 'resources/js/app.js'])
</head>
<body class="antialiased">
    <div class="auth-shell">

        {{-- Brand wordmark on top, links back to the marketing home. --}}
        <a href="/" class="mb-6 inline-flex items-center gap-2">
            <x-application-logo size="lg" />
        </a>

        <div class="auth-card">
            {{ $slot }}
        </div>

        <p class="mt-6 text-xs text-ink-500 text-center max-w-md">
            {{ __('messages.app.tagline') }} ·
            <a href="/" class="hover:text-brand-700 underline-offset-2 hover:underline">{{ __('messages.app.name') }}</a>
        </p>

    </div>
</body>
</html>
