@extends('layouts.portal')

@section('title', __('organisation.title'))

@section('content')
    <header class="mb-6">
        <h1 class="text-2xl font-bold mb-1">{{ __('organisation.title') }}</h1>
        <p class="text-stone-600">{{ __('organisation.subtitle') }}</p>
        <p class="text-sm text-stone-500 mt-1">{{ $org->legal_name_ar }}</p>
    </header>

    {{-- Active members table --}}
    <section class="rounded-lg border border-stone-200 bg-white p-6 mb-6">
        <h2 class="text-lg font-bold mb-3">{{ __('organisation.members_list.title') }}</h2>

        @if ($memberships->isEmpty())
            <p class="text-stone-600">{{ __('organisation.members_list.empty') }}</p>
        @else
            <table class="w-full text-sm">
                <thead class="border-b border-stone-200 text-stone-600">
                    <tr>
                        <th class="text-start py-2">{{ __('organisation.members_list.columns.name') }}</th>
                        <th class="text-start py-2">{{ __('organisation.members_list.columns.email') }}</th>
                        <th class="text-start py-2">{{ __('organisation.members_list.columns.role') }}</th>
                        <th class="text-start py-2">{{ __('organisation.members_list.columns.joined_at') }}</th>
                        <th class="text-start py-2">{{ __('organisation.members_list.columns.status') }}</th>
                        <th class="text-end py-2">{{ __('organisation.members_list.columns.actions') }}</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach ($memberships as $m)
                        @php
                            $user = $users[$m->team_member_id] ?? null;
                            $isYou = $user && $user->id === auth()->id();
                            $statusKey = $m->revoked_at ? 'revoked' : ($m->accepted_at ? 'active' : 'pending');
                            $statusClass = match ($statusKey) {
                                'active' => 'bg-green-100 text-green-800',
                                'pending' => 'bg-amber-100 text-amber-800',
                                'revoked' => 'bg-stone-100 text-stone-600',
                            };
                        @endphp
                        <tr class="border-b border-stone-100">
                            <td class="py-3">
                                {{ $user?->display_name ?? '—' }}
                                @if ($isYou) <span class="text-xs text-stone-500">(انت)</span> @endif
                            </td>
                            <td class="py-3 text-stone-600 text-sm">{{ $user?->email ?? '—' }}</td>
                            <td class="py-3">
                                <span class="rounded bg-stone-100 px-2 py-0.5 text-xs">{{ __('organisation.role.'.$m->role) }}</span>
                            </td>
                            <td class="py-3 text-xs text-stone-500">{{ $m->accepted_at?->format('Y-m-d') ?? '—' }}</td>
                            <td class="py-3">
                                <span class="text-xs px-2 py-1 rounded {{ $statusClass }}">
                                    {{ __('organisation.members_list.status.'.$statusKey) }}
                                </span>
                            </td>
                            <td class="py-3 text-end">
                                @if ($statusKey === 'active' && ! $isYou)
                                    <form method="POST" action="{{ route('portal.organisation.memberships.revoke', ['membership' => $m->id]) }}" class="inline-block"
                                          onsubmit="return confirm('{{ __('organisation.members_list.revoke_confirm', ['name' => $user?->display_name ?? $user?->email ?? '']) }}');">
                                        @csrf
                                        <button type="submit" class="rounded border border-red-300 text-red-700 px-3 py-1 text-xs hover:bg-red-50">
                                            {{ __('organisation.members_list.revoke_button') }}
                                        </button>
                                    </form>
                                @endif
                            </td>
                        </tr>
                    @endforeach
                </tbody>
            </table>
        @endif
    </section>

    {{-- Pending invitations --}}
    @if ($pendingInvitations->isNotEmpty())
        <section class="rounded-lg border border-amber-200 bg-amber-50 p-6 mb-6">
            <h2 class="text-lg font-bold mb-3">{{ __('organisation.pending_section_title') }}</h2>
            <table class="w-full text-sm">
                <thead class="border-b border-amber-200 text-stone-700">
                    <tr>
                        <th class="text-start py-2">{{ __('organisation.pending_columns.email') }}</th>
                        <th class="text-start py-2">{{ __('organisation.pending_columns.role') }}</th>
                        <th class="text-start py-2">{{ __('organisation.pending_columns.invited_by') }}</th>
                        <th class="text-start py-2">{{ __('organisation.pending_columns.expires_at') }}</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach ($pendingInvitations as $inv)
                        <tr class="border-b border-amber-100">
                            <td class="py-2">{{ $inv->email }}</td>
                            <td class="py-2">{{ __('organisation.role.'.$inv->role) }}</td>
                            <td class="py-2 text-xs text-stone-600">{{ $inv->invitedBy?->display_name ?? $inv->invitedBy?->email }}</td>
                            <td class="py-2 text-xs text-stone-600">{{ $inv->expires_at?->format('Y-m-d') }}</td>
                        </tr>
                    @endforeach
                </tbody>
            </table>
        </section>
    @endif

    {{-- Invite form --}}
    <section class="rounded-lg border border-stone-200 bg-white p-6">
        <h2 class="text-lg font-bold mb-3">{{ __('organisation.invite_form.title') }}</h2>

        <form method="POST" action="{{ route('portal.organisation.invitations.send') }}" class="space-y-3 max-w-2xl">
            @csrf
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div>
                    <label class="block text-sm font-semibold mb-1">{{ __('organisation.invite_form.email_label') }}</label>
                    <input name="email" type="email" required class="w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600" value="{{ old('email') }}">
                    @error('email')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
                </div>
                <div>
                    <label class="block text-sm font-semibold mb-1">{{ __('organisation.invite_form.role_label') }}</label>
                    <select name="role" required class="w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600">
                        @foreach (['Owner', 'BillingAdmin', 'SupportAdmin', 'ReadOnly'] as $r)
                            <option value="{{ $r }}" {{ old('role') === $r ? 'selected' : '' }}>
                                {{ __('organisation.role.'.$r) }} — {{ __('organisation.role_description.'.$r) }}
                            </option>
                        @endforeach
                    </select>
                </div>
            </div>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div>
                    <label class="block text-sm font-semibold mb-1">{{ __('organisation.invite_form.display_name_label') }}</label>
                    <input name="display_name" type="text" maxlength="128" class="w-full rounded border-stone-300" value="{{ old('display_name') }}">
                </div>
                <div>
                    <label class="block text-sm font-semibold mb-1">{{ __('organisation.invite_form.locale_label') }}</label>
                    <select name="locale_preference" class="w-full rounded border-stone-300">
                        <option value="ar-EG" {{ old('locale_preference', 'ar-EG') === 'ar-EG' ? 'selected' : '' }}>العربية</option>
                        <option value="en-US" {{ old('locale_preference') === 'en-US' ? 'selected' : '' }}>English</option>
                    </select>
                </div>
            </div>
            <button type="submit" class="rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
                {{ __('organisation.invite_form.submit') }}
            </button>
        </form>
    </section>
@endsection
