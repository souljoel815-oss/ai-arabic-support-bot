<?php

namespace App\Services\Licences;

use App\Models\Licence;

/**
 * T062 per FR-014 + data-model.md §5 validation rules.
 *
 * Two checks:
 *   1. Format — XXXX-XXXX-XXXX-XXXX hex (matches the on-prem product's
 *      HWID derivation).
 *   2. Cross-customer collision — the hwid isn't already bound to a
 *      different ACTIVE licence in any other customer organisation.
 *      Backed at the DB level by the filtered-unique index on hwid
 *      WHERE retired_at IS NULL.
 */
class HwidValidator
{
    public const HWID_PATTERN = '/^[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}$/';

    public function isWellFormed(string $hwid): bool
    {
        return (bool) preg_match(self::HWID_PATTERN, $hwid);
    }

    /**
     * Returns the Licence id of an existing ACTIVE licence with this
     * hwid, or null if none. Used to reject ActivatePaidLicence
     * attempts that would collide with an existing customer's machine.
     */
    public function findActiveCollisionLicenceId(string $hwid, ?string $excludeLicenceId = null): ?string
    {
        $query = Licence::query()
            ->where('hwid', $hwid)
            ->whereNull('retired_at');

        if ($excludeLicenceId !== null) {
            $query->where('id', '!=', $excludeLicenceId);
        }

        return $query->value('id');
    }
}
