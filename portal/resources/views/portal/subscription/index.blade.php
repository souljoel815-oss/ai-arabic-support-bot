@extends('layouts.portal')

@section('title', __('subscription.title'))

@section('content')
    <header class="mb-8 flex flex-wrap items-end justify-between gap-3">
        <div>
            <p class="eyebrow mb-2">إدارة الاشتراك</p>
            <h1 class="display-1 mb-1.5">{{ __('subscription.title') }}</h1>
            <p class="text-ink-600 text-base">{{ __('subscription.subtitle') }}</p>
        </div>
        <a href="{{ route('portal.subscription.start') }}" class="btn-primary">
            + {{ __('subscription.subscribe_cta') }}
        </a>
    </header>

    @if ($subscriptions->isEmpty())
        <div class="card-padded text-center py-14 bg-dots">
            <h2 class="display-2 text-2xl mb-2">{{ __('subscription.no_active') }}</h2>
            <p class="text-ink-600 max-w-md mx-auto mb-6">
                ابدأ اشتراكك واستمتع بكامل قدرات النظام بدون انقطاع.
            </p>
            <a href="{{ route('portal.subscription.start') }}" class="btn-primary">
                {{ __('subscription.subscribe_cta') }}
            </a>
        </div>
    @else
        <div class="space-y-5">
            @foreach ($subscriptions as $subscription)
                <article class="card-padded card-hover relative overflow-hidden">
                    {{-- Gradient accent strip on the leading edge --}}
                    <span class="absolute inset-y-0 start-0 w-1 bg-gradient-to-b from-brand-500 to-brand-700"></span>

                    <header class="flex flex-wrap items-start justify-between gap-4 mb-5 pb-5 border-b border-ink-100">
                        <div>
                            <p class="eyebrow-muted mb-1.5">
                                {{ __('subscription.cadence.'.$subscription->billing_cadence) }}
                            </p>
                            <h2 class="display-2 text-2xl">
                                {{ __('marketing.pricing.tiers.'.strtolower($subscription->tier).'.name') }}
                            </h2>
                            <div class="flex items-center gap-2 mt-2">
                                @if ($subscription->status === 'Active')
                                    <span class="pill-success">{{ __('subscription.status.Active') }}</span>
                                @elseif ($subscription->status === 'PastDue')
                                    <span class="pill-danger">{{ __('subscription.status.PastDue') }}</span>
                                @else
                                    <span class="pill-muted">{{ __('subscription.status.'.$subscription->status) }}</span>
                                @endif
                                @if ($subscription->hasPrioritySupport())
                                    <span class="pill-warning">⚡ {{ __('licences.priority_support_badge') }}</span>
                                @endif
                            </div>
                        </div>
                        @if ($subscription->status === \App\Models\Subscription::STATUS_ACTIVE && $subscription->cancelled_at === null)
                            <form method="POST" action="{{ route('portal.subscription.cancel', ['subscription' => $subscription->id]) }}"
                                  onsubmit="return confirm('{{ __('subscription.cancel.confirm', ['date' => $subscription->current_period_end_at?->format('Y-m-d')]) }}');">
                                @csrf
                                <button type="submit" class="btn-danger !py-2 !px-4 !text-sm">
                                    {{ __('subscription.cancel.title') }}
                                </button>
                            </form>
                        @endif
                    </header>

                    <dl class="grid grid-cols-2 md:grid-cols-4 gap-6 text-sm">
                        <div>
                            <dt class="eyebrow-muted mb-1.5">{{ __('subscription.columns.period_start') }}</dt>
                            <dd class="font-mono font-semibold text-ink-900">{{ $subscription->current_period_start_at?->format('Y-m-d') ?? '—' }}</dd>
                        </div>
                        <div>
                            <dt class="eyebrow-muted mb-1.5">{{ __('subscription.columns.period_end') }}</dt>
                            <dd class="font-mono font-semibold text-ink-900">{{ $subscription->current_period_end_at?->format('Y-m-d') ?? '—' }}</dd>
                        </div>
                        <div>
                            <dt class="eyebrow-muted mb-1.5">SLA الدعم</dt>
                            <dd class="font-semibold text-ink-900">{{ $subscription->hasPrioritySupport() ? '⚡ 4 ساعات' : '24 ساعة' }}</dd>
                        </div>
                        @if ($subscription->cancelled_at)
                            <div>
                                <dt class="eyebrow-muted mb-1.5">{{ __('subscription.columns.cancelled_at') }}</dt>
                                <dd class="font-mono font-semibold text-red-700">{{ $subscription->cancelled_at?->format('Y-m-d') }}</dd>
                            </div>
                        @endif
                    </dl>

                    @if ($subscription->cancelled_at)
                        <div class="mt-5 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800 flex items-center gap-3">
                            <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-red-100">
                                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-red-600" viewBox="0 0 24 24"
                                     fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <circle cx="12" cy="12" r="10" /><path d="M12 8v4m0 4h.01" />
                                </svg>
                            </span>
                            <span>{{ __('subscription.will_cancel_notice', ['date' => $subscription->current_period_end_at?->format('Y-m-d')]) }}</span>
                        </div>
                    @endif
                </article>
            @endforeach
        </div>
    @endif
@endsection
