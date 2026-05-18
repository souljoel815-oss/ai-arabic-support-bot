@extends('layouts.portal')

@section('title', __('licences.transfer.title'))

@section('content')
    <header class="mb-6">
        <a href="{{ route('portal.licences') }}" class="text-sm text-amber-700 hover:underline">
            ← {{ __('licences.title') }}
        </a>
        <h1 class="text-2xl font-bold mt-2">{{ __('licences.transfer.title') }}</h1>
    </header>

    <div class="max-w-2xl rounded-lg border border-stone-200 bg-white p-6">
        <div class="rounded border border-amber-200 bg-amber-50 p-4 mb-4 text-sm text-stone-700">
            {{ __('licences.transfer.instructions') }}
        </div>

        <div class="rounded border border-red-200 bg-red-50 p-4 mb-6 text-sm text-red-800">
            ⚠ {{ __('licences.transfer.warning') }}
        </div>

        <dl class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6 text-sm">
            <div>
                <dt class="text-stone-600 font-semibold">{{ __('licences.transfer.old_hwid_label') }}</dt>
                <dd class="font-mono text-base mt-1">{{ $licence->hwid }}</dd>
            </div>
            <div>
                <dt class="text-stone-600 font-semibold">{{ __('licences.transfer.edition_label') }}</dt>
                <dd class="mt-1">{{ $licence->edition }}</dd>
            </div>
            <div>
                <dt class="text-stone-600 font-semibold">{{ __('licences.transfer.expires_at_label') }}</dt>
                <dd class="mt-1">{{ $licence->expires_at?->format('Y-m-d') }}</dd>
            </div>
        </dl>

        <form method="POST" action="{{ route('portal.licences.transfer.submit', ['licence' => $licence->id]) }}" class="space-y-4">
            @csrf

            <div>
                <label for="new_hwid" class="block text-sm font-semibold mb-1">
                    {{ __('licences.transfer.new_hwid_label') }}
                </label>
                <input id="new_hwid" name="new_hwid" type="text" required
                       value="{{ old('new_hwid') }}"
                       placeholder="{{ __('licences.transfer.new_hwid_placeholder') }}"
                       class="w-full rounded border border-stone-300 px-3 py-2 font-mono text-sm focus:border-amber-600 focus:ring-1 focus:ring-amber-600 uppercase"
                       pattern="[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}"
                       maxlength="19">
                @error('new_hwid')
                    <p class="mt-1 text-sm text-red-600">{{ $message }}</p>
                @enderror
            </div>

            <div class="flex gap-3 pt-2">
                <button type="submit" class="rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
                    {{ __('licences.transfer.submit') }}
                </button>
                <a href="{{ route('portal.licences') }}" class="rounded border border-stone-300 px-5 py-2 hover:bg-stone-100">
                    {{ __('licences.transfer.cancel') }}
                </a>
            </div>
        </form>
    </div>
@endsection
