// android/settings.gradle.kts — T001 per specs/009-android-app/tasks.md
//
// Roots the Android Gradle module separately from the .NET solution. The
// `android/` folder is independent of `src/EgyptTax.Web/` etc.; the
// repo just shares git history so server + client changes can land in
// one PR (see plan.md "Structure Decision").

pluginManagement {
    repositories {
        google {
            content {
                includeGroupByRegex("com\\.android.*")
                includeGroupByRegex("com\\.google.*")
                includeGroupByRegex("androidx.*")
            }
        }
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "DaftarX"

include(":app")
