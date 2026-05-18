@extends('emails._layout')

@section('title', $isArabic ? 'دعوة للانضمام' : 'You\'re invited')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">دعوة للانضمام إلى {{ $organisation->legal_name_ar }}</h2>
        <p style="margin:0 0 16px;">
            دعاك <strong>{{ $inviterName }}</strong> للانضمام إلى مؤسسة <strong>{{ $organisation->legal_name_ar }}</strong>
            على منصة DaftarX بصفة <strong>{{ __('organisation.role.'.$invitation->role) }}</strong>.
        </p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $acceptUrl }}" style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:14px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                قبول الدعوة
            </a>
        </p>

        <p style="margin:24px 0 8px; color:#6e6862; font-size:13px;">
            الرابط صالح لمدة 7 أيام من إصداره. لو تجاهلته، الدعوة هتنتهي تلقائياً.
        </p>
        <p style="margin:0; color:#6e6862; font-size:12px; word-break:break-all;">
            أو افتح الرابط مباشرة:<br>
            <a href="{{ $acceptUrl }}" style="color:#c89933;">{{ $acceptUrl }}</a>
        </p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">You're invited to join {{ $organisation->legal_name_ar }}</h2>
        <p style="margin:0 0 16px;">
            <strong>{{ $inviterName }}</strong> has invited you to join <strong>{{ $organisation->legal_name_ar }}</strong>
            on DaftarX as <strong>{{ __('organisation.role.'.$invitation->role) }}</strong>.
        </p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $acceptUrl }}" style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:14px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                Accept invitation
            </a>
        </p>

        <p style="margin:24px 0 8px; color:#6e6862; font-size:13px;">
            This invitation expires in 7 days. If you ignore it, the invitation will expire automatically.
        </p>
        <p style="margin:0; color:#6e6862; font-size:12px; word-break:break-all;">
            Or open the link directly:<br>
            <a href="{{ $acceptUrl }}" style="color:#c89933;">{{ $acceptUrl }}</a>
        </p>
    @endif
@endsection
