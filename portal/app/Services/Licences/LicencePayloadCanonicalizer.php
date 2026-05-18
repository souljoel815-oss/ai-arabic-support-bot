<?php

namespace App\Services\Licences;

/**
 * T028 per research §10. Produces the canonical UTF-8 JSON byte stream
 * that gets signed with Ed25519 — MUST match what the on-prem product's
 * <c>LicensePayloadSerializer.CanonicalBytes</c> produces byte-for-byte.
 *
 * The on-prem .NET code uses System.Text.Json with WriteIndented = false
 * and JsonPropertyName attributes (all lowercase). PHP's json_encode with
 * JSON_UNESCAPED_SLASHES + JSON_UNESCAPED_UNICODE matches that output as
 * long as we control the key order via an associative-array literal (PHP
 * preserves insertion order).
 *
 * Wire format reference (from src/EgyptTax.Web/Licensing/LicenseEnvelope.cs):
 *   {
 *     "version": 1,
 *     "hwid": "ABCD-1234-EF56-7890",
 *     "customer": "...",
 *     "edition": "Solo",
 *     "issuedAtUtc": "2026-05-18T10:00:00Z",
 *     "expiresAtUtc": "2027-05-18T10:00:00Z",
 *     "salesPhone": "+20 ...",
 *     "salesEmail": "sales@daftarx.app"
 *     [, "maxUsers": N, "maxCompanies": M, "features": ["..."]]
 *   }
 *
 * v2 fields (maxUsers / maxCompanies / features) are OMITTED when null —
 * matches the on-prem JsonIgnoreCondition.WhenWritingNull behaviour.
 */
class LicencePayloadCanonicalizer
{
    /**
     * @param  array<string,mixed>  $payload
     */
    public function canonicalBytes(array $payload): string
    {
        // Enforce exact key order — PHP preserves associative-array
        // insertion order, so we rebuild the array in the on-prem order.
        $ordered = [
            'version' => $payload['version'],
            'hwid' => $payload['hwid'],
            'customer' => $payload['customer'],
            'edition' => $payload['edition'],
            'issuedAtUtc' => $payload['issuedAtUtc'],
            'expiresAtUtc' => $payload['expiresAtUtc'],
            'salesPhone' => $payload['salesPhone'],
            'salesEmail' => $payload['salesEmail'],
        ];

        // v2 fields — only included when non-null (matches the on-prem
        // JsonIgnoreCondition.WhenWritingNull behaviour).
        if (($payload['maxUsers'] ?? null) !== null) {
            $ordered['maxUsers'] = $payload['maxUsers'];
        }
        if (($payload['maxCompanies'] ?? null) !== null) {
            $ordered['maxCompanies'] = $payload['maxCompanies'];
        }
        if (($payload['features'] ?? null) !== null) {
            $ordered['features'] = $payload['features'];
        }

        $json = json_encode(
            $ordered,
            JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR
        );

        return $json;
    }
}
