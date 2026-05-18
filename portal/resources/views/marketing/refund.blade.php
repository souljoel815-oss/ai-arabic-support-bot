@extends('layouts.marketing')

@section('title', __('marketing.refund.title'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <h1 class="text-4xl font-bold mb-2">{{ __('marketing.refund.title') }}</h1>
        <p class="text-sm text-stone-500 mb-6">{{ __('marketing.refund.last_updated') }}</p>

        <div class="rounded-lg border border-amber-200 bg-amber-50 p-4 mb-8">
            <p class="text-stone-800 font-semibold">{{ __('marketing.refund.summary') }}</p>
        </div>

        <section class="mb-6">
            <h2 class="text-xl font-bold mb-3 text-green-700">{{ __('marketing.refund.eligibility_title') }}</h2>
            <ul class="space-y-2">
                @foreach ((array) __('marketing.refund.eligibility') as $line)
                    <li class="flex items-start gap-2 text-stone-700">
                        <span class="text-green-600 mt-0.5">✓</span>
                        <span>{{ $line }}</span>
                    </li>
                @endforeach
            </ul>
        </section>

        <section class="mb-6">
            <h2 class="text-xl font-bold mb-3 text-red-700">{{ __('marketing.refund.not_eligible_title') }}</h2>
            <ul class="space-y-2">
                @foreach ((array) __('marketing.refund.not_eligible') as $line)
                    <li class="flex items-start gap-2 text-stone-700">
                        <span class="text-red-600 mt-0.5">✗</span>
                        <span>{{ $line }}</span>
                    </li>
                @endforeach
            </ul>
        </section>

        <section class="mb-6">
            <h2 class="text-xl font-bold mb-3">{{ __('marketing.refund.process_title') }}</h2>
            <p class="text-stone-700 leading-relaxed">{{ __('marketing.refund.process') }}</p>
        </section>
    </article>
@endsection
