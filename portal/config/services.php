<?php

return [

    /*
    |--------------------------------------------------------------------------
    | Third Party Services
    |--------------------------------------------------------------------------
    |
    | This file is for storing the credentials for third party services such
    | as Mailgun, Postmark, AWS and more. This file provides the de facto
    | location for this type of information, allowing packages to have
    | a conventional file to locate the various service credentials.
    |
    */

    'postmark' => [
        'token' => env('POSTMARK_TOKEN'),
    ],

    'ses' => [
        'key' => env('AWS_ACCESS_KEY_ID'),
        'secret' => env('AWS_SECRET_ACCESS_KEY'),
        'region' => env('AWS_DEFAULT_REGION', 'us-east-1'),
    ],

    'slack' => [
        'notifications' => [
            'bot_user_oauth_token' => env('SLACK_BOT_USER_OAUTH_TOKEN'),
            'channel' => env('SLACK_BOT_USER_DEFAULT_CHANNEL'),
        ],
    ],

    /*
    | -----------------------------------------------------------------------
    | Paymob — Egypt PSP (T088 / T089). Per research §3 + FR-015.
    | -----------------------------------------------------------------------
    |
    | `api_base`   — Paymob's v3 base URL (sandbox uses `accept.paymobsolutions.com`).
    | `api_key`    — long-lived secret used for the `/auth/tokens` exchange.
    | `hmac_secret` — Paymob signs webhook payloads with this. Rotate via the
    |                 dashboard whenever credentials change.
    | `integration_ids` — one numeric ID per payment method (card, fawry,
    |                 vodafone_cash, instapay). The adapter picks the right
    |                 one based on the Invoice's payment_method column.
    */
    'paymob' => [
        'api_base' => env('PAYMOB_API_BASE', 'https://accept.paymob.com/api'),
        'api_key' => env('PAYMOB_API_KEY'),
        'hmac_secret' => env('PAYMOB_HMAC_SECRET'),
        'integration_ids' => [
            'card' => env('PAYMOB_INTEGRATION_ID_CARD'),
            'fawry' => env('PAYMOB_INTEGRATION_ID_FAWRY'),
            'vodafone_cash' => env('PAYMOB_INTEGRATION_ID_VODAFONE_CASH'),
            'instapay' => env('PAYMOB_INTEGRATION_ID_INSTAPAY'),
        ],
    ],

    /*
    | -----------------------------------------------------------------------
    | Cloudflare — cache purge after deploy (T144).
    | -----------------------------------------------------------------------
    */
    'cloudflare' => [
        'zone_id' => env('CLOUDFLARE_ZONE_ID'),
        'api_token' => env('CLOUDFLARE_API_TOKEN'),
    ],

];
