@extends('layouts.portal')

@section('title', __('licences.activate.title'))

@section('content')
    <header class="mb-6">
        <a href="{{ route('portal.licences') }}" class="text-sm text-amber-700 hover:underline">
            ← {{ __('licences.title') }}
        </a>
        <h1 class="text-2xl font-bold mt-2">{{ __('licences.activate.title') }}</h1>
    </header>

    <div class="max-w-2xl rounded-lg border border-stone-200 bg-white p-6">
        <div class="rounded border border-amber-200 bg-amber-50 p-4 mb-6 text-sm text-stone-700">
            {{ __('licences.activate.instructions') }}
        </div>

        <form method="POST" action="{{ route('portal.licences.activate-paid.submit') }}" class="space-y-4">
            @csrf
            <input type="hidden" name="subscription_id" value="{{ $subscription->id }}">

            <div>
                <label class="block text-sm font-semibold mb-1">{{ __('licences.activate.subscription_label') }}</label>
                <div class="rounded border border-stone-200 bg-stone-50 px-3 py-2 text-stone-700">
                    {{ __('marketing.pricing.tiers.'.strtolower($subscription->tier).'.name') }}
                    · {{ __('licences.cadence.'.$subscription->billing_cadence) }}
                    · {{ __('licences.period_end_label') }} {{ $subscription->current_period_end_at?->format('Y-m-d') }}
                </div>
            </div>

            <div>
                <label for="hwid" class="block text-sm font-semibold mb-1">
                    {{ __('licences.activate.hwid_label') }}
                </label>
                <input id="hwid" name="hwid" type="text" required
                       value="{{ old('hwid') }}"
                       placeholder="{{ __('licences.activate.hwid_placeholder') }}"
                       class="w-full rounded border border-stone-300 px-3 py-2 font-mono text-sm focus:border-amber-600 focus:ring-1 focus:ring-amber-600 uppercase"
                       pattern="[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}"
                       maxlength="19">
                @error('hwid')
                    <p class="mt-1 text-sm text-red-600">{{ $message }}</p>
                @enderror
            </div>

            <div class="flex gap-3 pt-2">
                <button type="submit" class="rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
                    {{ __('licences.activate.submit') }}
                </button>
                <a href="{{ route('portal.licences') }}" class="rounded border border-stone-300 px-5 py-2 hover:bg-stone-100">
                    {{ __('licences.activate.cancel') }}
                </a>
            </div>
        </form>
    </div>
@endsection
