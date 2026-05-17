# android/app/proguard-rules.pro — T012 per specs/009-android-app/tasks.md
#
# R8/ProGuard rules for release builds. Most rules come from the
# default android.txt + library consumer rules (AGP wires those in
# automatically). This file holds the project-specific keeps.

# -----------------------------------------------------------------------------
# Crashlytics — keep mapping file info so deobfuscated stack traces work
# -----------------------------------------------------------------------------
-keepattributes SourceFile,LineNumberTable
-renamesourcefileattribute DaftarX

# -----------------------------------------------------------------------------
# Compose — generally handled by androidx.compose consumer rules but
# the runtime classes that the compose compiler emits need to survive.
# -----------------------------------------------------------------------------
-keep class androidx.compose.** { *; }
-keepclasseswithmembers class * {
    @androidx.compose.runtime.Composable <methods>;
}

# -----------------------------------------------------------------------------
# WebView — keep the JS interface bridge classes if/when we add any.
# (None yet in the scaffold; the @JavascriptInterface annotation marks
# them. Leaving the rule here so future story phases inherit it.)
# -----------------------------------------------------------------------------
-keepclassmembers class * {
    @android.webkit.JavascriptInterface <methods>;
}
-keep class * extends android.webkit.WebViewClient { *; }

# -----------------------------------------------------------------------------
# Moshi — codegen-generated adapters are kept by the moshi-kotlin-codegen
# consumer rules. This explicit keep covers any hand-written ones.
# -----------------------------------------------------------------------------
-keep class com.daftarx.mobile.**.*JsonAdapter { *; }

# -----------------------------------------------------------------------------
# Retrofit / OkHttp — the libraries ship their own consumer rules; no
# project additions needed unless we add reflection-based services.
# -----------------------------------------------------------------------------

# -----------------------------------------------------------------------------
# Hilt — generated component classes
# -----------------------------------------------------------------------------
-keep,allowobfuscation,allowshrinking class * extends dagger.hilt.android.internal.lifecycle.HiltViewModelFactory
-keep class * implements dagger.hilt.internal.GeneratedComponent { *; }

# -----------------------------------------------------------------------------
# Firebase Messaging — service classes referenced by manifest only
# -----------------------------------------------------------------------------
-keep class com.daftarx.mobile.push.DaftarXMessagingService { *; }
