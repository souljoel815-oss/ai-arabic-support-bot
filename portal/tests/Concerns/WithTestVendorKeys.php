<?php

namespace Tests\Concerns;

/**
 * Tests that use LicenceSigningService need a vendor-keys.json file on
 * disk + the licence.vendor_keys_path config pointed at it. This trait
 * generates a fresh Ed25519 keypair into the test's storage_path() at
 * setUp time and tears it down after.
 */
trait WithTestVendorKeys
{
    private string $testKeysPath;

    protected function setUpTestVendorKeys(): void
    {
        $keypair = sodium_crypto_sign_keypair();
        $publicKey = sodium_crypto_sign_publickey($keypair);
        $secretKey = sodium_crypto_sign_secretkey($keypair);

        $this->testKeysPath = storage_path('test-vendor-keys.json');
        file_put_contents($this->testKeysPath, json_encode([
            'publicKeyBase64' => base64_encode($publicKey),
            'privateKeyBase64' => base64_encode($secretKey),
        ], JSON_UNESCAPED_SLASHES));

        config(['licence.vendor_keys_path' => $this->testKeysPath]);
    }

    protected function tearDownTestVendorKeys(): void
    {
        if (isset($this->testKeysPath) && file_exists($this->testKeysPath)) {
            @unlink($this->testKeysPath);
        }
    }
}
