@extends('errors.layout')

@section('title', 'خطأ في الخادم')

@section('code-block')
    <div class="mx-auto mb-6 w-20 h-20 rounded-full bg-red-100 flex items-center justify-center">
        <span class="text-3xl font-bold text-red-700">500</span>
    </div>
@endsection

@section('headline')
    حصل خطأ غير متوقع
@endsection

@section('explanation')
    معلش، حصلت مشكلة من جانبنا. الفريق التقني اتبلّغ تلقائياً وبنحاول نصلّحها بأسرع وقت.
@endsection

@section('extra-cta')
    <a href="/contact" class="btn-secondary">تواصل مع الدعم</a>
@endsection
