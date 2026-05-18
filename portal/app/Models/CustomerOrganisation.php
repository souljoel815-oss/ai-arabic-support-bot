<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsToMany;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * T022 per data-model.md §1. Top-level account that owns subscriptions,
 * licences, invoices, tickets, and team-member memberships. One per real
 * business customer.
 *
 * Soft-delete via the spec's FR-024 30-day window: set `soft_deleted_at`
 * to mark for purge; the nightly PurgeSoftDeletedAccounts command
 * (T142) hard-deletes 30 days later (audit-log rows are preserved per
 * data-model.md §9).
 */
class CustomerOrganisation extends Model
{
    use HasFactory;
    use HasUuids;

    protected $table = 'customer_organisations';

    protected $keyType = 'string';

    public $incrementing = false;

    protected $fillable = [
        'legal_name_ar',
        'legal_name_en',
        'tax_registration_number',
        'billing_email',
        'billing_phone',
        'billing_address',
        'country_code',
        'requires_mfa_for_owners',
    ];

    protected function casts(): array
    {
        return [
            'billing_address' => 'array',
            'requires_mfa_for_owners' => 'boolean',
            'soft_deleted_at' => 'datetime',
        ];
    }

    public function teamMembers(): BelongsToMany
    {
        return $this->belongsToMany(
            TeamMember::class,
            'organisation_memberships',
            'customer_organisation_id',
            'team_member_id'
        )
            ->using(OrganisationMembership::class)
            ->withPivot(['id', 'role', 'invited_at', 'accepted_at', 'revoked_at', 'security_stamp_version'])
            ->withTimestamps();
    }

    public function auditLogEntries(): HasMany
    {
        return $this->hasMany(AuditLogEntry::class);
    }
}
