@echo off
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-official\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
dotnet build "%~dp0SanguoSLG.Game\SanguoSLG.Game.csproj" -v:q
if errorlevel 1 exit /b %errorlevel%
if /i "%~1"=="bare" (
  "%GODOT%" --path "%~dp0SanguoSLG.Game" res://ImeInputBare.tscn
  exit /b %errorlevel%
)
"%GODOT%" --path "%~dp0SanguoSLG.Game" res://ImeInputTest.tscn
exit /b %errorlevel%
