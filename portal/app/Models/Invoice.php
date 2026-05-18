<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Casts\Attribute;
use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * T078 per data-model.md §6 + FR-016 + FR-034.
 *
 * The computed `refund_eligibility` accessor implements FR-034 — returns
 * one of:
 *   - eligible
 *   - not_eligible_renewal       (kind != FirstPeriod)
 *   - not_eligible_window_expired (Paid > 7 days ago)
 *   - not_eligible_already_refunded
 *   - not_eligible_not_paid
 * Computed rather than stored so we can tweak the rule without a
 * migration (the spec says "no refund after 7 days" but a future
 * regulator change could push that to 14 — accessor lets us adjust).
 */
class Invoice extends Model
{
    use HasFactory;
    use HasUuids;

    protected $table = 'invoices';

    protected $keyType = 'string';

    public $incrementing = false;

    public const KIND_FIRST_PERIOD = 'FirstPeriod';
    public const KIND_RENEWAL = 'Renewal';
    public const KIND_UPGRADE = 'Upgrade';
    public const KIND_REFUND = 'Refund';

    public const STATUS_PENDING = 'Pending';
    public const STATUS_PAID = 'Paid';
    public const STATUS_REFUNDED = 'Refunded';
    public const STATUS_FAILED = 'Failed';

    public const REFUND_WINDOW_DAYS = 7;

    protected $fillable = [
        'customer_organisation_id',
        'subscription_id',
        'invoice_number',
        'kind',
        'payment_method',
        'amount_egp_minor',
        'vat_egp_minor',
        'paymob_transaction_id',
        'status',
        'pdf_storage_path',
        'paid_at',
        'refunded_at',
    ];

    protected function casts(): array
    {
        return [
            'amount_egp_minor' => 'integer',
            'vat_egp_minor' => 'integer',
            'paid_at' => 'datetime',
            'refunded_at' => 'datetime',
        ];
    }

    public function customerOrganisation(): BelongsTo
    {
        return $this->belongsTo(CustomerOrganisation::class);
    }

    public function subscription(): BelongsTo
    {
        return $this->belongsTo(Subscription::class);
    }

    /** FR-034 — 7-day first-period-only refund eligibility. */
    protected function refundEligibility(): Attribute
    {
        return Attribute::make(get: function (): string {
            if ($this->status !== self::STATUS_PAID) {
                return 'not_eligible_not_paid';
            }
            if ($this->kind !== self::KIND_FIRST_PERIOD) {
                return 'not_eligible_renewal';
            }
            if ($this->paid_at === null || $this->paid_at->diffInDays(now()) > self::REFUND_WINDOW_DAYS) {
                return 'not_eligible_window_expired';
            }
            // Check for an already-issued refund pointing at this invoice.
            if (self::query()
                ->where('subscription_id', $this->subscription_id)
                ->where('kind', self::KIND_REFUND)
                ->exists()
            ) {
                return 'not_eligible_already_refunded';
            }
            return 'eligible';
        });
    }

    /** Convenience: format piasters as EGP decimal for display. */
    public function getAmountEgpAttribute(): string
    {
        return number_format($this->amount_egp_minor / 100, 2);
    }
}
