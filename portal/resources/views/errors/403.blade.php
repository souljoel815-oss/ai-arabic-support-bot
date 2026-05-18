@extends('errors.layout')

@section('title', 'لا تملك صلاحية الوصول')

@section('code-block')
    <div class="mx-auto mb-6 w-20 h-20 rounded-full bg-amber-100 flex items-center justify-center">
        <span class="text-3xl">🔒</span>
    </div>
@endsection

@section('headline')
    لا تملك صلاحية الوصول
@endsection

@section('explanation')
    {{ $exception?->getMessage() ?: 'الصفحة دي محجوزة لمالك المؤسسة أو دور معين لا تملكه.' }}
@endsection

@section('extra-cta')
    <a href="{{ route('portal.dashboard') }}" class="btn-secondary">لوحة التحكم</a>
@endsection
