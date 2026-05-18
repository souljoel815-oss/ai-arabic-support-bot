<?php

namespace Tests\Feature\Support;

use App\Models\CustomerOrganisation;
use App\Models\OrganisationMembership;
use App\Models\Subscription;
use App\Models\SupportTicket;
use App\Models\SupportTicketReply;
use App\Models\TeamMember;
use App\Services\Support\CreateTicketService;
use App\Services\Support\ReplyTicketService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Http\Testing\File;
use Illuminate\Http\UploadedFile;
use Illuminate\Support\Facades\Storage;
use Tests\TestCase;

/**
 * T104 + T105 — Support-ticket lifecycle + attachment-limit tests.
 */
class SupportTicketFlowsTest extends TestCase
{
    use RefreshDatabase;

    public function test_create_ticket_persists_row_plus_audit_entry(): void
    {
        [$user, $org] = $this->seedOrg();

        $ticket = app(CreateTicketService::class)->create(
            org: $org,
            actor: $user,
            category: SupportTicket::CATEGORY_BUG,
            priority: SupportTicket::PRIORITY_NORMAL,
            subject: 'فاتورة VAT بمبلغ غلط',
            body: 'الرقم بيظهر 14% بدل 10% على بعض الأصناف.',
            attachments: [],
            originatingIp: '127.0.0.1',
        );

        $this->assertSame(SupportTicket::STATUS_OPEN, $ticket->status);
        $this->assertSame(SupportTicket::CATEGORY_BUG, $ticket->category);
        $this->assertDatabaseHas('audit_log_entries', [
            'verb' => 'ticket.opened',
            'subject_id' => $ticket->id,
        ]);
    }

    public function test_create_ticket_rejects_high_priority_for_solo_only_org(): void
    {
        [$user, $org] = $this->seedOrg();
        // Solo subscription (the default in factories).
        Subscription::factory()->for($org, 'customerOrganisation')->create([
            'tier' => Subscription::TIER_SOLO,
            'status' => Subscription::STATUS_ACTIVE,
        ]);

        $this->expectException(\InvalidArgumentException::class);
        app(CreateTicketService::class)->create(
            org: $org,
            actor: $user,
            category: SupportTicket::CATEGORY_BUG,
            priority: SupportTicket::PRIORITY_HIGH,
            subject: 'urgent',
            body: 'something',
            attachments: [],
            originatingIp: '127.0.0.1',
        );
    }

    public function test_create_ticket_allows_high_priority_for_enterprise(): void
    {
        [$user, $org] = $this->seedOrg();
        Subscription::factory()->for($org, 'customerOrganisation')->create([
            'tier' => Subscription::TIER_ENTERPRISE,
            'status' => Subscription::STATUS_ACTIVE,
        ]);

        $ticket = app(CreateTicketService::class)->create(
            org: $org,
            actor: $user,
            category: SupportTicket::CATEGORY_URGENT,
            priority: SupportTicket::PRIORITY_HIGH,
            subject: 'system down',
            body: 'cannot login',
            attachments: [],
            originatingIp: '127.0.0.1',
        );

        $this->assertSame(SupportTicket::PRIORITY_HIGH, $ticket->priority);
    }

    public function test_create_ticket_rejects_more_than_3_attachments(): void
    {
        Storage::fake('local');
        [$user, $org] = $this->seedOrg();

        $this->expectException(\InvalidArgumentException::class);
        app(CreateTicketService::class)->create(
            org: $org,
            actor: $user,
            category: SupportTicket::CATEGORY_BUG,
            priority: SupportTicket::PRIORITY_NORMAL,
            subject: 's',
            body: 'b',
            attachments: [
                UploadedFile::fake()->image('a.png', 100, 100),
                UploadedFile::fake()->image('b.png', 100, 100),
                UploadedFile::fake()->image('c.png', 100, 100),
                UploadedFile::fake()->image('d.png', 100, 100),  // 4th — over the cap
            ],
            originatingIp: '127.0.0.1',
        );
    }

    public function test_create_ticket_rejects_attachment_over_5mb(): void
    {
        Storage::fake('local');
        [$user, $org] = $this->seedOrg();

        $this->expectException(\InvalidArgumentException::class);
        app(CreateTicketService::class)->create(
            org: $org,
            actor: $user,
            category: SupportTicket::CATEGORY_BUG,
            priority: SupportTicket::PRIORITY_NORMAL,
            subject: 's',
            body: 'b',
            attachments: [
                UploadedFile::fake()->create('big.pdf', 6 * 1024, 'application/pdf'),  // 6 MB
            ],
            originatingIp: '127.0.0.1',
        );
    }

