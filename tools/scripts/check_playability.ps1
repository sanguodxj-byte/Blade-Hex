# check_playability.ps1
# Blade & Hex playability check.
# Launches Godot once per scene with a real display driver; each run uses
# CHECK_SCENE to know which sub-scene to instantiate as a child.
# Results are written to per-run JSON.
#
# Usage:
#   tools\scripts\check_playability.ps1
#   tools\scripts\check_playability.ps1 -SkipBuild
#   tools\scripts\check_playability.ps1 -GodotExe "D:\123\Godot.exe"

[CmdletBinding()]
param(
    [string]$GodotExe  = '',
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repoRoot  = Get-RepoRoot
$timestamp = (Get-Date).ToUniversalTime().ToString("o")
$outFile   = Join-Path $repoRoot 'evaluation.json'
$tmpDir    = Join-Path $repoRoot 'playability_screenshots'
$checkScene = 'res://BladeHexFrontend/src/Scenes/test/playability_check.tscn'

$loop = [ordered]@{
    builds_clean        = $false
    boots_without_crash = $false
    overworld_loads     = $false
    combat_scene_loads  = $false
}
$errors      = [System.Collections.Generic.List[string]]::new()
$screenshots = [System.Collections.Generic.List[string]]::new()

# ─── helper: run one rendered check ───────────────────────────────────────────
function Invoke-SceneCheck {
    param(
        [string]$SceneKey,    # "boot" | "overworld" | "combat"
        [string]$ResultKey,   # key in $loop
        [string]$Label
    )

    Write-Step "Checking: $Label ($SceneKey)"

    $tmpJson = Join-Path $repoRoot "playability_screenshots\result_${SceneKey}.json"

    try {
        $godotArgs = @('--path', $repoRoot)
        if ($IsWindows -or $env:OS -eq 'Windows_NT') {
            $godotArgs += @('--display-driver', 'windows')
        }
        $godotArgs += $checkScene
        $envVars   = @{
            CHECK_MODE  = 'playability'
            CHECK_SCENE = $SceneKey
            CHECK_OUT   = $tmpJson
        }

        $exit = Invoke-Godot -Arguments $godotArgs -EnvVars $envVars -GodotExe $GodotExe

        # exit 0 = pass, 1 = fail (normal), anything else = crash
        if ($exit -le 1) {
            if (Test-Path $tmpJson) {
                $r = Get-Content $tmpJson -Raw | ConvertFrom-Json
                $val = [bool]$r.$ResultKey
                $script:loop[$ResultKey] = $val
                $status = if ($val) { "PASS" } else { "FAIL" }
                Write-Host ("  {0}  {1}" -f $(if ($val) { "[PASS]" } else { "[FAIL]" }), $Label) `
                    -ForegroundColor $(if ($val) { 'Green' } else { 'Red' })
                if ($r.error) { $script:errors.Add("[${Label}] $($r.error)") }
                if ($r.screenshot) { $script:screenshots.Add($r.screenshot) }
            } else {
                $script:loop[$ResultKey] = $false
                $script:errors.Add("${Label}: result JSON not written (exit=$exit)")
                Write-Err "${Label}: no result file"
            }
        } else {
            $script:loop[$ResultKey] = $false
            $script:errors.Add("${Label}: Godot crashed (exit=$exit)")
            Write-Err "${Label}: Godot crashed (exit=$exit)"
        }
    } catch {
        $script:loop[$ResultKey] = $false
        $script:errors.Add("${Label}: launch exception: $_")
        Write-Err "${Label}: launch failed: $_"
    }
}

# ─── Step 1: build ────────────────────────────────────────────────────────────
Write-Step "Step 1/5 -- dotnet build"
if ($SkipBuild) {
    Write-Warn2 "Build skipped (-SkipBuild)"
    $loop.builds_clean = $true
} else {
    try {
        Invoke-DotnetBuild
        $loop.builds_clean = $true
        Write-Ok "builds_clean = PASS"
    } catch {
        $errors.Add("Build failed: $_")
        Write-Err "builds_clean = FAIL"
    }
}

# Ensure temp dir exists
New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

# ─── Step 2: boot check (no sub-scene, just autoloads + check node) ────────────
Invoke-SceneCheck -SceneKey "boot"      -ResultKey "boots_without_crash" -Label "Boot (autoloads)"

# ─── Step 3: overworld/campaign scene ─────────────────────────────────────────
Invoke-SceneCheck -SceneKey "overworld" -ResultKey "overworld_loads"     -Label "Overworld/Campaign"

# ─── Step 4: combat scene ─────────────────────────────────────────────────────
Invoke-SceneCheck -SceneKey "combat"    -ResultKey "combat_scene_loads"  -Label "Quick Combat"

# ─── Step 5: write final evaluation.json ──────────────────────────────────────
$allPass = $loop.builds_clean -and
           $loop.boots_without_crash -and
           $loop.overworld_loads -and
           $loop.combat_scene_loads

$final = [ordered]@{
    result               = if ($allPass) { 'pass' } else { 'fail' }
    timestamp            = $timestamp
    playable_closed_loop = $loop
    screenshots          = @($screenshots)
    errors               = @($errors)
}
$final | ConvertTo-Json -Depth 5 | Out-File -FilePath $outFile -Encoding utf8

# ─── summary ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "========================================"
Write-Host "  Blade & Hex  Playability Check Result"
Write-Host "========================================"

$checks = @(
    @{ Label = "Build compiles clean    "; Key = "builds_clean" },
    @{ Label = "Boot / autoloads OK     "; Key = "boots_without_crash" },
    @{ Label = "Overworld scene loads   "; Key = "overworld_loads" },
    @{ Label = "Combat scene loads      "; Key = "combat_scene_loads" }
)
foreach ($c in $checks) {
    $val   = $loop[$c.Key]
    $mark  = if ($val) { "[PASS]" } else { "[FAIL]" }
    $color = if ($val) { 'Green' } else { 'Red' }
    Write-Host ("  {0}  {1}" -f $mark, $c.Label) -ForegroundColor $color
}

Write-Host "----------------------------------------"
if ($allPass) {
    Write-Host "  PASS -- main branch playability OK" -ForegroundColor Green
} else {
    Write-Host "  FAIL -- issues found!" -ForegroundColor Red
}

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "  Issues:" -ForegroundColor Yellow
    foreach ($e in $errors) { Write-Host ("    - {0}" -f $e) -ForegroundColor Yellow }
}
if ($screenshots.Count -gt 0) {
    Write-Host ""
    Write-Host "  Screenshots saved:"
    foreach ($s in $screenshots) { Write-Host ("    {0}" -f $s) -ForegroundColor Gray }
}

Write-Host "========================================"
Write-Host ("  Report: {0}" -f $outFile) -ForegroundColor Gray
Write-Host ""

exit $(if ($allPass) { 0 } else { 1 })
