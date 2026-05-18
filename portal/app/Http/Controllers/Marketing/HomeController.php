<?php

namespace App\Http\Controllers\Marketing;

use App\Http\Controllers\Controller;
use Illuminate\Contracts\View\View;

/**
 * T040 per FR-001. Public homepage — hero + value props + three CTAs
 * (Start trial / See pricing / Download). Rendered server-side from
 * `resources/views/marketing/home.blade.php` and edge-cached by
 * Cloudflare for the 3-second p75 budget per SC-006.
 */
class HomeController extends Controller
{
    public function show(): View
    {
        return view('marketing.home');
    }
}
