@echo off
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" goto missing
.venv\Scripts\python.exe test_connection.py
pause
exit /b
:missing
echo Run 01_setup.bat first.
pause
