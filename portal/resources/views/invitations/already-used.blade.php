@extends('layouts.marketing')

@section('title', __('organisation.already_used_title'))

@section('content')
    <div class="max-w-md mx-auto">
        <div class="card-padded text-center shadow-elevation-3 animate-fade-in-up">
            <div class="mx-auto mb-5 h-16 w-16 rounded-2xl flex items-center justify-center shadow-elevation-2"
                 style="background: linear-gradient(135deg, #fcd34d 0%, #d97706 100%);">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-white" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="12" cy="12" r="10" /><path d="M12 8v4M12 16h.01" />
                </svg>
            </div>
            <p class="eyebrow mb-2">دعوة مستخدمة</p>
            <h1 class="display-2 mb-3">{{ __('organisation.already_used_title') }}</h1>
            <p class="text-ink-600 leading-relaxed mb-7">{{ __('organisation.already_used_body') }}</p>
            <div class="flex flex-col sm:flex-row gap-3 justify-center">
                <a href="/login" class="btn-primary !py-3">{{ __('messages.nav.login') }}</a>
                <a href="/" class="btn-secondary !py-3">← {{ __('messages.app.name') }}</a>
            </div>
        </div>
    </div>
@endsection
