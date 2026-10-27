@echo off
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
set "GODOT_QA=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
"%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
if /i "%~1"=="qa" (
  "%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" res://GeneralCreator.tscn -- --generalcreatorqa
  exit /b %errorlevel%
)
"%GODOT%" --path "%~dp0SanguoSLG.Game" res://GeneralCreator.tscn
exit /b %errorlevel%
