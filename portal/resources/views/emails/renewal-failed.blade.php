@extends('emails._layout')

@section('title', $isArabic ? 'فشل تجديد الاشتراك' : 'Subscription renewal failed')

@section('body')
    @if ($isArabic)
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">{{ $displayName }} — حدثت مشكلة في التجديد</h2>
        <p style="margin:0 0 16px;">
            محاولة تجديد اشتراكك في باقة <strong>{{ $subscription->tier }}</strong> ({{ $subscription->billing_cadence }}) لم تنجح.
        </p>
        <p style="margin:0 0 16px;">عادةً يكون السبب رفض البنك أو انتهاء صلاحية البطاقة. حدّث طريقة الدفع لتفادي تعطل النظام.</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $updatePaymentUrl }}"
               style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                حدّث طريقة الدفع
            </a>
        </p>

        <p style="margin:0; color:#6e6862; font-size:13px;">سنحاول التجديد تلقائياً مرة أخرى خلال 24 ساعة.</p>
    @else
        <h2 style="margin:0 0 16px; font-size:20px; color:#1a1816;">{{ $displayName }} — renewal needs attention</h2>
        <p style="margin:0 0 16px;">
            We couldn't renew your <strong>{{ $subscription->tier }}</strong> ({{ $subscription->billing_cadence }}) subscription.
        </p>
        <p style="margin:0 0 16px;">This usually means the bank declined or the card expired. Update your payment method to keep DaftarX running.</p>

        <p style="text-align:center; margin:28px 0;">
            <a href="{{ $updatePaymentUrl }}"
               style="display:inline-block; background:#c89933; color:#ffffff; text-decoration:none; padding:12px 32px; border-radius:8px; font-weight:bold; font-size:15px;">
                Update payment method
            </a>
        </p>

        <p style="margin:0; color:#6e6862; font-size:13px;">We'll retry automatically within 24 hours.</p>
    @endif
@endsection
