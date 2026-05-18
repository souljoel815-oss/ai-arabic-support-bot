@extends('layouts.marketing')

@section('title', __('marketing.downloads.title'))
@section('description', __('marketing.downloads.subtitle'))

@section('content')
    <header class="text-center mb-16 max-w-3xl mx-auto">
        <p class="eyebrow mb-3">تحميل البرامج</p>
        <h1 class="display-1 text-5xl mb-4">{{ __('marketing.downloads.title') }}</h1>
        <p class="text-lg text-ink-700">{{ __('marketing.downloads.subtitle') }}</p>
    </header>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-5 mb-12">
        {{-- Desktop --}}
        <article class="card-padded card-hover relative overflow-hidden">
            <span class="absolute top-0 end-0 h-32 w-32 rounded-full opacity-10 -translate-y-12 translate-x-12"
                  style="background: radial-gradient(circle, #0078d4 0%, transparent 70%);"></span>
            <div class="flex items-start gap-4 mb-5">
                <span class="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-sky-100 text-sky-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-7 w-7" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M0 3.449 9.75 2.1v9.45H0V3.449zm10.949-1.524L24 0v11.4H10.949V1.925zM0 12.6h9.75v9.451L0 20.699V12.6zm10.949 0H24V24l-13.051-1.799V12.6z"/>
                    </svg>
                </span>
                <div class="flex-1 min-w-0">
                    <h2 class="display-2 text-2xl mb-1">{{ __('marketing.downloads.desktop.title') }}</h2>
                    <p class="text-sm text-ink-600 leading-relaxed">{{ __('marketing.downloads.desktop.description') }}</p>
                </div>
            </div>
            <p class="text-xs text-ink-500 mb-5">{{ __('marketing.downloads.desktop.size') }}</p>
            <div class="space-y-2.5">
                <a href="/downloads/{{ __('marketing.downloads.desktop.msi') }}" class="btn-primary w-full !py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M12 3v12m-5-5 5 5 5-5M5 21h14" />
                    </svg>
                    {{ __('marketing.downloads.desktop.msi') }}
                </a>
                <a href="/downloads/{{ __('marketing.downloads.desktop.lan') }}" class="btn-secondary w-full !py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M12 3v12m-5-5 5 5 5-5M5 21h14" />
                    </svg>
                    {{ __('marketing.downloads.desktop.lan') }}
                </a>
            </div>
        </article>

        {{-- Android --}}
        <article class="card-padded card-hover relative overflow-hidden">
            <div class="flex items-start gap-4 mb-5">
                <span class="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl text-white"
                      style="background: linear-gradient(135deg, #3ddc84 0%, #1ba768 100%);">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-7 w-7" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M17.523 15.341a1.118 1.118 0 1 1 0-2.236 1.118 1.118 0 0 1 0 2.236m-11.046 0a1.118 1.118 0 1 1 0-2.236 1.118 1.118 0 0 1 0 2.236M5.05 8.39 3.013 4.857a.416.416 0 1 1 .721-.418L5.799 8.02C7.078 7.385 8.493 7.045 10 7.045s2.922.34 4.201.975l2.065-3.581a.416.416 0 1 1 .72.418L14.95 8.39c2.27 1.158 3.811 3.302 3.811 5.815H1.239c0-2.513 1.541-4.657 3.811-5.815"/>
                    </svg>
                </span>
                <div class="flex-1 min-w-0">
                    <h2 class="display-2 text-2xl mb-1">{{ __('marketing.downloads.android.title') }}</h2>
                    <p class="text-sm text-ink-600 leading-relaxed">{{ __('marketing.downloads.android.description') }}</p>
                </div>
            </div>
            <div class="space-y-2.5 mt-auto">
                <a href="https://play.google.com/store/apps/details?id=com.daftarx.mobile" target="_blank" rel="noopener"
                   class="btn-primary w-full !py-3" style="background: linear-gradient(135deg, #2d2a23 0%, #1c1a16 100%);">
                    ▶ {{ __('marketing.downloads.android.play_store') }}
                </a>
                <a href="/downloads/daftarx-android.apk" class="btn-secondary w-full !py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M12 3v12m-5-5 5 5 5-5M5 21h14" />
                    </svg>
                    {{ __('marketing.downloads.android.apk') }}
                </a>
            </div>
        </article>
    </div>

    {{-- Requirements --}}
    <section class="card-padded">
        <div class="flex items-center gap-3 mb-5">
            <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-100 text-brand-700">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="12" cy="12" r="10" /><path d="M12 16v-4M12 8h.01" />
                </svg>
            </span>
            <h2 class="text-xl font-bold text-ink-950">{{ __('marketing.downloads.requirements_title') }}</h2>
        </div>
        <dl class="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
            <div>
                <dt class="eyebrow-muted mb-1.5">Desktop</dt>
                <dd class="text-ink-700">{{ __('marketing.downloads.requirements.desktop') }}</dd>
            </div>
            <div>
                <dt class="eyebrow-muted mb-1.5">Android</dt>
                <dd class="text-ink-700">{{ __('marketing.downloads.requirements.android') }}</dd>
            </div>
            <div>
                <dt class="eyebrow-muted mb-1.5">Network</dt>
                <dd class="text-ink-700">{{ __('marketing.downloads.requirements.network') }}</dd>
            </div>
        </dl>
    </section>
@endsection
