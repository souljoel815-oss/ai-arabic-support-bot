@extends('layouts.portal')

@section('title', __('billing.title'))

@section('content')
    <header class="mb-6">
        <h1 class="text-2xl font-bold mb-1">{{ __('billing.title') }}</h1>
        <p class="text-stone-600">{{ __('billing.subtitle') }}</p>
    </header>

    @if ($invoices->isEmpty())
        <div class="rounded border border-stone-200 bg-white p-8 text-center text-stone-600">
            {{ __('billing.no_invoices') }}
        </div>
    @else
        <div class="rounded-lg border border-stone-200 bg-white overflow-hidden">
            <table class="w-full text-sm">
                <thead class="bg-stone-50 border-b border-stone-200 text-stone-700">
                    <tr>
                        <th class="text-start px-4 py-3">{{ __('billing.columns.number') }}</th>
                        <th class="text-start px-4 py-3">{{ __('billing.columns.kind') }}</th>
                        <th class="text-start px-4 py-3">{{ __('billing.columns.amount') }}</th>
                        <th class="text-start px-4 py-3">{{ __('billing.columns.method') }}</th>
                        <th class="text-start px-4 py-3">{{ __('billing.columns.status') }}</th>
                        <th class="text-start px-4 py-3">{{ __('billing.columns.issued') }}</th>
                        <th class="text-end px-4 py-3">{{ __('billing.columns.actions') }}</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach ($invoices as $invoice)
                        @php
                            $statusClass = match ($invoice->status) {
                                'Paid' => 'bg-green-100 text-green-800',
                                'Pending' => 'bg-amber-100 text-amber-800',
                                'Refunded' => 'bg-stone-100 text-stone-700',
                                'Failed' => 'bg-red-100 text-red-800',
                                default => 'bg-stone-100 text-stone-700',
                            };
                            $eligibility = $invoice->refund_eligibility;
                        @endphp
                        <tr class="border-b border-stone-100">
                            <td class="px-4 py-3 font-mono text-xs">{{ $invoice->invoice_number }}</td>
                            <td class="px-4 py-3">{{ __('billing.kind.'.$invoice->kind) }}</td>
                            <td class="px-4 py-3 font-semibold">{{ $invoice->amount_egp }} EGP</td>
                            <td class="px-4 py-3 text-xs text-stone-600">{{ __('subscription.payment_method.'.$invoice->payment_method) }}</td>
                            <td class="px-4 py-3">
                                <span class="text-xs px-2 py-1 rounded {{ $statusClass }}">
                                    {{ __('billing.status.'.$invoice->status) }}
                                </span>
                                @if ($invoice->status === 'Paid' && $invoice->kind === 'FirstPeriod')
                                    <div class="text-xs mt-1 {{ $eligibility === 'eligible' ? 'text-green-700' : 'text-stone-500' }}">
                                        {{ __('billing.refund_eligibility.'.$eligibility) }}
                                    </div>
                                @endif
                            </td>
                            <td class="px-4 py-3 text-stone-600 text-xs">{{ $invoice->created_at?->format('Y-m-d') }}</td>
                            <td class="px-4 py-3 text-end space-y-1">
                                <a href="{{ route('portal.billing.pdf', ['invoice' => $invoice->id]) }}"
                                   class="inline-block rounded border border-stone-300 px-3 py-1 text-xs hover:bg-stone-100">
                                    ⬇ {{ __('billing.action_download') }}
                                </a>
                                @if ($eligibility === 'eligible')
                                    <form method="POST" action="{{ route('portal.billing.refund', ['invoice' => $invoice->id]) }}" class="inline-block"
                                          onsubmit="return confirm('{{ __('billing.refund_confirm', ['number' => $invoice->invoice_number]) }}');">
                                        @csrf
                                        <button type="submit" class="rounded border border-red-300 text-red-700 px-3 py-1 text-xs hover:bg-red-50">
                                            ↩ {{ __('billing.action_refund') }}
                                        </button>
                                    </form>
                                @endif
                                @if ($invoice->status === 'Pending')
                                    <div class="text-xs text-stone-500 max-w-[280px] mt-1">
                                        {{ __('billing.pending_note') }}
                                    </div>
                                @endif
                            </td>
                        </tr>
                    @endforeach
                </tbody>
            </table>
        </div>
    @endif
@endsection
