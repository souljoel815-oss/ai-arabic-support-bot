@extends('layouts.portal')

@section('title', __('billing.title'))

@section('content')
    <header class="mb-8">
        <div class="flex items-end justify-between gap-4 flex-wrap">
            <div>
                <p class="eyebrow mb-2">الفواتير والمدفوعات</p>
                <h1 class="display-1 mb-1.5">{{ __('billing.title') }}</h1>
                <p class="text-ink-600 text-base">{{ __('billing.subtitle') }}</p>
            </div>
            <div class="text-xs">
                <span class="pill-muted">{{ $invoices->count() }} فاتورة</span>
            </div>
        </div>
    </header>

    @if ($invoices->isEmpty())
        <div class="card-padded text-center py-16">
            <div class="mx-auto h-16 w-16 rounded-full bg-ink-50 flex items-center justify-center mb-4">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-ink-400" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M6 3v18l3-2 3 2 3-2 3 2V3zM9 8h6m-6 4h6m-6 4h4" />
                </svg>
            </div>
            <h2 class="text-lg font-bold text-ink-950 mb-1">لا توجد فواتير بعد</h2>
            <p class="text-sm text-ink-600 max-w-sm mx-auto">{{ __('billing.no_invoices') }}</p>
        </div>
    @else
        <div class="card overflow-hidden shadow-elevation-2">
            <table class="w-full text-sm">
                <thead class="bg-ink-50/70 border-b border-ink-100 text-ink-600">
                    <tr class="text-xs uppercase tracking-wider">
                        <th class="text-start px-5 py-3 font-semibold">{{ __('billing.columns.number') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('billing.columns.kind') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('billing.columns.amount') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('billing.columns.method') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('billing.columns.status') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('billing.columns.issued') }}</th>
                        <th class="text-end px-5 py-3 font-semibold">{{ __('billing.columns.actions') }}</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-ink-100">
                    @foreach ($invoices as $invoice)
                        @php
                            $pillClass = match ($invoice->status) {
                                'Paid' => 'pill-success',
                                'Pending' => 'pill-warning',
                                'Refunded' => 'pill-muted',
                                'Failed' => 'pill-danger',
                                default => 'pill-muted',
                            };
                            $eligibility = $invoice->refund_eligibility;
                        @endphp
                        <tr class="hover:bg-ink-50/40 transition">
                            <td class="px-5 py-4 font-mono text-xs text-ink-800">{{ $invoice->invoice_number }}</td>
                            <td class="px-5 py-4 text-ink-800">{{ __('billing.kind.'.$invoice->kind) }}</td>
                            <td class="px-5 py-4 font-semibold text-ink-950">{{ $invoice->amount_egp }} <span class="text-xs font-normal text-ink-500">EGP</span></td>
                            <td class="px-5 py-4 text-xs text-ink-600">{{ __('subscription.payment_method.'.$invoice->payment_method) }}</td>
                            <td class="px-5 py-4">
                                <span class="{{ $pillClass }}">{{ __('billing.status.'.$invoice->status) }}</span>
                                @if ($invoice->status === 'Paid' && $invoice->kind === 'FirstPeriod')
                                    <div class="text-xs mt-1.5 {{ $eligibility === 'eligible' ? 'text-emerald-700' : 'text-ink-500' }}">
                                        {{ __('billing.refund_eligibility.'.$eligibility) }}
                                    </div>
                                @endif
                            </td>
                            <td class="px-5 py-4 text-ink-600 text-xs">{{ $invoice->created_at?->format('Y-m-d') }}</td>
                            <td class="px-5 py-4 text-end">
                                <div class="flex items-center justify-end gap-2 flex-wrap">
                                    @if ($invoice->status === 'Paid')
                                        <a href="{{ route('portal.billing.pdf', ['invoice' => $invoice->id]) }}"
                                           class="inline-flex items-center gap-1.5 rounded-lg border border-ink-200 bg-white px-3 py-1.5 text-xs font-semibold text-ink-700 hover:border-brand-300 hover:text-brand-700 hover:bg-brand-50 transition">
                                            <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" viewBox="0 0 24 24"
                                                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                                <path d="M12 3v12m-5-5 5 5 5-5M5 21h14" />
                                            </svg>
                                            {{ __('billing.action_download') }}
                                        </a>
                                    @endif
                                    @if ($eligibility === 'eligible')
                                        <form method="POST" action="{{ route('portal.billing.refund', ['invoice' => $invoice->id]) }}"
                                              onsubmit="return confirm('{{ __('billing.refund_confirm', ['number' => $invoice->invoice_number]) }}');">
                                            @csrf
                                            <button type="submit" class="btn-danger !px-3 !py-1.5 !text-xs">
                                                ↩ {{ __('billing.action_refund') }}
                                            </button>
                                        </form>
                                    @endif
                                </div>
                                @if ($invoice->status === 'Pending')
                                    <div class="text-xs text-ink-500 max-w-[280px] mt-1.5 text-end">
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
