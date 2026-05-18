<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Model;

/**
 * T079 (early — landed with US1's Contact page). Per data-model.md §7.
 */
class SalesLead extends Model
{
    use HasUuids;

    protected $table = 'sales_leads';

    protected $keyType = 'string';

    public $incrementing = false;

    protected $fillable = [
        'name',
        'email',
        'phone',
        'interested_tier',
        'referrer_page',
        'message',
    ];

    protected function casts(): array
    {
        return [
            'last_contacted_at' => 'datetime',
        ];
    }
}
