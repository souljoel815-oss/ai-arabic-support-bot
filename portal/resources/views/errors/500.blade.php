@extends('errors.layout')

@section('title', 'خطأ في الخادم')

@section('code-block')
    <div class="mx-auto mb-6">
        <div class="h-24 w-24 mx-auto rounded-2xl flex items-center justify-center shadow-elevation-3"
             style="background: linear-gradient(135deg, #fca5a5 0%, #dc2626 100%);">
            <span class="text-4xl font-extrabold text-white tracking-tight">500</span>
        </div>
    </div>
@endsection

@section('headline')
    حصل خطأ غير متوقع
@endsection

@section('explanation')
    معلش، حصلت مشكلة من جانبنا. الفريق التقني اتبلّغ تلقائياً وبنحاول نصلّحها بأسرع وقت.
@endsection

@section('extra-cta')
    <a href="/contact" class="btn-secondary !py-3">تواصل مع الدعم</a>
@endsection
