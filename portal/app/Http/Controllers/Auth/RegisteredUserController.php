<?php

namespace App\Http\Controllers\Auth;

use App\Http\Controllers\Controller;
use App\Models\TeamMember;
use Illuminate\Auth\Events\Registered;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\Hash;
use Illuminate\Validation\Rules;
use Illuminate\Validation\ValidationException;
use Illuminate\View\View;

class RegisteredUserController extends Controller
{
    /**
     * Display the registration view.
     */
    public function create(): View
    {
        return view('auth.register');
    }

    /**
     * Handle an incoming registration request.
     *
     * @throws ValidationException
     */
    public function store(Request $request): RedirectResponse
    {
        $request->validate([
            'display_name' => ['required', 'string', 'max:128'],
            'email' => ['required', 'string', 'lowercase', 'email', 'max:256', 'unique:'.TeamMember::class],
            'password' => ['required', 'confirmed', Rules\Password::defaults()],
            // FR-008 — locale preference at signup; defaults to ar-EG.
            'locale_preference' => ['nullable', 'in:ar-EG,en-US'],
        ]);

        $user = TeamMember::create([
            'display_name' => $request->display_name,
            'email' => $request->email,
            'password' => Hash::make($request->password),
            'locale_preference' => $request->locale_preference ?? 'ar-EG',
        ]);

        event(new Registered($user));

        Auth::login($user);

        // Phase 5 (US3 T081 SignupService) replaces this with the full
        // signup flow that also creates a CustomerOrganisation + Owner
        // OrganisationMembership row. For now go to the portal dashboard;
        // the dashboard tells the user to subscribe before doing anything
        // real (the OrganisationScope middleware will catch the
        // no-active-membership case + bounce them appropriately).
        return redirect(route('portal.dashboard', absolute: false));
    }
}
