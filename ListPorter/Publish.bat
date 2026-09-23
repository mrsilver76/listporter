@echo off
rem Call the powershell script, passing along any command line options
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish.ps1" %*
timeout 30