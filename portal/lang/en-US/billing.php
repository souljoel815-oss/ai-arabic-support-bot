<?php

return [
    'title' => 'Billing &amp; payments',
    'subtitle' => 'All your issued invoices. Download a PDF copy or request a refund within 7 days of your first payment.',
    'no_invoices' => 'No invoices yet.',

    'columns' => [
        'number' => 'Invoice number',
        'kind' => 'Kind',
        'amount' => 'Amount',
        'method' => 'Method',
        'status' => 'Status',
        'issued' => 'Issued',
        'actions' => 'Actions',
    ],
    'kind' => [
        'FirstPeriod' => 'First period',
        'Renewal' => 'Renewal',
        'Upgrade' => 'Upgrade',
        'Refund' => 'Refund',
    ],
    'status' => [
        'Pending' => 'Pending',
        'Paid' => 'Paid',
        'Refunded' => 'Refunded',
        'Failed' => 'Failed',
    ],

    'action_download' => 'Download PDF',
    'action_refund' => 'Request refund',
    'refund_eligibility' => [
        'eligible' => 'Eligible for refund',
        'not_eligible_not_paid' => 'Not eligible (not paid)',
        'not_eligible_renewal' => 'Not eligible (renewal)',
        'not_eligible_window_expired' => 'Not eligible (7-day window expired)',
        'not_eligible_already_refunded' => 'Already refunded',
    ],

    'pending_note' => 'This invoice is still pending. If you chose bank transfer, contact support after sending the transfer. For electronic payments it takes a few minutes.',
    'refund_confirm' => 'Are you sure you want to request a refund for invoice :number? This cancels the subscription and refunds the full amount.',
    'refunded_flash' => 'Refund request submitted. The amount will return to your original payment method within 5-10 business days.',
];
