@echo off
setlocal
cd /d "%~dp0"

set "ARGS=%*"
if /i "%~1"=="test" set "ARGS=-SignForTesting %*"
if /i "%~1"=="fast" set "ARGS=-SkipPublish %*"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-msix.ps1" %ARGS%
set EXIT_CODE=%ERRORLEVEL%

if %EXIT_CODE% neq 0 (
    echo.
    echo [ERROR] Build failed with exit code: %EXIT_CODE%
)

echo %CMDCMDLINE% | find /i "%~f0" >nul
if not errorlevel 1 pause

exit /b %EXIT_CODE%
