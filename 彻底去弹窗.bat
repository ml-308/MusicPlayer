@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0fix_popup.ps1"
echo.
pause
