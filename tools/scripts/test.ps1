# test.ps1
# Run unit tests headless via godot --headless + TEST_MODE env var
# (which TerrainTestRunner.cs picks up). Builds first by default to prevent
# running stale DLLs.
#
# Usage:
#   tools\scripts\test.ps1                       # default: unit mode
#   tools\scripts\test.ps1 -Mode terrain         # terrain analysis
#   tools\scripts\test.ps1 -Mode golden_verify   # golden seed regression
#   tools\scripts\test.ps1 -Mode golden_record   # capture golden seed baseline
#   tools\scripts\test.ps1 -GodotExe "C:\Tools\godot_console.exe"
#   tools\scripts\test.ps1 -SkipBuild
[CmdletBinding()]
param(
    [ValidateSet('unit', 'terrain', 'golden_record', 'golden_verify')]
    [string]$Mode = 'unit',

    [string]$GodotExe = '',
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot 'Common.ps1')

if (-not $SkipBuild) {
    Invoke-DotnetBuild
}

$scenePath = Get-TestScene
if (-not (Test-Path $scenePath)) {
    Write-Err "Test scene not found: $scenePath"
    Write-Warn2 "Make sure BladeHexCore/tests/test_runner.tscn exists (see tests/README.md)."
    exit 2
}

# Godot resolves scenes via res://, which is relative to project root.
$repoRoot = Get-RepoRoot
$relScene = $scenePath.Substring($repoRoot.Length + 1) -replace '\\', '/'
$resPath  = "res://$relScene"

$godotArgs = @(
    '--path', $repoRoot,
    '--headless',
    $resPath
)

$exit = Invoke-Godot -Arguments $godotArgs -EnvVars @{ TEST_MODE = $Mode } -GodotExe $GodotExe
if ($exit -ne 0) {
    Write-Err "Tests exited with non-zero code: $exit"
    exit $exit
}

Write-Ok "Test run finished (mode=$Mode)"
