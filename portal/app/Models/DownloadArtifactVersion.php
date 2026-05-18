<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

/**
 * T154 per data-model.md §11. Each row is one released version of one
 * downloadable artefact (`desktop` / `lan` / `android`). Latest = the
 * row with the most recent `released_at` and `retired_at IS NULL`.
 * Prior versions = the next 3 most recent non-retired rows for the
 * same `artefact_key`.
 */
class DownloadArtifactVersion extends Model
{
    use HasFactory;
    use HasUuids;

    protected $table = 'download_artifact_versions';

    protected $keyType = 'string';

    public $incrementing = false;

    public const KEY_DESKTOP = 'desktop';
    public const KEY_LAN = 'lan';
    public const KEY_ANDROID = 'android';

    protected $fillable = [
        'artefact_key',
        'version',
        'filename',
        'download_url',
        'size_bytes',
        'sha256',
        'released_at',
        'retired_at',
        'retired_reason',
    ];

    protected function casts(): array
    {
        return [
            'released_at' => 'datetime',
            'retired_at' => 'datetime',
            'size_bytes' => 'integer',
        ];
    }

    /**
     * The latest non-retired version of a given artefact, or null if
     * none have been released yet.
     */
    public static function latestFor(string $artefactKey): ?self
    {
        return self::query()
            ->where('artefact_key', $artefactKey)
            ->whereNull('retired_at')
            ->orderByDesc('released_at')
            ->first();
    }

    /**
     * The most-recent N prior versions of a given artefact (strictly
     * older than the current latest, non-retired).
     *
     * @return \Illuminate\Database\Eloquent\Collection<int, self>
     */
    public static function priorVersionsFor(string $artefactKey, int $limit = 3): \Illuminate\Database\Eloquent\Collection
    {
        $latest = self::latestFor($artefactKey);
        if ($latest === null) {
            return self::query()->whereRaw('1=0')->get();
        }
        return self::query()
            ->where('artefact_key', $artefactKey)
            ->whereNull('retired_at')
            ->where('released_at', '<', $latest->released_at)
            ->orderByDesc('released_at')
            ->limit($limit)
            ->get();
    }
}
