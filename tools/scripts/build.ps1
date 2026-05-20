# build.ps1
# Single recommended entry point for building the project.
# Always builds BladeHexFrontend.csproj. Through ProjectReference this also
# rebuilds BladeHexCore and refreshes the BladeHexCore.dll copy that Godot
# loads at runtime, avoiding the "I changed core but Godot still runs the old
# DLL" trap.
#
# Usage:
#   tools\scripts\build.ps1                          # Debug build
#   tools\scripts\build.ps1 -Configuration Release
#   tools\scripts\build.ps1 -Restore                 # force dotnet restore
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Restore
)

. (Join-Path $PSScriptRoot 'Common.ps1')

Invoke-DotnetBuild -Configuration $Configuration -Restore:$Restore
