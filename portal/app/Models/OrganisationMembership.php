<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Relations\Pivot;

/**
 * T023 per data-model.md §3. The many-to-many link between team_members
 * and customer_organisations. Carries a per-row role + lifecycle
 * timestamps (invited / accepted / revoked).
 *
 * Used as the Pivot class via `->using(OrganisationMembership::class)`
 * on the parent models so accessing the join row through
 * `$user->customerOrganisations->first()->pivot` gives a real Eloquent
 * instance with relationships + helper methods, not a generic Pivot.
 */
class OrganisationMembership extends Pivot
{
    use HasUuids;

    protected $table = 'organisation_memberships';

    protected $keyType = 'string';

    public $incrementing = false;

    public const ROLE_OWNER = 'Owner';
    public const ROLE_BILLING_ADMIN = 'BillingAdmin';
    public const ROLE_SUPPORT_ADMIN = 'SupportAdmin';
    public const ROLE_READ_ONLY = 'ReadOnly';

    public const ROLES = [
        self::ROLE_OWNER,
        self::ROLE_BILLING_ADMIN,
        self::ROLE_SUPPORT_ADMIN,
        self::ROLE_READ_ONLY,
    ];

    protected $fillable = [
        'customer_organisation_id',
        'team_member_id',
        'role',
        'invited_at',
        'accepted_at',
        'revoked_at',
        'security_stamp_version',
    ];

    protected function casts(): array
    {
        return [
            'invited_at' => 'datetime',
            'accepted_at' => 'datetime',
            'revoked_at' => 'datetime',
            'security_stamp_version' => 'integer',
        ];
    }

    public function isActive(): bool
    {
        return $this->revoked_at === null && $this->accepted_at !== null;
    }
}
