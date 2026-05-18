<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * T107 per data-model.md §6 (Support Ticket entity).
 */
class SupportTicket extends Model
{
    use HasFactory;
    use HasUuids;

    protected $table = 'support_tickets';

    protected $keyType = 'string';

    public $incrementing = false;

    public const STATUS_OPEN = 'Open';
    public const STATUS_IN_PROGRESS = 'InProgress';
    public const STATUS_RESOLVED = 'Resolved';
    public const STATUS_CLOSED = 'Closed';

    public const CATEGORY_BILLING = 'Billing';
    public const CATEGORY_BUG = 'Bug';
    public const CATEGORY_FEATURE_REQUEST = 'FeatureRequest';
    public const CATEGORY_ACCOUNTING_QUESTION = 'AccountingQuestion';
    public const CATEGORY_URGENT = 'Urgent';

    public const CATEGORIES = [
        self::CATEGORY_BILLING,
        self::CATEGORY_BUG,
        self::CATEGORY_FEATURE_REQUEST,
        self::CATEGORY_ACCOUNTING_QUESTION,
        self::CATEGORY_URGENT,
    ];

    public const PRIORITY_LOW = 'Low';
    public const PRIORITY_NORMAL = 'Normal';
    public const PRIORITY_HIGH = 'High';

    protected $fillable = [
        'customer_organisation_id',
        'opened_by_team_member_id',
        'category',
        'priority',
        'subject',
        'body',
        'status',
        'assigned_vendor_staff_id',
        'opened_at',
        'first_reply_at',
        'resolved_at',
        'closed_at',
    ];

    protected function casts(): array
    {
        return [
            'opened_at' => 'datetime',
            'first_reply_at' => 'datetime',
            'resolved_at' => 'datetime',
            'closed_at' => 'datetime',
        ];
    }

    public function customerOrganisation(): BelongsTo
    {
        return $this->belongsTo(CustomerOrganisation::class);
    }

    public function openedBy(): BelongsTo
    {
        return $this->belongsTo(TeamMember::class, 'opened_by_team_member_id');
    }

    public function replies(): HasMany
    {
        return $this->hasMany(SupportTicketReply::class)->orderBy('created_at');
    }

    public function attachments(): HasMany
    {
        return $this->hasMany(SupportTicketAttachment::class);
    }
}
