@echo off
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
set "PROJECT=%~dp0SanguoSLG.Game"
"%GODOT%" --headless --path "%PROJECT%" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%PROJECT%" res://SeasonTerrainPreview.tscn %*
