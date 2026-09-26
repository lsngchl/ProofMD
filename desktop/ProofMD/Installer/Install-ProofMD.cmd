@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-ProofMD.ps1"
if errorlevel 1 (
  echo.
  echo ProofMD installation failed.
  pause
)
endlocal
