@extends('layouts.marketing')

@section('title', __('marketing.home.hero_title'))
@section('description', __('marketing.home.hero_subtitle'))

@section('content')
    {{-- Hero --}}
    <section class="text-center py-16">
        <h1 class="text-5xl font-bold mb-4 leading-tight">{{ __('marketing.home.hero_title') }}</h1>
        <p class="text-xl text-stone-700 mb-8 max-w-3xl mx-auto">{{ __('marketing.home.hero_subtitle') }}</p>
        <div class="flex flex-wrap justify-center gap-3">
            <a href="/register" class="rounded bg-amber-600 px-6 py-3 text-lg font-semibold text-white hover:bg-amber-700 shadow">
                {{ __('marketing.home.cta_trial') }}
            </a>
            <a href="/pricing" class="rounded border border-stone-400 px-6 py-3 text-lg hover:bg-stone-100">
                {{ __('marketing.home.cta_pricing') }}
            </a>
            <a href="/downloads" class="rounded border border-stone-400 px-6 py-3 text-lg hover:bg-stone-100">
                {{ __('marketing.home.cta_download') }}
            </a>
        </div>
    </section>

    {{-- Value props grid --}}
    <section class="mt-16">
        <h2 class="text-3xl font-bold text-center mb-10">{{ __('marketing.home.value_props_title') }}</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            @foreach (['arabic_first', 'eta', 'egyptian_taxes', 'multi_user', 'inventory', 'reports'] as $key)
                <div class="rounded-lg border border-stone-200 bg-white p-6 shadow-sm hover:shadow transition">
                    <h3 class="text-xl font-bold mb-2 text-amber-700">{{ __('marketing.home.value_props.'.$key.'.title') }}</h3>
                    <p class="text-stone-700 leading-relaxed">{{ __('marketing.home.value_props.'.$key.'.body') }}</p>
                </div>
            @endforeach
        </div>
    </section>

    {{-- Who for --}}
    <section class="mt-16 rounded-lg bg-white border border-stone-200 p-8">
        <h2 class="text-2xl font-bold mb-6 text-center">{{ __('marketing.home.who_for_title') }}</h2>
        <ul class="grid grid-cols-1 md:grid-cols-2 gap-4 text-stone-700">
            @foreach (['sme', 'accounting_firms', 'retail', 'services'] as $key)
                <li class="flex items-start gap-3">
                    <span class="mt-1 inline-block w-2 h-2 rounded-full bg-amber-600 shrink-0"></span>
                    <span>{{ __('marketing.home.who_for.'.$key) }}</span>
                </li>
            @endforeach
        </ul>
    </section>

    {{-- Final CTA --}}
    <section class="mt-16 text-center bg-amber-50 border border-amber-200 rounded-lg p-10">
        <h2 class="text-2xl font-bold mb-4">{{ __('marketing.pricing.trial_note') }}</h2>
        <a href="/register" class="inline-block rounded bg-amber-600 px-8 py-3 text-lg font-semibold text-white hover:bg-amber-700">
            {{ __('marketing.home.cta_trial') }}
        </a>
    </section>
@endsection
