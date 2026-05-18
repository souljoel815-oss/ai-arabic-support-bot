@extends('layouts.portal')

@section('title', __('support.detail.subject_prefix').': '.$ticket->subject)

@section('content')
    <header class="mb-6">
        <a href="{{ route('portal.support') }}" class="text-sm text-amber-700 hover:underline">← {{ __('support.title') }}</a>
        <h1 class="text-2xl font-bold mt-2">{{ $ticket->subject }}</h1>
        <p class="text-stone-600 mt-1 text-sm">
            {{ __('support.detail.opened_by') }}: <strong>{{ $ticket->openedBy?->display_name ?? $ticket->openedBy?->email }}</strong>
            ·
            {{ __('support.detail.opened_at') }}: {{ $ticket->opened_at?->format('Y-m-d H:i') }}
            ·
            {{ __('support.category.'.$ticket->category) }}
            ·
            <span class="px-2 py-0.5 rounded text-xs {{ $ticket->status === 'Open' ? 'bg-amber-100 text-amber-800' : ($ticket->status === 'InProgress' ? 'bg-blue-100 text-blue-800' : 'bg-green-100 text-green-800') }}">
                {{ __('support.status.'.$ticket->status) }}
            </span>
        </p>
        <p class="text-xs text-stone-500 mt-1">
            {{ __('support.detail.sla_deadline') }}: {{ $deadline->format('Y-m-d H:i') }}
            ({{ in_array($tier, \App\Models\Subscription::PRIORITY_SUPPORT_TIERS, true) ? '4h' : '24h' }})
            @if ($ticket->first_reply_at)
                · {{ __('support.detail.first_reply_at') }} {{ $ticket->first_reply_at->format('Y-m-d H:i') }} ✓
            @endif
        </p>
    </header>

    <div class="rounded-lg border border-stone-200 bg-white p-6 mb-4">
        <div class="prose max-w-none whitespace-pre-wrap text-sm">{{ $ticket->body }}</div>

        @if ($ticket->attachments->isNotEmpty())
            <div class="mt-4 pt-4 border-t border-stone-200">
                <h3 class="text-sm font-semibold mb-2">{{ __('support.detail.attachments_title') }}</h3>
                <ul class="space-y-1 text-sm">
                    @foreach ($ticket->attachments as $att)
                        <li>
                            <a href="{{ route('portal.support.attachment', ['attachment' => $att->id]) }}" class="text-amber-700 hover:underline">
                                📎 {{ $att->original_filename }}
                            </a>
                            <span class="text-xs text-stone-500">({{ number_format($att->size_bytes / 1024, 1) }} KB)</span>
                        </li>
                    @endforeach
                </ul>
            </div>
        @endif
    </div>

    @if ($ticket->replies->isNotEmpty())
        <h2 class="text-lg font-bold mb-3">{{ __('support.detail.thread_title') }}</h2>
        <div class="space-y-3 mb-6">
            @foreach ($ticket->replies as $reply)
                <div class="rounded border p-4 {{ $reply->author_kind === 'VendorStaff' ? 'border-amber-300 bg-amber-50' : 'border-stone-200 bg-white' }}">
                    <div class="flex items-center justify-between text-xs text-stone-600 mb-2">
                        <span class="font-semibold">
                            {{ $reply->author_kind === 'Customer' ? __('support.detail.reply_from_customer') : __('support.detail.reply_from_vendor') }}
                            @if ($reply->authorTeamMember)
                                — {{ $reply->authorTeamMember->display_name ?? $reply->authorTeamMember->email }}
                            @endif
                        </span>
                        <span>{{ $reply->created_at?->format('Y-m-d H:i') }}</span>
                    </div>
                    <div class="text-sm whitespace-pre-wrap">{{ $reply->body }}</div>
                </div>
            @endforeach
        </div>
    @endif

    @if ($ticket->status !== \App\Models\SupportTicket::STATUS_CLOSED)
        <form method="POST" action="{{ route('portal.support.reply', ['ticket' => $ticket->id]) }}" class="rounded-lg border border-stone-200 bg-white p-6">
            @csrf
            <label for="body" class="block text-sm font-semibold mb-2">{{ __('support.detail.reply_label') }}</label>
            <textarea id="body" name="body" rows="4" required maxlength="8000"
                      placeholder="{{ __('support.detail.reply_placeholder') }}"
                      class="w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600">{{ old('body') }}</textarea>
            @error('body')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
            <button type="submit" class="mt-3 rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
                {{ __('support.detail.reply_submit') }}
            </button>
        </form>
    @endif
@endsection
