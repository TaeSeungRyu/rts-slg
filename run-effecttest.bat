@echo off
rem 효과 검수 (doc/design-effect.md) — 실행 전 솔루션 빌드
set GODOT="D:\godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
%GODOT% --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
%GODOT% --path "%~dp0SanguoSLG.Game" --effecttest
