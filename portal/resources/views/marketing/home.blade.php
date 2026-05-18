@extends('layouts.marketing')

@section('title', __('messages.app.name'))

@section('content')
    <section class="text-center py-12">
        <h1 class="text-5xl font-bold mb-4">{{ __('messages.app.name') }}</h1>
        <p class="text-xl text-stone-700 mb-8">{{ __('messages.app.tagline') }}</p>
        <div class="flex justify-center gap-4">
            <a href="/register" class="rounded bg-amber-600 px-6 py-3 text-lg font-semibold text-white hover:bg-amber-700">
                {{ __('messages.nav.register') }}
            </a>
            <a href="/pricing" class="rounded border border-stone-400 px-6 py-3 text-lg hover:bg-stone-100">
                {{ __('messages.nav.pricing') }}
            </a>
            <a href="/downloads" class="rounded border border-stone-400 px-6 py-3 text-lg hover:bg-stone-100">
                {{ __('messages.nav.downloads') }}
            </a>
        </div>
    </section>

    <section class="mt-16 grid grid-cols-1 md:grid-cols-3 gap-6 text-center">
        <div class="rounded-lg border border-stone-200 bg-white p-6">
            <h2 class="text-xl font-bold mb-2">عربي بالكامل</h2>
            <p class="text-stone-600">واجهة RTL أصلية، دعم Unicode الكامل للأسماء + الفواتير + التقارير.</p>
        </div>
        <div class="rounded-lg border border-stone-200 bg-white p-6">
            <h2 class="text-xl font-bold mb-2">الفاتورة الإلكترونية (ETA)</h2>
            <p class="text-stone-600">تكامل مباشر مع منظومة مصلحة الضرائب — تقديم تلقائي + متابعة الحالة.</p>
        </div>
        <div class="rounded-lg border border-stone-200 bg-white p-6">
            <h2 class="text-xl font-bold mb-2">ضرائب مصرية</h2>
            <p class="text-stone-600">VAT، خصم من المنبع، ضريبة الدخل، الإقفال السنوي — كله محسوب لك.</p>
        </div>
    </section>

    <p class="mt-12 text-center text-sm text-stone-500">
        Phase 2 scaffold — full marketing surface lands in US1 (T040–T055).
    </p>
@endsection
