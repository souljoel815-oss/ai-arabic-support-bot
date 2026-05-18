@extends('layouts.marketing')

@section('title', __('organisation.already_used_title'))

@section('content')
    <div class="max-w-md mx-auto rounded-lg border border-stone-200 bg-white p-8 text-center">
        <h1 class="text-2xl font-bold mb-4">{{ __('organisation.already_used_title') }}</h1>
        <p class="text-stone-700">{{ __('organisation.already_used_body') }}</p>
        <div class="mt-6 flex justify-center gap-3">
            <a href="/login" class="rounded bg-amber-600 px-4 py-2 text-white font-semibold hover:bg-amber-700">{{ __('messages.nav.login') }}</a>
            <a href="/" class="rounded border border-stone-300 px-4 py-2 hover:bg-stone-100">← {{ __('messages.app.name') }}</a>
        </div>
    </div>
@endsection
