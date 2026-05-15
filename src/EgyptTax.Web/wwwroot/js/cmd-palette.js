/**
 * v5 UI Rebuild — Command Palette JS Interop
 * Handles Ctrl+K / Cmd+K keyboard shortcut to open the palette,
 * and provides the global `daftarxOpenCmdPalette` function that
 * the sidebar search button calls via IJSRuntime.
 */
(function () {
    "use strict";

    let _dotNetRef = null;
    let _keyHandler = null;

    /**
     * Called once from CommandPalette.razor OnAfterRenderAsync(firstRender).
     * Stores the .NET object reference and registers the global keydown.
     */
    window.daftarxCmdPaletteInit = function (dotNetRef) {
        _dotNetRef = dotNetRef;

        _keyHandler = function (e) {
            if ((e.ctrlKey || e.metaKey) && e.key === "k") {
                e.preventDefault();
                e.stopPropagation();
                openPalette();
            }
        };

        document.addEventListener("keydown", _keyHandler, { capture: true });
    };

    /**
     * Called from CommandPalette.razor DisposeAsync.
     */
    window.daftarxCmdPaletteDestroy = function () {
        if (_keyHandler) {
            document.removeEventListener("keydown", _keyHandler, { capture: true });
            _keyHandler = null;
        }
        _dotNetRef = null;
    };

    /**
     * Global function called by ModuleSidebar's search button.
     */
    window.daftarxOpenCmdPalette = function () {
        openPalette();
    };

    function openPalette() {
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync("Open");
        }
    }

    /**
     * Logout helper — called by ModuleSidebar logout button.
     * Navigates to /logout with a full page reload.
     */
    window.daftarxLogout = function () {
        window.location.href = "/logout";
    };
})();
