@echo off
rem Movement simulation GUI review (doc/test/movement-cases.md)
rem Builds the C# solution first to avoid a stale-assembly error.
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%~dp0SanguoSLG.Game" --movetest
