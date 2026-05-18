@extends('layouts.marketing')

@section('title', __('marketing.privacy_android.title'))
@section('description', __('marketing.privacy_android.play_store_required'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <header class="mb-10 pb-6 border-b border-ink-100">
            <p class="eyebrow mb-3">سياسة Android</p>
            <h1 class="display-1 text-5xl mb-3">{{ __('marketing.privacy_android.title') }}</h1>
            <p class="text-xs text-ink-500 font-mono">{{ __('marketing.privacy_android.last_updated') }}</p>
        </header>

        <div class="relative overflow-hidden rounded-2xl px-6 py-5 mb-10 text-white shadow-elevation-2"
             style="background: linear-gradient(135deg, #3ddc84 0%, #1ba768 100%);">
            <div class="relative flex items-center gap-4">
                <span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-white/15 backdrop-blur">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 text-white" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M17.523 15.341a1.118 1.118 0 1 1 0-2.236 1.118 1.118 0 0 1 0 2.236m-11.046 0a1.118 1.118 0 1 1 0-2.236 1.118 1.118 0 0 1 0 2.236M5.05 8.39 3.013 4.857a.416.416 0 1 1 .721-.418L5.799 8.02C7.078 7.385 8.493 7.045 10 7.045s2.922.34 4.201.975l2.065-3.581a.416.416 0 1 1 .72.418L14.95 8.39c2.27 1.158 3.811 3.302 3.811 5.815H1.239c0-2.513 1.541-4.657 3.811-5.815"/>
                    </svg>
                </span>
                <p class="text-white/95 font-semibold leading-relaxed">{{ __('marketing.privacy_android.play_store_required') }}</p>
            </div>
        </div>

        @php
            $sectionIcons = [
                'crash' => 'M12 9v4m0 4h.01M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z',
                'push' => 'M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 0 1-3.46 0',
                'data_local' => 'M5 12V7a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v5M5 12v5a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-5M5 12h14',
                'no_ads' => 'M18.36 5.64A9 9 0 1 1 5.64 18.36 9 9 0 0 1 18.36 5.64Zm-12.72 12.72L18.36 5.64',
            ];
        @endphp

        <div class="space-y-5">
            @foreach (['crash', 'push', 'data_local', 'no_ads'] as $key)
                <section class="card-padded">
                    <div class="flex items-center gap-3 mb-3">
                        <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-100 text-brand-700">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                                 fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                                <path d="{{ $sectionIcons[$key] }}" />
                            </svg>
                        </span>
                        <h2 class="text-xl font-bold text-ink-950">{{ __('marketing.privacy_android.'.$key.'_title') }}</h2>
                    </div>
                    <p class="text-ink-700 leading-relaxed">{{ __('marketing.privacy_android.'.$key.'_body') }}</p>
                </section>
            @endforeach
        </div>
    </article>
@endsection
