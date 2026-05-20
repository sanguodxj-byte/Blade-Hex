# sim.ps1
# Headless batch simulation entry point. Reuses test_runner.tscn with
# TEST_MODE=sim, plus SIM_BATTLES / SIM_SEED / SIM_SCENARIO env vars consumed
# by SimulationHarness.cs.
#
# Usage:
#   tools\scripts\sim.ps1                            # default scenario
#   tools\scripts\sim.ps1 -Battles 1000 -Seed 42     # 1000 battles, fixed seed
#   tools\scripts\sim.ps1 -Scenario overworld_ai     # strategic AI
#   tools\scripts\sim.ps1 -OutFile sim_log.txt
[CmdletBinding()]
param(
    [int]$Battles = 100,
    [int]$Seed = 0,
    [int]$Level = 5,
    [int]$TeamSize = 4,
    [string]$Scenario = 'combat',
    [ValidateSet('core', 'single', 'double', 'triple', 'quad', 'penta', 'all')]
    [string]$BuildFilter = 'core',
    [switch]$EnableSpells,
    [int]$EconReward = 90,
    [int]$EconFoodPrice = 4,
    [string]$OutFile = '',
    [string]$GodotExe = '',
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot 'Common.ps1')

if (-not $SkipBuild) {
    Invoke-DotnetBuild
}

$scenePath = Get-TestScene
if (-not (Test-Path $scenePath)) {
    Write-Err "Test scene not found: $scenePath (sim reuses test_runner.tscn)"
    exit 2
}

$repoRoot = Get-RepoRoot
$relScene = $scenePath.Substring($repoRoot.Length + 1) -replace '\\', '/'
$resPath  = "res://$relScene"

$godotArgs = @(
    '--path', $repoRoot,
    '--headless',
    $resPath
)

$envVars = @{
    TEST_MODE    = 'sim'
    SIM_BATTLES  = $Battles.ToString()
    SIM_SEED     = $Seed.ToString()
    SIM_SCENARIO = $Scenario
    SIM_LEVEL    = $Level.ToString()
    SIM_TEAM_SIZE = $TeamSize.ToString()
    SIM_BUILD_FILTER = $BuildFilter
    SIM_ENABLE_SPELLS = if ($EnableSpells) { '1' } else { '0' }
    SIM_ECON_REWARD = $EconReward.ToString()
    SIM_ECON_FOOD_PRICE = $EconFoodPrice.ToString()
}

if ($OutFile) {
    $absOut = if ([System.IO.Path]::IsPathRooted($OutFile)) { $OutFile } else { Join-Path $PWD $OutFile }
    Write-Step "Tee output to $absOut"
    Invoke-Godot -Arguments $godotArgs -EnvVars $envVars -GodotExe $GodotExe *>&1 | Tee-Object -FilePath $absOut
    $exit = $LASTEXITCODE
}
else {
    $exit = Invoke-Godot -Arguments $godotArgs -EnvVars $envVars -GodotExe $GodotExe
}

if ($exit -ne 0) {
    Write-Err "Simulation exited with non-zero code: $exit"
    exit $exit
}

Write-Ok "Simulation finished (scenario=$Scenario, battles=$Battles, seed=$Seed)"
