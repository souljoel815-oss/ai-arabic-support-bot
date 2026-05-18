<?php

namespace App\Services\Audit;

use App\Models\AuditLogEntry;
use Illuminate\Support\Facades\Log;
use InvalidArgumentException;
use RuntimeException;

/**
 * T027 per FR-023 + FR-028. Single canonical writer for the audit log.
 * Every customer-visible state change MUST go through this — never insert
 * AuditLogEntry rows directly from a service or controller.
 *
 * The payload object is JSON-serialised; callers are responsible for
 * stripping tokens, password hashes, and PII before passing it in. The
 * writer rejects the row if it detects obvious secret-like substrings as
 * a defence-in-depth check, not a substitute for caller discipline.
 */
class AuditLogWriter
{
    /**
     * Substring patterns we never want to find in the payload. NOT a
     * security boundary — discipline at the call sites is. Last-ditch
     * check to catch developer mistakes before they hit the DB.
     */
    private const FORBIDDEN_SUBSTRINGS = [
        'password',
        'secret',
        'token',
        'api_key',
        'apikey',
        'hwid_token',
        'private_key',
        'privatekey',
        'signed_token',
    ];

    public function write(
        string $organisationId,
        ?string $actorTeamMemberId,
        string $actorDisplayNameSnapshot,
        string $verb,
        string $subjectKind,
        string $subjectId,
        array|object|null $payload,
        string $originatingIp,
    ): AuditLogEntry {
        if (trim($organisationId) === '') {
            throw new InvalidArgumentException('organisationId must be non-empty.');
        }
        if (trim($verb) === '') {
            throw new InvalidArgumentException('verb must be non-empty.');
        }
        if (trim($subjectKind) === '') {
            throw new InvalidArgumentException('subjectKind must be non-empty.');
        }

        $payloadJson = null;
        if ($payload !== null) {
            $payloadJson = is_string($payload)
                ? $payload
                : json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

            foreach (self::FORBIDDEN_SUBSTRINGS as $forbidden) {
                if (stripos($payloadJson, $forbidden) !== false) {
                    Log::error(
                        "Refusing to write audit-log row for verb {$verb}: payload contains forbidden substring '{$forbidden}'. Caller must redact before passing."
                    );
                    throw new RuntimeException(
                        "Refusing to write audit-log row for verb '{$verb}': payload contains forbidden substring '{$forbidden}'. Redact at the call site."
                    );
                }
            }
        }

        $row = AuditLogEntry::create([
            'customer_organisation_id' => $organisationId,
            'actor_team_member_id' => $actorTeamMemberId,
            'actor_display_name_snapshot' => $actorDisplayNameSnapshot,
            'verb' => $verb,
            'subject_kind' => $subjectKind,
            'subject_id' => $subjectId,
            'payload_json' => $payloadJson === null ? null : json_decode($payloadJson, true),
            'originating_ip' => $originatingIp,
            'occurred_at' => now(),
        ]);

        Log::info("Audit: {$verb} on {$subjectKind} {$subjectId} by {$actorDisplayNameSnapshot} in org {$organisationId}");

        return $row;
    }
}
