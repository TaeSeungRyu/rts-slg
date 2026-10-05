@echo off
rem Phase 18D isolated continuous-movement QA scene. Does not switch run-maptest.
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
if not exist "%GODOT%" (
  echo [ERROR] Godot Mono executable not found: %GODOT%
  exit /b 1
)
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%~dp0SanguoSLG.Game" "res://RenewalMovementTest.tscn"
