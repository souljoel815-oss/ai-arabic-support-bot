@extends('emails._layout')

@section('title', $isArabic ? 'فعّل حسابك في دفترx' : 'Confirm your DaftarX account')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">أهلاً {{ $displayName }} 👋</h2>
        <p style="margin:0 0 16px;">شكراً لتسجيلك في دفترx. اضغط الزر تحت لتفعيل حسابك:</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $verificationUrl }}"
               style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                فعّل حسابي
            </a>
        </p>

        <p style="margin:0 0 12px; color:#6e6862; font-size:13px;">
            أو افتح الرابط مباشرة في المتصفح:
            <br><a href="{{ $verificationUrl }}" style="color:#c89933; word-break:break-all;">{{ $verificationUrl }}</a>
        </p>

        <p style="margin:24px 0 0; color:#6e6862; font-size:13px;">
            الرابط صالح لـ 60 دقيقة من إصداره.
        </p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">Welcome, {{ $displayName }} 👋</h2>
        <p style="margin:0 0 16px;">Thanks for signing up with DaftarX. Tap the button below to verify your email:</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $verificationUrl }}"
               style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                Verify my account
            </a>
        </p>

        <p style="margin:0 0 12px; color:#6e6862; font-size:13px;">
            Or open the link directly:
            <br><a href="{{ $verificationUrl }}" style="color:#c89933; word-break:break-all;">{{ $verificationUrl }}</a>
        </p>

        <p style="margin:24px 0 0; color:#6e6862; font-size:13px;">
            This link expires in 60 minutes.
        </p>
    @endif
@endsection
