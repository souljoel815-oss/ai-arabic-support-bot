{{-- T093 — Arabic-RTL invoice template. Per FR-016: Egyptian VAT line
     shown explicitly, sequential per-org invoice number, paid status
     stamp, and bilingual labels (Arabic primary + English subtitle so
     the doc reads in both registries). --}}
<!DOCTYPE html>
<html lang="ar-EG" dir="rtl">
<head>
<meta charset="UTF-8">
<title>{{ $invoice->invoice_number }} — DaftarX</title>
<style>
    @page { margin: 24mm 18mm; }
    /* If ops drops Cairo-Regular.ttf / Cairo-Bold.ttf into storage/fonts/
       DOMPDF will pick them up here. Otherwise fall back to DejaVu Sans
       which ships with the dompdf vendor package. */
    @font-face {
        font-family: 'Cairo';
        src: url('Cairo-Regular.ttf') format('truetype');
        font-weight: normal;
    }
    @font-face {
        font-family: 'Cairo';
        src: url('Cairo-Bold.ttf') format('truetype');
        font-weight: bold;
    }
    body {
        font-family: 'Cairo', 'DejaVu Sans', sans-serif;
        color: #1a1816;
        font-size: 11pt;
        direction: rtl;
    }
    .brand-bar {
        border-bottom: 3px solid #c89933;
        padding-bottom: 8mm;
        margin-bottom: 8mm;
    }
    .brand-bar h1 {
        font-size: 22pt;
        color: #c89933;
        margin: 0;
        font-weight: bold;
    }
    .brand-bar .tagline {
        font-size: 10pt;
        color: #6e6862;
        margin-top: 2mm;
    }
    .meta-grid {
        width: 100%;
        margin-bottom: 8mm;
    }
    .meta-grid td {
        vertical-align: top;
        padding: 1.5mm 0;
    }
    .meta-label {
        color: #6e6862;
        font-size: 9pt;
    }
    .meta-value {
        font-size: 11pt;
        font-weight: bold;
        color: #1a1816;
    }
    .customer-box {
        border: 1px solid #d8d2cc;
        border-radius: 4mm;
        padding: 5mm 6mm;
        margin-bottom: 8mm;
        background: #faf8f5;
    }
    .customer-box h2 {
        font-size: 10pt;
        color: #6e6862;
        margin: 0 0 2mm 0;
        font-weight: normal;
    }
    .customer-box .org-name {
        font-size: 13pt;
        font-weight: bold;
        color: #1a1816;
    }
    table.line-items {
        width: 100%;
        border-collapse: collapse;
        margin-bottom: 6mm;
    }
    table.line-items th {
        background: #f4efe8;
        color: #1a1816;
        text-align: right;
        padding: 3mm 4mm;
        font-size: 10pt;
        font-weight: bold;
        border-bottom: 2px solid #c89933;
    }
    table.line-items th.amount,
    table.line-items td.amount {
        text-align: left;
        font-variant-numeric: tabular-nums;
        white-space: nowrap;
    }
    table.line-items td {
        padding: 3mm 4mm;
        border-bottom: 1px solid #e8e3dd;
        font-size: 10pt;
    }
    table.totals {
        width: 60%;
        margin-right: 0;
        margin-left: auto;
        border-collapse: collapse;
    }
    table.totals td {
        padding: 2mm 4mm;
        font-size: 10pt;
    }
    table.totals td.label {
        color: #6e6862;
        text-align: right;
    }
    table.totals td.value {
        text-align: left;
        font-variant-numeric: tabular-nums;
        font-weight: bold;
        white-space: nowrap;
    }
    table.totals tr.total td {
        border-top: 2px solid #c89933;
        padding-top: 3mm;
        font-size: 12pt;
        color: #1a1816;
    }
    .paid-stamp {
        position: absolute;
        top: 35mm;
        left: 20mm;
        border: 3px solid #2e8a4a;
        color: #2e8a4a;
        padding: 3mm 6mm;
        font-size: 16pt;
        font-weight: bold;
        transform: rotate(-12deg);
        opacity: 0.85;
    }
    .footer-note {
        margin-top: 12mm;
        padding-top: 4mm;
        border-top: 1px solid #d8d2cc;
        font-size: 9pt;
        color: #6e6862;
        line-height: 1.6;
    }
    .english-subtitle {
        color: #a39d96;
        font-size: 9pt;
        font-weight: normal;
        direction: ltr;
        display: inline-block;
        margin-right: 4mm;
    }
