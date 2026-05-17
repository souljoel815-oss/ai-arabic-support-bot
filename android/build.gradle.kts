// android/build.gradle.kts (root) — T001 per specs/009-android-app/tasks.md
//
// Top-level project file. Subproject plugins are declared with
// `apply false` so each subproject opts in; concrete versions live in
// gradle/libs.versions.toml (the version catalog) to keep this file
// thin and let the catalog be the single source of truth.

plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.compose) apply false
    alias(libs.plugins.ksp) apply false
    alias(libs.plugins.hilt) apply false
    alias(libs.plugins.google.services) apply false
    alias(libs.plugins.firebase.crashlytics) apply false
}
