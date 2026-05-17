// T019 per specs/009-android-app/tasks.md.
//
// Single-activity host. Compose `setContent` mounts a placeholder
// Scaffold; the real navigation (SetupScreen → LockScreen →
// BiometricPrompt → WebView) is wired in user-story phases:
//   - T048 routes Setup ↔ LockScreen ↔ BiometricPrompt ↔ DaftarXWebView
//   - T085 adds the daftarx://… deep-link intent-filter
//   - T090 wires VersionGate + FeaturePoller into ON_RESUME
//   - T112 routes to AppOutdatedScreen / ServerOutdatedScreen / UpdateAvailableBanner
//
// The activity declares `android:configChanges` in the manifest so
// orientation / locale / layoutDirection changes don't re-create the
// activity; Compose preserves state across config changes via remember.
package com.daftarx.mobile

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import com.daftarx.mobile.ui.theme.DaftarXTheme
import dagger.hilt.android.AndroidEntryPoint

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            DaftarXTheme {
                AppRoot()
            }
        }
    }
}

/**
 * Placeholder root. Replaced by the navigation graph in T048 (US1) once
 * ServerConfig, BiometricGate, and DaftarXWebView are implemented. Until
 * then this renders a single neutral string so the app installs + opens
 * cleanly during Phase 2 verification.
 */
@Composable
private fun AppRoot() {
    Scaffold { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                text = "DaftarX",
                style = MaterialTheme.typography.displayLarge,
            )
        }
    }
}
