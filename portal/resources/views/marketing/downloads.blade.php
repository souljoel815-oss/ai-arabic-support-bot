@extends('layouts.marketing')

@section('title', __('marketing.downloads.title'))
@section('description', __('marketing.downloads.subtitle'))

@section('content')
    <header class="text-center mb-12">
        <h1 class="text-4xl font-bold mb-3">{{ __('marketing.downloads.title') }}</h1>
        <p class="text-lg text-stone-700 max-w-2xl mx-auto">{{ __('marketing.downloads.subtitle') }}</p>
    </header>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-8">
        {{-- Desktop --}}
        <section class="rounded-lg border border-stone-200 bg-white p-6">
            <h2 class="text-2xl font-bold mb-2">{{ __('marketing.downloads.desktop.title') }}</h2>
            <p class="text-stone-700 mb-4">{{ __('marketing.downloads.desktop.description') }}</p>
            <p class="text-xs text-stone-500 mb-4">{{ __('marketing.downloads.desktop.size') }}</p>
            <div class="space-y-2">
                <a href="/downloads/{{ __('marketing.downloads.desktop.msi') }}" class="block rounded bg-amber-600 px-4 py-2 text-center text-white font-semibold hover:bg-amber-700">
                    ⬇ {{ __('marketing.downloads.desktop.msi') }}
                </a>
                <a href="/downloads/{{ __('marketing.downloads.desktop.lan') }}" class="block rounded border border-stone-300 px-4 py-2 text-center hover:bg-stone-100">
                    ⬇ {{ __('marketing.downloads.desktop.lan') }}
                </a>
            </div>
        </section>

        {{-- Android --}}
        <section class="rounded-lg border border-stone-200 bg-white p-6">
            <h2 class="text-2xl font-bold mb-2">{{ __('marketing.downloads.android.title') }}</h2>
            <p class="text-stone-700 mb-4">{{ __('marketing.downloads.android.description') }}</p>
            <div class="space-y-2">
                <a href="https://play.google.com/store/apps/details?id=com.daftarx.mobile" target="_blank" rel="noopener" class="block rounded bg-stone-900 px-4 py-2 text-center text-white font-semibold hover:bg-stone-800">
                    ▶ {{ __('marketing.downloads.android.play_store') }}
                </a>
                <a href="/downloads/daftarx-android.apk" class="block rounded border border-stone-300 px-4 py-2 text-center hover:bg-stone-100">
                    ⬇ {{ __('marketing.downloads.android.apk') }}
                </a>
            </div>
        </section>
    </div>

    {{-- Requirements --}}
    <section class="mt-12 rounded-lg border border-stone-200 bg-amber-50 p-6">
        <h2 class="text-xl font-bold mb-4">{{ __('marketing.downloads.requirements_title') }}</h2>
        <ul class="space-y-2 text-stone-700">
            <li><strong>Desktop:</strong> {{ __('marketing.downloads.requirements.desktop') }}</li>
            <li><strong>Android:</strong> {{ __('marketing.downloads.requirements.android') }}</li>
            <li><strong>Network:</strong> {{ __('marketing.downloads.requirements.network') }}</li>
        </ul>
    </section>
@endsection
