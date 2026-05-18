@extends('emails._layout')

@section('title', $isArabic ? 'تم حل طلب الدعم' : 'Support ticket resolved')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">تم حل طلبك ✓</h2>
        <p style="margin:0 0 16px;">قمنا بحل طلب الدعم <strong>#{{ $ticket->id }}</strong> ({{ $ticket->subject }}).</p>
        <p style="margin:0 0 20px;">لو المشكلة رجعت أو حابب توضّح أكتر، تقدر تفتح الطلب وترد عليه لإعادة فتحه.</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $portalUrl }}" style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 28px; border-radius:8px; font-weight:bold; font-size:15px;">
                عرض الطلب
            </a>
        </p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">Ticket resolved ✓</h2>
        <p style="margin:0 0 16px;">We've resolved support ticket <strong>#{{ $ticket->id }}</strong> ({{ $ticket->subject }}).</p>
        <p style="margin:0 0 20px;">If the issue comes back or you'd like to clarify anything, you can reply on the ticket to reopen it.</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $portalUrl }}" style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 28px; border-radius:8px; font-weight:bold; font-size:15px;">
                View ticket
            </a>
        </p>
    @endif
@endsection
