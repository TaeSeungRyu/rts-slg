@echo off
rem 이동 시뮬레이션 GUI 검증 (doc/test/movement-cases.md)
rem 실행 전에 C# 솔루션을 먼저 빌드해 "Cannot instantiate C# script"(스테일 어셈블리)를 막는다.
set GODOT="D:\godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
%GODOT% --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
%GODOT% --path "%~dp0SanguoSLG.Game" --movetest
