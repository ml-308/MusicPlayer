@echo off
rem ============================================================
rem  MusicPlayer - build script  (.NET Framework csc, no deps)
rem  产物：MusicPlayer.exe（单文件，无第三方依赖）
rem  说明：播放后端用 WinRT Windows.Media.Playback，
rem        因此需要 WinMetadata（.winmd）与 WindowsRuntime 引用。
rem ============================================================
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" ( echo [ERROR] csc.exe not found & exit /b 1 )

echo [1/2] Compile MusicPlayer.exe ...
"%CSC%" -nologo -target:winexe -optimize+ -platform:anycpu ^
  -out:"MusicPlayer.exe" ^
  -win32manifest:"src\app.manifest" -win32icon:"src\app.ico" ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Xml.dll -r:System.Runtime.dll ^
  -r:%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\System.Runtime.InteropServices.WindowsRuntime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.InteropServices.WindowsRuntime.dll ^
  -r:%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\System.Runtime.WindowsRuntime\v4.0_4.0.0.0__b77a5c561934e089\System.Runtime.WindowsRuntime.dll ^
  -r:%WINDIR%\System32\WinMetadata\Windows.Foundation.winmd ^
  -r:%WINDIR%\System32\WinMetadata\Windows.Media.winmd ^
  -r:%WINDIR%\System32\WinMetadata\Windows.Storage.winmd ^
  "src\*.cs"
if errorlevel 1 ( echo [ERROR] compile failed & exit /b 1 )

rem === 代码签名：彻底消除 SmartScreen「无法识别的应用」（未配置证书时自动跳过）===
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sign.ps1" -Path "%~dp0MusicPlayer.exe"

echo [2/2] Done -^> MusicPlayer.exe
endlocal
