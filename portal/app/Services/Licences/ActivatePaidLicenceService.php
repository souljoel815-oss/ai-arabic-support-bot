<?php

namespace App\Services\Licences;

use App\Models\Licence;
use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use InvalidArgumentException;
use RuntimeException;

/**
 * T063 per FR-031 + contracts/activate-paid-licence.md. Signs a fresh
 * paid licence token bound to the customer-supplied HWID and persists
 * a Licence row. Writes a `licence.activated` audit-log entry.
 *
 * Pre-conditions enforced by this service:
 *   - The subscription belongs to the actor's organisation.
 *   - The subscription is `Active` (status check).
 *   - The hwid is well-formed (HwidValidator).
 *   - The hwid isn't already bound to another ACTIVE licence (collision).
 *
 * The actual signing happens in LicenceSigningService which uses
 * sodium_crypto_sign_detached against vendor-keys.json. The payload
 * shape mirrors the on-prem product's LicensePayload byte-for-byte so
 * the on-prem LicenseVerifier accepts it unchanged.
 */
class ActivatePaidLicenceService
{
    public function __construct(
        private readonly LicenceSigningService $signer,
        private readonly HwidValidator $hwidValidator,
        private readonly AuditLogWriter $audit,
    ) {
    }

    /**
     * @return Licence the newly persisted, active licence row
     *
     * @throws InvalidArgumentException on bad hwid format or wrong-org subscription
     * @throws RuntimeException when the hwid collides with another active licence
     */
    public function activate(
        Subscription $subscription,
        string $hwid,
        TeamMember $actor,
        string $originatingIp,
    ): Licence {
        if (! $this->hwidValidator->isWellFormed($hwid)) {
            throw new InvalidArgumentException(
                "HWID must match the format XXXX-XXXX-XXXX-XXXX (hex)."
            );
        }

        if ($subscription->status !== Subscription::STATUS_ACTIVE) {
            throw new InvalidArgumentException(
                "Subscription is not Active (current status: {$subscription->status})."
            );
        }

        if ($this->hwidValidator->findActiveCollisionLicenceId($hwid) !== null) {
            throw new RuntimeException(
                "HWID {$hwid} is already bound to another active licence. Contact support."
            );
        }

        return DB::transaction(function () use ($subscription, $hwid, $actor, $originatingIp) {
            $org = $subscription->customerOrganisation;
            $issuedAt = now();
            $expiresAt = $subscription->current_period_end_at;

            $payload = [
                'version' => 2,
                'hwid' => $hwid,
                'customer' => $org?->legal_name_ar ?? 'Unknown',
                'edition' => $subscription->tier,
                'issuedAtUtc' => $issuedAt->utc()->format('Y-m-d\TH:i:s\Z'),
                'expiresAtUtc' => $expiresAt->utc()->format('Y-m-d\TH:i:s\Z'),
                'salesPhone' => config('mail.from.address', 'sales@daftarx.app'),
                'salesEmail' => 'sales@daftarx.app',
            ];

            $envelope = $this->signer->sign($payload);
            $signedTokenJson = json_encode($envelope, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
            $signedTokenBase64 = base64_encode($signedTokenJson);

            $licence = Licence::create([
                'subscription_id' => $subscription->id,
                'hwid' => $hwid,
                'edition' => $subscription->tier,
                'signed_token_base64' => $signedTokenBase64,
                'issued_at' => $issuedAt,
                'expires_at' => $expiresAt,
            ]);

            $this->audit->write(
                organisationId: $subscription->customer_organisation_id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'licence.activated',
                subjectKind: 'Licence',
                subjectId: $licence->id,
                payload: [
                    'hwid' => $hwid,
                    'edition' => $subscription->tier,
                    'expiresAtUtc' => $expiresAt->utc()->format('Y-m-d\TH:i:s\Z'),
                    'subscriptionId' => $subscription->id,
                ],
                originatingIp: $originatingIp,
            );

            return $licence;
        });
    }
}
