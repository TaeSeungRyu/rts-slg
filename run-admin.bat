@echo off
rem Admin scene (Core AdminSession). Builds the C# solution first.
set "GODOT=D:\godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
"%GODOT%" --path "%~dp0SanguoSLG.Game" --admin
