<?php

namespace Tests\Feature\Marketing;

use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

/**
 * T037-T039 (HTTP-level smoke). Full Dusk perf-budget tests (SC-006 3s
 * p75 from a Cairo simulator) live in tests/Browser/ and run in CI
 * under headless Chrome; this PHPUnit file covers route resolution +
 * locale switching + Sales Lead persistence without a browser.
 */
class MarketingPagesTest extends TestCase
{
    use RefreshDatabase;

    public function test_serves_homepage_in_ar_eg_by_default(): void
    {
        $response = $this->get('/');
        $response->assertOk();
        $response->assertSee('lang="ar-EG"', escape: false);
        $response->assertSee('dir="rtl"', escape: false);
        $response->assertSee('دفترx', escape: false);
    }

    public function test_switches_to_en_us_ltr_with_en_prefix(): void
    {
        $response = $this->get('/en');
        $response->assertOk();
        $response->assertSee('lang="en-US"', escape: false);
        $response->assertSee('dir="ltr"', escape: false);
    }

    public function test_serves_all_marketing_routes_with_200(): void
    {
        $paths = ['/', '/features', '/pricing', '/downloads', '/about', '/contact', '/terms', '/refund', '/privacy', '/privacy/android'];
        foreach ($paths as $path) {
            $this->get($path)->assertOk();
            $this->get('/ar'.($path === '/' ? '' : $path))->assertOk();
            $this->get('/en'.($path === '/' ? '' : $path))->assertOk();
        }
    }

    public function test_privacy_android_url_stable_per_fr006_sc004(): void
    {
        // This URL is the contract with the Play Store Data Safety form;
        // it must never 404. If this test ever fails the deploy should
        // be blocked.
        $response = $this->get('/privacy/android');
        $response->assertOk();
        $response->assertSee('Crashlytics');
    }

    public function test_privacy_android_history_missing_snapshot_returns_404(): void
    {
        // T136 — when no snapshot is published for the requested date,
        // 404 (not a silent fallback to the live policy). Consent-claim
        // chains depend on the date returning the policy text AT THAT
        // date, not a later revision.
        $this->get('/privacy/android/history/2024-01-01')->assertNotFound();
    }

    public function test_privacy_android_history_rejects_invalid_date_format(): void
    {
        $this->get('/privacy/android/history/not-a-date')->assertNotFound();
        $this->get('/privacy/android/history/2024')->assertNotFound();
        $this->get('/privacy/android/history/2024-1-1')->assertNotFound();
    }

    public function test_exposes_robots_and_sitemap(): void
    {
        $this->get('/robots.txt')->assertOk()->assertSee('Sitemap:');
        $sitemap = $this->get('/sitemap.xml');
        $sitemap->assertOk();
        $sitemap->assertSee('<urlset', escape: false);
        $sitemap->assertHeader('Content-Type', 'application/xml; charset=utf-8');
    }

    public function test_embeds_jsonld_schemas(): void
    {
        $response = $this->get('/en');
        $response->assertSee('"@type": "Organization"', escape: false);
        $response->assertSee('"@type": "SoftwareApplication"', escape: false);
    }

    public function test_contact_form_persists_sales_lead(): void
    {
        $response = $this->from('/contact')->post('/contact', [
            'name' => 'Mariam Saleh',
            'email' => 'mariam@example.com',
            'phone' => '+20 100 000 0000',
            'interested_tier' => 'SMB',
            'message' => 'Interested in a demo.',
        ]);

        $response->assertRedirect('/contact');
        $response->assertSessionHas('status');

        $this->assertDatabaseHas('sales_leads', [
            'email' => 'mariam@example.com',
            'name' => 'Mariam Saleh',
            'interested_tier' => 'SMB',
        ]);
    }

    public function test_contact_form_rejects_invalid_email(): void
    {
        $response = $this->from('/contact')->post('/contact', [
            'name' => 'Test',
            'email' => 'not-an-email',
        ]);

        $response->assertSessionHasErrors('email');
        $this->assertDatabaseMissing('sales_leads', [
            'name' => 'Test',
        ]);
    }
}
