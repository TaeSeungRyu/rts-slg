@echo off
rem Combat review (Core AdvanceOrchestrator). Builds the C# solution first.
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%~dp0SanguoSLG.Game" --combattest
