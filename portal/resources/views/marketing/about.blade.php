@extends('layouts.marketing')

@section('title', __('marketing.about.title'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <header class="mb-10">
            <p class="eyebrow mb-3">من نحن</p>
            <h1 class="display-1 text-5xl mb-4">{{ __('marketing.about.title') }}</h1>
            <p class="text-lg text-ink-700 leading-relaxed">{{ __('marketing.about.mission') }}</p>
        </header>

        <section class="card-padded mb-6">
            <div class="flex items-center gap-3 mb-4">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-100 text-brand-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
                    </svg>
                </span>
                <h2 class="text-xl font-bold text-ink-950">{{ __('marketing.about.team_title') }}</h2>
            </div>
            <p class="text-ink-700 leading-relaxed">{{ __('marketing.about.team_body') }}</p>
        </section>

        <section class="relative overflow-hidden rounded-2xl border border-brand-200/60 p-8 bg-hero-gold shadow-elevation-2">
            <div class="relative">
                <p class="eyebrow mb-3">شركاء وموزّعون</p>
                <h2 class="display-2 text-2xl mb-3">{{ __('marketing.about.contact_partners_title') }}</h2>
                <p class="text-ink-700 mb-5 leading-relaxed max-w-2xl">{{ __('marketing.about.contact_partners_body') }}</p>
                <a href="/contact" class="btn-primary">
                    {{ __('messages.nav.contact') }}
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M5 12h14M13 5l7 7-7 7" />
                    </svg>
                </a>
            </div>
        </section>
    </article>
@endsection
