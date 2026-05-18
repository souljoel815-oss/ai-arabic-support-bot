@extends('layouts.portal')

@section('title', __('subscription.start.title'))

@section('content')
    <header class="mb-6">
        <a href="{{ route('portal.subscription') }}" class="text-sm text-amber-700 hover:underline">← {{ __('subscription.title') }}</a>
        <h1 class="text-2xl font-bold mt-2">{{ __('subscription.start.title') }}</h1>
        <p class="text-stone-600 mt-1">{{ __('subscription.start.subtitle') }}</p>
    </header>

    @if ($hasActive)
        <div class="rounded border border-amber-200 bg-amber-50 p-4 mb-6 text-sm text-stone-800">
            ⚠ {{ __('subscription.start.has_active_warning') }}
        </div>
    @endif

    <form method="POST" action="{{ route('portal.subscription.start.submit') }}" class="max-w-3xl space-y-6">
        @csrf

        {{-- Tier selector --}}
        <div>
            <h2 class="font-semibold mb-3">{{ __('subscription.start.tier_label') }}</h2>
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
                @foreach (['Solo', 'SMB', 'Enterprise', 'Firm'] as $tier)
                    @php
                        $monthlyP = $priceTable[$tier]['Monthly'];
                        $monthlyEgp = number_format($monthlyP / 100, 0);
                    @endphp
                    <label class="cursor-pointer rounded-lg border-2 p-4 {{ old('tier', $preferredTier) === $tier ? 'border-amber-600 bg-amber-50' : 'border-stone-200 bg-white hover:border-stone-400' }}">
                        <input type="radio" name="tier" value="{{ $tier }}" class="sr-only"
                               {{ old('tier', $preferredTier) === $tier ? 'checked' : '' }}>
                        <div class="font-bold text-lg">{{ __('marketing.pricing.tiers.'.strtolower($tier).'.name') }}</div>
                        <div class="text-sm text-stone-600 mb-1">{{ __('marketing.pricing.tiers.'.strtolower($tier).'.tagline') }}</div>
                        <div class="text-amber-700 font-semibold">{{ $monthlyEgp }} EGP / شهر</div>
                    </label>
                @endforeach
            </div>
            @error('tier')<p class="mt-2 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>

        {{-- Cadence --}}
        <div>
            <h2 class="font-semibold mb-3">{{ __('subscription.start.cadence_label') }}</h2>
            <div class="flex gap-3">
                @foreach (['Monthly', 'Annual'] as $cadence)
                    <label class="cursor-pointer rounded border-2 px-6 py-3 {{ old('billing_cadence', 'Monthly') === $cadence ? 'border-amber-600 bg-amber-50' : 'border-stone-200 bg-white' }}">
                        <input type="radio" name="billing_cadence" value="{{ $cadence }}" class="sr-only"
                               {{ old('billing_cadence', 'Monthly') === $cadence ? 'checked' : '' }}>
                        {{ __('subscription.cadence.'.$cadence) }}
                        @if ($cadence === 'Annual')
                            <span class="ms-1 text-xs text-green-700">(وفّر شهرين)</span>
                        @endif
                    </label>
                @endforeach
            </div>
        </div>

        {{-- Payment method --}}
        <div>
            <h2 class="font-semibold mb-3">{{ __('subscription.start.payment_method_label') }}</h2>
            <p class="text-xs text-stone-500 mb-3">{{ __('subscription.start.payment_method_note') }}</p>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                @foreach (['Card', 'Fawry', 'InstaPay', 'VodafoneCash', 'BankTransfer'] as $method)
                    <label class="cursor-pointer rounded border-2 px-4 py-3 {{ old('payment_method') === $method ? 'border-amber-600 bg-amber-50' : 'border-stone-200 bg-white hover:border-stone-400' }}">
                        <input type="radio" name="payment_method" value="{{ $method }}" class="sr-only"
                               {{ old('payment_method') === $method ? 'checked' : '' }}>
                        {{ __('subscription.payment_method.'.$method) }}
                    </label>
                @endforeach
            </div>
            @error('payment_method')<p class="mt-2 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>

        <div class="flex gap-3 pt-2">
            <button type="submit" class="rounded bg-amber-600 px-6 py-2 text-white font-semibold hover:bg-amber-700">
                {{ __('subscription.start.submit') }}
            </button>
            <a href="{{ route('portal.subscription') }}" class="rounded border border-stone-300 px-5 py-2 hover:bg-stone-100">
                {{ __('subscription.start.cancel') }}
            </a>
        </div>
    </form>
@endsection
