@echo off
cd /d "%~dp0"
set "KOTOMOGU_PY="
py -3.11 -c "import sys" >nul 2>&1
if not errorlevel 1 set "KOTOMOGU_PY=py -3.11"
if defined KOTOMOGU_PY goto found
py -3.12 -c "import sys" >nul 2>&1
if not errorlevel 1 set "KOTOMOGU_PY=py -3.12"
if defined KOTOMOGU_PY goto found
echo Please install Python 3.11 or 3.12 64-bit with Python Launcher.
pause
exit /b 1
:found
if exist .venv\Scripts\python.exe goto install
%KOTOMOGU_PY% -m venv .venv
if errorlevel 1 goto failed
:install
.venv\Scripts\python.exe -m pip install --upgrade pip
if errorlevel 1 goto failed
.venv\Scripts\python.exe -m pip install -r requirements.txt
if errorlevel 1 goto failed
echo Setup complete. Run 02_start.bat next.
pause
exit /b 0
:failed
echo Setup failed. Read the error above.
pause
exit /b 1
