@echo off
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
if not exist "%GODOT%" (
  echo Godot executable not found: "%GODOT%"
  exit /b 1
)
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%~dp0SanguoSLG.Game" "%~dp0SanguoSLG.Game\GeneralEditor.tscn"

