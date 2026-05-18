<?php

/**
 * T069 (ar) — US2 licence-management UI strings.
 */
return [
    'title' => 'إدارة التراخيص',
    'subtitle' => 'كل التراخيص المرتبطة باشتراكاتك. تقدر تنزّل ملف الترخيص، تنقله لجهاز جديد، أو تتابع تواريخ الانتهاء.',
    'no_subscriptions' => 'مفيش اشتراك مدفوع لحد دلوقتي. اشترك في خطة عشان تقدر تنشّط تراخيص.',
    'subscribe_cta' => 'اشترك في خطة',
    'priority_support_badge' => 'دعم مميز (4 ساعات عمل)',
    'tier_label' => 'الفئة',
    'cadence_label' => 'الدفع',
    'status_label' => 'الحالة',
    'period_end_label' => 'نهاية الفترة الحالية',
    'cadence' => [
        'Monthly' => 'شهري',
        'Annual' => 'سنوي',
    ],
    'status' => [
        'Active' => 'نشط',
        'PastDue' => 'متأخر',
        'Cancelled' => 'ملغى',
        'Paused' => 'موقوف مؤقتاً',
    ],
    'licences_list' => [
        'title' => 'التراخيص المنشّطة',
        'empty' => 'مفيش ترخيص منشّط في هذا الاشتراك. اضغط «تنشيط ترخيص جديد».',
        'columns' => [
            'hwid' => 'معرّف الجهاز (HWID)',
            'edition' => 'الإصدار',
            'issued_at' => 'تاريخ الإصدار',
            'expires_at' => 'تاريخ الانتهاء',
            'actions' => 'الإجراءات',
        ],
        'actions' => [
            'download' => 'تحميل ملف الترخيص',
            'transfer' => 'نقل لجهاز آخر',
        ],
        'retired_section_title' => 'تراخيص محفوظة (مؤرشفة)',
        'retired_columns' => [
            'reason' => 'سبب الأرشفة',
            'retired_at' => 'تاريخ الأرشفة',
        ],
        'reason' => [
            'Transferred' => 'تم النقل',
            'SubscriptionCancelled' => 'إلغاء الاشتراك',
            'Refunded' => 'استرداد',
            'TierUpgraded' => 'ترقية الفئة',
        ],
    ],
    'activate' => [
        'title' => 'تنشيط ترخيص مدفوع',
        'instructions' => 'افتح برنامج دفترx على الجهاز المراد ترخيصه. ستظهر شاشة التنشيط رقم HWID على هيئة XXXX-XXXX-XXXX-XXXX. انسخه الصق هنا.',
        'subscription_label' => 'الاشتراك',
        'hwid_label' => 'معرّف الجهاز (HWID)',
        'hwid_placeholder' => 'مثال: A1B2-C3D4-E5F6-7890',
        'submit' => 'تنشيط الترخيص',
        'cancel' => 'إلغاء',
    ],
    'transfer' => [
        'title' => 'نقل الترخيص لجهاز آخر',
        'instructions' => 'النقل ينهي الترخيص الحالي على الجهاز القديم ويصدر ترخيص جديد بنفس الإصدار وتاريخ الانتهاء على الجهاز الجديد.',
        'warning' => 'تحذير: الجهاز القديم لن يعد قادر على استخدام البرنامج بعد إكتمال النقل.',
        'old_hwid_label' => 'الجهاز الحالي',
        'new_hwid_label' => 'معرّف الجهاز الجديد (HWID)',
        'new_hwid_placeholder' => 'مثال: 7890-E5F6-C3D4-A1B2',
        'edition_label' => 'الإصدار',
        'expires_at_label' => 'تاريخ الانتهاء',
        'submit' => 'تأكيد النقل',
        'cancel' => 'إلغاء',
    ],
    'activated_flash' => 'تم تنشيط الترخيص. تنزيل ملف license.token جاهز الآن.',
    'transferred_flash' => 'تم النقل بنجاح. تنزيل ملف license.token الجديد جاهز الآن.',
];
