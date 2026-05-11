@echo off
REM Double-click to fully uninstall DaftarX. Auto-elevates to Admin.
REM Standard wipe — keeps SQL Server Express. For nuclear (also wipes
REM SQL), edit the line below to add: -Nuclear

setlocal
cd /d "%~dp0"

REM Auto-elevate if not running as admin.
net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall-daftarx.ps1"

echo.
echo ================================================================
echo  Uninstall finished. Press any key to close.
echo ================================================================
pause >nul
