@echo off
REM Double-click to install + activate DaftarX. Auto-elevates to Admin.
REM Requires DaftarX-Setup.exe + activate-customer.ps1 in the same folder.

setlocal
cd /d "%~dp0"

REM Auto-elevate if not running as admin.
net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

if not exist "%~dp0DaftarX-Setup.exe" (
    echo ERROR: DaftarX-Setup.exe not found in this folder.
    echo Put DaftarX-Setup.exe next to this .cmd file and try again.
    pause
    exit /b 1
)

if not exist "%~dp0activate-customer.ps1" (
    echo ERROR: activate-customer.ps1 not found in this folder.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0activate-customer.ps1"

echo.
echo ================================================================
echo  Install finished. Press any key to close.
echo ================================================================
pause >nul
