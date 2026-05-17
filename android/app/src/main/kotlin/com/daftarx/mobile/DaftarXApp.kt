// T017 per specs/009-android-app/tasks.md.
//
// Application class:
//   - @HiltAndroidApp wires the DI graph for every subsequent component.
//   - CrashlyticsBootstrap.init populates the FR-017 diagnostic keys.
//   - Per-app locale (FR-011 / research §13) — defaults to ar-EG on
//     first cold start so the WebView's Accept-Language and the native
//     shell screens are both Arabic-first; user can override from
//     SettingsScreen later.
package com.daftarx.mobile

import android.app.Application
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import com.daftarx.mobile.telemetry.CrashlyticsBootstrap
import dagger.hilt.android.HiltAndroidApp

@HiltAndroidApp
class DaftarXApp : Application() {

    override fun onCreate() {
        super.onCreate()
        bootstrapLocale()
        // serverHost stays null until ServerConfig is loaded in
        // MainActivity; Crashlytics still initialises with the
        // appVersion + locale keys. The serverHostHash key is attached
        // later by ServerConfig.observe.
        CrashlyticsBootstrap.init(applicationContext, serverHost = null)
    }

    private fun bootstrapLocale() {
        val current = AppCompatDelegate.getApplicationLocales()
        if (current.isEmpty) {
            // No per-app override has been set yet → default to Arabic.
            AppCompatDelegate.setApplicationLocales(
                LocaleListCompat.forLanguageTags("ar-EG"),
            )
        }
    }
}
