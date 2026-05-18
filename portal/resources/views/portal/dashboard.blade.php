@extends('layouts.portal')

@section('title', 'الرئيسية')

@php
    // Derive a primary subscription + countdown-ring metrics. If there's
    // no active subscription, the page renders the "subscribe to keep
    // going" hero instead and these vars stay null.
    $primary = $subscriptions->first();
    $daysToRenewal = $primary?->current_period_end_at
        ? max(0, (int) now()->startOfDay()->diffInDays($primary->current_period_end_at->startOfDay(), false))
        : null;
    // Assume monthly cadence = 30, annual = 365 for the ring math.
    $periodLength = $primary?->billing_cadence === 'Yearly' ? 365 : 30;
    $renewalProgress = $daysToRenewal !== null
        ? max(0, min(100, 100 - (int) round(($daysToRenewal / $periodLength) * 100)))
        : 0;
    // SVG ring: circumference = 2π·r where r=42 → ~263.89
    $ringCircumference = 2 * 3.1415927 * 42;
    $ringOffset = $ringCircumference * (1 - $renewalProgress / 100);

    $paidThisYear = $recentInvoices->where('status', 'Paid')
        ->filter(fn ($i) => $i->paid_at?->year === now()->year)
        ->sum('amount_egp_minor') / 100;
@endphp

