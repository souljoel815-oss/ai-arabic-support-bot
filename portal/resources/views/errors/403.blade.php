@extends('errors.layout')

@section('title', 'لا تملك صلاحية الوصول')

@section('code-block')
    <div class="mx-auto mb-6">
        <div class="h-24 w-24 mx-auto rounded-2xl flex items-center justify-center shadow-elevation-2"
             style="background: linear-gradient(135deg, #fcd34d 0%, #d97706 100%);">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-12 w-12 text-white" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                <rect x="3" y="11" width="18" height="11" rx="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
        </div>
    </div>
@endsection

@section('headline')
    لا تملك صلاحية الوصول
@endsection

@section('explanation')
    {{ $exception?->getMessage() ?: 'الصفحة دي محجوزة لمالك المؤسسة أو دور معين لا تملكه.' }}
@endsection

@section('extra-cta')
    <a href="{{ route('portal.dashboard') }}" class="btn-secondary !py-3">لوحة التحكم</a>
@endsection
