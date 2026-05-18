<?php

namespace App\Services\Licences;

use Illuminate\Support\Facades\Log;
use RuntimeException;

/**
 * T028 per FR-031 + research §10. Signs paid licence tokens.
 *
 * Reads the vendor Ed25519 keypair from <c>vendor-keys.json</c> (path
 * comes from <c>config('licence.vendor_keys_path')</c>). Production has
 * the file mounted as a secret on Hostinger; dev pulls it from the
 * vendor's password manager into the repo root (it's .gitignored).
 *
 * Uses PHP's built-in <c>sodium_crypto_sign_detached()</c> which
 * implements RFC 8032 — produces the same 64-byte Ed25519 signature
 * the on-prem product's BouncyCastle <c>Ed25519Signer</c> generates,
 * so customer installs accept portal-issued tokens unchanged.
 *
 * Wire-format envelope produced:
 *   {
 *     "payload": { ... },
 *     "signature": "<base64 of the 64-byte Ed25519 signature>"
 *   }
 *
 * The keypair file is expected to contain:
 *   {
 *     "publicKeyBase64": "<base64 of 32-byte Ed25519 public key>",
 *     "privateKeyBase64": "<base64 of 32-byte Ed25519 private key seed>"
 *   }
 *
 * sodium uses a 64-byte secret key internally (seed || public-key). If
 * the keypair file stores just the 32-byte seed, this service derives
 * the full secret-key at load time via sodium_crypto_sign_seed_keypair.
 */
class LicenceSigningService
{
    private ?string $cachedSecretKey = null;
    private ?string $cachedPublicKey = null;

    public function __construct(
        private readonly LicencePayloadCanonicalizer $canonicalizer,
    ) {
    }

    /**
     * Sign the given payload and return the wire-format envelope as a
     * UTF-8 JSON string the customer downloads as license.token.
     *
     * @param  array<string,mixed>  $payload
     * @return array{payload: array<string,mixed>, signature: string}
     */
    public function sign(array $payload): array
    {
        [$secretKey, $_] = $this->loadKeys();

        $canonical = $this->canonicalizer->canonicalBytes($payload);
        $signature = sodium_crypto_sign_detached($canonical, $secretKey);

        Log::info(
            "Signed licence for HWID {$payload['hwid']} edition {$payload['edition']} expiring {$payload['expiresAtUtc']}"
        );

        // The on-prem record type stores Payload (object) + Signature
        // (base64 string). We mirror that here so the consumer doesn't
        // need to redo the base64 encoding.
        return [
            'payload' => $payload,
            'signature' => base64_encode($signature),
        ];
    }

    /**
     * Verify an envelope's signature against the vendor public key.
     * Mostly used by tests + diagnostics — real verification happens on
     * the customer's machine in the on-prem product's LicenseVerifier.
     *
     * @param  array{payload: array<string,mixed>, signature: string}  $envelope
     */
    public function verify(array $envelope): bool
    {
        [$_, $publicKey] = $this->loadKeys();

        $signature = base64_decode($envelope['signature'], strict: true);
        if ($signature === false || strlen($signature) !== SODIUM_CRYPTO_SIGN_BYTES) {
            return false;
        }

        $canonical = $this->canonicalizer->canonicalBytes($envelope['payload']);

        return sodium_crypto_sign_verify_detached($signature, $canonical, $publicKey);
    }

    /**
     * @return array{0: string, 1: string} [secretKey64, publicKey32]
     */
    private function loadKeys(): array
    {
        if ($this->cachedSecretKey !== null && $this->cachedPublicKey !== null) {
            return [$this->cachedSecretKey, $this->cachedPublicKey];
        }

        $path = config('licence.vendor_keys_path');
        if (! is_string($path) || trim($path) === '') {
            throw new RuntimeException(
                "config('licence.vendor_keys_path') is not set. Configure the vendor keypair path in config/licence.php + .env."
            );
        }
        if (! file_exists($path)) {
            throw new RuntimeException(
                "Vendor keypair file not found at '{$path}'. Pull it from the vendor password manager and drop it at this path."
            );
        }

        $raw = file_get_contents($path);
        $keys = json_decode($raw, associative: true);
        if (! is_array($keys)) {
            throw new RuntimeException("Could not parse vendor keypair file at '{$path}'.");
        }

        $publicBase64 = $keys['publicKeyBase64'] ?? null;
        $privateBase64 = $keys['privateKeyBase64'] ?? null;
        if (! is_string($publicBase64) || ! is_string($privateBase64)) {
            throw new RuntimeException(
                "Vendor keypair file at '{$path}' is missing publicKeyBase64 or privateKeyBase64 fields."
            );
        }

        $publicKey = base64_decode($publicBase64, strict: true);
        $privateKey = base64_decode($privateBase64, strict: true);
        if ($publicKey === false || $privateKey === false) {
            throw new RuntimeException(
                "Vendor keypair file at '{$path}' contains malformed base64."
            );
        }
        if (strlen($publicKey) !== SODIUM_CRYPTO_SIGN_PUBLICKEYBYTES) {
            throw new RuntimeException(
                "Vendor public key must be ".SODIUM_CRYPTO_SIGN_PUBLICKEYBYTES." bytes (Ed25519); got ".strlen($publicKey)."."
            );
        }

        // sodium needs a 64-byte secret key (seed || pk). If the file
        // contains just the 32-byte seed, derive the full secret key.
        $secretKey = match (strlen($privateKey)) {
            SODIUM_CRYPTO_SIGN_SECRETKEYBYTES => $privateKey,
            SODIUM_CRYPTO_SIGN_SEEDBYTES => sodium_crypto_sign_secretkey(
                sodium_crypto_sign_seed_keypair($privateKey)
            ),
            default => throw new RuntimeException(
                "Vendor private key must be ".SODIUM_CRYPTO_SIGN_SEEDBYTES." (seed) or ".SODIUM_CRYPTO_SIGN_SECRETKEYBYTES." (full secret) bytes; got ".strlen($privateKey)."."
            ),
        };

        $this->cachedSecretKey = $secretKey;
        $this->cachedPublicKey = $publicKey;

        return [$secretKey, $publicKey];
    }
}
