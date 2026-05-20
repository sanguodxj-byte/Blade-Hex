@echo off
REM run.bat — 启动游戏（带窗口），自动先构建
REM 用法: run [-Editor] [-GodotExe path]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\scripts\run.ps1" %*
exit /b %ERRORLEVEL%
