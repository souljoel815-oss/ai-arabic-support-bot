@extends('layouts.portal')

@section('title', 'سياسة الأمان')

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.organisation') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            المؤسسة
        </a>
        <p class="eyebrow mb-2">سياسة الأمان</p>
        <h1 class="display-1 mb-1.5">سياسة المصادقة الثنائية</h1>
        <p class="text-ink-600">إعدادات MFA على مستوى المؤسسة لكل المالكين.</p>
    </header>

    <section class="card-padded max-w-3xl">
        <div class="flex items-start gap-4 mb-6 pb-6 border-b border-ink-100">
            <span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-brand-100 text-brand-700">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24"
                     fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M12 3 4 6v6c0 4.5 3.2 8.5 8 9 4.8-.5 8-4.5 8-9V6Z" />
                </svg>
            </span>
            <div class="flex-1">
                <h2 class="text-lg font-bold text-ink-950 mb-2">إجبار MFA على المالكين</h2>
                <p class="text-sm text-ink-600 leading-relaxed">
                    عند تفعيل هذه السياسة، أي مالك (Owner) في المؤسسة لن يستطيع استخدام البوابة قبل تفعيل
                    المصادقة الثنائية على حسابه الشخصي. الفائدة: حتى لو سُربت كلمة مرور مالك، لن يستطيع المهاجم
                    الدخول بدون كود MFA.
                </p>
            </div>
        </div>

        <form method="POST" action="{{ route('portal.organisation.security-policy.update') }}" class="space-y-5">
            @csrf
            @method('PATCH')

            <label class="flex items-start gap-3 p-4 rounded-xl border-2 cursor-pointer transition
                          {{ $org->requires_mfa_for_owners ? 'border-brand-500 bg-brand-50' : 'border-ink-200 hover:border-ink-300' }}">
                <input type="checkbox" name="requires_mfa_for_owners" value="1"
                       {{ $org->requires_mfa_for_owners ? 'checked' : '' }}
                       class="mt-0.5 rounded border-ink-300 text-brand-600 focus:ring-brand-400 h-4 w-4">
                <div class="flex-1">
                    <div class="font-semibold text-ink-950 mb-1">أجبر كل المالكين على تفعيل MFA</div>
                    <p class="text-sm text-ink-600">
                        المالكون الذين لم يفعّلوا MFA سيُعاد توجيههم لصفحة «الأمان» عند تسجيل الدخول حتى يكملوا التفعيل.
                    </p>
                </div>
            </label>

            <div class="pt-4 border-t border-ink-100 flex gap-3">
                <button type="submit" class="btn-primary !py-3 !px-5">
                    حفظ السياسة
                </button>
                <a href="{{ route('portal.organisation') }}" class="btn-secondary">رجوع</a>
            </div>
        </form>
    </section>
@endsection
