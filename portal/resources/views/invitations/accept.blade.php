@extends('layouts.marketing')

@section('title', __('organisation.accept.title'))

@section('content')
    <div class="max-w-md mx-auto">
        <div class="card-padded shadow-elevation-3 animate-fade-in-up">
            <div class="text-center mb-6">
                <div class="mx-auto h-14 w-14 rounded-2xl flex items-center justify-center shadow-brand-glow mb-3"
                     style="background: linear-gradient(135deg, #d68a1f 0%, #b06d18 100%);">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-7 w-7 text-white" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M19 8v6M22 11h-6M12.5 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0z" />
                    </svg>
                </div>
                <p class="eyebrow mb-2">دعوة فريق</p>
                <h1 class="display-2 mb-2">{{ __('organisation.accept.title') }}</h1>
                <p class="text-ink-600 text-sm leading-relaxed">
                    {{ __('organisation.accept.subtitle', [
                        'org' => $invitation->customerOrganisation?->legal_name_ar ?? '',
                        'role' => __('organisation.role.'.$invitation->role),
                    ]) }}
                </p>
            </div>

            @if ($alreadyTeamMember)
                <div class="rounded-xl border border-sky-200 bg-sky-50 p-4 text-sm text-sky-900 mb-5 flex items-start gap-3">
                    <span class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-sky-200/70 text-sky-700">ℹ</span>
                    <span>{{ __('organisation.accept.existing_account_note') }}</span>
                </div>
            @endif

            <form method="POST" action="{{ route('invitations.accept.submit') }}" class="space-y-4">
                @csrf
                <input type="hidden" name="token" value="{{ $token }}">

                @if (! $alreadyTeamMember)
                    <div>
                        <label class="form-label">{{ __('organisation.accept.display_name_label') }}</label>
                        <input name="display_name" type="text" required class="form-input"
                               value="{{ old('display_name', $invitation->display_name) }}">
                        @error('display_name')<p class="form-error">{{ $message }}</p>@enderror
                    </div>
                    <div>
                        <label class="form-label">{{ __('organisation.accept.set_password_label') }}</label>
                        <input name="password" type="password" required class="form-input" placeholder="••••••••••••">
                        @error('password')<p class="form-error">{{ $message }}</p>@enderror
                    </div>
                @endif

                @error('token')
                    <div class="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-800">{{ $message }}</div>
                @enderror

                <button type="submit" class="btn-primary w-full !py-3">
                    @if ($alreadyTeamMember)
                        {{ __('organisation.accept.submit_existing') }}
                    @else
                        {{ __('organisation.accept.submit_new') }}
                    @endif
                </button>
            </form>
        </div>
    </div>
@endsection
