<x-guest-layout>
    @section('title', 'تسجيل الدخول · ' . __('messages.app.name'))

    <header class="mb-6">
        <p class="eyebrow mb-2">تسجيل دخول</p>
        <h1 class="display-2 mb-1.5">أهلاً بعودتك</h1>
        <p class="text-sm text-ink-600">سجّل دخولك لإدارة اشتراكك وتراخيصك.</p>
    </header>

    <x-auth-session-status class="mb-4" :status="session('status')" />

    <form method="POST" action="{{ route('login') }}" class="space-y-4">
        @csrf

        <div>
            <x-input-label for="email" value="البريد الإلكتروني" />
            <x-text-input id="email" type="email" name="email" :value="old('email')" required autofocus autocomplete="username"
                          placeholder="name@company.com" />
            <x-input-error :messages="$errors->get('email')" />
        </div>

        <div>
            <div class="flex items-center justify-between mb-1.5">
                <label for="password" class="form-label !mb-0">كلمة المرور</label>
                @if (Route::has('password.request'))
                    <a class="text-xs text-brand-700 hover:text-brand-800 underline-offset-2 hover:underline" href="{{ route('password.request') }}">
                        نسيت كلمة المرور؟
                    </a>
                @endif
            </div>
            <x-text-input id="password" type="password" name="password" required autocomplete="current-password"
                          placeholder="••••••••" />
            <x-input-error :messages="$errors->get('password')" />
        </div>

        <label class="flex items-center gap-2 text-sm text-ink-700 cursor-pointer select-none">
            <input type="checkbox" name="remember" class="rounded border-ink-300 text-brand-600 focus:ring-brand-400">
            <span>تذكّرني</span>
        </label>

        <x-primary-button class="w-full !py-3">
            تسجيل الدخول
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M5 12h14M13 5l7 7-7 7" />
            </svg>
        </x-primary-button>
    </form>

    <div class="mt-7 pt-6 border-t border-ink-100">
        <p class="text-center text-sm text-ink-600">
            مفيش حساب؟
            <a href="{{ route('register') }}" class="text-brand-700 font-semibold hover:text-brand-800">ابدأ التجربة المجانية ←</a>
        </p>
    </div>
</x-guest-layout>
