@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-release.ps1" %*
