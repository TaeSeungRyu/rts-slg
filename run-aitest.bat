@echo off
rem Phase 21 AI fixture: data\aitest-fixture.json.
set "GODOT=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64.exe"
set "GODOT_QA=D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
if /i "%~1"=="qa" goto qa
"%GODOT%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
"%GODOT%" --path "%~dp0SanguoSLG.Game" -- --aitest %*
exit /b %errorlevel%

:qa
"%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" --build-solutions --quit
if errorlevel 1 exit /b %errorlevel%
for %%C in (0 1 2) do (
  "%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" -- --aitest --aitestcase=%%C --aitestfixtureqa
  if errorlevel 1 exit /b 1
)
"%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" -- --aitest --aitestcaseswitchqa
if errorlevel 1 exit /b 1
"%GODOT_QA%" --headless --path "%~dp0SanguoSLG.Game" -- --aitest --aitestwarqa
exit /b %errorlevel%
