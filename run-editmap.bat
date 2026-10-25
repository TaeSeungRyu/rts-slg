@echo off
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
if /i "%~1"=="qa" (
  set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
  "%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
  if errorlevel 1 exit /b %errorlevel%
  "%GODOT%" --headless --path "%~dp0SanguoSLG.Game" res://EditMap.tscn -- --editmapqa
  exit /b %errorlevel%
)
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%~dp0SanguoSLG.Game" res://EditMap.tscn
exit /b %errorlevel%
