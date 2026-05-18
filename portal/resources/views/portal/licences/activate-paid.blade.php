@extends('layouts.portal')

@section('title', __('licences.activate.title'))

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.licences') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            {{ __('licences.title') }}
        </a>
        <p class="eyebrow mb-2">تنشيط ترخيص</p>
        <h1 class="display-1 mb-1.5">{{ __('licences.activate.title') }}</h1>
    </header>

    <div class="max-w-2xl">
        <div class="rounded-2xl border border-brand-200 bg-brand-50/70 p-5 mb-5 text-sm text-ink-800 flex items-start gap-3">
            <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-200/70 text-brand-700">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="12" cy="12" r="10" /><path d="M12 16v-4M12 8h.01" />
                </svg>
            </span>
            <span>{{ __('licences.activate.instructions') }}</span>
        </div>

        <form method="POST" action="{{ route('portal.licences.activate-paid.submit') }}" class="card-padded space-y-5">
            @csrf
            <input type="hidden" name="subscription_id" value="{{ $subscription->id }}">

            <div>
                <label class="form-label">{{ __('licences.activate.subscription_label') }}</label>
                <div class="rounded-xl border-2 border-brand-200 bg-brand-50/50 px-4 py-3 flex items-center gap-3 flex-wrap">
                    <span class="pill-brand">{{ __('marketing.pricing.tiers.'.strtolower($subscription->tier).'.name') }}</span>
                    <span class="text-ink-300">·</span>
                    <span class="text-sm text-ink-700">{{ __('licences.cadence.'.$subscription->billing_cadence) }}</span>
                    <span class="text-ink-300">·</span>
                    <span class="text-sm text-ink-600">{{ __('licences.period_end_label') }} <span class="font-mono">{{ $subscription->current_period_end_at?->format('Y-m-d') }}</span></span>
                </div>
            </div>

            <div>
                <label for="hwid" class="form-label">{{ __('licences.activate.hwid_label') }}</label>
                <input id="hwid" name="hwid" type="text" required
                       value="{{ old('hwid') }}"
                       placeholder="{{ __('licences.activate.hwid_placeholder') }}"
                       class="form-input font-mono uppercase tracking-wider !text-center"
                       pattern="[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}"
                       maxlength="19">
                <p class="form-hint">معرف الجهاز ظاهر في البرنامج Desktop → الإعدادات → معلومات الترخيص</p>
                @error('hwid')
                    <p class="form-error">{{ $message }}</p>
                @enderror
            </div>

            <div class="flex gap-3 pt-4 border-t border-ink-100">
                <button type="submit" class="btn-primary !py-3 !px-6">
                    {{ __('licences.activate.submit') }}
                </button>
                <a href="{{ route('portal.licences') }}" class="btn-secondary">
                    {{ __('licences.activate.cancel') }}
                </a>
            </div>
        </form>
    </div>
@endsection
