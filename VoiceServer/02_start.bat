@echo off
cd /d "%~dp0"
if not exist .venv\Scripts\python.exe goto missing
.venv\Scripts\python.exe server.py --model small
pause
exit /b
:missing
echo Run 01_setup.bat first.
pause
