@extends('layouts.marketing')

@section('title', __('marketing.privacy.title'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <header class="mb-10 pb-6 border-b border-ink-100">
            <p class="eyebrow mb-3">سياسة الخصوصية</p>
            <h1 class="display-1 text-5xl mb-3">{{ __('marketing.privacy.title') }}</h1>
            <p class="text-xs text-ink-500 font-mono">{{ __('marketing.privacy.last_updated') }}</p>
        </header>

        <p class="text-ink-700 mb-10 leading-relaxed text-lg">{{ __('marketing.privacy.summary') }}</p>

        @php
            $sectionIcons = [
                'collect' => 'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12',
                'use'     => 'M19 21V5a2 2 0 0 0-2-2H7a2 2 0 0 0-2 2v16l4-2 4 2 4-2 4 2zM9 8h6m-6 4h6m-6 4h4',
                'share'   => 'M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8M16 6l-4-4-4 4M12 2v13',
                'rights'  => 'M9 12l2 2 4-4M21 12c0 5-3.5 9-9 9s-9-4-9-9 3.5-9 9-9 9 4 9 9z',
            ];
        @endphp

        <div class="space-y-5">
            @foreach ([
                'collect' => 'collect_title',
                'use' => 'use_title',
                'share' => 'share_title',
                'rights' => 'rights_title',
            ] as $listKey => $titleKey)
                <section class="card-padded">
                    <div class="flex items-center gap-3 mb-4">
                        <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-100 text-brand-700">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                                 fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                                <path d="{{ $sectionIcons[$listKey] }}" />
                            </svg>
                        </span>
                        <h2 class="text-xl font-bold text-ink-950">{{ __('marketing.privacy.'.$titleKey) }}</h2>
                    </div>
                    <ul class="space-y-2.5">
                        @foreach ((array) __('marketing.privacy.'.$listKey) as $line)
                            <li class="flex items-start gap-3 text-ink-700">
                                <span class="inline-block w-1.5 h-1.5 rounded-full bg-brand-500 mt-2 shrink-0"></span>
                                <span class="leading-relaxed">{{ $line }}</span>
                            </li>
                        @endforeach
                    </ul>
                </section>
            @endforeach
        </div>

        <div class="mt-8 rounded-2xl border border-sky-200 bg-sky-50 px-5 py-4 text-sm text-sky-900 flex items-start gap-3">
            <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-sky-200/70 text-sky-700 font-bold">ⓘ</span>
            <p>
                <strong>Android app users:</strong> {{ __('marketing.privacy_android.play_store_required') }}
                <a href="/privacy/android" class="text-brand-700 font-semibold hover:underline">→ {{ __('messages.footer.privacy_android') }}</a>
            </p>
        </div>
    </article>
@endsection
