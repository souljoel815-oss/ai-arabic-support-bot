@echo off
REM ============================================================
REM   DaftarX — Workstation Setup
REM   Run on each workstation (NOT the server) AS ADMINISTRATOR
REM ============================================================

NET SESSION >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo.
    echo This script must be run as Administrator.
    echo Right-click the file and choose "Run as administrator".
    echo.
    pause
    exit /b 1
)

cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0trust-cert.ps1"

pause
