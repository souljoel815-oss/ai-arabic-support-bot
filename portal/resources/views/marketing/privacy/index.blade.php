@extends('layouts.marketing')

@section('title', __('marketing.privacy.title'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <h1 class="text-4xl font-bold mb-2">{{ __('marketing.privacy.title') }}</h1>
        <p class="text-sm text-stone-500 mb-6">{{ __('marketing.privacy.last_updated') }}</p>

        <p class="text-stone-700 mb-8 leading-relaxed">{{ __('marketing.privacy.summary') }}</p>

        @foreach ([
            'collect' => 'collect_title',
            'use' => 'use_title',
            'share' => 'share_title',
            'rights' => 'rights_title',
        ] as $listKey => $titleKey)
            <section class="mb-6">
                <h2 class="text-xl font-bold mb-3">{{ __('marketing.privacy.'.$titleKey) }}</h2>
                <ul class="space-y-2 list-disc list-inside text-stone-700">
                    @foreach ((array) __('marketing.privacy.'.$listKey) as $line)
                        <li>{{ $line }}</li>
                    @endforeach
                </ul>
            </section>
        @endforeach

        <div class="mt-8 rounded border border-stone-200 bg-stone-50 p-4 text-sm text-stone-600">
            <strong>Android app users:</strong> {{ __('marketing.privacy_android.play_store_required') }}
            <a href="/privacy/android" class="text-amber-700 hover:underline">→ {{ __('messages.footer.privacy_android') }}</a>
        </div>
    </article>
@endsection
