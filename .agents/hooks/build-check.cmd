@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-check.ps1"
exit /b 0
