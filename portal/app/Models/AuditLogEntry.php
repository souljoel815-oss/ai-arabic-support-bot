<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * T024 per data-model.md §9. Immutable record of every customer-visible
 * state change (FR-023). Retained INDEFINITELY — never purged by the
 * soft-delete job.
 *
 * Inserts MUST go through AuditLogWriter::write() — that service
 * enforces the forbidden-substring guard on the payload so tokens /
 * password hashes / PII never end up here.
 */
class AuditLogEntry extends Model
{
    use HasUuids;

    protected $table = 'audit_log_entries';

    protected $keyType = 'string';

    public $incrementing = false;

    protected $fillable = [
        'customer_organisation_id',
        'actor_team_member_id',
        'actor_display_name_snapshot',
        'verb',
        'subject_kind',
        'subject_id',
        'payload_json',
        'originating_ip',
        'occurred_at',
    ];

    protected function casts(): array
    {
        return [
            'payload_json' => 'array',
            'occurred_at' => 'datetime',
        ];
    }

    public function customerOrganisation(): BelongsTo
    {
        return $this->belongsTo(CustomerOrganisation::class);
    }
}
