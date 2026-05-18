@php
    $isArabic = str_starts_with(app()->getLocale(), 'ar');
    $dir = $isArabic ? 'rtl' : 'ltr';
    $currentPath = '/' . trim(request()->path(), '/');
    $navItems = [
        ['url' => '/portal',                  'label' => 'الرئيسية',           'label_en' => 'Dashboard',         'icon' => '◆'],
        ['url' => '/portal/licences',         'label' => 'التراخيص',           'label_en' => 'Licences',          'icon' => '◇'],
        ['url' => '/portal/subscription',     'label' => 'الاشتراك',           'label_en' => 'Subscription',      'icon' => '★'],
        ['url' => '/portal/billing',          'label' => 'الفواتير',           'label_en' => 'Billing',           'icon' => '₣'],
        ['url' => '/portal/downloads',        'label' => 'التحميل',            'label_en' => 'Downloads',         'icon' => '⬇'],
        ['url' => '/portal/support',          'label' => 'الدعم الفني',        'label_en' => 'Support',           'icon' => '?'],
        ['url' => '/portal/organisation',     'label' => 'المؤسسة',            'label_en' => 'Organisation',      'icon' => '⌂'],
        ['url' => '/portal/account/security', 'label' => 'الأمان والحساب',     'label_en' => 'Account',           'icon' => '⚙'],
    ];
@endphp
<!DOCTYPE html>
<html lang="{{ app()->getLocale() }}" dir="{{ $dir }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@yield('title', __('messages.app.name')) · بوابة العملاء</title>
    @vite(['resources/css/app.css', 'resources/js/app.js'])
</head>
<body class="antialiased">
    <div class="flex min-h-screen">

        {{-- Portal sidebar — single-tier (slim) since the portal surface
             is ~12 screens vs. the on-prem product's ~55 (where the locked
             2-tier ModuleSidebar+SubNavPanel earns its keep). --}}
        <aside class="w-60 shrink-0 border-end border-ink-100 bg-white flex flex-col">
            <div class="border-b border-ink-100 px-5 py-4">
                <a href="/portal" class="flex items-center gap-2">
                    <x-application-logo size="md" />
                </a>
                <span class="mt-1 block text-xs text-ink-500">بوابة العملاء</span>
            </div>

            <nav class="flex-1 p-3 space-y-0.5 text-sm">
                @foreach ($navItems as $item)
                    @php
                        $isActive = $currentPath === $item['url']
                            || ($item['url'] !== '/portal' && str_starts_with($currentPath, $item['url']));
                        $classes = $isActive
                            ? 'flex items-center gap-3 rounded-lg px-3 py-2 bg-brand-100 text-brand-800 font-semibold'
                            : 'flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 hover:bg-ink-50 hover:text-ink-950 transition';
                    @endphp
                    <a href="{{ $item['url'] }}" class="{{ $classes }}">
                        <span class="w-5 text-center text-brand-600" aria-hidden="true">{{ $item['icon'] }}</span>
                        <span>{{ $isArabic ? $item['label'] : $item['label_en'] }}</span>
                    </a>
                @endforeach
            </nav>

            <div class="border-t border-ink-100 p-3">
                <div class="px-3 py-2 text-xs text-ink-500">
                    <div class="font-semibold text-ink-800">{{ auth()->user()?->display_name ?? auth()->user()?->email }}</div>
                    <div class="truncate">{{ auth()->user()?->email }}</div>
                </div>
                <form method="POST" action="{{ route('logout') }}" class="mt-2">
                    @csrf
                    <button type="submit" class="w-full text-start rounded-lg px-3 py-2 text-sm text-ink-700 hover:bg-red-50 hover:text-red-700 transition">
                        تسجيل الخروج
                    </button>
                </form>
            </div>
        </aside>

        <main class="flex-1 px-8 py-8 max-w-6xl">
            @if (session('status'))
                <div class="mb-6 rounded-lg bg-emerald-50 border border-emerald-200 px-4 py-3 text-sm text-emerald-800 flex items-center gap-2">
                    <span>✓</span>
                    <span>{{ session('status') }}</span>
                </div>
            @endif
            @if ($errors->any())
                <div class="mb-6 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-800">
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
