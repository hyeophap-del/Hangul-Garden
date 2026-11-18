@echo off
cd /d "%~dp0"
if not exist "dist\HangeulVR-1.1.exe" (
  echo Missing dist\HangeulVR-1.1.exe. Run build-vr.ps1 first.
  pause
  exit /b 1
)
start "" "dist\HangeulVR-1.1.exe"



