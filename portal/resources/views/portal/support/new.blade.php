@extends('layouts.portal')

@section('title', __('support.new.title'))

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.support') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            {{ __('support.title') }}
        </a>
        <p class="eyebrow mb-2">طلب جديد</p>
        <h1 class="display-1 mb-1.5">{{ __('support.new.title') }}</h1>
        <p class="text-ink-600">{{ __('support.new.subtitle') }}</p>
    </header>

    <form method="POST" action="{{ route('portal.support.create') }}" enctype="multipart/form-data" class="max-w-3xl space-y-5 card-padded">
        @csrf

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
                <label for="category" class="form-label">{{ __('support.new.category_label') }}</label>
                <select id="category" name="category" required class="form-select">
                    @foreach (\App\Models\SupportTicket::CATEGORIES as $cat)
                        <option value="{{ $cat }}" {{ old('category') === $cat ? 'selected' : '' }}>
                            {{ __('support.category.'.$cat) }}
                        </option>
                    @endforeach
                </select>
                @error('category')<p class="form-error">{{ $message }}</p>@enderror
            </div>

            <div>
                <label for="priority" class="form-label">{{ __('support.new.priority_label') }}</label>
                <select id="priority" name="priority" required class="form-select">
                    <option value="Low" {{ old('priority', 'Normal') === 'Low' ? 'selected' : '' }}>{{ __('support.priority.Low') }}</option>
                    <option value="Normal" {{ old('priority', 'Normal') === 'Normal' ? 'selected' : '' }}>{{ __('support.priority.Normal') }}</option>
                    <option value="High" {{ old('priority') === 'High' ? 'selected' : '' }} {{ ! $highAllowed ? 'disabled' : '' }}>
                        {{ __('support.priority.High') }}
                        @if (! $highAllowed) — {{ __('support.new.high_priority_restricted') }} @endif
                    </option>
                </select>
                @error('priority')<p class="form-error">{{ $message }}</p>@enderror
            </div>
        </div>

        <div>
            <label for="subject" class="form-label">{{ __('support.new.subject_label') }}</label>
            <input id="subject" name="subject" type="text" required maxlength="256"
                   value="{{ old('subject') }}"
                   placeholder="{{ __('support.new.subject_placeholder') }}"
                   class="form-input">
            @error('subject')<p class="form-error">{{ $message }}</p>@enderror
        </div>

        <div>
            <label for="body" class="form-label">{{ __('support.new.body_label') }}</label>
            <textarea id="body" name="body" rows="7" required maxlength="8000"
                      placeholder="{{ __('support.new.body_placeholder') }}"
                      class="form-textarea">{{ old('body') }}</textarea>
            @error('body')<p class="form-error">{{ $message }}</p>@enderror
        </div>

        <div>
            <label for="attachments" class="form-label">{{ __('support.new.attachments_label') }}</label>
            <label for="attachments" class="block cursor-pointer rounded-xl border-2 border-dashed border-ink-200 hover:border-brand-400 bg-ink-50/50 hover:bg-brand-50/40 transition px-6 py-8 text-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-9 w-9 mx-auto text-ink-400 mb-2" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21.44 11.05 12.25 20.24a6 6 0 0 1-8.49-8.49L13 2.51a4 4 0 0 1 5.66 5.66L9.41 17.41a2 2 0 0 1-2.83-2.83l8.49-8.48" />
                </svg>
                <div class="text-sm font-semibold text-ink-700">انقر للاختيار أو اسحب الملفات هنا</div>
                <div class="form-hint">{{ __('support.new.attachments_hint') }}</div>
            </label>
            <input id="attachments" name="attachments[]" type="file" multiple
                   accept=".png,.jpg,.jpeg,.gif,.webp,.pdf,.txt,.csv,.xls,.xlsx,.doc,.docx"
                   class="sr-only">
            @error('attachments')<p class="form-error">{{ $message }}</p>@enderror
            @error('attachments.*')<p class="form-error">{{ $message }}</p>@enderror
        </div>

        <div class="flex gap-3 pt-4 border-t border-ink-100">
            <button type="submit" class="btn-primary !py-3 !px-6">
                {{ __('support.new.submit') }}
            </button>
            <a href="{{ route('portal.support') }}" class="btn-secondary">
                {{ __('support.new.cancel') }}
            </a>
        </div>
    </form>
@endsection
