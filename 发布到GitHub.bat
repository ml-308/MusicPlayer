@echo off
chcp 936 >nul
title 发布到 GitHub
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish_to_github.ps1"
exit /b
