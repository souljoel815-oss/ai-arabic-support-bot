@extends('emails._layout')

@section('title', $isArabic ? 'تم إلغاء العضوية' : 'Access revoked')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">{{ $memberName }}</h2>
        <p style="margin:0 0 16px;">
            تم إلغاء عضويتك في مؤسسة <strong>{{ $organisation->legal_name_ar }}</strong> على DaftarX.
        </p>
        <p style="margin:0 0 16px;">
            لن تتمكّن من الوصول إلى بيانات المؤسسة عبر بوابة العملاء. حسابك الشخصي لا يزال نشطاً ويمكنك استخدامه مع مؤسسات أخرى.
        </p>
        <p style="margin:0 0 16px; color:#6e6862; font-size:13px;">
            لو كان الإلغاء غير مقصود، تواصل مع مالك المؤسسة أو
            <a href="{{ $supportUrl }}" style="color:#c89933;">فريق الدعم</a>.
        </p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">{{ $memberName }}</h2>
        <p style="margin:0 0 16px;">
            Your access to <strong>{{ $organisation->legal_name_ar }}</strong> on DaftarX has been revoked.
        </p>
        <p style="margin:0 0 16px;">
            You can no longer access this organisation's data through the customer portal. Your personal account remains active and can be used with other organisations.
        </p>
        <p style="margin:0 0 16px; color:#6e6862; font-size:13px;">
            If this was unintended, contact the organisation owner or
            <a href="{{ $supportUrl }}" style="color:#c89933;">our support team</a>.
        </p>
    @endif
@endsection
