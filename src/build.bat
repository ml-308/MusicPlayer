@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [错误] 找不到 .NET Framework 编译器 csc.exe（系统需要 .NET Framework 4.x）
  pause
  exit /b 1
)
echo 正在编译...
"%CSC%" @build.rsp
if errorlevel 1 (
  echo [错误] 编译失败
  pause
  exit /b 1
)
echo [完成] 已生成 ..\MusicPlayer.exe
pause
