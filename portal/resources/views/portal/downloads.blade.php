@extends('layouts.portal')

@section('title', 'التحميل')

@section('content')
    <header class="mb-8">
        <p class="eyebrow mb-2">تحميل البرامج</p>
        <h1 class="display-1 mb-1.5">أحدث إصدارات DaftarX</h1>
        <p class="text-ink-600 text-base">
            الترخيص يتم تفعيله من صفحة
            <a href="{{ route('portal.licences') }}" class="text-brand-700 hover:text-brand-800 font-semibold underline-offset-2 hover:underline">التراخيص</a>
            بعد تثبيت البرنامج.
        </p>
        @if ($hasSubscription)
            <div class="mt-4 inline-flex items-center gap-2 rounded-xl border border-brand-200 bg-brand-50 px-4 py-2 text-sm">
                <span class="text-ink-600">خطّتك الحالية:</span>
                <span class="pill-brand">{{ $activeTier }}</span>
                @if ($isLanCapable)
                    <span class="text-ink-300">·</span>
                    <span class="text-emerald-700 font-semibold">✓ يحق لك تنزيل LAN-client</span>
                @endif
            </div>
        @else
            <div class="mt-4 rounded-2xl border border-brand-200 bg-brand-50/70 px-5 py-4 text-sm text-ink-800 flex items-center justify-between gap-4 flex-wrap shadow-elevation-1">
                <span>
                    مفيش اشتراك نشط — أنت في وضع التجربة (14 يوم). البرنامج هيشتغل بكل المميزات حتى تنتهي التجربة.
                </span>
                <a href="{{ route('portal.subscription.start') }}" class="btn-primary !py-2 !px-4 !text-sm">
                    اشترك في خطة
                </a>
            </div>
        @endif
    </header>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-5">

        {{-- Windows Desktop --}}
        <article class="card-padded card-hover relative overflow-hidden">
            <span class="absolute top-0 end-0 h-32 w-32 rounded-full opacity-10 -translate-y-12 translate-x-12"
                  style="background: radial-gradient(circle, #0078d4 0%, transparent 70%);"></span>
            <div class="flex items-start gap-4 mb-4">
                <span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-sky-100 text-sky-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M0 3.449 9.75 2.1v9.45H0V3.449zm10.949-1.524L24 0v11.4H10.949V1.925zM0 12.6h9.75v9.451L0 20.699V12.6zm10.949 0H24V24l-13.051-1.799V12.6z"/>
                    </svg>
                </span>
                <div class="flex-1 min-w-0">
                    <h2 class="display-2 text-xl mb-1">Windows Desktop</h2>
                    <p class="text-sm text-ink-600">التثبيت الكامل — السيرفر + بياناتك على جهازك.</p>
                </div>
                <span class="pill-success">متاح</span>
            </div>
            <dl class="grid grid-cols-2 gap-3 text-xs mb-4">
                <div><dt class="eyebrow-muted mb-1">الإصدار</dt><dd class="font-mono font-semibold text-ink-900">v{{ $artifacts['desktop']['version'] }}</dd></div>
                <div><dt class="eyebrow-muted mb-1">تاريخ الإصدار</dt><dd class="text-ink-700">{{ $artifacts['desktop']['released_on'] }}</dd></div>
                <div><dt class="eyebrow-muted mb-1">الحجم</dt><dd class="text-ink-700">{{ number_format($artifacts['desktop']['size_bytes'] / 1024 / 1024, 0) }} ميجابايت</dd></div>
                <div><dt class="eyebrow-muted mb-1">SHA-256</dt><dd class="font-mono text-[10px] text-ink-500">{{ Str::limit($artifacts['desktop']['sha256'], 16) }}</dd></div>
            </dl>
            <a href="{{ $artifacts['desktop']['url'] }}" class="btn-primary w-full !py-3">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M12 3v12m-5-5 5 5 5-5M5 21h14" />
                </svg>
                تنزيل {{ $artifacts['desktop']['name'] }}
            </a>
            <p class="form-hint mt-2">يدعم Windows 10/11 (64-bit). 4 GB RAM أو أكثر.</p>
        </article>

        {{-- LAN client --}}
        <article class="card-padded card-hover relative overflow-hidden">
            <div class="flex items-start gap-4 mb-4">
                <span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-emerald-50 text-emerald-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <rect x="2" y="3" width="20" height="14" rx="2"/><line x1="8" y1="21" x2="16" y2="21"/><line x1="12" y1="17" x2="12" y2="21"/>
                    </svg>
                </span>
                <div class="flex-1 min-w-0">
                    <h2 class="display-2 text-xl mb-1">LAN Client</h2>
                    <p class="text-sm text-ink-600">برنامج خفيف للأجهزة الإضافية على نفس السيرفر.</p>
                </div>
                @if ($isLanCapable)
                    <span class="pill-success">متاح</span>
                @else
                    <span class="pill-muted">يحتاج SMB+</span>
                @endif
            </div>
            <dl class="grid grid-cols-2 gap-3 text-xs mb-4">
                <div><dt class="eyebrow-muted mb-1">الإصدار</dt><dd class="font-mono font-semibold text-ink-900">v{{ $artifacts['lan']['version'] }}</dd></div>
                <div><dt class="eyebrow-muted mb-1">تاريخ الإصدار</dt><dd class="text-ink-700">{{ $artifacts['lan']['released_on'] }}</dd></div>
                <div><dt class="eyebrow-muted mb-1">الحجم</dt><dd class="text-ink-700">{{ number_format($artifacts['lan']['size_bytes'] / 1024 / 1024, 0) }} ميجابايت</dd></div>
            </dl>
            @if ($isLanCapable)
                <a href="{{ $artifacts['lan']['url'] }}" class="btn-primary w-full !py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M12 3v12m-5-5 5 5 5-5M5 21h14" />
                    </svg>
                    تنزيل {{ $artifacts['lan']['name'] }}
                </a>
            @else
                <a href="{{ route('portal.subscription.start') }}" class="btn-secondary w-full !py-3">
                    رقّي اشتراكك لـ SMB أو أعلى
                </a>
            @endif
        </article>

        {{-- Android --}}
        <article class="card-padded card-hover lg:col-span-2 relative overflow-hidden">
            <div class="flex items-start gap-4 mb-4">
                <span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl text-white"
                      style="background: linear-gradient(135deg, #3ddc84 0%, #1ba768 100%);">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M17.523 15.341a1.118 1.118 0 1 1 0-2.236 1.118 1.118 0 0 1 0 2.236m-11.046 0a1.118 1.118 0 1 1 0-2.236 1.118 1.118 0 0 1 0 2.236M5.05 8.39 3.013 4.857a.416.416 0 1 1 .721-.418L5.799 8.02C7.078 7.385 8.493 7.045 10 7.045s2.922.34 4.201.975l2.065-3.581a.416.416 0 1 1 .72.418L14.95 8.39c2.27 1.158 3.811 3.302 3.811 5.815H1.239c0-2.513 1.541-4.657 3.811-5.815"/>
                    </svg>
                </span>
                <div class="flex-1 min-w-0">
                    <h2 class="display-2 text-xl mb-1">تطبيق Android</h2>
                    <p class="text-sm text-ink-600">افتح نفس بياناتك من الموبايل. يحتاج البرنامج Desktop شغّال على نفس الـ Wi-Fi.</p>
                </div>
                <span class="pill-success">متاح</span>
            </div>
            <dl class="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs mb-4">
                <div><dt class="eyebrow-muted mb-1">الإصدار</dt><dd class="font-mono font-semibold text-ink-900">v{{ $artifacts['android']['version'] }}</dd></div>
                <div><dt class="eyebrow-muted mb-1">تاريخ الإصدار</dt><dd class="text-ink-700">{{ $artifacts['android']['released_on'] }}</dd></div>
                <div><dt class="eyebrow-muted mb-1">الحجم</dt><dd class="text-ink-700">{{ number_format($artifacts['android']['size_bytes'] / 1024 / 1024, 0) }} MB</dd></div>
                <div><dt class="eyebrow-muted mb-1">الحد الأدنى</dt><dd class="text-ink-700">Android 10+</dd></div>
            </dl>
            <div class="flex flex-col sm:flex-row gap-3">
                <a href="{{ $artifacts['android']['play_url'] }}" target="_blank" rel="noopener" class="btn-primary flex-1 !py-3">
                    ▶ Google Play Store
                </a>
                <a href="{{ $artifacts['android']['url'] }}" class="btn-secondary flex-1 !py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M12 3v12m-5-5 5 5 5-5M5 21h14" />
                    </svg>
                    تنزيل APK مباشر
                </a>
            </div>
        </article>
    </div>

    <div class="mt-10 rounded-2xl border border-ink-100 bg-white/60 px-5 py-4 text-center text-xs text-ink-600 flex items-center justify-center gap-2">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-emerald-600" viewBox="0 0 24 24"
             fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
            <path d="M9 12l2 2 4-4M21 12c0 5-3.5 9-9 9s-9-4-9-9 3.5-9 9-9 9 4 9 9z" />
        </svg>
        كل الـ installers موقّعة رقمياً بشهادة DaftarX. تحقّق من SHA-256 لو في شك.
    </div>
@endsection
