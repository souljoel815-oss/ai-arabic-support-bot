@extends('layouts.marketing')

@section('title', __('organisation.expired_title'))

@section('content')
    <div class="max-w-md mx-auto rounded-lg border border-stone-200 bg-white p-8 text-center">
        <h1 class="text-2xl font-bold mb-4">{{ __('organisation.expired_title') }}</h1>
        <p class="text-stone-700">{{ __('organisation.expired_body') }}</p>
        <a href="/" class="mt-6 inline-block text-amber-700 hover:underline">← {{ __('messages.app.name') }}</a>
    </div>
@endsection
