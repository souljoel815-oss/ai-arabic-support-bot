<?php

namespace Tests\Feature\Auth;

use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class RegistrationTest extends TestCase
{
    use RefreshDatabase;

    public function test_registration_screen_can_be_rendered(): void
    {
        $response = $this->get('/register');

        $response->assertStatus(200);
    }

    public function test_new_users_can_register(): void
    {
        $response = $this->post('/register', [
            'display_name' => 'Test User',
            'email' => 'test@example.com',
            'password' => 'SecretP4ssw0rd!',
            'password_confirmation' => 'SecretP4ssw0rd!',
        ]);

        $this->assertAuthenticated();
        $response->assertRedirect(route('portal.dashboard', absolute: false));
    }
}
