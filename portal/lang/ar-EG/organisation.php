<?php

return [
    'title' => 'إدارة المؤسسة',
    'subtitle' => 'أعضاء فريقك في DaftarX. الأدوار تحدّد صلاحيات كل عضو.',
    'role' => [
        'Owner' => 'مالك',
        'BillingAdmin' => 'مسؤول الفواتير',
        'SupportAdmin' => 'مسؤول الدعم',
        'ReadOnly' => 'قارئ فقط',
    ],
    'role_description' => [
        'Owner' => 'صلاحيات كاملة (دعوة، فوترة، تراخيص، إلغاء)',
        'BillingAdmin' => 'فواتير + اشتراكات + ترقيات',
        'SupportAdmin' => 'تذاكر الدعم فقط',
        'ReadOnly' => 'مشاهدة فقط (لا تعديل)',
    ],
    'members_list' => [
        'title' => 'الأعضاء الحاليين',
        'empty' => 'لا يوجد أعضاء بعد. ادع شريك أو محاسب من النموذج بالأسفل.',
        'columns' => [
            'name' => 'الاسم',
            'email' => 'البريد',
            'role' => 'الدور',
            'joined_at' => 'انضم في',
            'status' => 'الحالة',
            'actions' => 'الإجراءات',
        ],
        'status' => [
            'active' => 'نشط',
            'pending' => 'بانتظار القبول',
            'revoked' => 'مُلغى الصلاحية',
        ],
        'revoke_button' => 'إلغاء الصلاحية',
        'revoke_confirm' => 'متأكد إنك عايز تشيل :name من المؤسسة؟ هيخرج فوراً ومش هيقدر يدخل تاني.',
    ],
    'pending_section_title' => 'الدعوات المعلّقة',
    'pending_columns' => [
        'email' => 'البريد',
        'role' => 'الدور',
        'invited_by' => 'بدعوة من',
        'expires_at' => 'تنتهي في',
    ],
    'invite_form' => [
        'title' => 'دعوة عضو جديد',
        'email_label' => 'البريد الإلكتروني',
        'role_label' => 'الدور',
        'display_name_label' => 'الاسم (اختياري)',
        'locale_label' => 'لغة الدعوة',
        'submit' => 'إرسال الدعوة',
    ],
    'invitation_sent_flash' => 'تم إرسال الدعوة إلى :email. تنتهي خلال 7 أيام.',
    'member_removed_flash' => 'تم إزالة العضو. سيخرج من الجلسة فور تحديثه للصفحة.',

    // Accept-invitation page (public)
    'accept' => [
        'title' => 'قبول الدعوة',
        'subtitle' => 'تم دعوتك للانضمام إلى :org بصلاحية :role.',
        'set_password_label' => 'كلمة المرور (12 حرف على الأقل + رقم + حرف كبير وصغير)',
        'display_name_label' => 'اسمك',
        'existing_account_note' => 'البريد ده موجود بالفعل في DaftarX. هتدخل بكلمة المرور الحالية بتاعتك.',
        'submit_new' => 'إنشاء حساب وقبول الدعوة',
        'submit_existing' => 'قبول الدعوة',
    ],
    'accept_welcome_flash' => 'مرحباً! تم قبول الدعوة، يمكنك الآن إدارة المؤسسة.',
    'expired_title' => 'الدعوة منتهية',
    'expired_body' => 'الرابط ده انتهت صلاحيته. اطلب من المالك يبعتلك دعوة جديدة.',
    'already_used_title' => 'الدعوة تم استخدامها',
    'already_used_body' => 'الرابط ده اتقبل قبل كده. لو إنت عايز تدخل البوابة سجل دخول من /login.',
];
