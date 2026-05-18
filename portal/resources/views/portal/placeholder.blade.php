@extends('layouts.portal')

@php
    $page = $page ?? 'page';
@endphp

@section('title', ucfirst($page))

@section('content')
    <h1 class="text-2xl font-bold mb-2 capitalize">{{ str_replace('_', ' ', $page) }}</h1>
    <p class="text-stone-600">
        Phase 2 scaffold — full implementation lands in the corresponding user-story tasks.
    </p>
@endsection
