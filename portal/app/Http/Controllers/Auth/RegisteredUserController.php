<?php

namespace App\Http\Controllers\Auth;

use App\Http\Controllers\Controller;
use App\Models\TeamMember;
use App\Services\Identity\SignupService;
use Illuminate\Auth\Events\Registered;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Illuminate\Validation\Rules;
use Illuminate\Validation\ValidationException;
use Illuminate\View\View;

class RegisteredUserController extends Controller
{
    public function __construct(private readonly SignupService $signupService)
    {
    }

    public function create(): View
    {
        return view('auth.register');
    }

    /**
     * @throws ValidationException
     */
    public function store(Request $request): RedirectResponse
    {
        $validated = $request->validate([
            'display_name' => ['required', 'string', 'max:128'],
            'email' => ['required', 'string', 'lowercase', 'email', 'max:256', 'unique:'.TeamMember::class],
            'password' => ['required', 'confirmed', Rules\Password::defaults()],
            'organisation_legal_name_ar' => ['required', 'string', 'max:256'],
            'locale_preference' => ['nullable', 'in:ar-EG,en-US'],
        ]);

        [$user, $_org] = $this->signupService->signUp(
            displayName: $validated['display_name'],
            email: $validated['email'],
            plainPassword: $validated['password'],
            organisationLegalNameAr: $validated['organisation_legal_name_ar'],
            localePreference: $validated['locale_preference'] ?? 'ar-EG',
            originatingIp: $request->ip() ?? '0.0.0.0',
        );

        event(new Registered($user));
        Auth::login($user);

        return redirect(route('portal.dashboard', absolute: false));
    }
}
