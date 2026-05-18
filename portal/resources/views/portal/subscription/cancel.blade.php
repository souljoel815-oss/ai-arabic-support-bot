@extends('layouts.portal')

@section('title', __('subscription.cancel.title'))

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.subscription') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            {{ __('subscription.title') }}
        </a>
        <p class="eyebrow mb-2 !text-red-700">إلغاء الاشتراك</p>
        <h1 class="display-1 mb-1.5">{{ __('subscription.cancel.title') }}</h1>
    </header>

    {{-- Reverse-CTA: keep going (don't lose them) --}}
    <section class="relative overflow-hidden rounded-2xl bg-hero-gold border border-brand-200/60 px-6 py-5 mb-8 shadow-elevation-2">
        <div class="relative flex flex-wrap items-center justify-between gap-4">
            <div>
                <p class="eyebrow mb-2">قبل ما تكمل</p>
                <h2 class="text-xl font-bold text-ink-950 mb-2">متأكد إنك عايز تلغي؟</h2>
                <p class="text-sm text-ink-700 max-w-xl leading-relaxed">
                    اشتراكك هيفضل شغّال لحد <strong class="font-mono">{{ $subscription->current_period_end_at?->format('Y-m-d') }}</strong> —
                    آخر يوم في الفترة اللي دفعتها. لو غيّرت رأيك قبل التاريخ ده، تواصل مع الدعم.
                </p>
            </div>
            <a href="{{ route('portal.subscription') }}" class="btn-primary !py-3 !px-6 shrink-0">
                خليني أكمل ←
            </a>
        </div>
    </section>

    {{-- Confirm cancellation --}}
    <section class="rounded-2xl border-2 border-red-200 bg-red-50/40 p-6 relative overflow-hidden">
        <span class="absolute inset-y-0 start-0 w-1 bg-red-500"></span>
        <div class="flex items-start gap-4">
            <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-red-100 text-red-700">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0ZM12 9v4m0 4h.01" />
                </svg>
            </span>
            <div class="flex-1">
                <h2 class="text-lg font-bold text-red-900 mb-2">ما يحصل عند الإلغاء</h2>
                <ul class="space-y-2 text-sm text-red-800 mb-5">
                    <li class="flex gap-2"><span class="text-red-600 mt-0.5">●</span><span>الاشتراك يبقى Active لحد نهاية الفترة الحالية</span></li>
                    <li class="flex gap-2"><span class="text-red-600 mt-0.5">●</span><span>بعد التاريخ ده، الاشتراك يصبح Cancelled وتراخيصك تنتهي</span></li>
                    <li class="flex gap-2"><span class="text-red-600 mt-0.5">●</span><span>لن يتم تجديد تلقائي ولن يُسحب أي مبلغ آخر</span></li>
                    <li class="flex gap-2"><span class="text-red-600 mt-0.5">●</span><span>تقدر تشترك مرة أخرى في أي وقت — بياناتك محفوظة</span></li>
                </ul>

                <form method="POST" action="{{ route('portal.subscription.cancel', ['subscription' => $subscription->id]) }}" class="flex gap-3 pt-3 border-t border-red-200">
                    @csrf
                    <button type="submit" class="btn-danger !py-3 !px-5">
                        أؤكد إلغاء الاشتراك
                    </button>
                    <a href="{{ route('portal.subscription') }}" class="btn-ghost">
                        رجوع
                    </a>
                </form>
            </div>
        </div>
    </section>
@endsection
