@extends('layouts.portal')

@section('title', 'حذف الحساب')

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.account.security') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            الأمان والحساب
        </a>
        <p class="eyebrow mb-2 !text-red-700">منطقة خطرة</p>
        <h1 class="display-1 mb-1.5">حذف الحساب</h1>
        <p class="text-ink-600">طلب حذف نهائي لمؤسستك من DaftarX.</p>
    </header>

    @if ($org->soft_deleted_at)
        <div class="rounded-2xl border-2 border-red-300 bg-red-50/60 p-6 mb-6 relative overflow-hidden shadow-elevation-1">
            <span class="absolute inset-y-0 start-0 w-1.5 bg-red-500"></span>
            <div class="flex items-start gap-4">
                <span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-red-100 text-red-700 animate-pulse-soft">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <circle cx="12" cy="12" r="10" /><path d="M12 6v6l4 2" />
                    </svg>
                </span>
                <div class="flex-1">
                    <h2 class="text-xl font-bold text-red-900 mb-2">حسابك مجدول للحذف</h2>
                    <p class="text-sm text-red-800 mb-4 leading-relaxed">
                        طلب الحذف تم في <strong class="font-mono">{{ $org->soft_deleted_at->format('Y-m-d') }}</strong>،
                        والحذف النهائي سيتم في
                        <strong class="font-mono">{{ $org->soft_deleted_at->copy()->addDays(30)->format('Y-m-d') }}</strong>.
                        لازال أمامك <strong>{{ now()->diffInDays($org->soft_deleted_at->copy()->addDays(30)) }} يوم</strong> لإلغاء الطلب.
                    </p>
                    <form method="POST" action="{{ route('portal.account.restore') }}">
                        @csrf
                        <button type="submit" class="btn-primary">
                            إلغاء طلب الحذف واستعادة الحساب
                        </button>
                    </form>
                </div>
            </div>
        </div>
    @else
        <div class="rounded-2xl border-2 border-red-200 bg-red-50/60 p-6 mb-6 relative overflow-hidden">
            <span class="absolute inset-y-0 start-0 w-1 bg-red-500"></span>
            <div class="flex items-start gap-4">
                <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-red-100 text-red-700">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24"
                         fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0ZM12 9v4m0 4h.01" />
                    </svg>
                </span>
                <div class="flex-1">
                    <h2 class="text-lg font-bold text-red-900 mb-3">قبل أن تكمل، اقرأ:</h2>
                    <ul class="space-y-2 text-sm text-red-800 mb-2">
                        <li class="flex gap-2"><span class="text-red-600 mt-0.5">●</span><span>الحذف بـ <strong>30 يوم Grace Period</strong>: الحساب deactivate فوراً + تقدر تستعيده خلال 30 يوم.</span></li>
                        <li class="flex gap-2"><span class="text-red-600 mt-0.5">●</span><span>بعد 30 يوم: حذف <strong>نهائي</strong> لكل البيانات (اشتراكات، تراخيص، فواتير، تذاكر).</span></li>
                        <li class="flex gap-2"><span class="text-red-600 mt-0.5">●</span><span>سجل التدقيق (Audit Log) يُحفظ دائماً عشان متطلبات الرقابة المالية.</span></li>
                        <li class="flex gap-2"><span class="text-red-600 mt-0.5">●</span><span>الأعضاء التانيين في المؤسسة سيخرجون من الحساب فوراً.</span></li>
                        <li class="flex gap-2"><span class="text-red-600 mt-0.5">●</span><span>التراخيص النشطة على أجهزة العملاء ستنتهي بانتهاء فترة الاشتراك الحالية.</span></li>
                    </ul>
                </div>
            </div>
        </div>

        <form method="POST" action="{{ route('portal.account.delete') }}" class="card-padded space-y-5 max-w-xl">
            @csrf
            <div>
                <label class="form-label">
                    اكتب اسم الشركة بالظبط للتأكيد:
                </label>
                <p class="text-sm mb-2 px-3 py-2 bg-ink-50 rounded-lg text-ink-900 font-mono border border-ink-100">{{ $org->legal_name_ar }}</p>
                <input type="text" name="confirmation_text" required dir="rtl" class="form-input" placeholder="اكتب الاسم هنا">
                @error('confirmation_text')<p class="form-error">{{ $message }}</p>@enderror
            </div>
            <div class="pt-4 border-t border-ink-100 flex gap-3">
                <button type="submit" class="btn-danger !py-3 !px-5">
                    أؤكد حذف الحساب
                </button>
                <a href="{{ route('portal.dashboard') }}" class="btn-ghost">إلغاء</a>
            </div>
        </form>
    @endif
@endsection
