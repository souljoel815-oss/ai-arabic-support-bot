@extends('layouts.marketing')

@section('title', __('marketing.privacy_android.title'))
@section('description', __('marketing.privacy_android.play_store_required'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <h1 class="text-4xl font-bold mb-2">{{ __('marketing.privacy_android.title') }}</h1>
        <p class="text-sm text-stone-500 mb-6">{{ __('marketing.privacy_android.last_updated') }}</p>

        <div class="rounded border border-amber-200 bg-amber-50 p-4 mb-8 text-sm">
            {{ __('marketing.privacy_android.play_store_required') }}
        </div>

        @foreach (['crash', 'push', 'data_local', 'no_ads'] as $key)
            <section class="mb-6">
                <h2 class="text-xl font-bold mb-3">{{ __('marketing.privacy_android.'.$key.'_title') }}</h2>
                <p class="text-stone-700 leading-relaxed">{{ __('marketing.privacy_android.'.$key.'_body') }}</p>
            </section>
        @endforeach
    </article>
@endsection
