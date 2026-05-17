// T022 per specs/009-android-app/tasks.md.
//
// Configures Firebase Crashlytics + attaches the FR-017-compliant
// diagnostic keys via CrashScrubber. Called once from DaftarXApp.onCreate.
//
// If google-services.json is absent (e.g. fresh dev clone before the
// vendor file lands), FirebaseApp.initializeApp returns null and we
// no-op gracefully — Crashlytics simply doesn't fire. The app stays
// usable.
package com.daftarx.mobile.telemetry

import android.content.Context
import android.util.Log
import com.daftarx.mobile.BuildConfig
import com.google.firebase.FirebaseApp
import com.google.firebase.crashlytics.FirebaseCrashlytics
import java.security.MessageDigest
import java.util.Locale

object CrashlyticsBootstrap {

    private const val TAG = "DaftarXCrashlytics"

    fun init(context: Context, serverHost: String?) {
        // If Firebase failed to init (no google-services.json), bail
        // quietly. FirebaseApp.initializeApp returns the existing
        // instance or null on missing config; calling getInstance()
        // without one throws — guard with the null check first.
        if (FirebaseApp.initializeApp(context) == null) {
            Log.i(TAG, "Crashlytics disabled (no Firebase config present).")
            return
        }

        val crashlytics = FirebaseCrashlytics.getInstance()
        val scrubber = CrashScrubber(crashlytics)

        // Allowlisted diagnostic keys per FR-017 — see CrashScrubber.ALLOWED_KEYS.
        scrubber.setCustomKey("appVersion", BuildConfig.VERSION_NAME)
        scrubber.setCustomKey("locale", Locale.getDefault().toLanguageTag())
        // serverHost is hashed before attachment so the cleartext URL
        // never enters Crashlytics' retention. A SHA-256 prefix is enough
        // for the vendor to group reports by customer without leaking
        // the customer's LAN topology.
        if (!serverHost.isNullOrBlank()) {
            scrubber.setCustomKey("serverHostHash", hashHostPrefix(serverHost))
        }
    }

    private fun hashHostPrefix(host: String): String {
        val md = MessageDigest.getInstance("SHA-256")
        val hash = md.digest(host.toByteArray(Charsets.UTF_8))
        // 8 hex chars = 32 bits of host identity, enough to disambiguate
        // customers without being a reversible identifier.
        return hash.take(4).joinToString("") { "%02x".format(it) }
    }
}
