@echo off
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-verified\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
set "GODOT_QA=D:\LOCAL-WORK-STATION\Godot_v4.7.2-verified\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
if not exist "%GODOT%" (
  echo Godot executable not found: "%GODOT%"
  exit /b 1
)
dotnet build "%~dp0SanguoSLG.Game\SanguoSLG.Game.csproj" -v:q
if errorlevel 1 exit /b %errorlevel%
if /i "%~1"=="qa" (
  "%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" res://EditMap.tscn -- --editmapqa
  exit /b %errorlevel%
)
"%GODOT%" --path "%~dp0SanguoSLG.Game" res://EditMap.tscn
exit /b %errorlevel%
