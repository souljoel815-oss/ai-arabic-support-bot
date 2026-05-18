@extends('emails._layout')

@section('title', $isArabic ? 'إيصال الدفع' : 'Payment receipt')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">تم استلام دفعتك ✓</h2>
        <p style="margin:0 0 16px;">شكراً لك. تم تأكيد الدفع للفاتورة <strong>{{ $invoice->invoice_number }}</strong>.</p>

        <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:20px 0; border:1px solid #e8e3dd; border-radius:8px; background:#faf8f5;">
            <tr><td style="padding:12px 16px; color:#6e6862; width:40%;">المبلغ</td><td style="padding:12px 16px; font-weight:bold;">{{ $amountEgp }} ج.م</td></tr>
            <tr><td style="padding:12px 16px; color:#6e6862;">طريقة الدفع</td><td style="padding:12px 16px;">{{ $invoice->payment_method }}</td></tr>
            <tr><td style="padding:12px 16px; color:#6e6862;">تاريخ الدفع</td><td style="padding:12px 16px;">{{ $invoice->paid_at?->format('Y-m-d H:i') ?? '—' }}</td></tr>
        </table>

        <p style="margin:0; color:#6e6862; font-size:13px;">الفاتورة الضريبية مرفقة بهذه الرسالة بصيغة PDF.</p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">Payment received ✓</h2>
        <p style="margin:0 0 16px;">Thanks. Your payment for invoice <strong>{{ $invoice->invoice_number }}</strong> has been confirmed.</p>

        <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:20px 0; border:1px solid #e8e3dd; border-radius:8px; background:#faf8f5;">
            <tr><td style="padding:12px 16px; color:#6e6862; width:40%;">Amount</td><td style="padding:12px 16px; font-weight:bold;">EGP {{ $amountEgp }}</td></tr>
            <tr><td style="padding:12px 16px; color:#6e6862;">Method</td><td style="padding:12px 16px;">{{ $invoice->payment_method }}</td></tr>
            <tr><td style="padding:12px 16px; color:#6e6862;">Paid</td><td style="padding:12px 16px;">{{ $invoice->paid_at?->format('Y-m-d H:i') ?? '—' }}</td></tr>
        </table>

        <p style="margin:0; color:#6e6862; font-size:13px;">The VAT invoice is attached as PDF.</p>
    @endif
@endsection