</style>
</head>
<body>

<div class="paid-stamp">PAID · مدفوع</div>

<div class="brand-bar">
    <h1>DaftarX <span class="english-subtitle">— Egypt Tax & Accounting Suite</span></h1>
    <div class="tagline">دفترx — منصة الضرائب والمحاسبة المصرية</div>
</div>

<table class="meta-grid">
    <tr>
        <td>
            <div class="meta-label">رقم الفاتورة <span class="english-subtitle">Invoice #</span></div>
            <div class="meta-value">{{ $invoice->invoice_number }}</div>
        </td>
        <td>
            <div class="meta-label">تاريخ الإصدار <span class="english-subtitle">Issued</span></div>
            <div class="meta-value">{{ $paidAt ?? '—' }}</div>
        </td>
        <td>
            <div class="meta-label">طريقة الدفع <span class="english-subtitle">Method</span></div>
            <div class="meta-value">{{ $invoice->payment_method }}</div>
        </td>
    </tr>
</table>

<div class="customer-box">
    <h2>العميل <span class="english-subtitle">Customer</span></h2>
    <div class="org-name">{{ $org?->legal_name_ar ?? '—' }}</div>
    @if (!empty($org?->tax_id))
        <div class="meta-label" style="margin-top: 2mm;">
            الرقم الضريبي <span class="english-subtitle">Tax ID</span>: <strong>{{ $org->tax_id }}</strong>
        </div>
    @endif
</div>

<table class="line-items">
    <thead>
        <tr>
            <th>البند <span class="english-subtitle">Description</span></th>
            <th class="amount">المبلغ (جنيه) <span class="english-subtitle">Amount (EGP)</span></th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>
                @switch($invoice->kind)
                    @case('FirstPeriod')
                        اشتراك أول فترة — باقة {{ $subscription?->tier ?? '—' }} ({{ $subscription?->billing_cadence ?? '—' }})
                        @break
                    @case('Renewal')
                        تجديد اشتراك — باقة {{ $subscription?->tier ?? '—' }} ({{ $subscription?->billing_cadence ?? '—' }})
                        @break
                    @case('Upgrade')
                        ترقية باقة — تناسب فترة متبقية للفترة الحالية
                        @break
                    @case('Refund')
                        استرداد — فاتورة أول فترة
                        @break
                    @default
                        {{ $invoice->kind }}
                @endswitch
            </td>
            <td class="amount">{{ $netEgp }}</td>
        </tr>
    </tbody>
</table>

<table class="totals">
    <tr>
        <td class="label">الإجمالي قبل الضريبة <span class="english-subtitle">Subtotal</span></td>
        <td class="value">{{ $netEgp }} ج.م</td>
    </tr>
    <tr>
        <td class="label">ضريبة القيمة المضافة 14% <span class="english-subtitle">VAT 14%</span></td>
        <td class="value">{{ $vatEgp }} ج.م</td>
    </tr>
    <tr class="total">
        <td class="label">الإجمالي المستحق <span class="english-subtitle">Total Due</span></td>
        <td class="value">{{ $amountEgp }} ج.م</td>
    </tr>
</table>

<div class="footer-note">
    شكرًا لاستخدامك دفترx. هذه فاتورة ضريبية صالحة طبقًا للقانون المصري.
    تحتفظ شركة DaftarX بهذا السجل لمدة 5 سنوات حسب اللوائح الضريبية المصرية.
    <br>
    <span class="english-subtitle">
        Thank you for using DaftarX. This is a valid VAT invoice under Egyptian tax law.
        DaftarX retains this record for 5 years per Egyptian tax regulations.
    </span>
</div>

</body>
</html>
