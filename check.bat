@echo off
REM check.bat — Blade & Hex 可玩性自动检查
REM 用法:
REM   check                        # 完整检查（含构建）
REM   check -SkipBuild             # 跳过构建，直接跑 headless
REM   check -GodotExe "D:\godot.exe"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\scripts\check_playability.ps1" %*
exit /b %ERRORLEVEL%
