@php
    $isArabic = str_starts_with(app()->getLocale(), 'ar');
    $dir = $isArabic ? 'rtl' : 'ltr';
    $currentPath = '/' . trim(request()->path(), '/');
    $user = auth()->user();
    $primaryMembership = $user
        ?->customerOrganisations()
        ?->wherePivotNull('revoked_at')
        ?->wherePivotNotNull('accepted_at')
        ?->first();
    $primaryRole = $primaryMembership?->pivot?->role;
    $primaryOrgName = $primaryMembership?->legal_name_ar;

    $navItems = [
        ['url' => '/portal',                  'label' => 'الرئيسية',         'label_en' => 'Dashboard',    'icon' => 'home'],
        ['url' => '/portal/licences',         'label' => 'التراخيص',         'label_en' => 'Licences',     'icon' => 'key'],
        ['url' => '/portal/subscription',     'label' => 'الاشتراك',         'label_en' => 'Subscription', 'icon' => 'star'],
        ['url' => '/portal/billing',          'label' => 'الفواتير',         'label_en' => 'Invoices',     'icon' => 'receipt'],
        ['url' => '/portal/downloads',        'label' => 'التحميل',          'label_en' => 'Downloads',    'icon' => 'download'],
        ['url' => '/portal/support',          'label' => 'الدعم الفني',      'label_en' => 'Support',      'icon' => 'support'],
        ['url' => '/portal/organisation',     'label' => 'المؤسسة',          'label_en' => 'Organisation', 'icon' => 'building'],
        ['url' => '/portal/account/security', 'label' => 'الأمان والحساب',   'label_en' => 'Account',      'icon' => 'shield'],
    ];

    $iconPaths = [
        'home'     => 'M3 12 12 3l9 9M5 10v10h14V10',
        'key'      => 'M21 2 13 10m3 3-2-2m-1 6a4 4 0 1 1-5.66-5.66 4 4 0 0 1 5.66 5.66Z',
        'star'     => 'm12 3 2.7 6 6.3.6-4.7 4.3 1.3 6.1L12 17l-5.6 3 1.3-6.1L3 9.6 9.3 9Z',
        'receipt'  => 'M6 3v18l3-2 3 2 3-2 3 2V3zM9 8h6m-6 4h6m-6 4h4',
        'download' => 'M12 3v12m-5-5 5 5 5-5M5 21h14',
        'support'  => 'M18.36 5.64A9 9 0 1 1 5.64 18.36 9 9 0 0 1 18.36 5.64Zm-9.55 9.55 2.83-2.83m4.95-4.95-2.83 2.83',
        'building' => 'M3 21h18M5 21V5l7-2 7 2v16M9 9h.01M13 9h.01M9 13h.01M13 13h.01M9 17h.01M13 17h.01',
        'shield'   => 'M12 3 4 6v6c0 4.5 3.2 8.5 8 9 4.8-.5 8-4.5 8-9V6Z',
    ];
@endphp
<!DOCTYPE html>
<html lang="{{ app()->getLocale() }}" dir="{{ $dir }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <link rel="icon" href="/favicon.ico" sizes="any">
    <link rel="icon" href="/images/daftarx-logo.png" type="image/png">
    <link rel="apple-touch-icon" href="/images/daftarx-logo.png">
    <title>@yield('title', __('messages.app.name')) · بوابة العملاء</title>
    @vite(['resources/css/app.css', 'resources/js/app.js'])
