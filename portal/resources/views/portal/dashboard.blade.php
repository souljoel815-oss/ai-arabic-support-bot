@extends('layouts.portal')

@section('title', 'Dashboard')

@section('content')
    <h1 class="text-2xl font-bold mb-4">
        Welcome, {{ auth()->user()->display_name ?? auth()->user()->email }}
    </h1>
    <p class="text-stone-600 mb-4">
        Phase 2 scaffold — full dashboard lands in US3 task T098 (FR-012):
        subscription status, active licences, next renewal date, open ticket
        count, and recent downloads.
    </p>

    <section class="mt-8 rounded border border-amber-200 bg-amber-50 p-4">
        <p class="text-sm">
            <strong>No active subscription yet.</strong>
            Your installed DaftarX is in trial. To subscribe, click below.
        </p>
        <a href="/portal/subscription" class="mt-3 inline-block rounded bg-amber-600 px-4 py-2 text-sm font-semibold text-white hover:bg-amber-700">
            اشترك في الخطة
        </a>
    </section>
@endsection
