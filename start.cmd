@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
if not exist "%~dp0.runtime\desktop\DDMonitor.exe" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-desktop.ps1"
    if errorlevel 1 (
        pause
        exit /b 1
    )
)
start "" "%~dp0.runtime\desktop\DDMonitor.exe"
