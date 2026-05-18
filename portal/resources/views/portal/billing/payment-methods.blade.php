@extends('layouts.portal')

@section('title', __('billing.payment_methods_title') ?? 'طرق الدفع')

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.billing') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            {{ __('billing.title') }}
        </a>
        <p class="eyebrow mb-2">طرق الدفع</p>
        <h1 class="display-1 mb-1.5">طرق الدفع المتاحة</h1>
        <p class="text-ink-600">DaftarX يدعم 5 طرق دفع مختلفة عشان نناسب كل عميل.</p>
    </header>

    @php
        $methods = [
            ['key' => 'Card',         'icon' => '💳', 'note' => 'Visa / Mastercard — أسرع طريقة. الفاتورة تتأكد فوراً.'],
            ['key' => 'Fawry',        'icon' => '🏪', 'note' => 'ادفع من أي فرع فوري في مصر. الكود يصلك على البريد.'],
            ['key' => 'InstaPay',     'icon' => '⚡', 'note' => 'تحويل مباشر من تطبيق بنكك عبر شبكة InstaPay.'],
            ['key' => 'VodafoneCash', 'icon' => '📱', 'note' => 'ادفع من محفظة فودافون كاش بشكل مباشر.'],
            ['key' => 'BankTransfer', 'icon' => '🏦', 'note' => 'تحويل بنكي يدوي — يتم التأكيد خلال ٢-٣ أيام عمل.'],
        ];
    @endphp

    <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-8">
        @foreach ($methods as $method)
            <article class="card-padded card-hover flex items-start gap-4">
                <span class="text-4xl shrink-0">{{ $method['icon'] }}</span>
                <div class="flex-1 min-w-0">
                    <h3 class="text-lg font-bold text-ink-950 mb-1">{{ __('subscription.payment_method.'.$method['key']) }}</h3>
                    <p class="text-sm text-ink-600 leading-relaxed">{{ $method['note'] }}</p>
                </div>
            </article>
        @endforeach
    </div>

    <div class="rounded-2xl border border-sky-200 bg-sky-50/60 px-5 py-4 text-sm text-sky-900 flex items-start gap-3">
        <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-sky-200/70 text-sky-700 font-bold">ⓘ</span>
        <p>
            بنحفظ آخر طريقة دفع استخدمتها كافتراضية للتجديدات. تقدر تغيّرها عند الاشتراك التالي من
            <a href="{{ route('portal.subscription.start') }}" class="font-semibold underline-offset-2 hover:underline">بدء الاشتراك</a>.
        </p>
    </div>
@endsection
