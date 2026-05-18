@extends('layouts.portal')

@section('title', __('subscription.start.title'))

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.subscription') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            {{ __('subscription.title') }}
        </a>
        <p class="eyebrow mb-2">اشتراك جديد</p>
        <h1 class="display-1 mb-1.5">{{ __('subscription.start.title') }}</h1>
        <p class="text-ink-600 text-base">{{ __('subscription.start.subtitle') }}</p>
    </header>

    @if ($hasActive)
        <div class="mb-6 rounded-2xl border border-amber-300 bg-amber-50 px-5 py-4 text-sm text-amber-900 flex items-center gap-3">
            <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-amber-200/70 text-amber-700">⚠</span>
            <span>{{ __('subscription.start.has_active_warning') }}</span>
        </div>
    @endif

    <form method="POST" action="{{ route('portal.subscription.start.submit') }}" class="max-w-4xl space-y-8">
        @csrf

        {{-- ── Step 1: Tier ─────────────────────────────────── --}}
        <section class="card-padded">
            <div class="flex items-center gap-3 mb-5">
                <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-brand-700 font-bold text-sm">١</span>
                <h2 class="text-lg font-bold text-ink-950">{{ __('subscription.start.tier_label') }}</h2>
            </div>
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
                @foreach (['Solo', 'SMB', 'Enterprise', 'Firm'] as $tier)
                    @php
                        $monthlyP = $priceTable[$tier]['Monthly'];
                        $monthlyEgp = number_format($monthlyP / 100, 0);
                        $isFeatured = $tier === 'SMB';
                        $isSelected = old('tier', $preferredTier) === $tier;
                    @endphp
                    <label class="relative cursor-pointer rounded-xl border-2 p-4 transition
                                  {{ $isSelected ? 'border-brand-500 bg-brand-50 shadow-brand-glow ring-2 ring-brand-200' : 'border-ink-200 bg-white hover:border-brand-300 hover:bg-brand-50/50' }}">
                        <input type="radio" name="tier" value="{{ $tier }}" class="sr-only" {{ $isSelected ? 'checked' : '' }}>
                        @if ($isFeatured)
                            <span class="absolute -top-2.5 end-3 pill-warning text-[10px] !py-0.5">الأكثر شعبية</span>
                        @endif
                        @if ($isSelected)
                            <span class="absolute top-3 start-3 h-5 w-5 rounded-full bg-brand-600 text-white flex items-center justify-center text-xs">✓</span>
                        @endif
                        <div class="font-bold text-lg text-ink-950 mb-1 {{ $isSelected ? 'ms-7' : '' }}">{{ __('marketing.pricing.tiers.'.strtolower($tier).'.name') }}</div>
                        <div class="text-xs text-ink-600 mb-3 leading-relaxed h-8 overflow-hidden">{{ __('marketing.pricing.tiers.'.strtolower($tier).'.tagline') }}</div>
                        <div class="font-extrabold text-2xl text-brand-700">{{ $monthlyEgp }} <span class="text-xs text-ink-500 font-semibold">EGP/شهر</span></div>
                    </label>
                @endforeach
            </div>
            @error('tier')<p class="mt-3 text-sm text-red-600">{{ $message }}</p>@enderror
        </section>

        {{-- ── Step 2: Cadence ──────────────────────────────── --}}
        <section class="card-padded">
            <div class="flex items-center gap-3 mb-5">
                <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-brand-700 font-bold text-sm">٢</span>
                <h2 class="text-lg font-bold text-ink-950">{{ __('subscription.start.cadence_label') }}</h2>
            </div>
            <div class="grid grid-cols-2 gap-3 max-w-md">
                @foreach (['Monthly', 'Annual'] as $cadence)
                    @php $isSelected = old('billing_cadence', 'Monthly') === $cadence; @endphp
                    <label class="relative cursor-pointer rounded-xl border-2 px-5 py-4 text-center transition
                                  {{ $isSelected ? 'border-brand-500 bg-brand-50 shadow-brand-glow' : 'border-ink-200 bg-white hover:border-brand-300' }}">
                        <input type="radio" name="billing_cadence" value="{{ $cadence }}" class="sr-only" {{ $isSelected ? 'checked' : '' }}>
                        <div class="font-bold text-ink-950">{{ __('subscription.cadence.'.$cadence) }}</div>
                        @if ($cadence === 'Annual')
                            <div class="mt-1"><span class="pill-success !text-[10px]">وفّر شهرين</span></div>
                        @endif
                    </label>
                @endforeach
            </div>
        </section>

        {{-- ── Step 3: Payment method ──────────────────────── --}}
        <section class="card-padded">
            <div class="flex items-center gap-3 mb-1">
                <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-brand-700 font-bold text-sm">٣</span>
                <h2 class="text-lg font-bold text-ink-950">{{ __('subscription.start.payment_method_label') }}</h2>
            </div>
            <p class="text-xs text-ink-500 mb-5 ms-11">{{ __('subscription.start.payment_method_note') }}</p>
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                @php
                    $methodIcons = [
                        'Card' => '💳',
                        'Fawry' => '🏪',
                        'InstaPay' => '⚡',
                        'VodafoneCash' => '📱',
                        'BankTransfer' => '🏦',
                    ];
                @endphp
                @foreach (['Card', 'Fawry', 'InstaPay', 'VodafoneCash', 'BankTransfer'] as $method)
                    @php $isSelected = old('payment_method') === $method; @endphp
                    <label class="relative cursor-pointer rounded-xl border-2 px-4 py-3 transition flex items-center gap-3
                                  {{ $isSelected ? 'border-brand-500 bg-brand-50 shadow-brand-glow' : 'border-ink-200 bg-white hover:border-brand-300' }}">
                        <input type="radio" name="payment_method" value="{{ $method }}" class="sr-only" {{ $isSelected ? 'checked' : '' }}>
                        <span class="text-2xl">{{ $methodIcons[$method] }}</span>
                        <span class="font-semibold text-ink-900 flex-1">{{ __('subscription.payment_method.'.$method) }}</span>
                        @if ($isSelected)
                            <span class="h-5 w-5 rounded-full bg-brand-600 text-white flex items-center justify-center text-xs">✓</span>
                        @endif
                    </label>
                @endforeach
            </div>
            @error('payment_method')<p class="mt-3 text-sm text-red-600">{{ $message }}</p>@enderror
        </section>

        <div class="flex flex-wrap gap-3 pt-2">
            <button type="submit" class="btn-primary !py-3 !px-7">
                {{ __('subscription.start.submit') }}
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M5 12h14M13 5l7 7-7 7" />
                </svg>
            </button>
            <a href="{{ route('portal.subscription') }}" class="btn-secondary">
                {{ __('subscription.start.cancel') }}
            </a>
        </div>
    </form>
@endsection
