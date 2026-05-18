@extends('layouts.marketing')

@section('title', __('marketing.about.title'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <h1 class="text-4xl font-bold mb-6">{{ __('marketing.about.title') }}</h1>

        <section class="mb-8">
            <p class="text-lg text-stone-700 leading-relaxed">{{ __('marketing.about.mission') }}</p>
        </section>

        <section class="mb-8 rounded-lg border border-stone-200 bg-white p-6">
            <h2 class="text-2xl font-bold mb-3">{{ __('marketing.about.team_title') }}</h2>
            <p class="text-stone-700">{{ __('marketing.about.team_body') }}</p>
        </section>

        <section class="mb-8 rounded-lg border border-amber-200 bg-amber-50 p-6">
            <h2 class="text-2xl font-bold mb-3">{{ __('marketing.about.contact_partners_title') }}</h2>
            <p class="text-stone-700 mb-4">{{ __('marketing.about.contact_partners_body') }}</p>
            <a href="/contact" class="inline-block rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
                {{ __('messages.nav.contact') }}
            </a>
        </section>
    </article>
@endsection
