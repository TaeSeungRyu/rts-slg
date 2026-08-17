@echo off
chcp 65001 >nul
rem Spectator campaign - autonomous war weekly log. Usage: watch.bat [weeks] [seed]
set WEEKS=%1
set SEED=%2
if "%WEEKS%"=="" set WEEKS=40
if "%SEED%"=="" set SEED=42
dotnet run --project "%~dp0SanguoSLG.Sandbox" -- --watch %WEEKS% --seed %SEED%
pause
