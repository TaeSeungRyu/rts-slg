@echo off
rem Simple campaign map spectator (Core CampaignEngine + FactionAI). Builds first.
set "GODOT=D:\godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
"%GODOT%" --path "%~dp0SanguoSLG.Game" --maptest
