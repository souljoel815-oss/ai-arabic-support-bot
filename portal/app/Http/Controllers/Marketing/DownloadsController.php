<?php

namespace App\Http\Controllers\Marketing;

use App\Http\Controllers\Controller;
use Illuminate\Contracts\View\View;

/**
 * T043 per FR-004. Downloads page — installer links + Play Store +
 * direct APK + checksums + version info. The actual installer files
 * land in `public/downloads/` and are managed via the
 * `DownloadArtifactVersion` model (Phase 9 T154) which exposes the
 * "3 prior versions for rollback" UI.
 */
class DownloadsController extends Controller
{
    public function show(): View
    {
        return view('marketing.downloads');
    }
}
