@extends('layouts.marketing')

@section('title', __('marketing.features.title'))
@section('description', __('marketing.features.subtitle'))

@section('content')
    <header class="text-center mb-16 max-w-3xl mx-auto">
        <p class="eyebrow mb-3">المميزات بالتفصيل</p>
        <h1 class="display-1 text-5xl mb-4">{{ __('marketing.features.title') }}</h1>
        <p class="text-lg text-ink-700">{{ __('marketing.features.subtitle') }}</p>
    </header>

    @php
        $categories = (array) __('marketing.features.categories');
        $allFeatures = (array) __('marketing.features.list');
        $grouped = collect($allFeatures)->groupBy('cat');
    @endphp

    <div class="space-y-16">
        @foreach ($categories as $catKey => $catLabel)
            <section>
                <div class="flex items-center gap-3 mb-6">
                    <span class="h-px flex-1 bg-ink-100"></span>
                    <h2 class="text-xl font-bold text-ink-950 px-4">{{ $catLabel }}</h2>
                    <span class="h-px flex-1 bg-ink-100"></span>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    @foreach ($grouped->get($catKey, []) as $feature)
                        <article class="card-padded card-hover group">
                            <div class="flex items-start gap-4">
                                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-100 text-brand-700 group-hover:bg-brand-600 group-hover:text-white transition">
                                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                        <path d="m5 12 5 5 9-12" />
                                    </svg>
                                </span>
                                <div class="flex-1 min-w-0">
                                    <h3 class="font-bold text-ink-950 mb-1.5 text-base">{{ $feature['name'] }}</h3>
                                    <p class="text-sm text-ink-600 leading-relaxed">{{ $feature['desc'] }}</p>
                                </div>
                            </div>
                        </article>
                    @endforeach
                </div>
            </section>
        @endforeach
    </div>

    {{-- Final CTA --}}
    <section class="mt-20 relative overflow-hidden rounded-3xl bg-hero-charcoal text-white p-12 text-center shadow-elevation-3">
        <span class="pointer-events-none absolute -bottom-24 -end-24 h-72 w-72 rounded-full"
              style="background: radial-gradient(circle, rgba(214,138,31,0.30) 0%, transparent 70%);"></span>

        <div class="relative max-w-2xl mx-auto">
            <p class="eyebrow-light mb-3">جاهز للبدء؟</p>
            <h2 class="text-3xl font-extrabold tracking-tight mb-4">{{ __('marketing.pricing.trial_note') }}</h2>
            <a href="/register" class="btn-primary !py-3 !px-8 !text-base">
                {{ __('marketing.home.cta_trial') }}
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M5 12h14M13 5l7 7-7 7" />
                </svg>
            </a>
        </div>
    </section>
@endsection
