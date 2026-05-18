<x-guest-layout>
    @section('title', 'استعادة كلمة المرور · ' . __('messages.app.name'))

    <header class="mb-6 text-center">
        <h1 class="text-2xl font-bold text-ink-950 mb-1">نسيت كلمة المرور؟</h1>
        <p class="text-sm text-ink-600">
            ادخل بريدك الإلكتروني وهنبعتلك لينك لإعادة تعيين كلمة المرور.
        </p>
    </header>

    <x-auth-session-status class="mb-4" :status="session('status')" />

    <form method="POST" action="{{ route('password.email') }}" class="space-y-4">
        @csrf

        <div>
            <x-input-label for="email" value="البريد الإلكتروني / Email" />
            <x-text-input id="email" type="email" name="email" :value="old('email')" required autofocus />
            <x-input-error :messages="$errors->get('email')" />
        </div>

        <x-primary-button class="w-full">
            إرسال لينك إعادة التعيين
        </x-primary-button>
    </form>

    <p class="mt-6 text-center text-sm text-ink-600">
        <a href="{{ route('login') }}" class="text-brand-700 font-semibold hover:text-brand-800">← العودة لتسجيل الدخول</a>
    </p>
</x-guest-layout>
