@echo off
setlocal
cd /d "%~dp0"
if exist ".venv\Scripts\python.exe" goto install
py -3.11 -m venv .venv
if not errorlevel 1 goto install
py -3.12 -m venv .venv
if errorlevel 1 goto fail
:install
.venv\Scripts\python.exe -m pip install --upgrade pip
if errorlevel 1 goto fail
.venv\Scripts\python.exe -m pip install -r requirements.txt
if errorlevel 1 goto fail
.venv\Scripts\python.exe download_model.py
if errorlevel 1 goto fail
echo Setup complete. Run 02_start.bat next.
pause
exit /b 0
:fail
echo Setup failed. Read the error above. Python 3.11 or 3.12 64-bit is required.
pause
exit /b 1
