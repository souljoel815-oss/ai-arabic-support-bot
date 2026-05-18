@extends('emails._layout')

@section('title', $isArabic ? 'تم استلام طلب الدعم' : 'Support ticket received')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">تم استلام طلبك ✓</h2>
        <p style="margin:0 0 16px;">شكرًا لتواصلك. سجّلنا طلبك بالرقم <strong>#{{ $ticket->id }}</strong>.</p>

        <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:20px 0; border:1px solid #e8e3dd; border-radius:8px; background:#faf8f5;">
            <tr><td style="padding:12px 16px; color:#6e6862; width:40%;">الموضوع</td><td style="padding:12px 16px; font-weight:bold;">{{ $ticket->subject }}</td></tr>
            <tr><td style="padding:12px 16px; color:#6e6862;">الأولوية</td><td style="padding:12px 16px;">{{ $ticket->priority }}</td></tr>
            <tr><td style="padding:12px 16px; color:#6e6862;">الفئة</td><td style="padding:12px 16px;">{{ $ticket->category }}</td></tr>
        </table>

        <p style="margin:0 0 20px;">سيتم الرد خلال SLA المنصوص عليه في خطتك (24h Solo/SMB · 4h Enterprise/Firm).</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $portalUrl }}" style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 28px; border-radius:8px; font-weight:bold; font-size:15px;">
                عرض الطلب
            </a>
        </p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">Ticket received ✓</h2>
        <p style="margin:0 0 16px;">Thanks for reaching out. We've logged your ticket as <strong>#{{ $ticket->id }}</strong>.</p>

        <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:20px 0; border:1px solid #e8e3dd; border-radius:8px; background:#faf8f5;">
            <tr><td style="padding:12px 16px; color:#6e6862; width:40%;">Subject</td><td style="padding:12px 16px; font-weight:bold;">{{ $ticket->subject }}</td></tr>
            <tr><td style="padding:12px 16px; color:#6e6862;">Priority</td><td style="padding:12px 16px;">{{ $ticket->priority }}</td></tr>
            <tr><td style="padding:12px 16px; color:#6e6862;">Category</td><td style="padding:12px 16px;">{{ $ticket->category }}</td></tr>
        </table>

        <p style="margin:0 0 20px;">A reply will arrive within the SLA on your plan (24h Solo/SMB · 4h Enterprise/Firm).</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $portalUrl }}" style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 28px; border-radius:8px; font-weight:bold; font-size:15px;">
                View ticket
            </a>
        </p>
    @endif
@endsection
