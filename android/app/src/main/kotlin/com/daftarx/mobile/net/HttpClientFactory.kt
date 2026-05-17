// T023 per specs/009-android-app/tasks.md.
//
// Per research §2: the static network_security_config.xml denies
// cleartext globally. This factory layers per-request enforcement on
// top — every HTTP call is rejected at runtime unless its host
// matches the user-configured server host (and is allowed cleartext
// only if the user explicitly typed an http:// URL, not https://).
//
// CookieJar wiring (placeholder): the persistent jar lives in
// SessionVault (auth/SessionVault.kt) implemented in T038. This file
// references CookieJar via interface so the test path can swap a
// no-op jar without dragging Keystore + EncryptedSharedPreferences
// into a JVM unit test.
package com.daftarx.mobile.net

import okhttp3.CookieJar
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.Response
import okhttp3.logging.HttpLoggingInterceptor
import java.io.IOException
import java.util.concurrent.TimeUnit

/**
 * Per-app HTTP client. Single instance per process; injected via Hilt.
 *
 * @param configuredHostProvider lambda returning the host the user typed
 *        in Setup, or null when not configured yet. The interceptor
 *        evaluates this on EVERY request so changing the host (via the
 *        reset-server flow in FR-014) takes effect immediately without
 *        rebuilding the client.
 * @param allowCleartextProvider lambda returning whether the user
 *        explicitly opted into HTTP (false = HTTPS-only). Same evaluation
 *        cadence as the host.
 * @param cookieJar persistent cookie store; production wires SessionVault,
 *        tests pass a no-op or in-memory implementation.
 */
class HttpClientFactory(
    private val configuredHostProvider: () -> String?,
    private val allowCleartextProvider: () -> Boolean,
    private val cookieJar: CookieJar = CookieJar.NO_COOKIES,
) {
    fun build(): OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(30, TimeUnit.SECONDS)
        .addInterceptor(HostWhitelistInterceptor(configuredHostProvider, allowCleartextProvider))
        .addInterceptor(loggingInterceptor())
        .cookieJar(cookieJar)
        .build()

    private fun loggingInterceptor() = HttpLoggingInterceptor().apply {
        // BODY-level logging would print session cookies + receipt bytes —
        // forbidden by FR-017. HEADERS leaks the FCM token and the
        // session cookie. BASIC is the cap: method + URL + status only.
        level = HttpLoggingInterceptor.Level.BASIC
    }
}

/**
 * Rejects any request whose host doesn't match the user-configured
 * server, OR whose scheme is http when the user hasn't opted into
 * cleartext. Throws IOException (mapped to a network error by callers)
 * rather than logging silently — security failures must be loud.
 */
internal class HostWhitelistInterceptor(
    private val configuredHostProvider: () -> String?,
    private val allowCleartextProvider: () -> Boolean,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        val url: HttpUrl = request.url
        val configured = configuredHostProvider()
            ?: throw IOException("Server URL not configured yet.")

        if (!url.host.equals(configured, ignoreCase = true)) {
            throw IOException(
                "Host ${url.host} not on the whitelist (configured: $configured)."
            )
        }
        if (!url.isHttps && !allowCleartextProvider()) {
            throw IOException(
                "Cleartext HTTP blocked for $configured (user has not opted in)."
            )
        }
        return chain.proceed(request)
    }
}

/**
 * Helper for code that needs a quick ad-hoc check on an URL string
 * (e.g. ServerProbe before any Retrofit machinery is built).
 */
internal fun String.parseToHttpUrlOrNullSafely(): HttpUrl? = this.toHttpUrlOrNull()
