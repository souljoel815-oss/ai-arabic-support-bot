@extends('layouts.marketing')

@section('title', __('marketing.refund.title'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <header class="mb-10 pb-6 border-b border-ink-100">
            <p class="eyebrow mb-3">سياسة الاسترداد</p>
            <h1 class="display-1 text-5xl mb-3">{{ __('marketing.refund.title') }}</h1>
            <p class="text-xs text-ink-500 font-mono">{{ __('marketing.refund.last_updated') }}</p>
        </header>

        <div class="relative overflow-hidden rounded-2xl bg-hero-gold border border-brand-200/60 px-6 py-5 mb-10 shadow-elevation-2">
            <div class="relative flex items-center gap-4">
                <span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-white/70 text-brand-700 shadow-elevation-1">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M3 9h13l-3-3M21 15H8l3 3" />
                    </svg>
                </span>
                <p class="text-ink-900 font-semibold text-lg leading-relaxed">{{ __('marketing.refund.summary') }}</p>
            </div>
        </div>

        <section class="card-padded mb-5">
            <div class="flex items-center gap-3 mb-4">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-emerald-100 text-emerald-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="m5 12 5 5 9-12" />
                    </svg>
                </span>
                <h2 class="text-xl font-bold text-ink-950">{{ __('marketing.refund.eligibility_title') }}</h2>
            </div>
            <ul class="space-y-3">
                @foreach ((array) __('marketing.refund.eligibility') as $line)
                    <li class="flex items-start gap-3 text-ink-700">
                        <span class="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-emerald-700 mt-0.5">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" viewBox="0 0 24 24"
                                 fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                                <path d="m5 12 5 5 9-12" />
                            </svg>
                        </span>
                        <span class="leading-relaxed">{{ $line }}</span>
                    </li>
                @endforeach
            </ul>
        </section>

        <section class="card-padded mb-5">
            <div class="flex items-center gap-3 mb-4">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-red-100 text-red-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M18 6 6 18M6 6l12 12" />
                    </svg>
                </span>
                <h2 class="text-xl font-bold text-ink-950">{{ __('marketing.refund.not_eligible_title') }}</h2>
            </div>
            <ul class="space-y-3">
                @foreach ((array) __('marketing.refund.not_eligible') as $line)
                    <li class="flex items-start gap-3 text-ink-700">
                        <span class="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-red-100 text-red-700 mt-0.5">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" viewBox="0 0 24 24"
                                 fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                                <path d="M18 6 6 18M6 6l12 12" />
                            </svg>
                        </span>
                        <span class="leading-relaxed">{{ $line }}</span>
                    </li>
                @endforeach
            </ul>
        </section>

        <section class="card-padded">
            <div class="flex items-center gap-3 mb-4">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-100 text-brand-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <circle cx="12" cy="12" r="10" /><path d="M12 6v6l4 2" />
                    </svg>
                </span>
                <h2 class="text-xl font-bold text-ink-950">{{ __('marketing.refund.process_title') }}</h2>
            </div>
            <p class="text-ink-700 leading-relaxed">{{ __('marketing.refund.process') }}</p>
        </section>
    </article>
@endsection
