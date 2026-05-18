<?php

namespace App\Services\Support;

use App\Models\CustomerOrganisation;
use App\Models\Subscription;
use App\Models\SupportTicket;
use App\Models\SupportTicketAttachment;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Http\UploadedFile;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Storage;
use InvalidArgumentException;

/**
 * T108 per FR-018. Creates a ticket + persists attachments.
 *
 * Validation enforced here (in addition to FormRequest at the
 * controller layer):
 *   - Max 3 attachments per ticket
 *   - Max 5 MB per attachment
 *   - Max 15 MB total per ticket
 *   - MIME-type whitelist (images, PDFs, common docs, CSV, .txt, .log)
 *   - High priority is disabled for Solo customers (per FR-018) — the
 *     UI hides the option but the service guard catches URL tampering.
 *
 * Attachments land under storage/app/private/tickets/{org}/{ticket}/.
 * Local disk per research §6; Phase 9 polish can swap to Backblaze B2
 * once disk pressure warrants it.
 */
class CreateTicketService
{
    private const MAX_ATTACHMENTS = 3;

    private const MAX_ATTACHMENT_SIZE_BYTES = 5 * 1024 * 1024;

    private const MAX_TOTAL_ATTACHMENT_SIZE_BYTES = 15 * 1024 * 1024;

    private const ALLOWED_MIMES = [
        'image/png',
        'image/jpeg',
        'image/gif',
        'image/webp',
        'application/pdf',
        'text/plain',
        'text/csv',
        'application/vnd.ms-excel',
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        'application/msword',
    ];

    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    /**
     * @param  array<UploadedFile>  $attachments
     */
    public function create(
        CustomerOrganisation $org,
        TeamMember $actor,
        string $category,
        string $priority,
        string $subject,
        string $body,
        array $attachments,
        string $originatingIp,
    ): SupportTicket {
        if (! in_array($category, SupportTicket::CATEGORIES, true)) {
            throw new InvalidArgumentException("Unknown category: {$category}");
        }
        if (! in_array($priority, [SupportTicket::PRIORITY_LOW, SupportTicket::PRIORITY_NORMAL, SupportTicket::PRIORITY_HIGH], true)) {
            throw new InvalidArgumentException("Unknown priority: {$priority}");
        }
        if ($priority === SupportTicket::PRIORITY_HIGH) {
            $this->guardHighPriorityForTier($org);
        }
        if (count($attachments) > self::MAX_ATTACHMENTS) {
            throw new InvalidArgumentException(
                'Maximum '.self::MAX_ATTACHMENTS.' attachments per ticket.'
            );
        }

        $totalSize = 0;
        foreach ($attachments as $att) {
            if (! $att instanceof UploadedFile) {
                throw new InvalidArgumentException('Each attachment must be an UploadedFile.');
            }
            if ($att->getSize() > self::MAX_ATTACHMENT_SIZE_BYTES) {
                throw new InvalidArgumentException(
                    "Attachment '{$att->getClientOriginalName()}' exceeds 5 MB."
                );
            }
            $mime = $att->getMimeType();
            if (! in_array($mime, self::ALLOWED_MIMES, true)) {
                throw new InvalidArgumentException(
                    "Attachment '{$att->getClientOriginalName()}' has unsupported type ({$mime})."
                );
            }
            $totalSize += $att->getSize();
        }
        if ($totalSize > self::MAX_TOTAL_ATTACHMENT_SIZE_BYTES) {
            throw new InvalidArgumentException(
                'Total attachment size exceeds 15 MB.'
            );
        }

        return DB::transaction(function () use ($org, $actor, $category, $priority, $subject, $body, $attachments, $originatingIp) {
            $ticket = SupportTicket::create([
                'customer_organisation_id' => $org->id,
                'opened_by_team_member_id' => $actor->id,
                'category' => $category,
                'priority' => $priority,
                'subject' => $subject,
                'body' => $body,
                'status' => SupportTicket::STATUS_OPEN,
                'opened_at' => now(),
            ]);

            foreach ($attachments as $att) {
                $storedName = (string) \Illuminate\Support\Str::uuid().'.'.$att->getClientOriginalExtension();
                $relativePath = "tickets/{$org->id}/{$ticket->id}/{$storedName}";
                Storage::disk('local')->putFileAs(
                    "tickets/{$org->id}/{$ticket->id}",
                    $att,
                    $storedName
                );
                SupportTicketAttachment::create([
                    'support_ticket_id' => $ticket->id,
                    'original_filename' => $att->getClientOriginalName(),
                    'mime_type' => $att->getMimeType(),
                    'size_bytes' => $att->getSize(),
                    'storage_path' => $relativePath,
                ]);
            }

            $this->audit->write(
                organisationId: $org->id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'ticket.opened',
                subjectKind: 'SupportTicket',
                subjectId: $ticket->id,
                payload: [
                    'category' => $category,
                    'priority' => $priority,
                    'subject' => $subject,
                    'attachments' => count($attachments),
                ],
                originatingIp: $originatingIp,
            );

            return $ticket;
        });
    }

    /**
     * FR-018 — High priority is only available to Solo customers who
     * happen to also have Enterprise/Firm subscriptions on the side
     * (effectively, anyone whose org carries a non-Solo subscription).
     * The UI hides it for Solo-only orgs; this guard catches URL
     * tampering at the service layer.
     */
    private function guardHighPriorityForTier(CustomerOrganisation $org): void
    {
        $hasNonSolo = Subscription::query()
            ->where('customer_organisation_id', $org->id)
            ->where('status', Subscription::STATUS_ACTIVE)
            ->whereIn('tier', [
                Subscription::TIER_SMB,
                Subscription::TIER_ENTERPRISE,
                Subscription::TIER_FIRM,
            ])
            ->exists();

        if (! $hasNonSolo) {
            throw new InvalidArgumentException(
                'High priority is reserved for SMB / Enterprise / Firm subscriptions. Upgrade to use it.'
            );
        }
    }
}
