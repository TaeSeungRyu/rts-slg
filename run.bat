@echo off
rem Run SanguoSLG (Godot .NET build required)
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
"%GODOT%" --path "%~dp0SanguoSLG.Game"