@section('content')
    {{-- ─── Hero: greeting + status + countdown ring ─────────── --}}
    <header class="relative overflow-hidden rounded-3xl border border-brand-200/60 mb-8 px-8 py-7 bg-hero-gold shadow-elevation-2">
        <div class="relative z-10 flex flex-wrap items-center justify-between gap-6">
            <div class="min-w-0">
                <p class="eyebrow mb-2">
                    {{ now()->locale('ar')->translatedFormat('l، j F Y') }}
                </p>
                <h1 class="display-1 mb-2">
                    أهلاً، {{ $user->display_name ?? explode('@', $user->email)[0] }} <span class="inline-block animate-pulse-soft">👋</span>
                </h1>
                <p class="text-ink-600 text-base">
                    {{ $user->email }}
                </p>
                @if ($hasActiveSubscription)
                    <div class="mt-4 flex items-center gap-2 flex-wrap">
                        <span class="pill-success">✓ اشتراك نشط</span>
                        <span class="pill-brand">
                            {{ __('marketing.pricing.tiers.'.strtolower($primary->tier).'.name') }}
                        </span>
                        @if ($primary?->hasPrioritySupport())
                            <span class="pill-warning">⚡ دعم سريع</span>
                        @endif
                    </div>
                @endif
            </div>

            @if ($hasActiveSubscription && $daysToRenewal !== null)
                {{-- Countdown ring — gold circular progress to next renewal --}}
                <div class="relative shrink-0">
                    <svg width="120" height="120" viewBox="0 0 100 100" class="-rotate-90">
                        <circle cx="50" cy="50" r="42" stroke="rgba(176,109,24,0.12)" stroke-width="6" fill="none" />
                        <circle cx="50" cy="50" r="42"
                                stroke="url(#renewalGrad)" stroke-width="6" fill="none"
                                stroke-linecap="round"
                                stroke-dasharray="{{ number_format($ringCircumference, 2, '.', '') }}"
                                stroke-dashoffset="{{ number_format($ringOffset, 2, '.', '') }}"
                                style="transition: stroke-dashoffset 1s ease-out;" />
                        <defs>
                            <linearGradient id="renewalGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                                <stop offset="0%" stop-color="#d68a1f" />
                                <stop offset="100%" stop-color="#8b5616" />
                            </linearGradient>
                        </defs>
                    </svg>
                    <div class="absolute inset-0 flex flex-col items-center justify-center">
                        <span class="text-2xl font-extrabold text-ink-950 leading-none">{{ $daysToRenewal }}</span>
                        <span class="text-[10px] uppercase tracking-wider text-ink-600 mt-0.5">يوم للتجديد</span>
                    </div>
                </div>
            @endif
        </div>
        {{-- Decorative coin top corner --}}
        <span class="pointer-events-none absolute -top-16 -end-16 h-56 w-56 rounded-full opacity-30"
              style="background: radial-gradient(circle, #d68a1f 0%, transparent 70%);"></span>
    </header>

    {{-- ─── FR-010 verification banner ─────────────────────── --}}
    @if ($user->email_verified_at === null)
        <div class="mb-6 rounded-2xl border border-amber-300 bg-amber-50/80 px-5 py-4 text-sm text-amber-900 flex items-center justify-between gap-4 shadow-elevation-1">
            <div class="flex items-center gap-3 min-w-0">
                <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-amber-200/70 text-amber-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M12 9v4m0 4h.01M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z" />
                    </svg>
                </span>
                <span class="min-w-0">
                    <strong class="font-semibold">إيميلك مش متأكد لسه.</strong>
                    تقدر تتفرّج على البوابة، لكن قبل ما تشترك أو تنشّط ترخيص لازم تأكّد إيميلك.
                </span>
            </div>
            <form method="POST" action="{{ route('verification.send') }}" class="shrink-0">
                @csrf
                <button type="submit" class="btn-ghost">أعد إرسال رسالة التأكيد</button>
            </form>
        </div>
    @endif

    @if (! $hasActiveSubscription)
        {{-- ─── Trial-ending CTA hero (no active subscription) ── --}}
        <section class="card-padded mb-8 relative overflow-hidden bg-dots">
            <div class="max-w-xl relative">
                <span class="pill-warning mb-3">⚡ التجربة تنتهي قريبًا</span>
                <h2 class="display-2 mb-3">{{ __('subscription.no_active') }}</h2>
                <p class="text-base text-ink-700 leading-relaxed mb-6">
                    برنامج دفترx المثبّت عندك يعمل في وضع التجربة (14 يوم).
                    للاستمرار في استخدام النظام بدون انقطاع بعد انتهاء التجربة، اشترك في الباقة المناسبة لمؤسستك.
                </p>
                <div class="flex flex-wrap items-center gap-3">
                    <a href="{{ route('portal.subscription.start') }}" class="btn-primary">
                        {{ __('subscription.subscribe_cta') }}
                    </a>
                    <a href="/pricing" class="btn-secondary">
                        عرض الباقات والأسعار
                    </a>
                </div>
            </div>
        </section>
    @else

        {{-- ─── Stat tiles (KPI strip) ─────────────────────── --}}
        <section class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
            <a href="{{ route('portal.licences') }}" class="stat-tile">
                <span class="stat-tile-icon bg-brand-100 text-brand-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M21 2 13 10m3 3-2-2m-1 6a4 4 0 1 1-5.66-5.66 4 4 0 0 1 5.66 5.66Z" />
                    </svg>
                </span>
                <div class="flex-1 min-w-0">
                    <p class="stat-tile-label">{{ __('licences.title') }}</p>
                    <p class="stat-tile-value">{{ $activeLicenceCount }}</p>
                    <p class="stat-tile-sub">ترخيص نشط</p>
                </div>
                <span class="text-ink-300">←</span>
            </a>

            <a href="{{ route('portal.billing') }}" class="stat-tile">
                <span class="stat-tile-icon bg-emerald-50 text-emerald-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                    </svg>
                </span>
                <div class="flex-1 min-w-0">
                    <p class="stat-tile-label">المدفوع هذا العام</p>
                    <p class="stat-tile-value">
                        {{ number_format($paidThisYear, 0) }}
                        <span class="text-base text-ink-500 font-bold">EGP</span>
                    </p>
                    <p class="stat-tile-sub">{{ now()->year }}</p>
                </div>
                <span class="text-ink-300">←</span>
            </a>

            <a href="/portal/downloads" class="stat-tile">
                <span class="stat-tile-icon bg-sky-50 text-sky-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M12 3v12m-5-5 5 5 5-5M5 21h14" />
                    </svg>
                </span>
                <div class="flex-1 min-w-0">
                    <p class="stat-tile-label">التحميل</p>
                    <p class="stat-tile-value text-2xl">Windows + Android</p>
                    <p class="stat-tile-sub">آخر الإصدارات جاهزة</p>
                </div>
                <span class="text-ink-300">←</span>
            </a>
        </section>

        {{-- ─── 2-column: subscriptions detail | recent activity ── --}}
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">

            {{-- Subscription cards — span 2 cols --}}
            <section class="lg:col-span-2 space-y-4">
                <div class="flex items-center justify-between mb-1">
                    <h3 class="display-2 text-xl">اشتراكاتك</h3>
                    <a href="{{ route('portal.subscription') }}" class="text-sm text-brand-700 hover:text-brand-800 font-semibold">
                        إدارة ←
                    </a>
                </div>
                @foreach ($subscriptions as $subscription)
                    <article class="card-padded card-hover relative overflow-hidden">
                        <div class="flex items-start justify-between gap-4 mb-4">
                            <div>
                                <p class="eyebrow-muted mb-1.5">
                                    {{ __('subscription.cadence.'.$subscription->billing_cadence) }}
                                </p>
                                <h4 class="text-2xl font-extrabold text-ink-950 tracking-tight">
                                    {{ __('marketing.pricing.tiers.'.strtolower($subscription->tier).'.name') }}
                                </h4>
                            </div>
                            @if ($subscription->status === 'Active')
                                <span class="pill-success">{{ __('subscription.status.Active') }}</span>
                            @elseif ($subscription->status === 'PastDue')
                                <span class="pill-danger">{{ __('subscription.status.PastDue') }}</span>
                            @else
                                <span class="pill-muted">{{ __('subscription.status.'.$subscription->status) }}</span>
                            @endif
                        </div>
                        <dl class="grid grid-cols-2 gap-4 text-sm border-t border-ink-100 pt-4">
                            <div>
                                <dt class="eyebrow-muted mb-1">تاريخ التجديد</dt>
                                <dd class="font-mono font-semibold text-ink-900">{{ $subscription->current_period_end_at?->format('Y-m-d') ?? '—' }}</dd>
                            </div>
                            <div>
                                <dt class="eyebrow-muted mb-1">الدعم الفني</dt>
                                <dd class="font-semibold text-ink-900">
                                    {{ $subscription->hasPrioritySupport() ? '⚡ 4 ساعات' : '24 ساعة' }}
                                </dd>
                            </div>
                        </dl>
                        @if ($subscription->cancelled_at)
                            <p class="mt-4 text-xs text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
                                {{ __('subscription.will_cancel_notice', ['date' => $subscription->current_period_end_at?->format('Y-m-d')]) }}
                            </p>
                        @endif
                    </article>
                @endforeach
            </section>

            {{-- Recent activity stream --}}
            <section class="card-padded h-fit">
                <div class="flex items-center justify-between mb-4">
                    <h3 class="card-section-title !mb-0">أحدث الفواتير</h3>
                    <a href="{{ route('portal.billing') }}" class="text-xs text-brand-700 hover:text-brand-800 font-semibold">
                        الكل ←
                    </a>
                </div>
                @if ($recentInvoices->isEmpty())
                    <div class="text-center py-6 text-sm text-ink-500">
                        لا توجد فواتير بعد
                    </div>
                @else
                    <ul class="-mx-2 divide-y divide-ink-100">
                        @foreach ($recentInvoices as $invoice)
                            @php
                                $pillClass = match ($invoice->status) {
                                    'Paid' => 'pill-success',
                                    'Pending' => 'pill-warning',
                                    'Refunded' => 'pill-muted',
                                    'Failed' => 'pill-danger',
                                    default => 'pill-muted',
                                };
                            @endphp
                            <li class="px-2 py-3 flex items-center justify-between gap-3 hover:bg-ink-50/50 transition rounded-lg">
                                <div class="min-w-0 flex-1">
                                    <p class="font-mono text-[11px] text-ink-500 truncate">{{ $invoice->invoice_number }}</p>
                                    <p class="font-semibold text-ink-900">
                                        {{ $invoice->amount_egp }}
                                        <span class="text-xs text-ink-500 font-normal">EGP</span>
                                    </p>
                                </div>
                                <div class="text-end shrink-0">
                                    <span class="{{ $pillClass }}">{{ __('billing.status.'.$invoice->status) }}</span>
                                    <p class="text-[11px] text-ink-500 mt-1">{{ $invoice->created_at?->format('Y-m-d') }}</p>
                                </div>
                            </li>
                        @endforeach
                    </ul>
                @endif
            </section>
        </div>
    @endif
@endsection
