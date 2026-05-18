@extends('layouts.portal')

@section('title', 'سياسة الاسترداد')

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.billing') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            {{ __('billing.title') }}
        </a>
        <p class="eyebrow mb-2">سياسة الاسترداد</p>
        <h1 class="display-1 mb-1.5">شروط الاسترداد</h1>
        <p class="text-ink-600">FR-034 — 7 أيام من فاتورة أول فترة فقط.</p>
    </header>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-5 mb-8">
        <article class="card-padded">
            <div class="flex items-center gap-3 mb-4">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-emerald-100 text-emerald-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="m5 12 5 5 9-12" />
                    </svg>
                </span>
                <h2 class="text-lg font-bold text-ink-950">تستحق الاسترداد</h2>
            </div>
            <ul class="space-y-2.5 text-sm text-ink-700">
                <li class="flex items-start gap-2"><span class="text-emerald-600 mt-1">●</span><span>الفاتورة من نوع <strong>FirstPeriod</strong> (أول دفعة)</span></li>
                <li class="flex items-start gap-2"><span class="text-emerald-600 mt-1">●</span><span>الفاتورة <strong>مدفوعة</strong> (Paid)</span></li>
                <li class="flex items-start gap-2"><span class="text-emerald-600 mt-1">●</span><span>أقل من 7 أيام على تاريخ الدفع</span></li>
                <li class="flex items-start gap-2"><span class="text-emerald-600 mt-1">●</span><span>لم يصدر استرداد سابق لنفس الاشتراك</span></li>
            </ul>
        </article>

        <article class="card-padded">
            <div class="flex items-center gap-3 mb-4">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-red-100 text-red-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M18 6 6 18M6 6l12 12" />
                    </svg>
                </span>
                <h2 class="text-lg font-bold text-ink-950">لا تستحق الاسترداد</h2>
            </div>
            <ul class="space-y-2.5 text-sm text-ink-700">
                <li class="flex items-start gap-2"><span class="text-red-600 mt-1">●</span><span>فواتير <strong>التجديد</strong> (Renewal)</span></li>
                <li class="flex items-start gap-2"><span class="text-red-600 mt-1">●</span><span>فواتير <strong>الترقية</strong> (Upgrade)</span></li>
                <li class="flex items-start gap-2"><span class="text-red-600 mt-1">●</span><span>الفاتورة pending أو failed</span></li>
                <li class="flex items-start gap-2"><span class="text-red-600 mt-1">●</span><span>مرور أكثر من 7 أيام على الدفع</span></li>
            </ul>
        </article>
    </div>

    <div class="rounded-2xl border border-sky-200 bg-sky-50/60 px-5 py-4 text-sm text-sky-900 flex items-start gap-3">
        <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-sky-200/70 text-sky-700 font-bold">ⓘ</span>
        <p>
            لو فاتورتك مستحقة للاسترداد، هتلاقي زرار «استرداد» جنبها مباشرة في
            <a href="{{ route('portal.billing') }}" class="font-semibold underline-offset-2 hover:underline">قائمة الفواتير</a>.
            بنرد المبلغ خلال 5-10 أيام عمل حسب البنك أو طريقة الدفع.
        </p>
    </div>
@endsection
