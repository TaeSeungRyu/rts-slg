@echo off
rem Phase 18D renewal movement campaign map. Add --legacy-movement after --maptest to compare/rollback.
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
if /i "%~1"=="qa" (
  set "GODOT_QA=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
  goto campaignqa
)
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%~dp0SanguoSLG.Game" -- --maptest %*
exit /b %errorlevel%

:campaignqa
"%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" -- --maptest --maptestrenewalcampaignqa
exit /b %errorlevel%
