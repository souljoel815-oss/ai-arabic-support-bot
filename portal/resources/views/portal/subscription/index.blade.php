@extends('layouts.portal')

@section('title', __('subscription.title'))

@section('content')
    <header class="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
            <h1 class="text-2xl font-bold mb-1">{{ __('subscription.title') }}</h1>
            <p class="text-stone-600">{{ __('subscription.subtitle') }}</p>
        </div>
        <a href="{{ route('portal.subscription.start') }}" class="rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
            + {{ __('subscription.subscribe_cta') }}
        </a>
    </header>

    @if ($subscriptions->isEmpty())
        <div class="rounded border border-amber-200 bg-amber-50 p-6 text-center">
            <p>{{ __('subscription.no_active') }}</p>
        </div>
    @else
        <div class="space-y-4">
            @foreach ($subscriptions as $subscription)
                <section class="rounded-lg border border-stone-200 bg-white p-6">
                    <header class="flex flex-wrap items-center justify-between gap-3 mb-3">
                        <div>
                            <h2 class="text-xl font-bold">
                                {{ __('marketing.pricing.tiers.'.strtolower($subscription->tier).'.name') }}
                                @if ($subscription->hasPrioritySupport())
                                    <span class="ms-2 inline-block rounded-full bg-amber-100 text-amber-800 px-2 py-0.5 text-xs font-semibold">
                                        ⚡ {{ __('licences.priority_support_badge') }}
                                    </span>
                                @endif
                            </h2>
                            <p class="text-sm text-stone-600">
                                {{ __('subscription.cadence.'.$subscription->billing_cadence) }} ·
                                {{ __('subscription.status.'.$subscription->status) }}
                            </p>
                        </div>
                        @if ($subscription->status === \App\Models\Subscription::STATUS_ACTIVE && $subscription->cancelled_at === null)
                            <form method="POST" action="{{ route('portal.subscription.cancel', ['subscription' => $subscription->id]) }}"
                                  onsubmit="return confirm('{{ __('subscription.cancel.confirm', ['date' => $subscription->current_period_end_at?->format('Y-m-d')]) }}');">
                                @csrf
                                <button type="submit" class="rounded border border-red-300 text-red-700 px-4 py-1 text-sm hover:bg-red-50">
                                    {{ __('subscription.cancel.title') }}
                                </button>
                            </form>
                        @endif
                    </header>

                    <dl class="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                        <div>
                            <dt class="text-stone-500">{{ __('subscription.columns.period_start') }}</dt>
                            <dd>{{ $subscription->current_period_start_at?->format('Y-m-d') }}</dd>
                        </div>
                        <div>
                            <dt class="text-stone-500">{{ __('subscription.columns.period_end') }}</dt>
                            <dd>{{ $subscription->current_period_end_at?->format('Y-m-d') }}</dd>
                        </div>
                        @if ($subscription->cancelled_at)
                            <div>
                                <dt class="text-stone-500">{{ __('subscription.columns.cancelled_at') }}</dt>
                                <dd class="text-red-700">{{ $subscription->cancelled_at?->format('Y-m-d') }}</dd>
                            </div>
                        @endif
                    </dl>

                    @if ($subscription->cancelled_at)
                        <div class="mt-3 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">
                            {{ __('subscription.will_cancel_notice', ['date' => $subscription->current_period_end_at?->format('Y-m-d')]) }}
                        </div>
                    @endif
                </section>
            @endforeach
        </div>
    @endif
@endsection
