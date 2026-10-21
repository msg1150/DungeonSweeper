@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Open-Unity-DX11.ps1" %*
if errorlevel 1 pause
