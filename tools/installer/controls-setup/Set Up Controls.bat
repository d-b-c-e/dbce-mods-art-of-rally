@echo off
setlocal
REM Optional, private: set up art of sim rally's shared-input wheel controls, screen measurements and mod settings without Wheelkit.
title art of sim rally - controls setup
REM Use Windows PowerShell's built-in modules even when called from PowerShell 7.
set "PSModulePath="
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-Controls.ps1" %*
set "setupExit=%ERRORLEVEL%"
echo.
pause
exit /b %setupExit%
