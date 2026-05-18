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
    {{-- Split-screen auth shell: brand panel on one side, form on the
         other. On mobile it stacks (brand panel collapses to a slim
         header bar above the form). --}}
    <div class="min-h-screen flex flex-col md:flex-row">

        {{-- ─── Brand panel ───────────────────────────────── --}}
        <aside class="relative md:w-1/2 lg:w-[42%] bg-hero-charcoal text-white overflow-hidden flex flex-col">
            {{-- Decorative grid + glow --}}
            <span class="pointer-events-none absolute inset-0 opacity-20"
                  style="background-image: linear-gradient(rgba(245,214,145,0.08) 1px, transparent 1px), linear-gradient(90deg, rgba(245,214,145,0.08) 1px, transparent 1px); background-size: 44px 44px;"></span>
            <span class="pointer-events-none absolute -bottom-32 -end-32 h-96 w-96 rounded-full"
                  style="background: radial-gradient(circle, rgba(214,138,31,0.35) 0%, transparent 70%);"></span>
            <span class="pointer-events-none absolute -top-24 -start-24 h-72 w-72 rounded-full"
                  style="background: radial-gradient(circle, rgba(245,214,145,0.18) 0%, transparent 70%);"></span>

            <div class="relative z-10 flex flex-col h-full px-10 py-10 md:px-14 md:py-12">
                <a href="/" class="inline-flex items-center gap-2">
                    <x-application-logo size="lg" variant="dark" />
                </a>

                {{-- Tagline + selling points pulled forward — visible on
                     desktop, hidden on mobile (auth form needs the room) --}}
                <div class="hidden md:flex flex-1 flex-col justify-center max-w-md">
                    <p class="eyebrow-light mb-4">منصة الضرائب والمحاسبة المصرية</p>
                    <h2 class="text-4xl font-extrabold tracking-tight leading-tight mb-5">
                        إدارة ضرائبك ومحاسبتك<br>
                        <span class="text-brand-300">في مكان واحد.</span>
                    </h2>
                    <p class="text-ink-300 text-base leading-relaxed mb-8">
                        من فاتورة المبيعات لإقرار الضرائب — كل عملياتك المحاسبية في برنامج واحد متوافق مع مصلحة الضرائب المصرية.
                    </p>

                    <ul class="space-y-3 text-sm">
                        @foreach ([
                            ['icon' => '⚡', 'label' => 'إقرارات ضريبية محسوبة تلقائياً'],
                            ['icon' => '🔒', 'label' => 'بياناتك تعمل أوفلاين بالكامل'],
                            ['icon' => '📊', 'label' => 'تقارير محاسبية جاهزة للمراجع'],
                            ['icon' => '🇪🇬', 'label' => 'متوافق مع اللوائح المصرية'],
                        ] as $feature)
                            <li class="flex items-center gap-3 text-ink-200">
                                <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white/5 ring-1 ring-inset ring-white/10 text-base">
                                    {{ $feature['icon'] }}
                                </span>
                                <span>{{ $feature['label'] }}</span>
                            </li>
                        @endforeach
                    </ul>
                </div>

                <p class="hidden md:block text-xs text-ink-500 mt-auto">
                    © {{ date('Y') }} {{ __('messages.app.name') }}
                </p>
            </div>
        </aside>

        {{-- ─── Form panel ───────────────────────────────── --}}
        <main class="flex-1 flex items-center justify-center px-6 py-10 bg-brand-50">
            <div class="w-full max-w-md">
                <div class="card-padded shadow-elevation-3 animate-fade-in-up">
                    {{ $slot }}
                </div>

                <p class="mt-6 text-xs text-ink-500 text-center">
                    {{ __('messages.app.tagline') }} ·
                    <a href="/" class="hover:text-brand-700 underline-offset-2 hover:underline">العودة للموقع</a>
                </p>
            </div>
        </main>
    </div>
</body>
</html>
