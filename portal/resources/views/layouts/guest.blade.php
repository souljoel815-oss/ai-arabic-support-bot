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
    {{-- Clean, centered auth shell. No split-screen — the form is the
         hero. Two decorative orbs in the background give the brand a
         presence without distracting from the form. --}}
    <div class="min-h-screen flex flex-col items-center px-4 py-8 relative overflow-hidden bg-brand-50">

        {{-- Decorative orbs --}}
        <span class="pointer-events-none absolute -top-40 -end-40 h-96 w-96 rounded-full opacity-25"
              style="background: radial-gradient(circle, #d68a1f 0%, transparent 70%);"></span>
        <span class="pointer-events-none absolute -bottom-40 -start-40 h-96 w-96 rounded-full opacity-20"
              style="background: radial-gradient(circle, #f5d691 0%, transparent 70%);"></span>

        {{-- Brand mark on top --}}
        <a href="/" class="relative z-10 mt-4 mb-8 inline-flex">
            <x-application-logo size="lg" />
        </a>

        {{-- The card --}}
        <div class="relative z-10 w-full max-w-md flex-1 flex flex-col">
            <div class="card-padded shadow-elevation-3 animate-fade-in-up">
                {{ $slot }}
            </div>

            <p class="mt-6 text-xs text-ink-500 text-center">
                {{ __('messages.app.tagline') }} ·
                <a href="/" class="hover:text-brand-700 underline-offset-2 hover:underline">العودة للموقع</a>
            </p>
        </div>
    </div>
</body>
</html>
