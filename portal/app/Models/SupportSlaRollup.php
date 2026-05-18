<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

/**
 * T156 — daily aggregate. Each row covers one calendar day for one tier
 * bucket. The "ALL" tier_bucket holds the cross-tier total for the
 * dashboard's headline metric.
 */
class SupportSlaRollup extends Model
{
    use HasFactory;
    use HasUuids;

    protected $table = 'support_sla_rollups';

    protected $keyType = 'string';

    public $incrementing = false;

    public const BUCKET_ALL = 'ALL';

    protected $fillable = [
        'rollup_date',
        'tier_bucket',
        'total_tickets',
        'replies_within_sla',
        'percent_within_sla',
    ];

    protected function casts(): array
    {
        return [
            'rollup_date' => 'date',
            'total_tickets' => 'integer',
            'replies_within_sla' => 'integer',
            'percent_within_sla' => 'decimal:2',
        ];
    }
}
