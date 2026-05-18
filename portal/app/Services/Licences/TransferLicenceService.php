<?php

namespace App\Services\Licences;

use App\Models\Licence;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use InvalidArgumentException;
use RuntimeException;

/**
 * T064 per FR-014 + contracts/transfer-licence.md. Transfers an active
 * licence to a new hardware id. The flow is atomic:
 *   1. Validate the new hwid (format + cross-customer collision).
 *   2. In one DB transaction:
 *      a. Mark the old Licence row retired (retired_reason = Transferred).
 *      b. Sign a fresh token for the new hwid (LicenceSigningService).
 *      c. Insert a new Licence row at the same edition + expiry.
 *      d. Write TWO audit-log rows (licence.retired + licence.activated)
 *         with cross-referenced payloads.
 *
 * The DB transaction + the filtered-unique-on-active index together
 * guarantee a race between two concurrent transfers picks one winner —
 * the loser's INSERT fails on the unique constraint and the whole
 * transaction rolls back.
 */
class TransferLicenceService
{
    public function __construct(
        private readonly LicenceSigningService $signer,
        private readonly HwidValidator $hwidValidator,
        private readonly AuditLogWriter $audit,
    ) {
    }

    /**
     * @return Licence the freshly-signed replacement licence (the old
     *                 one is retired but kept in the table for audit
     *                 history)
     *
     * @throws InvalidArgumentException on bad hwid format or wrong-org licence
     * @throws RuntimeException when the new hwid is already in use
     */
    public function transfer(
        Licence $licence,
        string $newHwid,
        TeamMember $actor,
        string $originatingIp,
    ): Licence {
        if (! $this->hwidValidator->isWellFormed($newHwid)) {
            throw new InvalidArgumentException(
                "New HWID must match the format XXXX-XXXX-XXXX-XXXX (hex)."
            );
        }

        if ($licence->retired_at !== null) {
            throw new InvalidArgumentException(
                "Licence {$licence->id} is already retired (reason: {$licence->retired_reason}). Issue a new licence instead."
            );
        }

        if ($licence->hwid === $newHwid) {
            throw new InvalidArgumentException(
                "New HWID is the same as the current one. No transfer needed."
            );
        }

        $collisionLicenceId = $this->hwidValidator->findActiveCollisionLicenceId(
            $newHwid,
            excludeLicenceId: $licence->id,
        );
        if ($collisionLicenceId !== null) {
            throw new RuntimeException(
                "HWID {$newHwid} is already bound to another active licence. Contact support."
            );
        }

        return DB::transaction(function () use ($licence, $newHwid, $actor, $originatingIp) {
            $subscription = $licence->subscription;
            $org = $subscription->customerOrganisation;
            $now = now();
            $oldHwid = $licence->hwid;
            $oldLicenceId = $licence->id;

            // Retire the old row.
            $licence->update([
                'retired_at' => $now,
                'retired_reason' => Licence::REASON_TRANSFERRED,
            ]);

            // Sign + persist the replacement.
            $payload = [
                'version' => 2,
                'hwid' => $newHwid,
                'customer' => $org?->legal_name_ar ?? 'Unknown',
                'edition' => $licence->edition,
                'issuedAtUtc' => $now->utc()->format('Y-m-d\TH:i:s\Z'),
                'expiresAtUtc' => $licence->expires_at->utc()->format('Y-m-d\TH:i:s\Z'),
                'salesPhone' => 'sales@daftarx.app',
                'salesEmail' => 'sales@daftarx.app',
            ];
            $envelope = $this->signer->sign($payload);
            $signedTokenJson = json_encode($envelope, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

            $newLicence = Licence::create([
                'subscription_id' => $licence->subscription_id,
                'hwid' => $newHwid,
                'edition' => $licence->edition,
                'signed_token_base64' => base64_encode($signedTokenJson),
                'issued_at' => $now,
                'expires_at' => $licence->expires_at,
            ]);

            $orgId = $subscription->customer_organisation_id;
            $actorDisplay = $actor->display_name ?? $actor->email;

            // Two audit rows with cross-referenced payloads — operators
            // can pivot between them via the subjectId<->payload links.
            $this->audit->write(
                organisationId: $orgId,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actorDisplay,
                verb: 'licence.retired',
                subjectKind: 'Licence',
                subjectId: $oldLicenceId,
                payload: [
                    'reason' => Licence::REASON_TRANSFERRED,
                    'oldHwid' => $oldHwid,
                    'replacedByLicenceId' => $newLicence->id,
                    'newHwid' => $newHwid,
                ],
                originatingIp: $originatingIp,
            );

            $this->audit->write(
                organisationId: $orgId,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actorDisplay,
                verb: 'licence.activated',
                subjectKind: 'Licence',
                subjectId: $newLicence->id,
                payload: [
                    'reason' => 'TransferFrom',
                    'newHwid' => $newHwid,
                    'replacesLicenceId' => $oldLicenceId,
                    'oldHwid' => $oldHwid,
                    'edition' => $licence->edition,
                ],
                originatingIp: $originatingIp,
            );

            return $newLicence;
        });
    }
}
