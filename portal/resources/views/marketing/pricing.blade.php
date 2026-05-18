@extends('layouts.marketing')

@section('title', __('marketing.pricing.title'))
@section('description', __('marketing.pricing.subtitle'))

@php
    // Alpine.js handles the monthly↔annual toggle inline.
    $tierKeys = ['solo', 'smb', 'enterprise', 'firm'];
    $currency = __('marketing.pricing.currency');
@endphp

@section('content')
    <header class="text-center mb-10">
        <h1 class="text-4xl font-bold mb-3">{{ __('marketing.pricing.title') }}</h1>
        <p class="text-lg text-stone-700 max-w-2xl mx-auto">{{ __('marketing.pricing.subtitle') }}</p>
    </header>

    <div x-data="{ cadence: 'monthly' }" class="mx-auto">
        {{-- Cadence toggle --}}
        <div class="flex justify-center mb-10">
            <div class="inline-flex rounded-full border border-stone-300 bg-white p-1 shadow-sm">
                <button type="button" class="px-6 py-2 rounded-full transition" :class="cadence === 'monthly' ? 'bg-amber-600 text-white' : 'text-stone-700 hover:bg-stone-100'" @click="cadence = 'monthly'">
                    {{ __('marketing.pricing.period.monthly') }}
                </button>
                <button type="button" class="px-6 py-2 rounded-full transition" :class="cadence === 'annual' ? 'bg-amber-600 text-white' : 'text-stone-700 hover:bg-stone-100'" @click="cadence = 'annual'">
                    {{ __('marketing.pricing.period.annual') }}
                </button>
            </div>
        </div>

        {{-- 4 tier cards --}}
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            @foreach ($tierKeys as $tier)
                @php
                    $monthly = __('marketing.pricing.tiers.'.$tier.'.monthly');
                    $annual = __('marketing.pricing.tiers.'.$tier.'.annual');
                    $badge = trans()->has('marketing.pricing.tiers.'.$tier.'.badge')
                        ? __('marketing.pricing.tiers.'.$tier.'.badge') : null;
                    $featuresList = (array) __('marketing.pricing.tiers.'.$tier.'.features');
                @endphp
                <div class="relative rounded-lg border bg-white p-6 shadow-sm flex flex-col {{ $badge ? 'border-amber-500 ring-2 ring-amber-300' : 'border-stone-200' }}">
                    @if ($badge)
                        <div class="absolute -top-3 inset-x-0 flex justify-center">
                            <span class="bg-amber-500 text-white text-xs font-semibold px-3 py-1 rounded-full shadow">{{ $badge }}</span>
                        </div>
                    @endif

                    <h2 class="text-2xl font-bold mb-1">{{ __('marketing.pricing.tiers.'.$tier.'.name') }}</h2>
                    <p class="text-sm text-stone-600 mb-4">{{ __('marketing.pricing.tiers.'.$tier.'.tagline') }}</p>

                    <div class="mb-4">
                        <span class="text-4xl font-bold" x-text="cadence === 'monthly' ? '{{ $monthly }}' : '{{ $annual }}'"></span>
                        <span class="text-stone-600 ms-1">{{ $currency }}</span>
                        <span class="text-stone-500 text-sm" x-text="cadence === 'monthly' ? '{{ __('marketing.pricing.period.monthly_short') }}' : '{{ __('marketing.pricing.period.annual_short') }}'"></span>
                    </div>

                    <ul class="space-y-2 text-sm text-stone-700 mb-6 flex-1">
                        @foreach ($featuresList as $line)
                            <li class="flex items-start gap-2">
                                <span class="text-amber-600 mt-0.5">✓</span>
                                <span>{{ $line }}</span>
                            </li>
                        @endforeach
                    </ul>

                    <a href="/register?tier={{ $tier }}" class="block text-center rounded bg-amber-600 px-4 py-2 text-white font-semibold hover:bg-amber-700">
                        {{ __('marketing.pricing.tiers.'.$tier.'.cta') }}
                    </a>
                </div>
            @endforeach
        </div>
    </div>

    <p class="mt-8 text-center text-sm text-stone-600">{{ __('marketing.pricing.trial_note') }}</p>
@endsection
