# run.ps1
# Launch the game (windowed) after refreshing the build.
#
# Usage:
#   tools\scripts\run.ps1                       # main scene
#   tools\scripts\run.ps1 -Editor               # open the editor instead
#   tools\scripts\run.ps1 -GodotExe "C:\Tools\godot_console.exe"
[CmdletBinding()]
param(
    [switch]$Editor,
    [string]$GodotExe = '',
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot 'Common.ps1')

if (-not $SkipBuild) {
    Invoke-DotnetBuild
}

$godotArgs = @('--path', (Get-RepoRoot))
if ($Editor) { $godotArgs += '--editor' }

Invoke-Godot -Arguments $godotArgs -GodotExe $GodotExe | Out-Null
