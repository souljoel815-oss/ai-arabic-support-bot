@extends('layouts.portal')

@section('title', __('support.new.title'))

@section('content')
    <header class="mb-6">
        <a href="{{ route('portal.support') }}" class="text-sm text-amber-700 hover:underline">← {{ __('support.title') }}</a>
        <h1 class="text-2xl font-bold mt-2">{{ __('support.new.title') }}</h1>
        <p class="text-stone-600 mt-1">{{ __('support.new.subtitle') }}</p>
    </header>

    <form method="POST" action="{{ route('portal.support.create') }}" enctype="multipart/form-data" class="max-w-2xl space-y-4 rounded-lg border border-stone-200 bg-white p-6">
        @csrf

        <div>
            <label for="category" class="block text-sm font-semibold mb-1">{{ __('support.new.category_label') }}</label>
            <select id="category" name="category" required class="w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600">
                @foreach (\App\Models\SupportTicket::CATEGORIES as $cat)
                    <option value="{{ $cat }}" {{ old('category') === $cat ? 'selected' : '' }}>
                        {{ __('support.category.'.$cat) }}
                    </option>
                @endforeach
            </select>
            @error('category')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>

        <div>
            <label for="priority" class="block text-sm font-semibold mb-1">{{ __('support.new.priority_label') }}</label>
            <select id="priority" name="priority" required class="w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600">
                <option value="Low" {{ old('priority', 'Normal') === 'Low' ? 'selected' : '' }}>{{ __('support.priority.Low') }}</option>
                <option value="Normal" {{ old('priority', 'Normal') === 'Normal' ? 'selected' : '' }}>{{ __('support.priority.Normal') }}</option>
                <option value="High" {{ old('priority') === 'High' ? 'selected' : '' }} {{ ! $highAllowed ? 'disabled' : '' }}>
                    {{ __('support.priority.High') }}
                    @if (! $highAllowed) — {{ __('support.new.high_priority_restricted') }} @endif
                </option>
            </select>
            @error('priority')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>

        <div>
            <label for="subject" class="block text-sm font-semibold mb-1">{{ __('support.new.subject_label') }}</label>
            <input id="subject" name="subject" type="text" required maxlength="256"
                   value="{{ old('subject') }}"
                   placeholder="{{ __('support.new.subject_placeholder') }}"
                   class="w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600">
            @error('subject')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>

        <div>
            <label for="body" class="block text-sm font-semibold mb-1">{{ __('support.new.body_label') }}</label>
            <textarea id="body" name="body" rows="6" required maxlength="8000"
                      placeholder="{{ __('support.new.body_placeholder') }}"
                      class="w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600">{{ old('body') }}</textarea>
            @error('body')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>

        <div>
            <label for="attachments" class="block text-sm font-semibold mb-1">{{ __('support.new.attachments_label') }}</label>
            <input id="attachments" name="attachments[]" type="file" multiple
                   accept=".png,.jpg,.jpeg,.gif,.webp,.pdf,.txt,.csv,.xls,.xlsx,.doc,.docx"
                   class="w-full rounded border-stone-300 file:rounded file:border-0 file:bg-amber-600 file:text-white file:px-3 file:py-1 file:me-3">
            <p class="mt-1 text-xs text-stone-500">{{ __('support.new.attachments_hint') }}</p>
            @error('attachments')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
            @error('attachments.*')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>

        <div class="flex gap-3 pt-2 border-t border-stone-200">
            <button type="submit" class="rounded bg-amber-600 px-5 py-2 text-white font-semibold hover:bg-amber-700">
                {{ __('support.new.submit') }}
            </button>
            <a href="{{ route('portal.support') }}" class="rounded border border-stone-300 px-5 py-2 hover:bg-stone-100">
                {{ __('support.new.cancel') }}
            </a>
        </div>
    </form>
@endsection
