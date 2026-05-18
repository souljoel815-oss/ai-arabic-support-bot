<?php

namespace App\Services\Support;

use App\Models\SupportTicket;
use App\Models\SupportTicketReply;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;

/**
 * T109 per FR-018. Append a reply to a ticket thread.
 *
 * Side effects beyond the insert:
 *   - If author is VendorStaff AND first_reply_at is null, set it now.
 *     This drives SC-005 measurement (T156 rollup job + Ops dashboard).
 *   - Customer reply on a Resolved ticket flips status back to Open.
 *   - Audit row written for the customer-facing reply (so Owners can
 *     see "X replied to ticket Y" in their audit log).
 */
class ReplyTicketService
{
    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    public function replyAsCustomer(SupportTicket $ticket, TeamMember $actor, string $body, string $originatingIp): SupportTicketReply
    {
        return DB::transaction(function () use ($ticket, $actor, $body, $originatingIp) {
            $reply = SupportTicketReply::create([
                'support_ticket_id' => $ticket->id,
                'author_kind' => SupportTicketReply::AUTHOR_CUSTOMER,
                'author_team_member_id' => $actor->id,
                'body' => $body,
            ]);

            // Customer reply on a Resolved ticket reopens it.
            if ($ticket->status === SupportTicket::STATUS_RESOLVED) {
                $ticket->update(['status' => SupportTicket::STATUS_OPEN, 'resolved_at' => null]);
            }

            $this->audit->write(
                organisationId: $ticket->customer_organisation_id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'ticket.replied',
                subjectKind: 'SupportTicket',
                subjectId: $ticket->id,
                payload: ['author' => 'Customer'],
                originatingIp: $originatingIp,
            );

            return $reply;
        });
    }

    public function replyAsVendorStaff(SupportTicket $ticket, string $vendorStaffId, string $vendorStaffDisplayName, string $body, string $originatingIp): SupportTicketReply
    {
        return DB::transaction(function () use ($ticket, $vendorStaffId, $vendorStaffDisplayName, $body, $originatingIp) {
            $reply = SupportTicketReply::create([
                'support_ticket_id' => $ticket->id,
                'author_kind' => SupportTicketReply::AUTHOR_VENDOR_STAFF,
                'author_vendor_staff_id' => $vendorStaffId,
                'body' => $body,
            ]);

            // T156 / SC-005 — set first_reply_at on the FIRST vendor reply.
            if ($ticket->first_reply_at === null) {
                $ticket->update(['first_reply_at' => now()]);
            }
            // Vendor reply moves the ticket to InProgress (it stays in
            // that state until a customer reply or an explicit resolve).
            if ($ticket->status === SupportTicket::STATUS_OPEN) {
                $ticket->update(['status' => SupportTicket::STATUS_IN_PROGRESS]);
            }

            $this->audit->write(
                organisationId: $ticket->customer_organisation_id,
                actorTeamMemberId: null,
                actorDisplayNameSnapshot: "vendor:{$vendorStaffDisplayName}",
                verb: 'ticket.replied',
                subjectKind: 'SupportTicket',
                subjectId: $ticket->id,
                payload: ['author' => 'VendorStaff'],
                originatingIp: $originatingIp,
            );

            return $reply;
        });
    }
}
