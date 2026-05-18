<?php

return [
    'title' => 'Subscription',
    'subtitle' => 'Manage your plan, billing cycle, and cancellation.',
    'no_active' => 'No active paid subscription yet. Start your plan from the "Subscribe to a plan" button.',
    'subscribe_cta' => 'Subscribe to a plan',

    'status' => [
        'Active' => 'Active',
        'PastDue' => 'Past due',
        'Cancelled' => 'Cancelled',
        'Paused' => 'Paused',
    ],
    'cadence' => [
        'Monthly' => 'Monthly',
        'Annual' => 'Annual',
    ],
    'payment_method' => [
        'Card' => 'Card (Visa / Mastercard)',
        'Fawry' => 'Fawry',
        'InstaPay' => 'InstaPay',
        'VodafoneCash' => 'Vodafone Cash',
        'BankTransfer' => 'Bank transfer',
    ],

    'columns' => [
        'tier' => 'Tier',
        'cadence' => 'Billing',
        'status' => 'Status',
        'period_start' => 'Period start',
        'period_end' => 'Period end',
        'cancelled_at' => 'Cancelled at',
    ],

    'will_cancel_notice' => 'Subscription will end automatically on :date.',

    'start' => [
        'title' => 'Choose your plan',
        'subtitle' => 'Prices include VAT (14%). You can upgrade or downgrade later.',
        'tier_label' => 'Tier',
        'cadence_label' => 'Billing cycle',
        'payment_method_label' => 'Payment method',
        'payment_method_note' => 'Electronic payments (Card/Fawry/InstaPay/VodafoneCash) go through Paymob. Bank transfer requires manual review by the support team.',
        'submit' => 'Create subscription',
        'cancel' => 'Cancel',
        'has_active_warning' => 'You already have an active subscription. To switch tiers use the "Upgrade" button on the Subscription page.',
    ],

    'cancel' => [
        'title' => 'Cancel subscription',
        'confirm' => 'Are you sure you want to cancel this subscription? It stays active until the end of the current period (:date) then will not renew.',
        'submit' => 'Confirm cancellation',
        'cancel_action' => 'Go back',
    ],

    'started_flash' => 'Subscription created. Invoice :number ready for payment on the Billing page.',
    'cancelled_flash' => 'Cancelled. Subscription stays active until the end of the current period.',
];
