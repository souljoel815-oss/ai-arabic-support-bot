@extends('layouts.marketing')

@php
    $page = $page ?? 'page';
@endphp

@section('title', ucfirst($page))

@section('content')
    <section class="text-center py-24">
        <h1 class="text-3xl font-bold mb-2 capitalize">{{ str_replace('_', ' ', $page) }}</h1>
        <p class="text-stone-600">Phase 2 scaffold — full implementation lands in US1 (T040–T055).</p>
        <p class="mt-4">
            <a href="/" class="text-amber-600 hover:underline">{{ __('messages.app.name') }}</a>
        </p>
    </section>
@endsection
