<?php

namespace App\Http\Controllers\Marketing;

use App\Http\Controllers\Controller;
use Illuminate\Contracts\View\View;

/**
 * T041 per FR-002. Features catalog grouped by category. The full list
 * lives in lang/{ar,en}/marketing.php under marketing.features.list so
 * it can be edited by ops without code changes.
 */
class FeaturesController extends Controller
{
    public function show(): View
    {
        return view('marketing.features');
    }
}
