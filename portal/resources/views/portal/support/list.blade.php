@extends('layouts.portal')

@section('title', __('support.title'))

@section('content')
    <header class="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
            <h1 class="text-2xl font-bold mb-1">{{ __('support.title') }}</h1>
            <p class="text-stone-600">{{ __('support.subtitle') }}</p>
        </div>
        <a href="{{ route('portal.support.new') }}" class="rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
            {{ __('support.list.new_button') }}
        </a>
    </header>

    @if ($tickets->isEmpty())
        <div class="rounded border border-stone-200 bg-white p-8 text-center text-stone-600">
            {{ __('support.list.empty') }}
        </div>
    @else
        <div class="rounded-lg border border-stone-200 bg-white overflow-hidden">
            <table class="w-full text-sm">
                <thead class="bg-stone-50 border-b border-stone-200 text-stone-700">
                    <tr>
                        <th class="text-start px-4 py-3">{{ __('support.list.columns.subject') }}</th>
                        <th class="text-start px-4 py-3">{{ __('support.list.columns.category') }}</th>
                        <th class="text-start px-4 py-3">{{ __('support.list.columns.priority') }}</th>
                        <th class="text-start px-4 py-3">{{ __('support.list.columns.status') }}</th>
                        <th class="text-start px-4 py-3">{{ __('support.list.columns.opened_at') }}</th>
                        <th class="text-start px-4 py-3">{{ __('support.list.columns.sla_deadline') }}</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach ($tickets as $ticket)
                        @php
                            $statusClass = match ($ticket->status) {
                                'Open' => 'bg-amber-100 text-amber-800',
                                'InProgress' => 'bg-blue-100 text-blue-800',
                                'Resolved' => 'bg-green-100 text-green-800',
                                'Closed' => 'bg-stone-100 text-stone-700',
                                default => 'bg-stone-100',
                            };
                            $deadline = $slaDeadlines[$ticket->id] ?? null;
                            $missed = $deadline && $ticket->first_reply_at === null && $deadline->getTimestamp() < time();
                        @endphp
                        <tr class="border-b border-stone-100 hover:bg-stone-50">
                            <td class="px-4 py-3">
                                <a href="{{ route('portal.support.detail', ['ticket' => $ticket->id]) }}" class="text-amber-700 hover:underline font-medium">
                                    {{ $ticket->subject }}
                                </a>
                            </td>
                            <td class="px-4 py-3 text-stone-600">{{ __('support.category.'.$ticket->category) }}</td>
                            <td class="px-4 py-3">{{ __('support.priority.'.$ticket->priority) }}</td>
                            <td class="px-4 py-3">
                                <span class="text-xs px-2 py-1 rounded {{ $statusClass }}">
                                    {{ __('support.status.'.$ticket->status) }}
                                </span>
                            </td>
                            <td class="px-4 py-3 text-xs text-stone-600">{{ $ticket->opened_at?->format('Y-m-d H:i') }}</td>
                            <td class="px-4 py-3 text-xs {{ $missed ? 'text-red-700 font-bold' : 'text-stone-600' }}">
                                @if ($deadline)
                                    {{ $deadline->format('Y-m-d H:i') }}
                                    @if ($missed) ⚠ @endif
                                @endif
                            </td>
                        </tr>
                    @endforeach
                </tbody>
            </table>
        </div>
    @endif
@endsection
