<x-guest-layout>
    @section('title', 'تأكيد كلمة المرور · ' . __('messages.app.name'))

    <header class="mb-6 text-center">
        <div class="mx-auto mb-3 w-12 h-12 rounded-full bg-brand-100 flex items-center justify-center text-2xl">
            🔒
        </div>
        <h1 class="text-2xl font-bold text-ink-950 mb-1">تأكيد كلمة المرور</h1>
        <p class="text-sm text-ink-600">
            ده جزء حسّاس من البوابة. يُرجى تأكيد كلمة مرورك للمتابعة.
        </p>
    </header>

    <form method="POST" action="{{ route('password.confirm') }}" class="space-y-4">
        @csrf

        <div>
            <x-input-label for="password" value="كلمة المرور" />
            <x-text-input id="password" type="password" name="password" required autocomplete="current-password" autofocus />
            <x-input-error :messages="$errors->get('password')" />
        </div>

        <x-primary-button class="w-full">
            تأكيد
        </x-primary-button>
    </form>
</x-guest-layout>
