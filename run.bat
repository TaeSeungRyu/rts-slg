@echo off
rem Run SanguoSLG (Godot .NET build required)
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
dotnet build "%~dp0tools\ime-native-probe\ImeNativeProbe.csproj" --configuration Release -v:q
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%~dp0SanguoSLG.Game"