    public function test_create_ticket_rejects_disallowed_mime(): void
    {
        Storage::fake('local');
        [$user, $org] = $this->seedOrg();

        $this->expectException(\InvalidArgumentException::class);
        app(CreateTicketService::class)->create(
            org: $org,
            actor: $user,
            category: SupportTicket::CATEGORY_BUG,
            priority: SupportTicket::PRIORITY_NORMAL,
            subject: 's',
            body: 'b',
            attachments: [
                UploadedFile::fake()->create('virus.exe', 100, 'application/x-msdownload'),
            ],
            originatingIp: '127.0.0.1',
        );
    }

    public function test_create_ticket_persists_attachments_to_disk(): void
    {
        Storage::fake('local');
        [$user, $org] = $this->seedOrg();

        $ticket = app(CreateTicketService::class)->create(
            org: $org,
            actor: $user,
            category: SupportTicket::CATEGORY_BUG,
            priority: SupportTicket::PRIORITY_NORMAL,
            subject: 's',
            body: 'b',
            attachments: [
                UploadedFile::fake()->image('screenshot.png', 100, 100),
            ],
            originatingIp: '127.0.0.1',
        );

        $att = $ticket->attachments()->first();
        $this->assertNotNull($att);
        $this->assertSame('screenshot.png', $att->original_filename);
        Storage::disk('local')->assertExists($att->storage_path);
    }

    public function test_vendor_reply_sets_first_reply_at_and_flips_status_to_in_progress(): void
    {
        [$user, $org] = $this->seedOrg();
        $ticket = app(CreateTicketService::class)->create(
            org: $org,
            actor: $user,
            category: SupportTicket::CATEGORY_BUG,
            priority: SupportTicket::PRIORITY_NORMAL,
            subject: 's',
            body: 'b',
            attachments: [],
            originatingIp: '127.0.0.1',
        );

        $this->assertNull($ticket->first_reply_at);
        $this->assertSame(SupportTicket::STATUS_OPEN, $ticket->status);

        app(ReplyTicketService::class)->replyAsVendorStaff(
            $ticket,
            vendorStaffId: 'aaaaaaaa-1111-2222-3333-444444444444',
            vendorStaffDisplayName: 'Mariam (Support)',
            body: 'استلمنا التذكرة. هنفحصها وهنرد عليك.',
            originatingIp: '10.0.0.1',
        );

        $ticket->refresh();
        $this->assertNotNull($ticket->first_reply_at, 'first_reply_at must be set on first vendor reply (SC-005)');
        $this->assertSame(SupportTicket::STATUS_IN_PROGRESS, $ticket->status);
    }

    public function test_customer_reply_on_resolved_reopens_the_ticket(): void
    {
        [$user, $org] = $this->seedOrg();
        $ticket = app(CreateTicketService::class)->create(
            org: $org,
            actor: $user,
            category: SupportTicket::CATEGORY_BUG,
            priority: SupportTicket::PRIORITY_NORMAL,
            subject: 's',
            body: 'b',
            attachments: [],
            originatingIp: '127.0.0.1',
        );
        $ticket->update(['status' => SupportTicket::STATUS_RESOLVED, 'resolved_at' => now()]);

        app(ReplyTicketService::class)->replyAsCustomer($ticket, $user, 'فيه مشكلة تانية', '127.0.0.1');

        $ticket->refresh();
        $this->assertSame(SupportTicket::STATUS_OPEN, $ticket->status);
        $this->assertNull($ticket->resolved_at);
    }

    /**
     * @return array{0: TeamMember, 1: CustomerOrganisation}
     */
    private function seedOrg(): array
    {
        $user = TeamMember::factory()->create();
        $org = CustomerOrganisation::factory()->create();
        OrganisationMembership::create([
            'customer_organisation_id' => $org->id,
            'team_member_id' => $user->id,
            'role' => OrganisationMembership::ROLE_OWNER,
            'invited_at' => now(),
            'accepted_at' => now(),
        ]);
        return [$user, $org];
    }
}
