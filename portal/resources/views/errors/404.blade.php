@extends('errors.layout')

@section('title', 'الصفحة غير موجودة')

@section('code-block')
    <div class="mx-auto mb-6 relative">
        <div class="h-24 w-24 mx-auto rounded-2xl flex items-center justify-center shadow-brand-glow-lg"
             style="background: linear-gradient(135deg, #f5d691 0%, #d68a1f 100%);">
            <span class="text-4xl font-extrabold text-white tracking-tight">404</span>
        </div>
    </div>
@endsection

@section('headline')
    الصفحة دي مش موجودة
@endsection

@section('explanation')
    الرابط اللي حاولت تفتحه إما اتنقل، اتغيّر، أو ما كانش موجود من الأصل.
@endsection
