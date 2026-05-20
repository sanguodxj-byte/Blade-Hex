@echo off
REM build.bat — 唯一推荐的构建入口（Debug 默认）
REM 用法: build [Debug|Release] [-Restore]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\scripts\build.ps1" %*
exit /b %ERRORLEVEL%
