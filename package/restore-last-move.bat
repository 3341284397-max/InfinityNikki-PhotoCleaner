@echo off
chcp 65001 >nul 2>&1
title Infinity Nikki Photo Cleanup Assistant
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0_src\Restore.ps1" 
if errorlevel 1 (
    echo.
    echo Startup failed. Make sure Windows PowerShell is available.
    echo 启动失败，请确认系统里 PowerShell 可用。
    pause
)
