@echo off
setlocal

set VERSION=1.0.0
if not "%~1"=="" set VERSION=%~1

powershell -ExecutionPolicy Bypass -File "%~dp0create-installer.ps1" -Configuration Release -Runtime win-x64 -Version %VERSION%
if errorlevel 1 (
  echo Installer build failed.
  exit /b 1
)

echo Installer build completed.
exit /b 0
