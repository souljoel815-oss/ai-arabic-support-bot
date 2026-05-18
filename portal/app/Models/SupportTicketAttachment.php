<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class SupportTicketAttachment extends Model
{
    use HasUuids;

    protected $table = 'support_ticket_attachments';

    protected $keyType = 'string';

    public $incrementing = false;

    protected $fillable = [
        'support_ticket_id',
        'original_filename',
        'mime_type',
        'size_bytes',
        'storage_path',
    ];

    protected function casts(): array
    {
        return [
            'size_bytes' => 'integer',
        ];
    }

    public function ticket(): BelongsTo
    {
        return $this->belongsTo(SupportTicket::class, 'support_ticket_id');
    }
}
