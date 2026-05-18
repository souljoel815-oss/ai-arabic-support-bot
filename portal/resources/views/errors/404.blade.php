@extends('errors.layout')

@section('title', 'الصفحة غير موجودة')

@section('code-block')
    <div class="mx-auto mb-6 w-20 h-20 rounded-full bg-brand-100 flex items-center justify-center">
        <span class="text-3xl font-bold text-brand-700">404</span>
    </div>
@endsection

@section('headline')
    الصفحة دي مش موجودة
@endsection

@section('explanation')
    الرابط اللي حاولت تفتحه إما اتنقل، اتغيّر، أو ما كانش موجود من الأصل.
@endsection
