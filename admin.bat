@echo off
cd /d "%~dp0"
python tools\gen_admin_sheet.py
start "" "doc\admin-sheet.html"
