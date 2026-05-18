<?php

namespace Tests\Feature\Identity;

use App\Models\OrganisationMembership;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

/**
 * T070 (HTTP-level contract) — POST /register creates TeamMember +
 * CustomerOrganisation + Owner OrganisationMembership in one
 * transaction. The full contract per contracts/signup.md.
 */
class SignupFlowTest extends TestCase
{
    use RefreshDatabase;

    public function test_register_creates_team_member_organisation_and_owner_membership(): void
    {
        $response = $this->post('/register', [
            'display_name' => 'Ahmed Hassan',
            'email' => 'ahmed@my-business.eg',
            'password' => 'SuperSecretP4ss!',
            'password_confirmation' => 'SuperSecretP4ss!',
            'organisation_legal_name_ar' => 'شركة الأمل للتجارة',
            'locale_preference' => 'ar-EG',
        ]);

        $response->assertRedirect(route('portal.dashboard', absolute: false));
        $this->assertAuthenticated();

        $this->assertDatabaseHas('team_members', [
            'email' => 'ahmed@my-business.eg',
            'display_name' => 'Ahmed Hassan',
            'locale_preference' => 'ar-EG',
        ]);
        $this->assertDatabaseHas('customer_organisations', [
            'legal_name_ar' => 'شركة الأمل للتجارة',
            'billing_email' => 'ahmed@my-business.eg',
            'country_code' => 'EG',
        ]);
        // Owner membership with accepted_at = now (self-signup).
        $membership = OrganisationMembership::query()
            ->where('role', OrganisationMembership::ROLE_OWNER)
            ->first();
        $this->assertNotNull($membership);
        $this->assertNotNull($membership->accepted_at);
        $this->assertNull($membership->revoked_at);

        $this->assertDatabaseHas('audit_log_entries', [
            'verb' => 'organisation.signedUp',
        ]);
    }

    public function test_register_rejects_missing_organisation_legal_name(): void
    {
        $response = $this->post('/register', [
            'display_name' => 'Test',
            'email' => 'test@example.com',
            'password' => 'SuperSecretP4ss!',
            'password_confirmation' => 'SuperSecretP4ss!',
            'locale_preference' => 'ar-EG',
        ]);
        $response->assertSessionHasErrors('organisation_legal_name_ar');
        $this->assertDatabaseMissing('team_members', ['email' => 'test@example.com']);
    }

    public function test_register_rejects_short_password(): void
    {
        $response = $this->post('/register', [
            'display_name' => 'Test',
            'email' => 'test@example.com',
            'password' => 'short',
            'password_confirmation' => 'short',
            'organisation_legal_name_ar' => 'شركة الاختبار',
        ]);
        $response->assertSessionHasErrors('password');
    }

    public function test_register_rejects_duplicate_email(): void
    {
        // Seed an existing user directly (avoids the multi-request SQLite
        // locking that the auth-state reset would otherwise trigger).
        \App\Models\TeamMember::factory()->create(['email' => 'dup@example.com']);

        $response = $this->post('/register', [
            'display_name' => 'Second',
            'email' => 'dup@example.com',
            'password' => 'AnotherP4ss123!',
            'password_confirmation' => 'AnotherP4ss123!',
            'organisation_legal_name_ar' => 'شركة الثانية',
        ]);
        $response->assertSessionHasErrors('email');
    }
}
