@echo off
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-official\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
set "GODOT_QA=D:\LOCAL-WORK-STATION\Godot_v4.7.2-official\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
dotnet build "%~dp0SanguoSLG.Game\SanguoSLG.Game.csproj" --nologo -v:q
if errorlevel 1 exit /b %errorlevel%
rem Use the map saved in run-editmap. Create the editor's current default only on first use.
if not exist "%APPDATA%\Godot\app_userdata\SanguoSLG\map-presets\east-asia.json" (
  "%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" res://EditMap.tscn -- --export-newmap-if-missing
  if errorlevel 1 exit /b %errorlevel%
)
if /i "%~1"=="stress100" (
  if /i "%~2"=="qa" (
    "%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" -- --newmap --newmapstress100 --newmapstress100advanceqa
    exit /b %errorlevel%
  )
  "%GODOT%" --path "%~dp0SanguoSLG.Game" -- --newmap --newmapstress100
  exit /b %errorlevel%
)
if /i "%~1"=="battle60" (
  if /i "%~2"=="qa" (
    "%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" -- --newmap --newmapbattle60 --newmapbattle60qa
    exit /b %errorlevel%
  )
  "%GODOT%" --path "%~dp0SanguoSLG.Game" -- --newmap --newmapbattle60
  exit /b %errorlevel%
)
if "%~1"=="" (
  "%GODOT%" --path "%~dp0SanguoSLG.Game" -- --newmap --newmapbattle60
  exit /b %errorlevel%
)
if /i "%~1"=="qa" (
  "%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" -- --newmap --newmapqa
  exit /b %errorlevel%
)
if /i "%~1"=="aiqa" (
  "%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" -- --newmap --newmapaiqa
  exit /b %errorlevel%
)
"%GODOT%" --path "%~dp0SanguoSLG.Game" -- --newmap %*
exit /b %errorlevel%
