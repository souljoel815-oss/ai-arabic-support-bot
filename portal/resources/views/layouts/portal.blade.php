@php
    $isArabic = str_starts_with(app()->getLocale(), 'ar');
    $dir = $isArabic ? 'rtl' : 'ltr';
@endphp
<!DOCTYPE html>
<html lang="{{ app()->getLocale() }}" dir="{{ $dir }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@yield('title', __('messages.app.name')) — Portal</title>
    @vite(['resources/css/app.css', 'resources/js/app.js'])
</head>
<body class="min-h-screen bg-amber-50 text-stone-900 antialiased">
    <div class="flex min-h-screen">

        {{-- T034 — Mission Control portal sidebar. Single-tier (the
             portal has ~12 screens vs the on-prem product's ~55, so the
             2-tier sidebar locked into the on-prem build is overkill
             here). --}}
        <aside class="w-56 shrink-0 border-e border-stone-200 bg-white">
            <div class="border-b border-stone-200 px-4 py-4">
                <a href="/portal" class="block text-lg font-bold text-stone-900">{{ __('messages.app.name') }}</a>
                <span class="text-xs text-stone-500">Customer portal</span>
            </div>
            <nav class="flex flex-col p-2 text-sm">
                <a href="/portal" class="rounded px-3 py-2 hover:bg-stone-100">Dashboard</a>
                <a href="/portal/licences" class="rounded px-3 py-2 hover:bg-stone-100">Licences</a>
                <a href="/portal/subscription" class="rounded px-3 py-2 hover:bg-stone-100">Subscription</a>
                <a href="/portal/billing" class="rounded px-3 py-2 hover:bg-stone-100">Billing</a>
                <a href="/portal/downloads" class="rounded px-3 py-2 hover:bg-stone-100">Downloads</a>
                <a href="/portal/support" class="rounded px-3 py-2 hover:bg-stone-100">Support</a>
                <a href="/portal/organisation" class="rounded px-3 py-2 hover:bg-stone-100">Organisation</a>
                <a href="/portal/account/security" class="rounded px-3 py-2 hover:bg-stone-100">Account &amp; security</a>
                <form method="POST" action="{{ route('logout') }}" class="mt-4 border-t border-stone-200 pt-2">
                    @csrf
                    <button type="submit" class="w-full rounded px-3 py-2 text-start hover:bg-stone-100">
                        Log out
                    </button>
                </form>
            </nav>
        </aside>

        <main class="flex-1 px-8 py-8">
            @if (session('status'))
                <div class="mb-4 rounded bg-amber-100 px-4 py-2 text-sm text-amber-900">
                    {{ session('status') }}
                </div>
            @endif

            @yield('content')
        </main>
    </div>
</body>
</html>
