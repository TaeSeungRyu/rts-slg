@echo off
rem Movement simulation GUI review (doc/test/movement-cases.md)
rem Builds the C# solution first to avoid a stale-assembly error.
set "GODOT=D:\godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
"%GODOT%" --path "%~dp0SanguoSLG.Game" --movetest
