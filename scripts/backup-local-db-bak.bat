@echo off
setlocal

set SCRIPT_DIR=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%backup-local-db-bak.ps1"

if errorlevel 1 (
    echo.
    echo Backup .bak that bai.
    exit /b 1
)

echo.
echo Backup .bak hoan tat.
exit /b 0
