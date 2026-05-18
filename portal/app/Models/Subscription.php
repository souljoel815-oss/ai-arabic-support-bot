<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * T060 per data-model.md §4. Carries the commercial state of one
 * customer's plan (tier + cadence + status + period boundaries).
 *
 * Lifecycle (state machine, enforced by services not at the DB level):
 *   [Active]  ──Owner upgrades tier──▶  [Active] (tier flipped + Invoice for proration)
 *   [Active]  ──Owner downgrades──▶     [Active + pending_tier_change_to set]
 *                                       ──renewal job──▶ [Active at new tier]
 *   [Active]  ──renewal fails──▶        [PastDue] ──N retries fail──▶ [Cancelled]
 *   [Active]  ──Owner cancels──▶        [Active until current_period_end_at]
 *                                       ──renewal job──▶ [Cancelled]
 *   [Active]  ──Owner pauses──▶         [Paused] (licences valid until period end)
 *   [Cancelled] ──Owner resubscribes──▶ new [Active] row (old retained)
 *
 * NO `Trial` status — trial is owned client-side per FR-030.
 */
class Subscription extends Model
{
    use HasFactory;
    use HasUuids;

    protected $table = 'subscriptions';

    protected $keyType = 'string';

    public $incrementing = false;

    public const STATUS_ACTIVE = 'Active';
    public const STATUS_PAST_DUE = 'PastDue';
    public const STATUS_CANCELLED = 'Cancelled';
    public const STATUS_PAUSED = 'Paused';

    public const TIER_SOLO = 'Solo';
    public const TIER_SMB = 'SMB';
    public const TIER_ENTERPRISE = 'Enterprise';
    public const TIER_FIRM = 'Firm';

    public const TIERS = [self::TIER_SOLO, self::TIER_SMB, self::TIER_ENTERPRISE, self::TIER_FIRM];

    /** Tiers that include the Priority-support entitlement (FR-019 / US2 AS#3). */
    public const PRIORITY_SUPPORT_TIERS = [self::TIER_ENTERPRISE, self::TIER_FIRM];

    protected $fillable = [
        'customer_organisation_id',
        'tier',
        'billing_cadence',
        'payment_method_id',
        'status',
        'current_period_start_at',
        'current_period_end_at',
        'pending_tier_change_to',
        'cancelled_at',
    ];

    protected function casts(): array
    {
        return [
            'current_period_start_at' => 'datetime',
            'current_period_end_at' => 'datetime',
            'cancelled_at' => 'datetime',
        ];
    }

    public function customerOrganisation(): BelongsTo
    {
        return $this->belongsTo(CustomerOrganisation::class);
    }

    public function licences(): HasMany
    {
        return $this->hasMany(Licence::class);
    }

    public function activeLicences(): HasMany
    {
        return $this->licences()->whereNull('retired_at');
    }

    public function hasPrioritySupport(): bool
    {
        return in_array($this->tier, self::PRIORITY_SUPPORT_TIERS, true);
    }
}
