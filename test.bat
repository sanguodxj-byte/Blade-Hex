@echo off
REM test.bat — headless 单元测试，自动先构建
REM 用法: test [-Mode unit|terrain|golden_record|golden_verify] [-GodotExe path]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\scripts\test.ps1" %*
exit /b %ERRORLEVEL%
