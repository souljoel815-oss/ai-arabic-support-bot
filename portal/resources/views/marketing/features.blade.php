@extends('layouts.marketing')

@section('title', __('marketing.features.title'))
@section('description', __('marketing.features.subtitle'))

@section('content')
    <header class="text-center mb-12">
        <h1 class="text-4xl font-bold mb-3">{{ __('marketing.features.title') }}</h1>
        <p class="text-lg text-stone-700 max-w-2xl mx-auto">{{ __('marketing.features.subtitle') }}</p>
    </header>

    @php
        $categories = (array) __('marketing.features.categories');
        $allFeatures = (array) __('marketing.features.list');
        // Group features by category.
        $grouped = collect($allFeatures)->groupBy('cat');
    @endphp

    <div class="space-y-12">
        @foreach ($categories as $catKey => $catLabel)
            <section>
                <h2 class="text-2xl font-bold mb-4 border-b border-stone-200 pb-2">{{ $catLabel }}</h2>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    @foreach ($grouped->get($catKey, []) as $feature)
                        <div class="rounded border border-stone-200 bg-white p-4">
                            <h3 class="font-semibold text-amber-700 mb-1">{{ $feature['name'] }}</h3>
                            <p class="text-sm text-stone-600">{{ $feature['desc'] }}</p>
                        </div>
                    @endforeach
                </div>
            </section>
        @endforeach
    </div>

    <section class="mt-16 text-center bg-amber-50 border border-amber-200 rounded-lg p-8">
        <p class="text-lg mb-4">{{ __('marketing.pricing.trial_note') }}</p>
        <a href="/register" class="inline-block rounded bg-amber-600 px-6 py-3 font-semibold text-white hover:bg-amber-700">
            {{ __('marketing.home.cta_trial') }}
        </a>
    </section>
@endsection
