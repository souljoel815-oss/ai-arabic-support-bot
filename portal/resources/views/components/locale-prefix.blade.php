{{-- T151 — emits the URL prefix for the current locale ("/ar" / "/en")
     or empty string for the locale-less default route. Lets Blade
     templates write locale-aware links without recomputing the prefix
     in every view:

         <a href="{{ locale-prefix }}/features">…</a>

     becomes the right link whether the visitor lands on /features,
     /ar/features, or /en/features. --}}
@php
    $path = '/' . trim(request()->path(), '/');
    if (preg_match('#^/(ar|en)(/|$)#', $path, $m)) {
        echo '/' . $m[1];
    } else {
        echo '';
    }
@endphp
