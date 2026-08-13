@echo off
cd /d "%~dp0"
python tools\gen_roster.py
start "" "doc\roster.html"
