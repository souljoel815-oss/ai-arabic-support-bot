@extends('layouts.marketing')

@section('title', __('marketing.contact.title'))

@section('content')
    <header class="text-center mb-12 max-w-3xl mx-auto">
        <p class="eyebrow mb-3">تواصل معنا</p>
        <h1 class="display-1 text-5xl mb-4">{{ __('marketing.contact.title') }}</h1>
        <p class="text-lg text-ink-700">{{ __('marketing.contact.subtitle') }}</p>
    </header>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {{-- Form --}}
        <div class="lg:col-span-2 card-padded">
            @if (session('status'))
                <div class="mb-5 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900 flex items-center gap-2">
                    <span class="text-emerald-600">✓</span>
                    <span>{{ session('status') }}</span>
                </div>
            @endif

            <form method="POST" action="/contact" class="space-y-5">
                @csrf
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                        <label for="name" class="form-label">{{ __('marketing.contact.form.name') }}</label>
                        <input id="name" name="name" type="text" required value="{{ old('name') }}" class="form-input" placeholder="أحمد محمد">
                        @error('name')<p class="form-error">{{ $message }}</p>@enderror
                    </div>
                    <div>
                        <label for="email" class="form-label">{{ __('marketing.contact.form.email') }}</label>
                        <input id="email" name="email" type="email" required value="{{ old('email') }}" class="form-input" placeholder="name@company.com">
                        @error('email')<p class="form-error">{{ $message }}</p>@enderror
                    </div>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                        <label for="phone" class="form-label">{{ __('marketing.contact.form.phone') }}</label>
                        <input id="phone" name="phone" type="tel" value="{{ old('phone') }}" class="form-input" placeholder="+20 ___ ___ ____">
                    </div>
                    <div>
                        <label for="interested_tier" class="form-label">{{ __('marketing.contact.form.interested_tier') }}</label>
                        <select id="interested_tier" name="interested_tier" class="form-select">
                            <option value="">—</option>
                            @foreach (['Solo', 'SMB', 'Enterprise', 'Firm'] as $tier)
                                <option value="{{ $tier }}" {{ old('interested_tier') === $tier ? 'selected' : '' }}>
                                    {{ __('marketing.pricing.tiers.'.strtolower($tier).'.name') }}
                                </option>
                            @endforeach
                        </select>
                    </div>
                </div>
                <div>
                    <label for="message" class="form-label">{{ __('marketing.contact.form.message') }}</label>
                    <textarea id="message" name="message" rows="6" class="form-textarea" placeholder="اكتب رسالتك هنا…">{{ old('message') }}</textarea>
                </div>
                <button type="submit" class="btn-primary !py-3 !px-7">
                    {{ __('marketing.contact.form.submit') }}
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M22 2 11 13M22 2l-7 20-4-9-9-4z" />
                    </svg>
                </button>
            </form>
        </div>

        {{-- Channels sidebar --}}
        <aside class="card-padded h-fit space-y-5">
            <div>
                <p class="eyebrow mb-2">قنوات التواصل</p>
                <h2 class="text-lg font-bold text-ink-950">{{ __('marketing.contact.channels_title') }}</h2>
            </div>

            <a href="mailto:{{ __('marketing.contact.channels.sales') }}" class="flex items-center gap-3 rounded-xl border border-ink-100 px-3 py-3 hover:border-brand-300 hover:bg-brand-50/40 transition group">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-brand-700 group-hover:bg-brand-600 group-hover:text-white transition">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2zM22 6l-10 7L2 6" />
                    </svg>
                </span>
                <div class="min-w-0">
                    <div class="text-xs text-ink-500">{{ __('marketing.contact.channels.email') }}</div>
                    <div class="font-semibold text-ink-900 truncate text-sm">{{ __('marketing.contact.channels.sales') }}</div>
                </div>
            </a>

            <a href="mailto:{{ __('marketing.contact.channels.support') }}" class="flex items-center gap-3 rounded-xl border border-ink-100 px-3 py-3 hover:border-brand-300 hover:bg-brand-50/40 transition group">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-sky-100 text-sky-700 group-hover:bg-sky-600 group-hover:text-white transition">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M18.36 5.64A9 9 0 1 1 5.64 18.36 9 9 0 0 1 18.36 5.64Zm-9.55 9.55 2.83-2.83m4.95-4.95-2.83 2.83" />
                    </svg>
                </span>
                <div class="min-w-0">
                    <div class="text-xs text-ink-500">{{ __('messages.nav.contact') }}</div>
                    <div class="font-semibold text-ink-900 truncate text-sm">{{ __('marketing.contact.channels.support') }}</div>
                </div>
            </a>

            <div class="flex items-center gap-3 rounded-xl border border-ink-100 px-3 py-3">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg text-white"
                      style="background: linear-gradient(135deg, #25D366 0%, #128C7E 100%);">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 0 1-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 0 1-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 0 1 2.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0 0 12.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 0 0 5.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 0 0-3.48-8.413"/>
                    </svg>
                </span>
                <div class="min-w-0">
                    <div class="text-xs text-ink-500">{{ __('marketing.contact.channels.whatsapp') }}</div>
                    <div class="font-semibold text-ink-900 text-sm font-mono">+20 100 000 0000</div>
                </div>
            </div>

            <div class="flex items-center gap-3 rounded-xl border border-ink-100 px-3 py-3">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-ink-100 text-ink-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" />
                    </svg>
                </span>
                <div class="min-w-0">
                    <div class="text-xs text-ink-500">{{ __('marketing.contact.channels.phone') }}</div>
                    <div class="font-semibold text-ink-900 text-sm font-mono">+20 2 0000 0000</div>
                </div>
            </div>
        </aside>
    </div>
@endsection
