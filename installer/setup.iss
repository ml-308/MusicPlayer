; 轻音播放器 · 安装程序（Inno Setup 6）
; 页面结构与选项由 common.iss 统一提供；这里只声明应用身份与要安装的文件。
; 构建：installer\build-setup.cmd   产物：dist\MusicPlayer-Setup-1.1.0.exe

#define AppName "轻音播放器"
#define AppSlug "MusicPlayer"
; 沿用旧安装器的目录（升级不换位置），避免出现两份安装
#define DirName "LightMusicPlayer"
#define AppVersion "1.1.0"
#define AppId "{{3B7E2C41-6D18-4A2F-B953-8E0C5A71D264}"
#define AppExe "MusicPlayer.exe"
#define AppRepo "MusicPlayer"
#define AppTagline "本地音乐播放器：只读本地文件、不联网、不写注册表，双击就能听。"
#define IconPath "src\app.ico"

; 旧版自绘安装器（setup\SetupForm.cs）留下的痕迹，装完自动清理
#define LegacyUninstallKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\LightMusicPlayer"

#include "common.iss"

[Files]
Source: "{#AppRoot}\MusicPlayer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppRoot}\MusicPlayer.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppRoot}\README.md"; DestDir: "{app}"
Source: "{#AppRoot}\LICENSE"; DestDir: "{app}"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: startmenuicon
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon
