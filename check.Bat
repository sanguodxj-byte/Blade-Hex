@echo off
"C:\Users\Administrator\Desktop\Godot_v4.6.2-stable_win64.exe" --headless --path . > godot_log.txt 2>&1
type godot_log.txt | findstr /I "ERROR WARNING SCRIPT"