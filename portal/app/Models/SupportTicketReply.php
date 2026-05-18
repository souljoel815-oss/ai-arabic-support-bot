<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class SupportTicketReply extends Model
{
    use HasUuids;

    protected $table = 'support_ticket_replies';

    protected $keyType = 'string';

    public $incrementing = false;

    public const AUTHOR_CUSTOMER = 'Customer';
    public const AUTHOR_VENDOR_STAFF = 'VendorStaff';

    protected $fillable = [
        'support_ticket_id',
        'author_kind',
        'author_team_member_id',
        'author_vendor_staff_id',
        'body',
    ];

    public function ticket(): BelongsTo
    {
        return $this->belongsTo(SupportTicket::class, 'support_ticket_id');
    }

    public function authorTeamMember(): BelongsTo
    {
        return $this->belongsTo(TeamMember::class, 'author_team_member_id');
    }
}
