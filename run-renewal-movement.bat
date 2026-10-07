@echo off
rem Phase 18D isolated continuous-movement QA scene. Does not switch run-maptest.
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
set "GODOT_CONSOLE=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
if not exist "%GODOT%" (
  echo [ERROR] Godot Mono executable not found: %GODOT%
  exit /b 1
)
if /i "%~1"=="qa" (
  if not exist "%GODOT_CONSOLE%" (
    echo [ERROR] Godot Mono console executable not found: %GODOT_CONSOLE%
    exit /b 1
  )
  "%GODOT_CONSOLE%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
  if errorlevel 1 exit /b %errorlevel%
  "%GODOT_CONSOLE%" --headless --path "%~dp0SanguoSLG.Game" --scene "res://RenewalMovementTest.tscn" -- --renewalmovementauto
  exit /b %errorlevel%
)
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%~dp0SanguoSLG.Game" "res://RenewalMovementTest.tscn"
