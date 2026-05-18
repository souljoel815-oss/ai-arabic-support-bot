<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T154 per data-model.md §11. Tracks every released version of every
 * downloadable artefact (Desktop installer, LAN client, Android APK).
 * The downloads page renders the latest + a dropdown of the last 3
 * non-retired prior versions per artefact (FR-017 rollback).
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('download_artifact_versions', function (Blueprint $table) {
            $table->uuid('id')->primary();
            // 'desktop', 'lan', 'android' — keep narrow string so a typo
            // in a release script fails fast on the FK-less join in code.
            $table->string('artefact_key', 32);
            $table->string('version', 32);          // e.g. "5.2.3"
            $table->string('filename', 256);
            $table->string('download_url', 512);
            $table->unsignedBigInteger('size_bytes');
            $table->string('sha256', 64);
            $table->dateTime('released_at');
            $table->dateTime('retired_at')->nullable();
            $table->string('retired_reason', 128)->nullable();
            $table->timestamps();

            $table->index(['artefact_key', 'released_at']);
            $table->unique(['artefact_key', 'version']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('download_artifact_versions');
    }
};
