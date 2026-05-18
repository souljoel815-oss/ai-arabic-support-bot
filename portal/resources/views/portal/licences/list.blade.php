@extends('layouts.portal')

@section('title', __('licences.title'))

@section('content')
    <header class="mb-8">
        <p class="eyebrow mb-2">إدارة التراخيص</p>
        <h1 class="display-1 mb-1.5">{{ __('licences.title') }}</h1>
        <p class="text-ink-600 text-base">{{ __('licences.subtitle') }}</p>
    </header>

    @if ($subscriptions->isEmpty())
        <div class="card-padded text-center py-14">
            <div class="mx-auto h-16 w-16 rounded-full bg-brand-100 flex items-center justify-center mb-4">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-brand-700" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 2 13 10m3 3-2-2m-1 6a4 4 0 1 1-5.66-5.66 4 4 0 0 1 5.66 5.66Z" />
                </svg>
            </div>
            <h2 class="text-lg font-bold text-ink-950 mb-2">{{ __('licences.no_subscriptions') }}</h2>
            <a href="/portal/subscription" class="btn-primary mt-2">
                {{ __('licences.subscribe_cta') }}
            </a>
        </div>
    @else
        @foreach ($subscriptions as $subscription)
            <section class="card-padded mb-6">
                <header class="flex flex-wrap items-start justify-between gap-3 mb-6 pb-5 border-b border-ink-100">
                    <div>
                        <div class="flex items-center gap-2 mb-1">
                            <h2 class="text-2xl font-bold text-ink-950">
                                {{ __('marketing.pricing.tiers.'.strtolower($subscription->tier).'.name') }}
                            </h2>
                            @if ($subscription->hasPrioritySupport())
                                <span class="pill-info">⚡ {{ __('licences.priority_support_badge') }}</span>
                            @endif
                        </div>
                        <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-600">
                            <span>{{ __('licences.cadence.'.$subscription->billing_cadence) }}</span>
                            <span class="text-ink-300">·</span>
                            <span>
                                @if ($subscription->status === 'Active')
                                    <span class="pill-success">{{ __('licences.status.'.$subscription->status) }}</span>
                                @else
                                    <span class="pill-muted">{{ __('licences.status.'.$subscription->status) }}</span>
                                @endif
                            </span>
                            <span class="text-ink-300">·</span>
                            <span class="text-xs">
                                {{ __('licences.period_end_label') }}:
                                <span class="font-mono text-ink-800">{{ $subscription->current_period_end_at?->format('Y-m-d') ?? '—' }}</span>
                            </span>
                        </div>
                    </div>
                    @if ($subscription->status === \App\Models\Subscription::STATUS_ACTIVE)
                        <a href="{{ route('portal.licences.activate-paid', ['subscription' => $subscription->id]) }}"
                           class="btn-primary !text-sm !py-2 !px-4">
                            + {{ __('licences.activate.title') }}
                        </a>
                    @endif
                </header>

                @php
                    $active = $subscription->licences->whereNull('retired_at');
                    $retired = $subscription->licences->whereNotNull('retired_at');
                @endphp

                @if ($active->isEmpty())
                    <div class="rounded-xl border border-dashed border-ink-200 bg-ink-50/50 p-6 text-center">
                        <p class="text-ink-500">{{ __('licences.licences_list.empty') }}</p>
                    </div>
                @else
                    <h3 class="text-sm font-semibold text-ink-700 mb-3 uppercase tracking-wider">{{ __('licences.licences_list.title') }}</h3>
                    <div class="overflow-x-auto rounded-xl border border-ink-100">
                        <table class="w-full text-sm">
                            <thead class="bg-ink-50/70 text-ink-600">
                                <tr class="text-xs uppercase tracking-wider">
                                    <th class="text-start px-4 py-3 font-semibold">{{ __('licences.licences_list.columns.hwid') }}</th>
                                    <th class="text-start px-4 py-3 font-semibold">{{ __('licences.licences_list.columns.edition') }}</th>
                                    <th class="text-start px-4 py-3 font-semibold">{{ __('licences.licences_list.columns.issued_at') }}</th>
                                    <th class="text-start px-4 py-3 font-semibold">{{ __('licences.licences_list.columns.expires_at') }}</th>
                                    <th class="text-end px-4 py-3 font-semibold">{{ __('licences.licences_list.columns.actions') }}</th>
                                </tr>
                            </thead>
                            <tbody class="divide-y divide-ink-100">
                                @foreach ($active as $licence)
                                    <tr class="hover:bg-ink-50/40 transition">
                                        <td class="px-4 py-3 font-mono text-xs text-ink-800">{{ $licence->hwid }}</td>
                                        <td class="px-4 py-3">{{ $licence->edition }}</td>
                                        <td class="px-4 py-3 text-ink-600 text-xs">{{ $licence->issued_at?->format('Y-m-d') }}</td>
                                        <td class="px-4 py-3 text-ink-600 text-xs">{{ $licence->expires_at?->format('Y-m-d') }}</td>
                                        <td class="px-4 py-3 text-end">
                                            <div class="flex items-center justify-end gap-2 flex-wrap">
                                                <a href="{{ route('portal.licences.token', ['licence' => $licence->id]) }}"
                                                   class="inline-flex items-center gap-1.5 rounded-lg border border-ink-200 bg-white px-3 py-1.5 text-xs font-semibold text-ink-700 hover:border-brand-300 hover:text-brand-700 hover:bg-brand-50 transition">
                                                    <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" viewBox="0 0 24 24"
                                                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                                        <path d="M12 3v12m-5-5 5 5 5-5M5 21h14" />
                                                    </svg>
                                                    {{ __('licences.licences_list.actions.download') }}
                                                </a>
                                                <a href="{{ route('portal.licences.transfer', ['licence' => $licence->id]) }}"
                                                   class="btn-primary !text-xs !py-1.5 !px-3">
                                                    ⇄ {{ __('licences.licences_list.actions.transfer') }}
                                                </a>
                                            </div>
                                        </td>
                                    </tr>
                                @endforeach
                            </tbody>
                        </table>
                    </div>
                @endif

                @if ($retired->isNotEmpty())
                    <details class="mt-5 group">
                        <summary class="cursor-pointer text-sm font-semibold text-ink-600 hover:text-ink-900 transition list-none flex items-center gap-2">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 transition group-open:rotate-90" viewBox="0 0 24 24"
                                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <path d="m9 18 6-6-6-6" />
                            </svg>
                            {{ __('licences.licences_list.retired_section_title') }}
                            <span class="pill-muted">{{ $retired->count() }}</span>
                        </summary>
                        <div class="overflow-x-auto mt-3 rounded-xl border border-ink-100">
                            <table class="w-full text-sm">
                                <thead class="bg-ink-50/70 text-ink-600">
                                    <tr class="text-xs uppercase tracking-wider">
                                        <th class="text-start px-4 py-2.5 font-semibold">{{ __('licences.licences_list.columns.hwid') }}</th>
                                        <th class="text-start px-4 py-2.5 font-semibold">{{ __('licences.licences_list.retired_columns.reason') }}</th>
                                        <th class="text-start px-4 py-2.5 font-semibold">{{ __('licences.licences_list.retired_columns.retired_at') }}</th>
                                    </tr>
                                </thead>
                                <tbody class="divide-y divide-ink-100 text-ink-600">
                                    @foreach ($retired as $licence)
                                        <tr>
                                            <td class="px-4 py-2.5 font-mono text-xs">{{ $licence->hwid }}</td>
                                            <td class="px-4 py-2.5">{{ __('licences.licences_list.reason.'.$licence->retired_reason) }}</td>
                                            <td class="px-4 py-2.5 text-xs">{{ $licence->retired_at?->format('Y-m-d') }}</td>
                                        </tr>
                                    @endforeach
                                </tbody>
                            </table>
                        </div>
                    </details>
                @endif
            </section>
        @endforeach
    @endif
@endsection
