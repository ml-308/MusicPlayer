@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
rem 先确保存在便携包
if not exist "..\dist\MusicPlayer-1.0.0-portable.zip" (
  echo [错误] 缺少 ..\dist\MusicPlayer-1.0.0-portable.zip，请先打包便携版
  pause
  exit /b 1
)
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
echo 正在编译安装器...
"%CSC%" @build-setup.rsp
if errorlevel 1 (
  echo [错误] 编译失败
  pause
  exit /b 1
)
echo [完成] 已生成 ..\dist\MusicPlayerSetup.exe
pause
