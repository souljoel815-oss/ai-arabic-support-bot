@extends('emails._layout')

@section('title', $isArabic ? 'تبقى 7 أيام على انتهاء فترة التجربة' : 'Your trial ends in 7 days')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">{{ $displayName }} — تبقى 7 أيام</h2>
        <p style="margin:0 0 16px;">فترة تجربة دفترx ستنتهي خلال أسبوع. اشترك الآن لاستكمال استخدام النظام بدون انقطاع.</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $convertUrl }}"
               style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                اشترك الآن
            </a>
        </p>

        <p style="margin:0; color:#6e6862; font-size:13px;">كل بياناتك محفوظة — ستكمل من حيث توقفت بدون أي إعداد جديد.</p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">{{ $displayName }} — 7 days left</h2>
        <p style="margin:0 0 16px;">Your DaftarX trial ends in a week. Subscribe now to keep working without interruption.</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $convertUrl }}"
               style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                Subscribe now
            </a>
        </p>

        <p style="margin:0; color:#6e6862; font-size:13px;">All your data is preserved — you'll pick up right where you left off.</p>
    @endif
@endsection
