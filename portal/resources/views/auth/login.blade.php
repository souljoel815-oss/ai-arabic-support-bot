<x-guest-layout>
    @section('title', 'تسجيل الدخول · ' . __('messages.app.name'))

    <header class="mb-6 text-center">
        <h1 class="text-2xl font-bold text-ink-950 mb-1">أهلاً بعودتك</h1>
        <p class="text-sm text-ink-600">سجّل دخولك لإدارة اشتراكك وتراخيصك.</p>
    </header>

    <x-auth-session-status class="mb-4" :status="session('status')" />

    <form method="POST" action="{{ route('login') }}" class="space-y-4">
        @csrf

        <div>
            <x-input-label for="email" value="البريد الإلكتروني / Email" />
            <x-text-input id="email" type="email" name="email" :value="old('email')" required autofocus autocomplete="username" />
            <x-input-error :messages="$errors->get('email')" />
        </div>

        <div>
            <div class="flex items-center justify-between mb-1.5">
                <label for="password" class="form-label !mb-0">كلمة المرور / Password</label>
                @if (Route::has('password.request'))
                    <a class="text-xs text-brand-700 hover:text-brand-800 underline-offset-2 hover:underline" href="{{ route('password.request') }}">
                        نسيت كلمة المرور؟
                    </a>
                @endif
            </div>
            <x-text-input id="password" type="password" name="password" required autocomplete="current-password" />
            <x-input-error :messages="$errors->get('password')" />
        </div>

        <label class="flex items-center gap-2 text-sm text-ink-700">
            <input type="checkbox" name="remember" class="rounded border-ink-300 text-brand-600 focus:ring-brand-400">
            <span>تذكّرني / Remember me</span>
        </label>

        <x-primary-button class="w-full">
            تسجيل الدخول
        </x-primary-button>
    </form>

    <p class="mt-6 text-center text-sm text-ink-600">
        مفيش حساب؟
        <a href="{{ route('register') }}" class="text-brand-700 font-semibold hover:text-brand-800">ابدأ التجربة المجانية ←</a>
    </p>
</x-guest-layout>
