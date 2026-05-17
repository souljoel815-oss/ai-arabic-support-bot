// T024 per specs/009-android-app/tasks.md.
//
// Retrofit interface for the three new server endpoints. Wire formats
// per:
//   - contracts/me-features.md
//   - contracts/notifications-register.md
//   - contracts/scan-receipt-mobile.md
//
// Endpoint implementations land in their owning user-story phases:
//   T056 — ScanReceiptMobileEndpoint (US2)
//   T071 — NotificationRegisterEndpoint (US3)
//   T104 — MeFeaturesEndpoint (US5)
//
// This file gives the Android codebase a typed API surface from Phase 2
// so subsequent user-story tasks (LicenseSnapshot, TokenRegistration,
// ReceiptUploader) can compile against it before the server is built.
package com.daftarx.mobile.net

import com.squareup.moshi.JsonClass
import okhttp3.RequestBody
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST
import retrofit2.http.Query

interface DaftarXApi {

    // -------------------------------------------------------------------------
    // GET /api/v1/me/features — license feature snapshot + version triple
    // (per contracts/me-features.md and data-model.md §4 LicenseFeatureSnapshot)
    // -------------------------------------------------------------------------
    @GET("api/v1/me/features")
    suspend fun getMeFeatures(): Response<MeFeaturesResponse>

    // -------------------------------------------------------------------------
    // POST /api/v1/notifications/register — first registration or refresh
    // (per contracts/notifications-register.md)
    // -------------------------------------------------------------------------
    @POST("api/v1/notifications/register")
    suspend fun registerNotificationToken(
        @Body body: NotificationRegisterRequest,
    ): Response<NotificationRegisterResponse>

    // -------------------------------------------------------------------------
    // DELETE /api/v1/notifications/register?deviceId=… — soft-revoke
    // (per contracts/notifications-register.md)
    // -------------------------------------------------------------------------
    @DELETE("api/v1/notifications/register")
    suspend fun unregisterNotificationToken(
        @Query("deviceId") deviceId: String,
    ): Response<Unit>

    // -------------------------------------------------------------------------
    // POST /api/v1/ai/scan-receipt-mobile — binary upload, headers carry
    // device-id + capture-source (per contracts/scan-receipt-mobile.md)
    // -------------------------------------------------------------------------
    @POST("api/v1/ai/scan-receipt-mobile")
    suspend fun scanReceiptMobile(
        @Header("Content-Type") contentType: String,
        @Header("X-DaftarX-Device-Id") deviceId: String,
        @Header("X-DaftarX-Capture-Source") captureSource: String,
        @Body body: RequestBody,
    ): Response<ScanReceiptResponse>
}

// -----------------------------------------------------------------------------
// Wire-format data classes — Moshi codegen via @JsonClass(generateAdapter)
// -----------------------------------------------------------------------------

@JsonClass(generateAdapter = true)
data class MeFeaturesResponse(
    val edition: String,
    val features: List<String>,
    val expiresAtUtc: String?,
    val fetchedAtUtc: String,
    val serverVersion: String,
    val minAppVersion: String,
    val latestKnownAppVersion: String,
)

@JsonClass(generateAdapter = true)
data class NotificationRegisterRequest(
    val deviceId: String,
    val fcmToken: String,
    val platform: String,
    val appVersion: String,
)

@JsonClass(generateAdapter = true)
data class NotificationRegisterResponse(
    val registrationId: String,
)

@JsonClass(generateAdapter = true)
data class ScanReceiptResponse(
    val scanId: String,
    val viewUrl: String,
    val status: String,
)
