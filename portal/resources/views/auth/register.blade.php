<x-guest-layout>
    <form method="POST" action="{{ route('register') }}">
        @csrf

        <!-- Display name -->
        <div>
            <x-input-label for="display_name" value="الاسم / Name" />
            <x-text-input id="display_name" class="block mt-1 w-full" type="text" name="display_name" :value="old('display_name')" required autofocus autocomplete="name" />
            <x-input-error :messages="$errors->get('display_name')" class="mt-2" />
        </div>

        <!-- Organisation legal name (Arabic) -->
        <div class="mt-4">
            <x-input-label for="organisation_legal_name_ar" value="اسم الشركة بالعربية / Organisation legal name (Arabic)" />
            <x-text-input id="organisation_legal_name_ar" class="block mt-1 w-full" type="text" name="organisation_legal_name_ar" :value="old('organisation_legal_name_ar')" required dir="rtl" />
            <x-input-error :messages="$errors->get('organisation_legal_name_ar')" class="mt-2" />
            <p class="mt-1 text-xs text-stone-500">
                ده اللي هيظهر على الفواتير الضريبية. تقدر تعدّله من إعدادات الشركة بعدين.
            </p>
        </div>

        <!-- Email -->
        <div class="mt-4">
            <x-input-label for="email" value="البريد الإلكتروني / Email" />
            <x-text-input id="email" class="block mt-1 w-full" type="email" name="email" :value="old('email')" required autocomplete="username" />
            <x-input-error :messages="$errors->get('email')" class="mt-2" />
        </div>

        <!-- Password -->
        <div class="mt-4">
            <x-input-label for="password" value="كلمة المرور / Password (≥ 12 chars)" />
            <x-text-input id="password" class="block mt-1 w-full" type="password" name="password" required autocomplete="new-password" />
            <x-input-error :messages="$errors->get('password')" class="mt-2" />
        </div>

        <!-- Confirm Password -->
        <div class="mt-4">
            <x-input-label for="password_confirmation" value="تأكيد كلمة المرور / Confirm password" />
            <x-text-input id="password_confirmation" class="block mt-1 w-full" type="password" name="password_confirmation" required autocomplete="new-password" />
            <x-input-error :messages="$errors->get('password_confirmation')" class="mt-2" />
        </div>

        <!-- Locale preference -->
        <div class="mt-4">
            <x-input-label for="locale_preference" value="اللغة المفضلة / Preferred language" />
            <select id="locale_preference" name="locale_preference" class="block mt-1 w-full rounded border-stone-300 focus:border-amber-600 focus:ring-amber-600">
                <option value="ar-EG" {{ old('locale_preference', 'ar-EG') === 'ar-EG' ? 'selected' : '' }}>العربية</option>
                <option value="en-US" {{ old('locale_preference') === 'en-US' ? 'selected' : '' }}>English</option>
            </select>
        </div>

        <div class="flex items-center justify-end mt-6">
            <a class="underline text-sm text-gray-600 dark:text-gray-400 hover:text-gray-900 rounded-md focus:outline-none focus:ring-2 focus:ring-amber-500" href="{{ route('login') }}">
                مسجّل فعلاً؟ Already registered?
            </a>

            <x-primary-button class="ms-4">
                تسجيل / Register
            </x-primary-button>
        </div>
    </form>
</x-guest-layout>
