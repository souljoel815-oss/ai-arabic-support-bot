@extends('layouts.portal')

@section('title', 'التحميل')

@section('content')
    <header class="mb-6">
        <h1 class="text-2xl font-bold text-ink-950 mb-1">تحميل البرامج</h1>
        <p class="text-ink-600">
            أحدث الإصدارات من برامج DaftarX. الترخيص بيتفعّل من صفحة
            <a href="{{ route('portal.licences') }}" class="text-brand-700 hover:underline">التراخيص</a>
            بعد ما تركّب البرنامج.
        </p>
        @if ($hasSubscription)
            <p class="mt-2 text-xs text-ink-500">
                خطّتك الحالية: <strong>{{ $activeTier }}</strong>
                @if ($isLanCapable)
                    · يحق لك تنزيل LAN-client للأجهزة الإضافية
                @endif
            </p>
        @else
            <div class="mt-3 rounded-lg bg-brand-50 border border-brand-200 px-4 py-3 text-sm text-ink-800">
                مفيش اشتراك نشط — أنت في وضع التجربة (14 يوم). البرنامج هيشتغل بكل المميزات حتى تنتهي التجربة.
                <a href="{{ route('portal.subscription.start') }}" class="text-brand-700 font-semibold hover:underline">اشترك في خطة ←</a>
            </div>
        @endif
    </header>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {{-- Desktop installer (always available) --}}
        <div class="card-padded">
            <div class="flex items-start justify-between gap-4 mb-3">
                <div>
                    <h2 class="text-lg font-bold text-ink-950">برنامج Windows Desktop</h2>
                    <p class="text-sm text-ink-600 mt-1">
                        التثبيت الكامل — السيرفر + بيانات شركتك على جهازك.
                    </p>
                </div>
                <span class="pill-success">متاح</span>
            </div>
            <dl class="text-xs text-ink-600 space-y-1 mb-4">
                <div class="flex justify-between"><dt>الإصدار</dt> <dd class="font-semibold text-ink-800">v{{ $artifacts['desktop']['version'] }}</dd></div>
                <div class="flex justify-between"><dt>تاريخ الإصدار</dt> <dd>{{ $artifacts['desktop']['released_on'] }}</dd></div>
                <div class="flex justify-between"><dt>الحجم</dt> <dd>{{ number_format($artifacts['desktop']['size_bytes'] / 1024 / 1024, 0) }} ميجابايت</dd></div>
                <div class="flex justify-between"><dt>SHA-256</dt> <dd class="font-mono text-[10px]">{{ Str::limit($artifacts['desktop']['sha256'], 16) }}</dd></div>
            </dl>
            <a href="{{ $artifacts['desktop']['url'] }}" class="btn-primary w-full">
                ⬇ تنزيل {{ $artifacts['desktop']['name'] }}
            </a>
            <p class="form-hint">يدعم Windows 10/11 (64-bit). 4 GB RAM أو أكثر.</p>
        </div>

        {{-- LAN client (SMB+) --}}
        <div class="card-padded">
            <div class="flex items-start justify-between gap-4 mb-3">
                <div>
                    <h2 class="text-lg font-bold text-ink-950">LAN Client</h2>
                    <p class="text-sm text-ink-600 mt-1">
                        برنامج خفيف للأجهزة الإضافية اللي بتشتغل على نفس السيرفر.
                    </p>
                </div>
                @if ($isLanCapable)
                    <span class="pill-success">متاح</span>
                @else
                    <span class="pill-muted">يحتاج SMB+</span>
                @endif
            </div>
            <dl class="text-xs text-ink-600 space-y-1 mb-4">
                <div class="flex justify-between"><dt>الإصدار</dt> <dd class="font-semibold text-ink-800">v{{ $artifacts['lan']['version'] }}</dd></div>
                <div class="flex justify-between"><dt>تاريخ الإصدار</dt> <dd>{{ $artifacts['lan']['released_on'] }}</dd></div>
                <div class="flex justify-between"><dt>الحجم</dt> <dd>{{ number_format($artifacts['lan']['size_bytes'] / 1024 / 1024, 0) }} ميجابايت</dd></div>
            </dl>
            @if ($isLanCapable)
                <a href="{{ $artifacts['lan']['url'] }}" class="btn-primary w-full">
                    ⬇ تنزيل {{ $artifacts['lan']['name'] }}
                </a>
            @else
                <a href="{{ route('portal.subscription.start') }}" class="btn-secondary w-full">
                    رقّي اشتراكك لـ SMB أو أعلى
                </a>
            @endif
        </div>

        {{-- Android --}}
        <div class="card-padded lg:col-span-2">
            <div class="flex items-start justify-between gap-4 mb-3">
                <div>
                    <h2 class="text-lg font-bold text-ink-950">تطبيق Android</h2>
                    <p class="text-sm text-ink-600 mt-1">
                        افتح نفس بياناتك من الموبايل. يحتاج البرنامج Desktop شغّال على نفس الـ Wi-Fi.
                    </p>
                </div>
                <span class="pill-success">متاح</span>
            </div>
            <dl class="text-xs text-ink-600 grid grid-cols-2 md:grid-cols-4 gap-3 mb-4">
                <div><dt class="text-ink-500">الإصدار</dt> <dd class="font-semibold text-ink-800">v{{ $artifacts['android']['version'] }}</dd></div>
                <div><dt class="text-ink-500">تاريخ الإصدار</dt> <dd>{{ $artifacts['android']['released_on'] }}</dd></div>
                <div><dt class="text-ink-500">الحجم</dt> <dd>{{ number_format($artifacts['android']['size_bytes'] / 1024 / 1024, 0) }} ميجابايت</dd></div>
                <div><dt class="text-ink-500">الحد الأدنى</dt> <dd>Android 10+</dd></div>
            </dl>
            <div class="flex flex-col sm:flex-row gap-3">
                <a href="{{ $artifacts['android']['play_url'] }}" target="_blank" rel="noopener" class="btn-primary flex-1">
                    ▶ Google Play
                </a>
                <a href="{{ $artifacts['android']['url'] }}" class="btn-secondary flex-1">
                    ⬇ تنزيل APK مباشر
                </a>
            </div>
        </div>
    </div>

    <p class="mt-8 text-center text-xs text-ink-500">
        كل الـ installers موقّعة رقمياً بشهادة DaftarX. تحقّق من SHA-256 لو في شك.
    </p>
@endsection
