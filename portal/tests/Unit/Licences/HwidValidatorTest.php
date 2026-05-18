<?php

namespace Tests\Unit\Licences;

use App\Services\Licences\HwidValidator;
use PHPUnit\Framework\TestCase;

/**
 * T059 per FR-014 format check. The collision check needs the DB so it
 * lives in the integration test (TransferLicenceFlowTest); this file
 * covers the regex part only.
 */
class HwidValidatorTest extends TestCase
{
    private HwidValidator $validator;

    protected function setUp(): void
    {
        parent::setUp();
        $this->validator = new HwidValidator();
    }

    /** @dataProvider validHwidProvider */
    public function test_well_formed_hwids_pass(string $hwid): void
    {
        $this->assertTrue($this->validator->isWellFormed($hwid));
    }

    public static function validHwidProvider(): array
    {
        return [
            'all-digits' => ['1234-5678-9ABC-DEF0'],
            'all-letters' => ['ABCD-EF12-3456-7890'],
            'mixed' => ['A1B2-C3D4-E5F6-7890'],
        ];
    }

    /** @dataProvider invalidHwidProvider */
    public function test_malformed_hwids_fail(string $hwid): void
    {
        $this->assertFalse($this->validator->isWellFormed($hwid));
    }

    public static function invalidHwidProvider(): array
    {
        return [
            'empty' => [''],
            'lowercase-rejected' => ['a1b2-c3d4-e5f6-7890'],
            'too-short' => ['A1B2-C3D4-E5F6'],
            'too-long' => ['A1B2-C3D4-E5F6-7890-DEAD'],
            'wrong-separator' => ['A1B2.C3D4.E5F6.7890'],
            'non-hex-G' => ['A1B2-C3D4-E5G6-7890'],
            'spaces' => ['A1B2 C3D4 E5F6 7890'],
            'random-junk' => ['not-a-hwid'],
        ];
    }
}
