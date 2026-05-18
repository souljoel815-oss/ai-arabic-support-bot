@extends('layouts.marketing')

@section('title', __('organisation.accept.title'))

@section('content')
    <div class="max-w-md mx-auto rounded-lg border border-stone-200 bg-white p-8">
        <h1 class="text-2xl font-bold mb-2">{{ __('organisation.accept.title') }}</h1>
        <p class="text-stone-600 mb-6 text-sm">
            {{ __('organisation.accept.subtitle', [
                'org' => $invitation->customerOrganisation?->legal_name_ar ?? '',
                'role' => __('organisation.role.'.$invitation->role),
            ]) }}
        </p>

        @if ($alreadyTeamMember)
            <div class="rounded border border-stone-200 bg-stone-50 p-3 text-sm text-stone-700 mb-4">
                {{ __('organisation.accept.existing_account_note') }}
            </div>
        @endif

        <form method="POST" action="{{ route('invitations.accept.submit') }}" class="space-y-4">
            @csrf
            <input type="hidden" name="token" value="{{ $token }}">

            @if (! $alreadyTeamMember)
                <div>
                    <label class="block text-sm font-semibold mb-1">{{ __('organisation.accept.display_name_label') }}</label>
                    <input name="display_name" type="text" required class="w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600"
                           value="{{ old('display_name', $invitation->display_name) }}">
                    @error('display_name')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
                </div>
                <div>
                    <label class="block text-sm font-semibold mb-1">{{ __('organisation.accept.set_password_label') }}</label>
                    <input name="password" type="password" required class="w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600">
                    @error('password')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
                </div>
            @endif

            @error('token')
                <div class="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-700">{{ $message }}</div>
            @enderror

            <button type="submit" class="w-full rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
                @if ($alreadyTeamMember)
                    {{ __('organisation.accept.submit_existing') }}
                @else
                    {{ __('organisation.accept.submit_new') }}
                @endif
            </button>
        </form>
    </div>
@endsection
