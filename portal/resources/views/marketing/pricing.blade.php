@extends('layouts.marketing')

@section('title', __('marketing.pricing.title'))
@section('description', __('marketing.pricing.subtitle'))

@php
    $tierKeys = ['solo', 'smb', 'enterprise', 'firm'];
    $currency = __('marketing.pricing.currency');
@endphp

@section('content')
    <header class="text-center mb-12 max-w-3xl mx-auto">
        <p class="eyebrow mb-3">الأسعار والباقات</p>
        <h1 class="display-1 text-5xl mb-4">{{ __('marketing.pricing.title') }}</h1>
        <p class="text-lg text-ink-700">{{ __('marketing.pricing.subtitle') }}</p>
    </header>

    <div x-data="{ cadence: 'monthly' }" class="mx-auto">
        {{-- ─── Cadence toggle ────────────────────────────── --}}
        <div class="flex justify-center mb-12">
            <div class="inline-flex items-center rounded-full border border-ink-200 bg-white p-1 shadow-elevation-1">
                <button type="button"
                        class="px-6 py-2 rounded-full text-sm font-semibold transition"
                        :class="cadence === 'monthly' ? 'bg-brand-600 text-white shadow-brand-glow' : 'text-ink-600 hover:text-ink-900'"
                        @click="cadence = 'monthly'">
                    {{ __('marketing.pricing.period.monthly') }}
                </button>
                <button type="button"
                        class="px-6 py-2 rounded-full text-sm font-semibold transition flex items-center gap-2"
                        :class="cadence === 'annual' ? 'bg-brand-600 text-white shadow-brand-glow' : 'text-ink-600 hover:text-ink-900'"
                        @click="cadence = 'annual'">
                    {{ __('marketing.pricing.period.annual') }}
                    <span class="pill-success !text-[10px] !px-2"
                          :class="cadence === 'annual' ? '!bg-white/20 !text-white' : ''">
                        وفّر شهرين
                    </span>
                </button>
            </div>
        </div>

        {{-- ─── 4 tier cards ────────────────────────────── --}}
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-5">
            @foreach ($tierKeys as $tier)
                @php
                    $monthly = __('marketing.pricing.tiers.'.$tier.'.monthly');
                    $annual = __('marketing.pricing.tiers.'.$tier.'.annual');
                    $badge = trans()->has('marketing.pricing.tiers.'.$tier.'.badge')
                        ? __('marketing.pricing.tiers.'.$tier.'.badge') : null;
                    $featuresList = (array) __('marketing.pricing.tiers.'.$tier.'.features');
                    $isFeatured = (bool) $badge;
                @endphp
                <article class="relative rounded-2xl bg-white p-6 flex flex-col transition
                                {{ $isFeatured
                                    ? 'border-2 border-brand-500 ring-2 ring-brand-200 shadow-brand-glow-lg'
                                    : 'border border-ink-100 hover:border-brand-300 hover:shadow-elevation-2' }}">
                    @if ($isFeatured)
                        <div class="absolute -top-3 inset-x-0 flex justify-center">
                            <span class="rounded-full px-3 py-1 text-xs font-bold text-white shadow-brand-glow"
                                  style="background: linear-gradient(135deg, #d68a1f 0%, #b06d18 100%);">
                                {{ $badge }}
                            </span>
                        </div>
                    @endif

                    <h2 class="text-2xl font-extrabold text-ink-950 mb-1 tracking-tight">{{ __('marketing.pricing.tiers.'.$tier.'.name') }}</h2>
                    <p class="text-sm text-ink-600 mb-5 min-h-[40px]">{{ __('marketing.pricing.tiers.'.$tier.'.tagline') }}</p>

                    <div class="mb-6 pb-6 border-b border-ink-100">
                        <span class="text-4xl font-extrabold text-ink-950" x-text="cadence === 'monthly' ? '{{ $monthly }}' : '{{ $annual }}'"></span>
                        <span class="text-ink-600 ms-1 text-sm">{{ $currency }}</span>
                        <span class="block text-ink-500 text-xs mt-1" x-text="cadence === 'monthly' ? '{{ __('marketing.pricing.period.monthly_short') }}' : '{{ __('marketing.pricing.period.annual_short') }}'"></span>
                    </div>

                    <ul class="space-y-2.5 text-sm text-ink-700 mb-6 flex-1">
                        @foreach ($featuresList as $line)
                            <li class="flex items-start gap-2.5">
                                <span class="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-brand-100 text-brand-700 mt-0.5">
                                    <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" viewBox="0 0 24 24"
                                         fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                                        <path d="m5 12 5 5 9-12" />
                                    </svg>
                                </span>
                                <span class="leading-relaxed">{{ $line }}</span>
                            </li>
                        @endforeach
                    </ul>

                    <a href="/register?tier={{ $tier }}"
                       class="{{ $isFeatured ? 'btn-primary' : 'btn-secondary' }} w-full justify-center !py-2.5">
                        {{ __('marketing.pricing.tiers.'.$tier.'.cta') }}
                    </a>
                </article>
            @endforeach
        </div>
    </div>

    <p class="mt-10 text-center text-sm text-ink-500">{{ __('marketing.pricing.trial_note') }}</p>
@endsection
