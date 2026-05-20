@echo off
REM sim.bat — headless 大规模模拟入口（战斗/AI），自动先构建
REM 用法: sim [-Battles N] [-Seed N] [-Scenario name] [-OutFile path]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\scripts\sim.ps1" %*
exit /b %ERRORLEVEL%
