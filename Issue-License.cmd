@echo off
REM Double-click to issue a DaftarX license interactively.
REM Calls Issue-License.ps1 in the same folder.

setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Issue-License.ps1" %*

if errorlevel 1 (
    echo.
    echo License issuing failed. See messages above.
    pause >nul
    exit /b %errorlevel%
)

echo.
echo ================================================================
echo  License issued. Folder opened in Explorer.
echo ================================================================
start "" "%~dp0licenses"
pause >nul
