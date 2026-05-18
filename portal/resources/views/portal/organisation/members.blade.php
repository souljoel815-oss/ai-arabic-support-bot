@extends('layouts.portal')

@section('title', __('organisation.title'))

@section('content')
    <header class="mb-8 flex flex-wrap items-end justify-between gap-3">
        <div>
            <p class="eyebrow mb-2">إدارة الفريق</p>
            <h1 class="display-1 mb-1.5">{{ __('organisation.title') }}</h1>
            <p class="text-ink-600">{{ __('organisation.subtitle') }}</p>
            <p class="text-sm text-ink-500 mt-1">{{ $org->legal_name_ar }}</p>
        </div>
        <a href="{{ route('portal.organisation.audit-log') }}" class="btn-secondary !text-sm">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M16 13H8M16 17H8M10 9H8" />
            </svg>
            سجل التدقيق
        </a>
    </header>

    {{-- Active members table --}}
    <section class="card-padded mb-6">
        <div class="flex items-center justify-between mb-5">
            <h2 class="card-section-title !mb-0">{{ __('organisation.members_list.title') }}</h2>
            <span class="pill-muted">{{ $memberships->count() }} عضو</span>
        </div>

        @if ($memberships->isEmpty())
            <div class="text-center py-10 text-ink-500">{{ __('organisation.members_list.empty') }}</div>
        @else
            <div class="overflow-x-auto rounded-xl border border-ink-100">
                <table class="w-full text-sm">
                    <thead class="bg-ink-50/70 text-ink-600">
                        <tr class="text-xs uppercase tracking-wider">
                            <th class="text-start px-4 py-3 font-semibold">{{ __('organisation.members_list.columns.name') }}</th>
                            <th class="text-start px-4 py-3 font-semibold">{{ __('organisation.members_list.columns.email') }}</th>
                            <th class="text-start px-4 py-3 font-semibold">{{ __('organisation.members_list.columns.role') }}</th>
                            <th class="text-start px-4 py-3 font-semibold">{{ __('organisation.members_list.columns.joined_at') }}</th>
                            <th class="text-start px-4 py-3 font-semibold">{{ __('organisation.members_list.columns.status') }}</th>
                            <th class="text-end px-4 py-3 font-semibold">{{ __('organisation.members_list.columns.actions') }}</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-ink-100">
                        @foreach ($memberships as $m)
                            @php
                                $user = $users[$m->team_member_id] ?? null;
                                $isYou = $user && $user->id === auth()->id();
                                $statusKey = $m->revoked_at ? 'revoked' : ($m->accepted_at ? 'active' : 'pending');
                                $statusPill = match ($statusKey) {
                                    'active' => 'pill-success',
                                    'pending' => 'pill-warning',
                                    'revoked' => 'pill-muted',
                                };
                                $initial = mb_strtoupper(mb_substr($user?->display_name ?? $user?->email ?? '?', 0, 1));
                            @endphp
                            <tr class="hover:bg-ink-50/40 transition">
                                <td class="px-4 py-3">
                                    <div class="flex items-center gap-3">
                                        <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-100 text-brand-800 font-bold">{{ $initial }}</span>
                                        <div class="min-w-0">
                                            <div class="font-semibold text-ink-900">
                                                {{ $user?->display_name ?? '—' }}
                                                @if ($isYou) <span class="text-xs text-brand-700 font-normal">(انت)</span> @endif
                                            </div>
                                        </div>
                                    </div>
                                </td>
                                <td class="px-4 py-3 text-ink-600 text-sm">{{ $user?->email ?? '—' }}</td>
                                <td class="px-4 py-3">
                                    <span class="pill-brand">{{ __('organisation.role.'.$m->role) }}</span>
                                </td>
                                <td class="px-4 py-3 text-xs text-ink-500 font-mono">{{ $m->accepted_at?->format('Y-m-d') ?? '—' }}</td>
                                <td class="px-4 py-3">
                                    <span class="{{ $statusPill }}">{{ __('organisation.members_list.status.'.$statusKey) }}</span>
                                </td>
                                <td class="px-4 py-3 text-end">
                                    @if ($statusKey === 'active' && ! $isYou)
                                        <form method="POST" action="{{ route('portal.organisation.memberships.revoke', ['membership' => $m->id]) }}" class="inline-block"
                                              onsubmit="return confirm('{{ __('organisation.members_list.revoke_confirm', ['name' => $user?->display_name ?? $user?->email ?? '']) }}');">
                                            @csrf
                                            <button type="submit" class="btn-danger !py-1.5 !px-3 !text-xs">
                                                {{ __('organisation.members_list.revoke_button') }}
                                            </button>
                                        </form>
                                    @endif
                                </td>
                            </tr>
                        @endforeach
                    </tbody>
                </table>
            </div>
        @endif
    </section>

    {{-- Pending invitations --}}
    @if ($pendingInvitations->isNotEmpty())
        <section class="rounded-2xl border border-amber-200/70 bg-amber-50/50 p-6 mb-6 shadow-elevation-1">
            <div class="flex items-center gap-3 mb-4">
                <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-amber-200/70 text-amber-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <circle cx="12" cy="12" r="10" /><path d="M12 6v6l4 2" />
                    </svg>
                </span>
                <h2 class="text-lg font-bold text-ink-950">{{ __('organisation.pending_section_title') }}</h2>
                <span class="pill-warning">{{ $pendingInvitations->count() }} في الانتظار</span>
            </div>
            <div class="overflow-x-auto rounded-xl border border-amber-200/70 bg-white/60">
                <table class="w-full text-sm">
                    <thead class="bg-amber-100/50 text-ink-700">
                        <tr class="text-xs uppercase tracking-wider">
                            <th class="text-start px-4 py-2.5 font-semibold">{{ __('organisation.pending_columns.email') }}</th>
                            <th class="text-start px-4 py-2.5 font-semibold">{{ __('organisation.pending_columns.role') }}</th>
                            <th class="text-start px-4 py-2.5 font-semibold">{{ __('organisation.pending_columns.invited_by') }}</th>
                            <th class="text-start px-4 py-2.5 font-semibold">{{ __('organisation.pending_columns.expires_at') }}</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-amber-100">
                        @foreach ($pendingInvitations as $inv)
                            <tr>
                                <td class="px-4 py-2.5 text-ink-800">{{ $inv->email }}</td>
                                <td class="px-4 py-2.5"><span class="pill-brand">{{ __('organisation.role.'.$inv->role) }}</span></td>
                                <td class="px-4 py-2.5 text-xs text-ink-600">{{ $inv->invitedBy?->display_name ?? $inv->invitedBy?->email }}</td>
                                <td class="px-4 py-2.5 text-xs text-ink-600 font-mono">{{ $inv->expires_at?->format('Y-m-d') }}</td>
                            </tr>
                        @endforeach
                    </tbody>
                </table>
            </div>
        </section>
    @endif

    {{-- Invite form --}}
    <section class="card-padded">
        <div class="flex items-center gap-3 mb-5">
            <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-brand-100 text-brand-700">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM19 8v6M22 11h-6" />
                </svg>
            </span>
            <h2 class="text-lg font-bold text-ink-950">{{ __('organisation.invite_form.title') }}</h2>
        </div>

        <form method="POST" action="{{ route('portal.organisation.invitations.send') }}" class="space-y-4 max-w-2xl">
            @csrf
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                    <label class="form-label">{{ __('organisation.invite_form.email_label') }}</label>
                    <input name="email" type="email" required class="form-input" value="{{ old('email') }}" placeholder="member@company.com">
                    @error('email')<p class="form-error">{{ $message }}</p>@enderror
                </div>
                <div>
                    <label class="form-label">{{ __('organisation.invite_form.role_label') }}</label>
                    <select name="role" required class="form-select">
                        @foreach (['Owner', 'BillingAdmin', 'SupportAdmin', 'ReadOnly'] as $r)
                            <option value="{{ $r }}" {{ old('role') === $r ? 'selected' : '' }}>
                                {{ __('organisation.role.'.$r) }} — {{ __('organisation.role_description.'.$r) }}
                            </option>
                        @endforeach
                    </select>
                </div>
            </div>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                    <label class="form-label">{{ __('organisation.invite_form.display_name_label') }}</label>
                    <input name="display_name" type="text" maxlength="128" class="form-input" value="{{ old('display_name') }}" placeholder="اختياري">
                </div>
                <div>
                    <label class="form-label">{{ __('organisation.invite_form.locale_label') }}</label>
                    <select name="locale_preference" class="form-select">
                        <option value="ar-EG" {{ old('locale_preference', 'ar-EG') === 'ar-EG' ? 'selected' : '' }}>العربية</option>
                        <option value="en-US" {{ old('locale_preference') === 'en-US' ? 'selected' : '' }}>English</option>
                    </select>
                </div>
            </div>
            <button type="submit" class="btn-primary !py-3 !px-6">
                {{ __('organisation.invite_form.submit') }}
            </button>
        </form>
    </section>
@endsection
