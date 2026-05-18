<?php

/**
 * T069 (en) — US2 licence-management UI strings.
 */
return [
    'title' => 'Licence management',
    'subtitle' => 'All licences across your subscriptions. Download a licence file, transfer to a new device, or track expiry dates.',
    'no_subscriptions' => 'No paid subscription yet. Subscribe to a plan to activate licences.',
    'subscribe_cta' => 'Subscribe to a plan',
    'priority_support_badge' => 'Priority support (4 business hours)',
    'tier_label' => 'Tier',
    'cadence_label' => 'Billing',
    'status_label' => 'Status',
    'period_end_label' => 'Current period ends',
    'cadence' => [
        'Monthly' => 'Monthly',
        'Annual' => 'Annual',
    ],
    'status' => [
        'Active' => 'Active',
        'PastDue' => 'Past due',
        'Cancelled' => 'Cancelled',
        'Paused' => 'Paused',
    ],
    'licences_list' => [
        'title' => 'Active licences',
        'empty' => 'No active licences on this subscription. Click "Activate a new licence".',
        'columns' => [
            'hwid' => 'Hardware id (HWID)',
            'edition' => 'Edition',
            'issued_at' => 'Issued',
            'expires_at' => 'Expires',
            'actions' => 'Actions',
        ],
        'actions' => [
            'download' => 'Download licence file',
            'transfer' => 'Transfer to another device',
        ],
        'retired_section_title' => 'Retired licences (archived)',
        'retired_columns' => [
            'reason' => 'Retire reason',
            'retired_at' => 'Retired',
        ],
        'reason' => [
            'Transferred' => 'Transferred',
            'SubscriptionCancelled' => 'Subscription cancelled',
            'Refunded' => 'Refunded',
            'TierUpgraded' => 'Tier upgraded',
        ],
    ],
    'activate' => [
        'title' => 'Activate paid licence',
        'instructions' => 'Open DaftarX on the target device. The activation screen shows a HWID in the form XXXX-XXXX-XXXX-XXXX. Copy and paste it here.',
        'subscription_label' => 'Subscription',
        'hwid_label' => 'Hardware id (HWID)',
        'hwid_placeholder' => 'Example: A1B2-C3D4-E5F6-7890',
        'submit' => 'Activate licence',
        'cancel' => 'Cancel',
    ],
    'transfer' => [
        'title' => 'Transfer licence to another device',
        'instructions' => 'Transfer retires the current licence on the old device + issues a fresh licence at the same edition + expiry on the new device.',
        'warning' => 'Warning: the old device will no longer be able to use the product after the transfer completes.',
        'old_hwid_label' => 'Current device',
        'new_hwid_label' => 'New hardware id (HWID)',
        'new_hwid_placeholder' => 'Example: 7890-E5F6-C3D4-A1B2',
        'edition_label' => 'Edition',
        'expires_at_label' => 'Expires',
        'submit' => 'Confirm transfer',
        'cancel' => 'Cancel',
    ],
    'activated_flash' => 'Licence activated. The license.token download is ready now.',
    'transferred_flash' => 'Transfer completed. The new license.token download is ready now.',
];
