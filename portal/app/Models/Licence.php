<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * T061 per data-model.md §5. Signed Ed25519 token bound to one
 * hardware id. Created by ActivatePaidLicenceService; retired (with
 * a retired_reason) by TransferLicenceService / RefundFirstPeriodService /
 * CancelSubscriptionService when the subscription state changes.
 *
 * NEVER mutate signed_token_base64 in place — generate a fresh row
 * for any tier change (TierUpgraded retire reason on the old, new row
 * at the new edition).
 */
class Licence extends Model
{
    use HasFactory;
    use HasUuids;

    protected $table = 'licences';

    protected $keyType = 'string';

    public $incrementing = false;

    public const REASON_TRANSFERRED = 'Transferred';
    public const REASON_SUBSCRIPTION_CANCELLED = 'SubscriptionCancelled';
    public const REASON_REFUNDED = 'Refunded';
    public const REASON_TIER_UPGRADED = 'TierUpgraded';

    protected $fillable = [
        'subscription_id',
        'hwid',
        'edition',
        'signed_token_base64',
        'issued_at',
        'expires_at',
        'retired_at',
        'retired_reason',
    ];

    protected function casts(): array
    {
        return [
            'issued_at' => 'datetime',
            'expires_at' => 'datetime',
            'retired_at' => 'datetime',
        ];
    }

    public function subscription(): BelongsTo
    {
        return $this->belongsTo(Subscription::class);
    }

    public function isActive(): bool
    {
        return $this->retired_at === null && $this->expires_at?->isFuture() === true;
    }
}
