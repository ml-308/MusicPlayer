@echo off
rem 通过命令行启动可以避开 SmartScreen 对未签名程序的信誉弹窗
cd /d "%~dp0"
start "" "%~dp0MusicPlayer.exe"
exit
