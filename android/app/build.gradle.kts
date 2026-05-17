// android/app/build.gradle.kts — T009 per specs/009-android-app/tasks.md
//
// Application module: Compose + Hilt + Crashlytics + FCM + Retrofit + CameraX.
// minSdk 24 / target 34 per plan.md Target Platform; Kotlin 2.0 + Java 17.
// All versions sourced from gradle/libs.versions.toml (the catalog).

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.ksp)
    alias(libs.plugins.hilt)
    alias(libs.plugins.google.services)
    alias(libs.plugins.firebase.crashlytics)
    alias(libs.plugins.junit5.android)
}

android {
    namespace = "com.daftarx.mobile"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.daftarx.mobile"
        minSdk = 24                                 // ~98% device coverage per SC-005
        targetSdk = 34                              // 2026 Play Store requirement per plan.md
        versionCode = 1
        versionName = "1.0.0"                       // Compared against serverVersion / minAppVersion per FR-019
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"

        // BuildConfig fields the version-gate code in license/VersionGate.kt reads.
        // Bump MIN_SERVER_VERSION whenever this app gains a feature that depends on a newer server.
        buildConfigField("String", "MIN_SERVER_VERSION", "\"5.1.0\"")
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    // T013 — signing scaffold. Debug uses the default debug keystore.
    // Release reads from signing.properties (gitignored). The same upload
    // key is used for both side-load APK and Play upload per research §11 +
    // FR-013 + SC-006. See android/SIGNING.md for the dual-channel flow.
    signingConfigs {
        getByName("debug") {
            // Default debug keystore is fine for emulator testing.
        }
        create("release") {
            val propsFile = rootProject.file("signing.properties")
            if (propsFile.exists()) {
                val props = java.util.Properties().apply { propsFile.inputStream().use { load(it) } }
                storeFile = rootProject.file(props.getProperty("storeFile"))
                storePassword = props.getProperty("storePassword")
                keyAlias = props.getProperty("keyAlias")
                keyPassword = props.getProperty("keyPassword")
            }
            // CI / fresh-clone fallback: leave unsigned; assembleRelease will fail
            // with a clear message until signing.properties is provided.
        }
    }

    buildTypes {
        debug {
            isMinifyEnabled = false
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
            signingConfig = signingConfigs.getByName("debug")
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfig = signingConfigs.getByName("release")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlin {
        jvmToolchain(17)
    }

    // Kotlin sources live under src/<sourceSet>/kotlin/ (not java/) per
    // plan.md project structure.
    sourceSets {
        getByName("main") { kotlin.srcDirs("src/main/kotlin") }
        getByName("test") { kotlin.srcDirs("src/test/kotlin") }
        getByName("androidTest") { kotlin.srcDirs("src/androidTest/kotlin") }
    }

    packaging {
        resources {
            excludes += setOf(
                "META-INF/AL2.0", "META-INF/LGPL2.1",
                "META-INF/LICENSE.md", "META-INF/LICENSE-notice.md",
            )
        }
    }
}

dependencies {
    // Compose BOM aligns all Compose versions.
    val composeBom = platform(libs.compose.bom)
    implementation(composeBom)
    androidTestImplementation(composeBom)

    // UI
    implementation(libs.compose.ui)
    implementation(libs.compose.ui.tooling.preview)
    implementation(libs.compose.material3)
    implementation(libs.compose.material.icons.extended)
    implementation(libs.compose.foundation)
    implementation(libs.compose.runtime)
    implementation(libs.compose.activity)
    debugImplementation(libs.compose.ui.tooling)

    // AndroidX foundations
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.appcompat)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.lifecycle.process)
    implementation(libs.androidx.lifecycle.viewmodel.compose)

    // Native shell — research §1, §4, §5, §6
    implementation(libs.androidx.webkit)
    implementation(libs.androidx.biometric)
    implementation(libs.androidx.security.crypto)
    implementation(libs.androidx.camera.core)
    implementation(libs.androidx.camera.camera2)
    implementation(libs.androidx.camera.lifecycle)
    implementation(libs.androidx.camera.view)

    // Firebase — research §8 + §16. The Firebase BoM aligns FCM + Crashlytics versions.
    implementation(platform(libs.firebase.bom))
    implementation(libs.firebase.messaging)
    implementation(libs.firebase.crashlytics)

    // Hilt — DI
    implementation(libs.hilt.android)
    ksp(libs.hilt.compiler)
    implementation(libs.hilt.navigation.compose)

    // HTTP — Retrofit + Moshi
    implementation(libs.retrofit)
    implementation(libs.retrofit.moshi)
    implementation(libs.moshi)
    ksp(libs.moshi.kotlin.codegen)
    implementation(libs.okhttp)
    implementation(libs.okhttp.logging)

    // Coroutines
    implementation(libs.coroutines.core)
    implementation(libs.coroutines.android)

    // Test — research §14
    testImplementation(libs.junit.jupiter)
    testRuntimeOnly(libs.junit.jupiter.engine)
    testImplementation(libs.mockk)
    testImplementation(libs.robolectric)
    testImplementation(libs.truth)
    testImplementation(libs.coroutines.test)

    androidTestImplementation(libs.androidx.test.junit.ktx)
    androidTestImplementation(libs.androidx.test.core.ktx)
    androidTestImplementation(libs.espresso.core)
    androidTestImplementation(libs.compose.ui.test)
    androidTestImplementation(libs.mockk.android)
    androidTestImplementation(libs.truth)
    debugImplementation(libs.compose.ui.test.manifest)
}
