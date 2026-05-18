@extends('layouts.portal')

@section('title', __('licences.transfer.title'))

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.licences') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            {{ __('licences.title') }}
        </a>
        <p class="eyebrow mb-2">نقل ترخيص</p>
        <h1 class="display-1 mb-1.5">{{ __('licences.transfer.title') }}</h1>
    </header>

    <div class="max-w-2xl space-y-5">
        <div class="rounded-2xl border border-brand-200 bg-brand-50/70 p-5 text-sm text-ink-800 flex items-start gap-3">
            <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-200/70 text-brand-700">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="12" cy="12" r="10" /><path d="M12 16v-4M12 8h.01" />
                </svg>
            </span>
            <span>{{ __('licences.transfer.instructions') }}</span>
        </div>

        <div class="rounded-2xl border border-red-200 bg-red-50/70 p-5 text-sm text-red-800 flex items-start gap-3">
            <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-red-200/70 text-red-700">⚠</span>
            <span>{{ __('licences.transfer.warning') }}</span>
        </div>

        <div class="card-padded">
            <h3 class="eyebrow-muted mb-4">الترخيص الحالي</h3>
            <dl class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
                <div>
                    <dt class="eyebrow-muted mb-1">{{ __('licences.transfer.old_hwid_label') }}</dt>
                    <dd class="font-mono text-sm bg-ink-50 rounded-lg px-3 py-2 border border-ink-100 text-ink-900">{{ $licence->hwid }}</dd>
                </div>
                <div>
                    <dt class="eyebrow-muted mb-1">{{ __('licences.transfer.edition_label') }}</dt>
                    <dd class="font-semibold text-ink-900 pt-1.5">{{ $licence->edition }}</dd>
                </div>
                <div>
                    <dt class="eyebrow-muted mb-1">{{ __('licences.transfer.expires_at_label') }}</dt>
                    <dd class="font-mono text-sm text-ink-700 pt-1.5">{{ $licence->expires_at?->format('Y-m-d') }}</dd>
                </div>
            </dl>

            <div class="my-6 flex items-center gap-3">
                <span class="h-px flex-1 bg-ink-100"></span>
                <span class="flex h-10 w-10 items-center justify-center rounded-full bg-brand-100 text-brand-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M7 17l-4-4 4-4M17 7l4 4-4 4M3 13h18" />
                    </svg>
                </span>
                <span class="h-px flex-1 bg-ink-100"></span>
            </div>

            <form method="POST" action="{{ route('portal.licences.transfer.submit', ['licence' => $licence->id]) }}" class="space-y-5">
                @csrf
                <div>
                    <label for="new_hwid" class="form-label">{{ __('licences.transfer.new_hwid_label') }}</label>
                    <input id="new_hwid" name="new_hwid" type="text" required
                           value="{{ old('new_hwid') }}"
                           placeholder="{{ __('licences.transfer.new_hwid_placeholder') }}"
                           class="form-input font-mono uppercase tracking-wider !text-center"
                           pattern="[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}"
                           maxlength="19">
                    <p class="form-hint">معرف الجهاز الجديد ظاهر في برنامج Desktop على الجهاز التاني</p>
                    @error('new_hwid')<p class="form-error">{{ $message }}</p>@enderror
                </div>

                <div class="flex gap-3 pt-4 border-t border-ink-100">
                    <button type="submit" class="btn-primary !py-3 !px-6">
                        {{ __('licences.transfer.submit') }}
                    </button>
                    <a href="{{ route('portal.licences') }}" class="btn-secondary">
                        {{ __('licences.transfer.cancel') }}
                    </a>
                </div>
            </form>
        </div>
    </div>
@endsection
