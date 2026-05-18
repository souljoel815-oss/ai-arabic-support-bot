<?php

namespace App\Models;

use App\Mail\SignupConfirmation;
use App\Services\Email\ResendMailer;
use Illuminate\Contracts\Auth\MustVerifyEmail;
use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Relations\BelongsToMany;
use Illuminate\Foundation\Auth\User as Authenticatable;
use Illuminate\Notifications\Notifiable;
use Illuminate\Support\Carbon;
use Illuminate\Support\Facades\URL;

/**
 * T021 per data-model.md §2 + FR-032. The portal's authenticated user.
 * Completely separate from the on-prem product's user store (no SSO, no
 * cross-system password sync). Replaces Breeze's default App\Models\User.
 *
 * Auth-related columns (email, password, email_verified_at, remember_token)
 * are inherited from Authenticatable + are wired by Breeze. The extra
 * columns (locale_preference, display_name, mfa_*, last_login_at,
 * soft_deleted_at) are this model's own additions per the spec.
 *
 * UUID PK via HasUuids — no incrementing integer leak about how many
 * customers signed up. Auth scaffolding in Breeze adapts automatically
 * because we set $keyType = 'string' + $incrementing = false.
 */
class TeamMember extends Authenticatable implements MustVerifyEmail
{
    use HasFactory;
    use HasUuids;
    use Notifiable;

    protected $table = 'team_members';

    protected $keyType = 'string';

    public $incrementing = false;

    /**
     * Mass-assignable fields (used by signup / invitation flows).
     */
    protected $fillable = [
        'display_name',
        'email',
        'password',
        'locale_preference',
    ];

    /**
     * Always hidden from JSON / array casts — these are sensitive
     * (password is a hash, mfa_secret is the TOTP shared secret,
     * remember_token is a session key).
     */
    protected $hidden = [
        'password',
        'remember_token',
        'mfa_secret',
    ];

    protected function casts(): array
    {
        return [
            'email_verified_at' => 'datetime',
            'mfa_secret' => 'encrypted',
            'mfa_enabled_at' => 'datetime',
            'last_login_at' => 'datetime',
            'soft_deleted_at' => 'datetime',
            'password' => 'hashed',
        ];
    }

    /**
     * Override Laravel's default plain-text verification notification.
     * We dispatch our branded bilingual `SignupConfirmation` Mailable
     * instead, picking ar / en based on this member's locale_preference.
     *
     * The verification URL is built exactly the same way Laravel's stock
     * notification builds it (60-min signed temporary URL), so the
     * `verification.verify` route handles it unchanged.
     */
    public function sendEmailVerificationNotification(): void
    {
        $verificationUrl = URL::temporarySignedRoute(
            'verification.verify',
            Carbon::now()->addMinutes((int) config('auth.verification.expire', 60)),
            [
                'id' => $this->getKey(),
                'hash' => sha1($this->getEmailForVerification()),
            ],
        );

        $mailable = new SignupConfirmation(
            localePreference: $this->locale_preference ?? 'ar-EG',
            displayName: $this->display_name ?? $this->email,
            verificationUrl: $verificationUrl,
        );

        app(ResendMailer::class)->send($mailable, $this->email, $this->display_name);
    }

    /**
     * Customer organisations this member belongs to (many-to-many via
     * organisation_memberships). Use ->wherePivot('revoked_at', null)
     * when you only want active memberships.
     */
    public function customerOrganisations(): BelongsToMany
    {
        return $this->belongsToMany(
            CustomerOrganisation::class,
            'organisation_memberships',
            'team_member_id',
            'customer_organisation_id'
        )
            ->using(OrganisationMembership::class)
            ->withPivot(['id', 'role', 'invited_at', 'accepted_at', 'revoked_at', 'security_stamp_version'])
            ->withTimestamps();
    }
}
