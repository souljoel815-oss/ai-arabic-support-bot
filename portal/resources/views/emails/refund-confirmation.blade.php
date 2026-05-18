@extends('emails._layout')

@section('title', $isArabic ? 'تأكيد استرداد' : 'Refund confirmation')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">تم استرداد دفعتك ✓</h2>
        <p style="margin:0 0 16px;">
            تم استرداد <strong>{{ $amountEgp }} ج.م</strong> للفاتورة
            <strong>{{ $invoice->invoice_number }}</strong>.
        </p>
        <p style="margin:0 0 16px;">عادةً يصل المبلغ خلال 5–10 أيام عمل اعتماداً على البنك المصدر للبطاقة أو طريقة الدفع.</p>
        <p style="margin:0; color:#6e6862; font-size:13px;">إن لم يصل المبلغ خلال المدة المذكورة، تواصل معنا عبر بوابة الدعم.</p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">Refund processed ✓</h2>
        <p style="margin:0 0 16px;">
            We've refunded <strong>EGP {{ $amountEgp }}</strong> for invoice
            <strong>{{ $invoice->invoice_number }}</strong>.
        </p>
        <p style="margin:0 0 16px;">The funds usually arrive within 5–10 business days depending on your issuing bank or payment method.</p>
        <p style="margin:0; color:#6e6862; font-size:13px;">If you don't see the refund after that window, reach out via the support portal.</p>
    @endif
@endsection
