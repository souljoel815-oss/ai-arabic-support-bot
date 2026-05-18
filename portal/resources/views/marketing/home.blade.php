@extends('layouts.marketing')

@section('title', __('marketing.home.hero_title'))
@section('description', __('marketing.home.hero_subtitle'))

@php
    $valueIcons = [
        'arabic_first'    => 'M4 7h16M4 12h10M4 17h7',
        'eta'             => 'M9 12l2 2 4-4M21 12c0 5-3.5 9-9 9s-9-4-9-9 3.5-9 9-9 9 4 9 9z',
        'egyptian_taxes'  => 'M9 14l2 2 4-4M3 3h18l-2 14H5zM5 21h14',
        'multi_user'      => 'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
        'inventory'       => 'M20 7h-2V5a2 2 0 0 0-2-2H8a2 2 0 0 0-2 2v2H4a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h16a1 1 0 0 0 1-1V8a1 1 0 0 0-1-1zM8 5h8v2H8zM3 12h18',
        'reports'         => 'M3 3v18h18M7 16l4-4 4 4 6-8',
    ];
    $whoForIcons = [
        'sme'              => '🏬',
        'accounting_firms' => '📊',
        'retail'           => '🛍',
        'services'         => '💼',
    ];
@endphp

@section('content')
    {{-- ─── Hero ──────────────────────────────────────────── --}}
    <section class="relative overflow-hidden rounded-3xl bg-hero-gold border border-brand-200/60 px-8 py-16 md:py-20 mb-16 text-center shadow-elevation-2">
        {{-- Decorative orbs --}}
        <span class="pointer-events-none absolute -top-32 -end-32 h-80 w-80 rounded-full opacity-30"
              style="background: radial-gradient(circle, #d68a1f 0%, transparent 70%);"></span>
        <span class="pointer-events-none absolute -bottom-32 -start-32 h-72 w-72 rounded-full opacity-25"
              style="background: radial-gradient(circle, #f5d691 0%, transparent 70%);"></span>

        <div class="relative max-w-4xl mx-auto">
            <p class="eyebrow mb-4">DaftarX</p>
            <h1 class="text-5xl md:text-6xl font-extrabold tracking-tight text-ink-950 mb-5 leading-[1.15]">
                {{ __('marketing.home.hero_title') }}
            </h1>
            <p class="text-lg md:text-xl text-ink-700 mb-8 max-w-3xl mx-auto leading-relaxed">
                {{ __('marketing.home.hero_subtitle') }}
            </p>
            <div class="flex flex-wrap justify-center gap-3">
                <a href="/register" class="btn-primary !py-3 !px-7 !text-base">
                    {{ __('marketing.home.cta_trial') }}
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M5 12h14M13 5l7 7-7 7" />
                    </svg>
                </a>
                <a href="/pricing" class="btn-secondary !py-3 !px-6">
                    {{ __('marketing.home.cta_pricing') }}
                </a>
                <a href="/downloads" class="btn-ghost !py-3 !px-5">
                    {{ __('marketing.home.cta_download') }}
                </a>
            </div>
        </div>
    </section>

    {{-- ─── Value props grid ──────────────────────────────── --}}
    <section class="mb-20">
        <div class="text-center mb-12">
            <p class="eyebrow mb-3">المميزات الرئيسية</p>
            <h2 class="display-1 text-4xl">{{ __('marketing.home.value_props_title') }}</h2>
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
            @foreach (['arabic_first', 'eta', 'egyptian_taxes', 'multi_user', 'inventory', 'reports'] as $key)
                <article class="card-padded card-hover group">
                    <span class="inline-flex h-12 w-12 items-center justify-center rounded-xl bg-brand-100 text-brand-700 mb-4 group-hover:bg-brand-600 group-hover:text-white group-hover:shadow-brand-glow transition">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24"
                             fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                            <path d="{{ $valueIcons[$key] }}" />
                        </svg>
                    </span>
                    <h3 class="text-xl font-bold text-ink-950 mb-2">{{ __('marketing.home.value_props.'.$key.'.title') }}</h3>
                    <p class="text-ink-700 leading-relaxed text-sm">{{ __('marketing.home.value_props.'.$key.'.body') }}</p>
                </article>
            @endforeach
        </div>
    </section>

    {{-- ─── Who for ─────────────────────────────────────── --}}
    <section class="card-padded mb-20 p-10">
        <div class="text-center mb-8">
            <p class="eyebrow mb-3">مين بيستخدمنا</p>
            <h2 class="display-1 text-3xl">{{ __('marketing.home.who_for_title') }}</h2>
        </div>
        <ul class="grid grid-cols-1 md:grid-cols-2 gap-4">
            @foreach (['sme', 'accounting_firms', 'retail', 'services'] as $key)
                <li class="flex items-center gap-4 rounded-xl border border-ink-100 bg-ink-50/40 px-5 py-4 hover:border-brand-200 hover:bg-brand-50/40 transition">
                    <span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-white border border-ink-100 text-2xl shadow-elevation-1">
                        {{ $whoForIcons[$key] }}
                    </span>
                    <span class="text-ink-800 font-medium leading-relaxed">{{ __('marketing.home.who_for.'.$key) }}</span>
                </li>
            @endforeach
        </ul>
    </section>

    {{-- ─── Final CTA ───────────────────────────────────── --}}
    <section class="relative overflow-hidden rounded-3xl bg-hero-charcoal text-white p-12 md:p-16 text-center shadow-elevation-3">
        <span class="pointer-events-none absolute -bottom-24 -end-24 h-72 w-72 rounded-full"
              style="background: radial-gradient(circle, rgba(214,138,31,0.30) 0%, transparent 70%);"></span>
        <span class="pointer-events-none absolute -top-24 -start-24 h-64 w-64 rounded-full"
              style="background: radial-gradient(circle, rgba(245,214,145,0.20) 0%, transparent 70%);"></span>

        <div class="relative max-w-2xl mx-auto">
            <p class="eyebrow-light mb-3">ابدأ مجاناً</p>
            <h2 class="text-3xl md:text-4xl font-extrabold tracking-tight mb-4">{{ __('marketing.pricing.trial_note') }}</h2>
            <p class="text-ink-300 mb-7 text-lg">14 يوم تجربة مجانية لكل المميزات. بدون بطاقة ائتمان.</p>
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
