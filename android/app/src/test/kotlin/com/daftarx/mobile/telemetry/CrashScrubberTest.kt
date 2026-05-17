// T020 per specs/009-android-app/tasks.md (test-first per Constitution III).
//
// Per FR-017 the app MUST NOT send any business data / server URLs /
// session credentials / push tokens / receipt content / WebView DOM
// to the crash-reporting service. The CrashScrubber is a thin gate
// around FirebaseCrashlytics.setCustomKey that whitelists a handful
// of safe diagnostic keys and rejects everything else.
//
// These tests MUST observe RED before CrashScrubber.kt is implemented.
package com.daftarx.mobile.telemetry

import com.google.common.truth.Truth.assertThat
import io.mockk.mockk
import io.mockk.verify
import com.google.firebase.crashlytics.FirebaseCrashlytics
import org.junit.jupiter.api.Test

class CrashScrubberTest {

    private val crashlytics = mockk<FirebaseCrashlytics>(relaxed = true)
    private val scrubber = CrashScrubber(crashlytics)

    @Test
    fun `appVersion key is forwarded`() {
        scrubber.setCustomKey("appVersion", "1.0.0")

        verify { crashlytics.setCustomKey("appVersion", "1.0.0") }
    }

    @Test
    fun `locale key is forwarded`() {
        scrubber.setCustomKey("locale", "ar-EG")

        verify { crashlytics.setCustomKey("locale", "ar-EG") }
    }

    @Test
    fun `idleTimeoutSec key is forwarded`() {
        scrubber.setCustomKey("idleTimeoutSec", 300L)

        verify { crashlytics.setCustomKey("idleTimeoutSec", 300L) }
    }

    @Test
    fun `serverHostHash key is forwarded`() {
        scrubber.setCustomKey("serverHostHash", "a3f5c9d1")

        verify { crashlytics.setCustomKey("serverHostHash", "a3f5c9d1") }
    }

    @Test
    fun `arbitrary key is rejected and never reaches Crashlytics`() {
        scrubber.setCustomKey("currentInvoiceId", "7a9b2c3d-1234")

        verify(exactly = 0) { crashlytics.setCustomKey(any<String>(), any<String>()) }
        verify(exactly = 0) { crashlytics.setCustomKey(any<String>(), any<Long>()) }
    }

    @Test
    fun `server URL key is rejected even though it would help debugging`() {
        scrubber.setCustomKey("serverUrl", "http://192.168.1.10:50063")

        verify(exactly = 0) { crashlytics.setCustomKey(any<String>(), any<String>()) }
    }

    @Test
    fun `session credential key is rejected`() {
        scrubber.setCustomKey("sessionCookie", "AspNetCore.Cookies=abc123")

        verify(exactly = 0) { crashlytics.setCustomKey(any<String>(), any<String>()) }
    }

    @Test
    fun `push token key is rejected`() {
        scrubber.setCustomKey("fcmToken", "dEr3X8q5RFa...")

        verify(exactly = 0) { crashlytics.setCustomKey(any<String>(), any<String>()) }
    }

    @Test
    fun `WebView DOM dump key is rejected`() {
        scrubber.setCustomKey("page", "<html><body>...customer financial data...</body></html>")

        verify(exactly = 0) { crashlytics.setCustomKey(any<String>(), any<String>()) }
    }

    @Test
    fun `allowlist contains exactly the four FR-017 keys`() {
        assertThat(CrashScrubber.ALLOWED_KEYS).containsExactly(
            "appVersion", "locale", "idleTimeoutSec", "serverHostHash",
        )
    }
}
