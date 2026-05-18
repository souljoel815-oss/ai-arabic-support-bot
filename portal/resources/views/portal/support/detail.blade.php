@extends('layouts.portal')

@section('title', __('support.detail.subject_prefix').': '.$ticket->subject)

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.support') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            {{ __('support.title') }}
        </a>
        <p class="eyebrow mb-2">{{ __('support.category.'.$ticket->category) }}</p>
        <h1 class="display-1 mb-3">{{ $ticket->subject }}</h1>

        <div class="flex items-center gap-2 flex-wrap mb-3">
            @php
                $statusPill = match ($ticket->status) {
                    'Open' => 'pill-warning',
                    'InProgress' => 'pill-info',
                    'Resolved' => 'pill-success',
                    'Closed' => 'pill-muted',
                    default => 'pill-muted',
                };
            @endphp
            <span class="{{ $statusPill }}">{{ __('support.status.'.$ticket->status) }}</span>
            <span class="text-ink-300">·</span>
            <span class="text-sm text-ink-600">{{ __('support.detail.opened_by') }}:</span>
            <strong class="text-sm text-ink-800">{{ $ticket->openedBy?->display_name ?? $ticket->openedBy?->email }}</strong>
            <span class="text-ink-300">·</span>
            <span class="text-sm text-ink-600 font-mono">{{ $ticket->opened_at?->format('Y-m-d H:i') }}</span>
        </div>

        <div class="rounded-xl border border-ink-100 bg-ink-50/40 px-4 py-2.5 text-xs text-ink-600 inline-flex items-center gap-2">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-brand-600" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="10" /><path d="M12 6v6l4 2" />
            </svg>
            <span>
                {{ __('support.detail.sla_deadline') }}:
                <span class="font-mono text-ink-800">{{ $deadline->format('Y-m-d H:i') }}</span>
                ({{ in_array($tier, \App\Models\Subscription::PRIORITY_SUPPORT_TIERS, true) ? '4h ⚡' : '24h' }})
            </span>
            @if ($ticket->first_reply_at)
                <span class="text-ink-300">·</span>
                <span class="text-emerald-700 font-semibold">✓ {{ __('support.detail.first_reply_at') }} {{ $ticket->first_reply_at->format('Y-m-d H:i') }}</span>
            @endif
        </div>
    </header>

    {{-- Original ticket --}}
    <article class="card-padded mb-5 relative">
        <span class="absolute inset-y-0 start-0 w-1 bg-brand-500 rounded-s-2xl"></span>
        <div class="flex items-center gap-2 mb-4 text-xs text-ink-500 uppercase tracking-wider font-semibold">
            <span class="flex h-7 w-7 items-center justify-center rounded-full bg-brand-100 text-brand-800 font-bold normal-case tracking-normal">
                {{ mb_strtoupper(mb_substr($ticket->openedBy?->display_name ?? $ticket->openedBy?->email ?? '?', 0, 1)) }}
            </span>
            <span>الطلب الأصلي</span>
        </div>
        <div class="prose max-w-none whitespace-pre-wrap text-[15px] leading-relaxed text-ink-800">{{ $ticket->body }}</div>

        @if ($ticket->attachments->isNotEmpty())
            <div class="mt-5 pt-4 border-t border-ink-100">
                <h3 class="eyebrow-muted mb-3">{{ __('support.detail.attachments_title') }}</h3>
                <ul class="space-y-2">
                    @foreach ($ticket->attachments as $att)
                        <li>
                            <a href="{{ route('portal.support.attachment', ['attachment' => $att->id]) }}"
                               class="inline-flex items-center gap-2 rounded-lg border border-ink-200 bg-white px-3 py-2 text-sm text-ink-700 hover:border-brand-300 hover:bg-brand-50 hover:text-brand-800 transition">
                                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                                     fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M21.44 11.05 12.25 20.24a6 6 0 0 1-8.49-8.49L13 2.51a4 4 0 0 1 5.66 5.66L9.41 17.41a2 2 0 0 1-2.83-2.83l8.49-8.48" />
                                </svg>
                                {{ $att->original_filename }}
                                <span class="text-xs text-ink-500">{{ number_format($att->size_bytes / 1024, 1) }} KB</span>
                            </a>
                        </li>
                    @endforeach
                </ul>
            </div>
        @endif
    </article>

    {{-- Reply thread --}}
    @if ($ticket->replies->isNotEmpty())
        <h2 class="eyebrow mb-4">{{ __('support.detail.thread_title') }} ({{ $ticket->replies->count() }})</h2>
        <div class="space-y-4 mb-6">
            @foreach ($ticket->replies as $reply)
                @php $isVendor = $reply->author_kind === 'VendorStaff'; @endphp
                <article class="relative card-padded {{ $isVendor ? 'bg-brand-50/60 border-brand-200' : '' }}">
                    <span class="absolute inset-y-0 start-0 w-1 rounded-s-2xl {{ $isVendor ? 'bg-brand-500' : 'bg-ink-300' }}"></span>
                    <div class="flex items-center justify-between mb-3">
                        <div class="flex items-center gap-2">
                            <span class="flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold {{ $isVendor ? 'bg-brand-600 text-white' : 'bg-ink-200 text-ink-700' }}">
                                {{ $isVendor ? 'DX' : mb_strtoupper(mb_substr($reply->authorTeamMember?->display_name ?? $reply->authorTeamMember?->email ?? '?', 0, 1)) }}
                            </span>
                            <span class="text-sm font-semibold text-ink-900">
                                {{ $isVendor ? 'فريق DaftarX' : ($reply->authorTeamMember?->display_name ?? __('support.detail.reply_from_customer')) }}
                            </span>
                            @if ($isVendor)
                                <span class="pill-brand">⚡ فريق الدعم</span>
                            @endif
                        </div>
                        <span class="text-xs text-ink-500 font-mono">{{ $reply->created_at?->format('Y-m-d H:i') }}</span>
                    </div>
                    <div class="text-[15px] whitespace-pre-wrap leading-relaxed text-ink-800">{{ $reply->body }}</div>
                </article>
            @endforeach
        </div>
    @endif

    {{-- Reply form --}}
    @if ($ticket->status !== \App\Models\SupportTicket::STATUS_CLOSED)
        <form method="POST" action="{{ route('portal.support.reply', ['ticket' => $ticket->id]) }}" class="card-padded">
            @csrf
            <label for="body" class="form-label">{{ __('support.detail.reply_label') }}</label>
            <textarea id="body" name="body" rows="5" required maxlength="8000"
                      placeholder="{{ __('support.detail.reply_placeholder') }}"
                      class="form-textarea">{{ old('body') }}</textarea>
            @error('body')<p class="form-error">{{ $message }}</p>@enderror
            <button type="submit" class="btn-primary mt-3 !py-2.5 !px-5">
                {{ __('support.detail.reply_submit') }}
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M22 2 11 13M22 2l-7 20-4-9-9-4z" />
                </svg>
            </button>
        </form>
    @endif
@endsection