</head>
<body class="antialiased" x-data="{ sidebarOpen: false }">
    <div class="flex min-h-screen">

        {{-- ─── Mobile top bar (md:hidden) — visible only on small screens ── --}}
        <header class="md:hidden fixed inset-x-0 top-0 z-30 flex items-center justify-between bg-white border-b border-ink-100 px-4 h-14">
            <a href="/portal" class="flex items-center">
                <x-application-logo size="sm" />
            </a>
            <button type="button"
                    class="inline-flex items-center justify-center h-10 w-10 rounded-lg text-ink-700 hover:bg-ink-100 transition"
                    @click="sidebarOpen = true"
                    aria-label="Open menu">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M3 12h18M3 6h18M3 18h18" />
                </svg>
            </button>
        </header>

        {{-- Mobile backdrop --}}
        <div x-show="sidebarOpen"
             x-transition.opacity
             @click="sidebarOpen = false"
             class="md:hidden fixed inset-0 z-40 bg-ink-950/40 backdrop-blur-sm"
             style="display:none"></div>

        {{-- ─── Sidebar — clean, light, generous spacing ──────── --}}
        {{-- Desktop: always visible 240px column. Mobile: hidden by
             default, slides in as drawer from the side when toggled.
             Use a slot-aware off-screen class so RTL slides from the
             trailing edge naturally. --}}
        @php
            $offscreenClass = $dir === 'rtl' ? 'translate-x-full' : '-translate-x-full';
        @endphp
        <aside class="fixed md:relative inset-y-0 start-0 z-50 w-[260px] md:w-[240px] shrink-0 flex flex-col bg-white border-end border-ink-100/80 transform transition-transform duration-200 md:translate-x-0 {{ $offscreenClass }}"
               :class="sidebarOpen ? 'translate-x-0' : '{{ $offscreenClass }} md:translate-x-0'">

            {{-- Brand bar --}}
            <div class="px-5 pt-6 pb-5 flex items-center justify-between">
                <a href="/portal" class="block" @click="sidebarOpen = false">
                    <x-application-logo size="md" />
                </a>
                {{-- Close button — only on mobile --}}
                <button type="button"
                        class="md:hidden inline-flex items-center justify-center h-9 w-9 rounded-lg text-ink-500 hover:bg-ink-100 hover:text-ink-900 transition"
                        @click="sidebarOpen = false"
                        aria-label="Close menu">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M18 6 6 18M6 6l12 12" />
                    </svg>
                </button>
            </div>

            {{-- Nav --}}
            <nav class="flex-1 overflow-y-auto px-3 pb-4 space-y-0.5 text-[15px]">
                @foreach ($navItems as $item)
                    @php
                        $isActive = $currentPath === $item['url']
                            || ($item['url'] !== '/portal' && str_starts_with($currentPath, $item['url']));
                    @endphp
                    <a href="{{ $item['url'] }}"
                       class="group relative flex items-center gap-3 rounded-lg px-3 py-2.5 transition
                              {{ $isActive
                                  ? 'bg-brand-50 text-brand-800 font-semibold'
                                  : 'text-ink-700 hover:bg-ink-50 hover:text-ink-950' }}">
                        @if ($isActive)
                            <span class="absolute inset-y-2 start-0 w-[3px] rounded-full bg-brand-600"></span>
                        @endif
                        <span class="{{ $isActive ? 'text-brand-600' : 'text-ink-400 group-hover:text-ink-600' }} shrink-0 transition">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-[18px] w-[18px]" viewBox="0 0 24 24"
                                 fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
                                <path d="{{ $iconPaths[$item['icon']] }}" />
                            </svg>
                        </span>
                        <span>{{ $isArabic ? $item['label'] : $item['label_en'] }}</span>
                    </a>
                @endforeach
            </nav>

            {{-- Profile footer — compact, just identity + logout --}}
            <div class="border-t border-ink-100 p-3">
                <div class="flex items-center gap-3 px-2 py-2">
                    @php
                        $initial = mb_strtoupper(mb_substr($user?->display_name ?? $user?->email ?? '?', 0, 1));
                    @endphp
                    <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-white text-sm font-bold"
                          style="background: linear-gradient(135deg, #d68a1f 0%, #b06d18 100%);">
                        {{ $initial }}
                    </span>
                    <div class="min-w-0 flex-1">
                        <div class="truncate text-sm font-semibold text-ink-900 leading-tight">{{ $user?->display_name ?? $user?->email }}</div>
                        <div class="truncate text-xs text-ink-500 mt-0.5">
                            @if ($primaryRole)
                                {{ __('organisation.role.'.$primaryRole) }}
                            @else
                                {{ $user?->email }}
                            @endif
                        </div>
                    </div>
                    <form method="POST" action="{{ route('logout') }}">
                        @csrf
                        <button type="submit"
                                class="flex h-8 w-8 items-center justify-center rounded-lg text-ink-400 hover:bg-red-50 hover:text-red-600 transition"
                                title="تسجيل الخروج">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                                 fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9" />
                            </svg>
                        </button>
                    </form>
                </div>
            </div>
        </aside>

        {{-- ─── Main content area ──────────────────────────────── --}}
        {{-- Top padding pt-20 on mobile to clear the fixed header bar; pt-8 on desktop. --}}
        <main class="flex-1 min-w-0 px-4 md:px-8 pt-20 md:pt-8 pb-8 max-w-[1180px] animate-fade-in-up">
            @if (session('status'))
                <div class="mb-6 rounded-xl bg-emerald-50 border border-emerald-200 px-4 py-3 text-sm text-emerald-900 flex items-center gap-3 shadow-elevation-1">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-emerald-600" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14M22 4 12 14.01l-3-3" />
                    </svg>
                    <span>{{ session('status') }}</span>
                </div>
            @endif
            @if ($errors->any())
                <div class="mb-6 rounded-xl bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-800 shadow-elevation-1">
                    <ul class="list-disc list-inside space-y-1">
                        @foreach ($errors->all() as $msg)
                            <li>{{ $msg }}</li>
                        @endforeach
                    </ul>
                </div>
            @endif

            @yield('content')
        </main>
    </div>
</body>
</html>
