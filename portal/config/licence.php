<?php

/**
 * T010 — Licence signing config. The vendor Ed25519 keypair lives in a
 * JSON file outside the repo (.gitignored) and the path is set via env
 * for portability across dev / staging / production.
 *
 * Production: file is mounted as a Hostinger secret at the configured
 * absolute path. Dev: typically `<repo-root>/vendor-keys.json`.
 */
return [
    'vendor_keys_path' => env('LICENCE_VENDOR_KEYS_PATH', base_path('../vendor-keys.json')),
];
