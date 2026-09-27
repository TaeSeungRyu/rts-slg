@echo off
setlocal
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64_console.exe"

"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%

"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --maptest --maptestmodalframeworkqa
exit /b %errorlevel%
