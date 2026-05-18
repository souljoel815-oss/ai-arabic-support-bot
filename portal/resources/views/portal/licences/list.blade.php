@extends('layouts.portal')

@section('title', __('licences.title'))

@section('content')
    <header class="mb-6">
        <h1 class="text-2xl font-bold mb-1">{{ __('licences.title') }}</h1>
        <p class="text-stone-600">{{ __('licences.subtitle') }}</p>
    </header>

    @if ($subscriptions->isEmpty())
        <div class="rounded border border-amber-200 bg-amber-50 p-6 text-center">
            <p class="mb-4">{{ __('licences.no_subscriptions') }}</p>
            <a href="/portal/subscription" class="inline-block rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
                {{ __('licences.subscribe_cta') }}
            </a>
        </div>
    @else
        @foreach ($subscriptions as $subscription)
            <section class="mb-8 rounded-lg border border-stone-200 bg-white p-6 shadow-sm">
                <header class="flex flex-wrap items-center justify-between gap-3 mb-4">
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
                            {{ __('licences.cadence_label') }}: {{ __('licences.cadence.'.$subscription->billing_cadence) }}
                            ·
                            {{ __('licences.status_label') }}: {{ __('licences.status.'.$subscription->status) }}
                            ·
                            {{ __('licences.period_end_label') }}: {{ $subscription->current_period_end_at?->format('Y-m-d') }}
                        </p>
                    </div>
                    @if ($subscription->status === \App\Models\Subscription::STATUS_ACTIVE)
                        <a href="{{ route('portal.licences.activate-paid', ['subscription' => $subscription->id]) }}"
                           class="rounded bg-amber-600 px-4 py-2 text-white text-sm font-semibold hover:bg-amber-700">
                            + {{ __('licences.activate.title') }}
                        </a>
                    @endif
                </header>

                @php
                    $active = $subscription->licences->whereNull('retired_at');
                    $retired = $subscription->licences->whereNotNull('retired_at');
                @endphp

                @if ($active->isEmpty())
                    <p class="text-stone-500 italic">{{ __('licences.licences_list.empty') }}</p>
                @else
                    <h3 class="text-lg font-semibold mb-2">{{ __('licences.licences_list.title') }}</h3>
                    <div class="overflow-x-auto">
                        <table class="w-full text-sm">
                            <thead class="border-b border-stone-200 text-stone-600">
                                <tr>
                                    <th class="text-start py-2">{{ __('licences.licences_list.columns.hwid') }}</th>
                                    <th class="text-start py-2">{{ __('licences.licences_list.columns.edition') }}</th>
                                    <th class="text-start py-2">{{ __('licences.licences_list.columns.issued_at') }}</th>
                                    <th class="text-start py-2">{{ __('licences.licences_list.columns.expires_at') }}</th>
                                    <th class="text-end py-2">{{ __('licences.licences_list.columns.actions') }}</th>
                                </tr>
                            </thead>
                            <tbody>
                                @foreach ($active as $licence)
                                    <tr class="border-b border-stone-100">
                                        <td class="py-3 font-mono text-xs">{{ $licence->hwid }}</td>
                                        <td class="py-3">{{ $licence->edition }}</td>
                                        <td class="py-3">{{ $licence->issued_at?->format('Y-m-d') }}</td>
                                        <td class="py-3">{{ $licence->expires_at?->format('Y-m-d') }}</td>
                                        <td class="py-3 text-end space-x-1 rtl:space-x-reverse">
                                            <a href="{{ route('portal.licences.token', ['licence' => $licence->id]) }}"
                                               class="inline-block rounded border border-stone-300 px-3 py-1 text-xs hover:bg-stone-100">
                                                ⬇ {{ __('licences.licences_list.actions.download') }}
                                            </a>
                                            <a href="{{ route('portal.licences.transfer', ['licence' => $licence->id]) }}"
                                               class="inline-block rounded bg-amber-600 px-3 py-1 text-xs text-white hover:bg-amber-700">
                                                ⇄ {{ __('licences.licences_list.actions.transfer') }}
                                            </a>
                                        </td>
                                    </tr>
                                @endforeach
                            </tbody>
                        </table>
                    </div>
                @endif

                @if ($retired->isNotEmpty())
                    <details class="mt-4">
                        <summary class="cursor-pointer text-sm text-stone-600 hover:text-stone-900">
                            {{ __('licences.licences_list.retired_section_title') }} ({{ $retired->count() }})
                        </summary>
                        <table class="w-full text-sm mt-2">
                            <thead class="border-b border-stone-200 text-stone-600">
                                <tr>
                                    <th class="text-start py-2">{{ __('licences.licences_list.columns.hwid') }}</th>
                                    <th class="text-start py-2">{{ __('licences.licences_list.retired_columns.reason') }}</th>
                                    <th class="text-start py-2">{{ __('licences.licences_list.retired_columns.retired_at') }}</th>
                                </tr>
                            </thead>
                            <tbody>
                                @foreach ($retired as $licence)
                                    <tr class="border-b border-stone-100 text-stone-600">
                                        <td class="py-2 font-mono text-xs">{{ $licence->hwid }}</td>
                                        <td class="py-2">{{ __('licences.licences_list.reason.'.$licence->retired_reason) }}</td>
                                        <td class="py-2">{{ $licence->retired_at?->format('Y-m-d') }}</td>
                                    </tr>
                                @endforeach
                            </tbody>
                        </table>
                    </details>
                @endif
            </section>
        @endforeach
    @endif
@endsection
