# Common.ps1
# Shared helpers for build / test / sim / run scripts.
# Dot-source from sibling scripts.
#
# Provides: project paths, Godot executable lookup, colored logging,
# wrappers for `dotnet build` and `godot` invocations.

$ErrorActionPreference = 'Stop'

# ============================================================================
# Repository paths
# ============================================================================
# This file lives at <root>/tools/scripts/Common.ps1, so root is two levels up.
$script:RepoRoot       = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:CoreCsproj     = Join-Path $script:RepoRoot 'BladeHexCore\BladeHexCore.csproj'
# Workspace-root csproj: this is what Godot's project actually loads.
# Building this one ensures Godot picks up our changes. The subdir
# BladeHexFrontend\BladeHexFrontend.csproj is part of the BladeHex.sln
# split-project layout used by tests / IDE; it must NOT be the entry
# point for runtime builds because it produces DLLs Godot doesn't load.
$script:FrontendCsproj = Join-Path $script:RepoRoot 'BladeHexFrontend.csproj'
$script:Solution       = Join-Path $script:RepoRoot 'BladeHexFrontend.sln'
$script:TestScene      = Join-Path $script:RepoRoot 'BladeHexCore\tests\test_runner.tscn'

function Get-RepoRoot       { $script:RepoRoot }
function Get-FrontendCsproj { $script:FrontendCsproj }
function Get-CoreCsproj     { $script:CoreCsproj }
function Get-Solution       { $script:Solution }
function Get-TestScene      { $script:TestScene }

# ============================================================================
# Colored logging
# ============================================================================
function Write-Step($msg)  { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok  ($msg)  { Write-Host "[ok] $msg" -ForegroundColor Green }
function Write-Warn2($msg) { Write-Host "[warn] $msg" -ForegroundColor Yellow }
function Write-Err ($msg)  { Write-Host "[err] $msg" -ForegroundColor Red }

# ============================================================================
# Locate the Godot executable.
# Priority: 1) -GodotExe parameter  2) $env:GODOT  3) PATH
# Recommend godot_console.exe on Windows so headless stdout flows correctly.
# ============================================================================
function Resolve-GodotExe {
    param([string]$ExplicitPath)

    if ($ExplicitPath -and (Test-Path $ExplicitPath)) {
        return (Resolve-Path $ExplicitPath).Path
    }
    if ($env:GODOT -and (Test-Path $env:GODOT)) {
        return (Resolve-Path $env:GODOT).Path
    }

    # 1. 尝试从 PATH 中寻找
    $candidates = @(
        'godot_console.exe',
        'godot.exe',
        'Godot_v4.6.2-stable_mono_win64_console.exe',
        'godot',
        'godot_console'
    )
    foreach ($name in $candidates) {
        $cmd = Get-Command $name -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }

    # 2. 尝试从桌面或常用开发路径中寻找，提高免配置运行的成功率
    $customPaths = @(
        "$env:USERPROFILE\Desktop\Godot_v4.6.2-stable_mono_win64_console.exe",
        "$env:USERPROFILE\Desktop\Godot_v4.6.2-stable_mono_win64.exe",
        "d:\123\Godot_v4.6.2-stable_mono_win64_console.exe",
        "d:\123\Godot_v4.6.2-stable_mono_win64.exe"
    )
    foreach ($p in $customPaths) {
        if (Test-Path $p) { return (Resolve-Path $p).Path }
    }

    throw "Godot executable not found. Set `$env:GODOT or pass -GodotExe explicitly (godot_console.exe is recommended on Windows for headless stdout)."
}

# ============================================================================
# dotnet build wrapper.
# Always builds BladeHexFrontend.csproj at the workspace root. That csproj
# uses default-compile items, so it pulls in EVERY *.cs under both
# BladeHexCore\ and BladeHexFrontend\ and produces a single
# BladeHexFrontend.dll which Godot loads at runtime (project_assembly_name
# in project.godot).
#
# Building the split-layout BladeHex.sln (BladeHexCore + BladeHexFrontend
# subdir csprojs) is fine for IDE/tests but does NOT update the DLL Godot
# actually runs. So we deliberately stick to the root csproj here.
# ============================================================================
function Invoke-DotnetBuild {
    param(
        [string]$Configuration = 'Debug',
        [switch]$Restore
    )

    if ($Restore) {
        Write-Step "dotnet restore $($script:Solution)"
        & dotnet restore $script:Solution --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed (exit=$LASTEXITCODE)" }
    }

    $buildArgs = @(
        'build',
        $script:FrontendCsproj,
        '--nologo',
        '-v', 'minimal',
        '-c', $Configuration
    )
    if (-not $Restore) { $buildArgs += '--no-restore' }

    Write-Step "dotnet $($buildArgs -join ' ')"
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit=$LASTEXITCODE)" }
    Write-Ok "build complete"
}

# ============================================================================
# Run godot with optional environment variables.
# Variables are restored after the call regardless of outcome.
# ============================================================================
function Invoke-Godot {
    param(
        [Parameter(Mandatory)] [string[]]$Arguments,
        [hashtable]$EnvVars,
        [string]$GodotExe
    )

    $exe = Resolve-GodotExe -ExplicitPath $GodotExe

    $oldEnv = @{}
    if ($EnvVars) {
        foreach ($k in $EnvVars.Keys) {
            $oldEnv[$k] = [Environment]::GetEnvironmentVariable($k)
            [Environment]::SetEnvironmentVariable($k, $EnvVars[$k])
        }
    }

    $oldEap = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        Write-Step "godot $($Arguments -join ' ')"
        # Run in a sub-shell so we can capture stderr without it polluting our
        # return value. Godot writes its banner ("Godot Engine v4.6.2...") to
        # stderr, which would otherwise be merged into $exit by 2>&1 callers.
        & $exe @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
        return [int]$LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldEap
        foreach ($k in $oldEnv.Keys) {
            [Environment]::SetEnvironmentVariable($k, $oldEnv[$k])
        }
    }
}
