@echo off
REM Double-click to capture diagnostics. Auto-elevates to Admin.
REM Produces daftarx-diagnostic.txt next to this file. Send the txt
REM back to support.

setlocal
cd /d "%~dp0"

REM Auto-elevate if not running as admin.
net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

if not exist "%~dp0diagnose.ps1" (
    echo ERROR: diagnose.ps1 not found in this folder.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0diagnose.ps1"

echo.
echo ================================================================
echo  Diagnostic finished. Send daftarx-diagnostic.txt to support.
echo ================================================================
pause >nul
