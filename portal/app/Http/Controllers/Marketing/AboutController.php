<?php

namespace App\Http\Controllers\Marketing;

use App\Http\Controllers\Controller;
use Illuminate\Contracts\View\View;

/**
 * T044 per FR-005. About page — vendor story + team + partnership pitch
 * for accounting firms.
 */
class AboutController extends Controller
{
    public function show(): View
    {
        return view('marketing.about');
    }
}
