// T021 per specs/009-android-app/tasks.md — implementation that makes
// CrashScrubberTest pass (test-first per Constitution III).
//
// FR-017 forbids customer business data / server URLs / session
// credentials / push tokens / receipt content / WebView DOM from ever
// reaching the crash-reporting service. This gate wraps every
// setCustomKey call against a hard-coded allowlist; anything not on
// the list is silently dropped (no exception thrown — we never want a
// telemetry bug to crash the app).
//
// Stack traces themselves come straight from Crashlytics' uncaught-
// exception handler and don't go through this class; the scrubber only
// gates the *custom-key* attachment path.
package com.daftarx.mobile.telemetry

import com.google.firebase.crashlytics.FirebaseCrashlytics

class CrashScrubber(
    private val crashlytics: FirebaseCrashlytics,
) {
    fun setCustomKey(key: String, value: String) {
        if (key !in ALLOWED_KEYS) return
        crashlytics.setCustomKey(key, value)
    }

    fun setCustomKey(key: String, value: Long) {
        if (key !in ALLOWED_KEYS) return
        crashlytics.setCustomKey(key, value)
    }

    fun setCustomKey(key: String, value: Int) {
        if (key !in ALLOWED_KEYS) return
        crashlytics.setCustomKey(key, value)
    }

    fun setCustomKey(key: String, value: Boolean) {
        if (key !in ALLOWED_KEYS) return
        crashlytics.setCustomKey(key, value)
    }

    companion object {
        /**
         * The exhaustive set of keys this app is allowed to attach to a
         * crash report. Adding to this list = adding a non-secret
         * diagnostic field, and MUST be paired with a documented FR
         * justification — see FR-017.
         */
        val ALLOWED_KEYS: Set<String> = setOf(
            "appVersion",       // BuildConfig.VERSION_NAME
            "locale",           // e.g. "ar-EG"
            "idleTimeoutSec",   // user-chosen idle-timeout setting
            "serverHostHash",   // SHA-256 prefix of configured server host, NEVER the URL itself
        )
    }
}
