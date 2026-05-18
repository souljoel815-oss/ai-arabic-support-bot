@extends('emails._layout')

@section('title', $isArabic ? 'فترة التجربة تنتهي غدًا' : 'Your trial ends tomorrow')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">{{ $displayName }} — يوم واحد فقط</h2>
        <p style="margin:0 0 16px;"><strong>فترة التجربة تنتهي غدًا.</strong> بعد انتهائها لن يفتح النظام حتى يتم الاشتراك.</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $convertUrl }}"
               style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                اشترك الآن
            </a>
        </p>

        <p style="margin:0; color:#6e6862; font-size:13px;">الاشتراك خطوة واحدة — لن تعيد إدخال أي بيانات.</p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">{{ $displayName }} — final day</h2>
        <p style="margin:0 0 16px;"><strong>Your trial ends tomorrow.</strong> Subscribe today to avoid any interruption.</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $convertUrl }}"
               style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                Subscribe now
            </a>
        </p>

        <p style="margin:0; color:#6e6862; font-size:13px;">One-tap convert — no fields to re-enter.</p>
    @endif
@endsection
