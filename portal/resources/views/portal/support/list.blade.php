@extends('layouts.portal')

@section('title', __('support.title'))

@section('content')
    <header class="mb-8 flex flex-wrap items-end justify-between gap-3">
        <div>
            <p class="eyebrow mb-2">طلبات الدعم</p>
            <h1 class="display-1 mb-1.5">{{ __('support.title') }}</h1>
            <p class="text-ink-600">{{ __('support.subtitle') }}</p>
        </div>
        <a href="{{ route('portal.support.new') }}" class="btn-primary">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M12 5v14M5 12h14" />
            </svg>
            {{ __('support.list.new_button') }}
        </a>
    </header>

    @if ($tickets->isEmpty())
        <div class="card-padded text-center py-16">
            <div class="mx-auto h-16 w-16 rounded-2xl flex items-center justify-center mb-4 bg-gradient-to-br from-sky-100 to-sky-200 text-sky-700">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z" />
                </svg>
            </div>
            <h2 class="display-2 text-2xl mb-2">لا توجد طلبات بعد</h2>
            <p class="text-ink-600 max-w-md mx-auto mb-6">{{ __('support.list.empty') }}</p>
            <a href="{{ route('portal.support.new') }}" class="btn-primary">{{ __('support.list.new_button') }}</a>
        </div>
    @else
        <div class="card overflow-hidden shadow-elevation-2">
            <table class="w-full text-sm">
                <thead class="bg-ink-50/70 border-b border-ink-100 text-ink-600">
                    <tr class="text-xs uppercase tracking-wider">
                        <th class="text-start px-5 py-3 font-semibold">{{ __('support.list.columns.subject') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('support.list.columns.category') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('support.list.columns.priority') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('support.list.columns.status') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('support.list.columns.opened_at') }}</th>
                        <th class="text-start px-5 py-3 font-semibold">{{ __('support.list.columns.sla_deadline') }}</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-ink-100">
                    @foreach ($tickets as $ticket)
                        @php
                            $statusPill = match ($ticket->status) {
                                'Open' => 'pill-warning',
                                'InProgress' => 'pill-info',
                                'Resolved' => 'pill-success',
                                'Closed' => 'pill-muted',
                                default => 'pill-muted',
                            };
                            $priorityPill = match ($ticket->priority) {
                                'High' => 'pill-danger',
                                'Medium' => 'pill-warning',
                                default => 'pill-muted',
                            };
                            $deadline = $slaDeadlines[$ticket->id] ?? null;
                            $missed = $deadline && $ticket->first_reply_at === null && $deadline->getTimestamp() < time();
                        @endphp
                        <tr class="hover:bg-ink-50/40 transition">
                            <td class="px-5 py-4">
                                <a href="{{ route('portal.support.detail', ['ticket' => $ticket->id]) }}" class="text-ink-900 hover:text-brand-700 font-semibold transition">
                                    {{ $ticket->subject }}
                                </a>
                            </td>
                            <td class="px-5 py-4 text-ink-700 text-xs">{{ __('support.category.'.$ticket->category) }}</td>
                            <td class="px-5 py-4"><span class="{{ $priorityPill }}">{{ __('support.priority.'.$ticket->priority) }}</span></td>
                            <td class="px-5 py-4"><span class="{{ $statusPill }}">{{ __('support.status.'.$ticket->status) }}</span></td>
                            <td class="px-5 py-4 text-xs text-ink-600 font-mono">{{ $ticket->opened_at?->format('Y-m-d H:i') }}</td>
                            <td class="px-5 py-4 text-xs {{ $missed ? 'text-red-700 font-bold' : 'text-ink-600 font-mono' }}">
                                @if ($deadline)
                                    {{ $deadline->format('Y-m-d H:i') }}
                                    @if ($missed) <span class="ms-1">⚠</span> @endif
                                @endif
                            </td>
                        </tr>
                    @endforeach
                </tbody>
            </table>
        </div>
    @endif
@endsection
