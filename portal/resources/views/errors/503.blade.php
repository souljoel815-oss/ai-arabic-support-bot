@extends('errors.layout')

@section('title', 'صيانة مؤقتة')

@section('code-block')
    <div class="mx-auto mb-6">
        <div class="h-24 w-24 mx-auto rounded-2xl flex items-center justify-center shadow-elevation-2"
             style="background: linear-gradient(135deg, #fcd34d 0%, #d97706 100%);">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-12 w-12 text-white animate-pulse-soft" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
            </svg>
        </div>
    </div>
@endsection

@section('headline')
    الموقع في صيانة مؤقتة
@endsection

@section('explanation')
    بنعمل تحديث سريع للخدمة. الموقع هيرجع خلال دقايق. حاول تاني بعد شوية.
@endsection
