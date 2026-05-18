@extends('emails._layout')

@section('title', $isArabic ? 'رد جديد على طلب الدعم' : 'New reply on support ticket')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">رد جديد من فريق الدعم</h2>
        <p style="margin:0 0 16px;">فيه رد جديد على طلبك <strong>#{{ $ticket->id }}</strong> ({{ $ticket->subject }}).</p>

        <div style="margin:20px 0; padding:16px; background:#faf8f5; border-inline-start:4px solid #c89933; border-radius:8px; white-space:pre-wrap; font-size:14px; line-height:1.7; color:#403a30;">{{ $reply->body }}</div>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $portalUrl }}" style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 28px; border-radius:8px; font-weight:bold; font-size:15px;">
                الرد من البوابة
            </a>
        </p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">New reply from support</h2>
        <p style="margin:0 0 16px;">There's a new reply on your ticket <strong>#{{ $ticket->id }}</strong> ({{ $ticket->subject }}).</p>

        <div style="margin:20px 0; padding:16px; background:#faf8f5; border-inline-start:4px solid #c89933; border-radius:8px; white-space:pre-wrap; font-size:14px; line-height:1.7; color:#403a30;">{{ $reply->body }}</div>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $portalUrl }}" style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 28px; border-radius:8px; font-weight:bold; font-size:15px;">
                Reply in portal
            </a>
        </p>
    @endif
@endsection
