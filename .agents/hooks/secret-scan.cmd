@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0secret-scan.ps1"
exit /b %ERRORLEVEL%
