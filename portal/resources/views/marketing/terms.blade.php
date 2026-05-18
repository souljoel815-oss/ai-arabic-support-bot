@extends('layouts.marketing')

@section('title', __('marketing.terms.title'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <header class="mb-10 pb-6 border-b border-ink-100">
            <p class="eyebrow mb-3">شروط الاستخدام</p>
            <h1 class="display-1 text-5xl mb-3">{{ __('marketing.terms.title') }}</h1>
            <p class="text-xs text-ink-500 font-mono">{{ __('marketing.terms.last_updated') }}</p>
        </header>

        <p class="text-ink-700 mb-10 leading-relaxed text-lg">{{ __('marketing.terms.body') }}</p>

        <div class="space-y-7">
            @foreach (['use', 'data', 'liability', 'changes'] as $i => $key)
                <section class="card-padded">
                    <div class="flex items-center gap-3 mb-3">
                        <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-brand-700 font-bold text-sm">{{ $i + 1 }}</span>
                        <h2 class="text-xl font-bold text-ink-950">{{ __('marketing.terms.sections.'.$key.'.title') }}</h2>
                    </div>
                    <p class="text-ink-700 leading-relaxed">{{ __('marketing.terms.sections.'.$key.'.body') }}</p>
                </section>
            @endforeach
        </div>
    </article>
@endsection
