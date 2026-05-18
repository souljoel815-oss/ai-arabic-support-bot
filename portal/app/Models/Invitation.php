<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * T121 per data-model.md §10 + FR-020.
 *
 * The HASH-AT-REST contract: NEVER store the raw token here. The
 * raw token is generated once in InviteMemberService, base64url-
 * encoded into the invitation URL emailed to the invitee, and
 * never touches the DB. token_hash is SHA-256 of the raw token —
 * AcceptInvitationService re-hashes the URL token on click and
 * compares.
 */
class Invitation extends Model
{
    use HasUuids;

    protected $table = 'invitations';

    protected $keyType = 'string';

    public $incrementing = false;

    public const ROLES = [
        OrganisationMembership::ROLE_OWNER,
        OrganisationMembership::ROLE_BILLING_ADMIN,
        OrganisationMembership::ROLE_SUPPORT_ADMIN,
        OrganisationMembership::ROLE_READ_ONLY,
    ];

    public const DEFAULT_EXPIRY_DAYS = 7;

    protected $fillable = [
        'customer_organisation_id',
        'email',
        'role',
        'display_name',
        'locale_preference',
        'token_hash',
        'invited_by_team_member_id',
        'expires_at',
        'accepted_at',
    ];

    protected function casts(): array
    {
        return [
            'expires_at' => 'datetime',
            'accepted_at' => 'datetime',
        ];
    }

    public function customerOrganisation(): BelongsTo
    {
        return $this->belongsTo(CustomerOrganisation::class);
    }

    public function invitedBy(): BelongsTo
    {
        return $this->belongsTo(TeamMember::class, 'invited_by_team_member_id');
    }

    public function isExpired(): bool
    {
        return $this->expires_at !== null && $this->expires_at->isPast();
    }

    public function isAccepted(): bool
    {
        return $this->accepted_at !== null;
    }

    public function isLive(): bool
    {
        return ! $this->isAccepted() && ! $this->isExpired();
    }

    public static function hashToken(string $rawToken): string
    {
        return hash('sha256', $rawToken);
    }
}
