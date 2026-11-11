@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0command-guard.ps1"
exit /b %ERRORLEVEL%
