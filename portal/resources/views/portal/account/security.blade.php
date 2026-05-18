@extends('layouts.portal')

@section('title', 'الأمان والحساب')

@section('content')
    <header class="mb-8">
        <p class="eyebrow mb-2">الأمان والإعدادات</p>
        <h1 class="display-1 mb-1.5">الأمان والحساب</h1>
        <p class="text-ink-600 text-base">إعدادات الحساب الشخصي + المصادقة الثنائية + الجلسات النشطة.</p>
    </header>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">

        {{-- Account info --}}
        <div class="card-padded">
            <h2 class="card-section-title">معلومات الحساب</h2>
            <dl class="space-y-3 text-sm">
                <div>
                    <dt class="text-ink-500 text-xs">الاسم</dt>
                    <dd class="text-ink-900 font-medium">{{ $user->display_name ?? '—' }}</dd>
                </div>
                <div>
                    <dt class="text-ink-500 text-xs">البريد الإلكتروني</dt>
                    <dd class="text-ink-900 font-medium">{{ $user->email }}</dd>
                </div>
                <div>
                    <dt class="text-ink-500 text-xs">حالة البريد</dt>
                    <dd>
                        @if ($user->email_verified_at)
                            <span class="pill-success">مؤكد ✓</span>
                            <span class="text-xs text-ink-500 ms-2">{{ $user->email_verified_at->format('Y-m-d') }}</span>
                        @else
                            <span class="pill-warning">غير مؤكد</span>
                        @endif
                    </dd>
                </div>
                <div>
                    <dt class="text-ink-500 text-xs">اللغة المفضلة</dt>
                    <dd class="text-ink-900">{{ $user->locale_preference === 'ar-EG' ? 'العربية' : 'English' }}</dd>
                </div>
                <div>
                    <dt class="text-ink-500 text-xs">آخر دخول</dt>
                    <dd class="text-ink-900 text-sm">{{ $user->last_login_at?->format('Y-m-d H:i') ?? 'دلوقتي' }}</dd>
                </div>
            </dl>
        </div>

        {{-- TOTP MFA --}}
        <div class="card-padded">
            <h2 class="card-section-title">المصادقة الثنائية (TOTP)</h2>
            @if ($user->mfa_enabled_at)
                <div class="rounded-lg bg-emerald-50 border border-emerald-200 p-4 mb-4">
                    <p class="text-sm text-emerald-800">
                        <strong>✓ MFA مفعّل</strong> منذ {{ $user->mfa_enabled_at->format('Y-m-d') }}.
                        لازم تكتب الكود من Google Authenticator عند كل تسجيل دخول.
                    </p>
                </div>
                <button type="button" class="btn-danger" disabled>
                    تعطيل MFA (يحتاج كلمة المرور — Phase 9 polish)
                </button>
            @else
                <p class="text-sm text-ink-600 mb-3">
                    أضف طبقة أمان إضافية بـ TOTP. هتحتاج تطبيق Google Authenticator أو Authy على موبايلك.
                </p>
                <ul class="space-y-1 text-xs text-ink-500 mb-4 list-disc list-inside">
                    <li>الكود بيتولّد على موبايلك كل 30 ثانية</li>
                    <li>بيمنع دخول حد عرف كلمة مرورك (حتى لو سرقها)</li>
                    <li>إجباري للمالكين لو المؤسسة فعّلت السياسة (FR-011 / T155)</li>
                </ul>
                <button type="button" class="btn-primary" disabled>
                    تفعيل MFA (Phase 9 polish — تظهر QR code + verify)
                </button>
            @endif
        </div>

        {{-- Active sessions --}}
        <div class="card-padded lg:col-span-2">
            <h2 class="card-section-title">الجلسات النشطة</h2>
            <p class="text-sm text-ink-600 mb-3">
                كل الأجهزة اللي مسجّل دخول منها لحسابك. لو شفت جلسة مش بتاعتك، سجّل خروج فوراً وغيّر كلمة المرور.
            </p>
            <div class="rounded-lg bg-ink-50 border border-ink-100 p-4 text-sm text-ink-600">
                Phase 9 polish — هتعرض الجلسات من جدول `sessions` مع IP + المتصفح + آخر نشاط + زر "إنهاء الجلسة دي" لكل واحدة.
            </div>
        </div>

        {{-- Auth events log --}}
        <div class="card-padded lg:col-span-2">
            <h2 class="card-section-title">سجل أحداث الدخول</h2>
            <p class="text-sm text-ink-600 mb-3">
                كل عملية تسجيل دخول، MFA challenge، تغيير كلمة مرور — مع timestamp + IP. محفوظ لمدة 90 يوم (FR-028).
            </p>
            <div class="rounded-lg bg-ink-50 border border-ink-100 p-4 text-sm text-ink-600">
                Phase 9 polish — هتعرض آخر 90 يوم من login_at + ip_address + user_agent من جدول authentication_events لما يتعمل (T028 من spec).
            </div>
        </div>

        {{-- Danger zone --}}
        <div class="lg:col-span-2 rounded-2xl border-2 border-red-200 bg-red-50/60 p-6 relative overflow-hidden">
            <span class="absolute inset-y-0 start-0 w-1 bg-red-500"></span>
            <div class="flex items-start gap-4">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-red-100 text-red-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0ZM12 9v4m0 4h.01" />
                    </svg>
                </span>
                <div class="flex-1">
                    <h2 class="text-lg font-bold text-red-900 mb-1">المنطقة الخطرة</h2>
                    <p class="text-sm text-red-800 mb-4">
                        حذف حسابك نهائياً — مع 30 يوم Grace Period عشان تستعيده لو غيّرت رأيك.
                    </p>
                    <a href="{{ route('portal.account.delete') }}" class="btn-danger">
                        طلب حذف الحساب
                    </a>
                </div>
            </div>
        </div>
    </div>
@endsection
