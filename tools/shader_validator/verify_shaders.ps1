# verify_shaders.ps1
# General Godot shader validation scheduling and parsing script
#
# Usage:
#   powershell -File verify_shaders.ps1 [-GodotExe path] [-RenderingMethod method] [-NoHeadless]
#
param(
    [string]$GodotExe = "",
    [string]$RenderingMethod = "",
    [switch]$NoHeadless
)

$ErrorActionPreference = "Stop"

# 1. Paths and Common.ps1 resolution (if exists)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path

# Try loading project's Common.ps1
$CommonPath = Join-Path $ScriptDir "..\scripts\Common.ps1"
$godotPath = $null

if (Test-Path $CommonPath) {
    try {
        . $CommonPath
        $godotPath = Resolve-GodotExe -ExplicitPath $GodotExe
    } catch {
        # Fallback to local search if Common.ps1 load/resolve fails
    }
}

# Local fallback search for Godot
if (-not $godotPath) {
    if ($GodotExe -and (Test-Path $GodotExe)) {
        $godotPath = (Resolve-Path $GodotExe).Path
    } elseif ($env:GODOT -and (Test-Path $env:GODOT)) {
        $godotPath = (Resolve-Path $env:GODOT).Path
    } else {
        # Search in PATH or common directories
        $candidates = @('godot_console.exe', 'godot.exe', 'godot')
        foreach ($name in $candidates) {
            $cmd = Get-Command $name -ErrorAction SilentlyContinue
            if ($cmd) { $godotPath = $cmd.Source; break }
        }
        if (-not $godotPath) {
            $userProfile = $env:USERPROFILE
            $defaultPaths = @(
                "$userProfile\Desktop\Godot_v4.6.2-stable_mono_win64_console.exe",
                "$userProfile\Desktop\Godot_v4.6.2-stable_mono_win64.exe",
                "d:\123\Godot_v4.6.2-stable_mono_win64_console.exe",
                "d:\123\Godot_v4.6.2-stable_mono_win64.exe"
            )
            foreach ($p in $defaultPaths) {
                if (Test-Path $p) { $godotPath = (Resolve-Path $p).Path; break }
            }
        }
    }
}

if (-not $godotPath) {
    Write-Error "Godot executable not found. Configure GODOT environment variable or pass -GodotExe."
    exit 1
}

# 2. Choose rendering method
$renderMethod = $RenderingMethod
if ([string]::IsNullOrWhiteSpace($renderMethod)) {
    # Auto-parse project.godot configuration
    $projectGodotPath = Join-Path $RepoRoot "project.godot"
    if (Test-Path $projectGodotPath) {
        $content = Get-Content $projectGodotPath -Raw
        # Match Mobile feature
        if ($content -match '"Mobile"') {
            $renderMethod = "mobile"
        } elseif ($content -match '"Forward\+"') {
            $renderMethod = "forward_plus"
        } else {
            $renderMethod = "gl_compatibility"
        }
    } else {
        $renderMethod = "gl_compatibility"
    }
}

# 3. Construct arguments
$verifyGdScript = Join-Path $ScriptDir "verify_shaders.gd"
$godotArgs = @('--path', $RepoRoot, '-s', $verifyGdScript)

if (-not $NoHeadless) {
    $godotArgs += '--headless'
}
if (-not [string]::IsNullOrWhiteSpace($renderMethod)) {
    $godotArgs += @('--rendering-method', $renderMethod)
}

Write-Host "Validating Godot shaders..."
Write-Host "Godot Exe: $godotPath"
Write-Host "Rendering Method: $renderMethod"

# 4. Run Godot validation script and parse stdout/stderr in real time
$global:currentFile = $null
$global:shaderErrorMsg = $null
$global:anyErrors = $false

# Output processor function
function Process-Line([string]$line) {
    if ([string]::IsNullOrWhiteSpace($line)) { return }

    # Detect file section marker
    if ($line -match '^\[VERIFY_START\]:\s*(.*)') {
        $resPath = $Matches[1].Trim()
        # Convert res:// to absolute local path
        $relPath = $resPath.Replace("res://", "")
        # Handle path separators
        $relPath = $relPath.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
        $global:currentFile = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $relPath))
        $global:shaderErrorMsg = $null
    }
    elseif ($line -match '^\[VERIFY_END\]') {
        # Clean up at verify end, dump leftover errors
        if ($global:currentFile -and $global:shaderErrorMsg) {
            Write-Host "$($global:currentFile)(1): error GDSHADER: $($global:shaderErrorMsg)"
            $global:anyErrors = $true
            $global:shaderErrorMsg = $null
        }
        $global:currentFile = $null
    }
    # Parse within shader scope
    elseif ($global:currentFile) {
        if ($line -match 'SHADER ERROR:\s*(.*)') {
            $global:shaderErrorMsg = $Matches[1].Trim()
        }
        elseif ($line -match 'at:.*\(:(?<line>\d+)\)') {
            if ($global:shaderErrorMsg) {
                $errLine = $Matches['line']
                # Output MSBuild-compatible error format
                Write-Host "$($global:currentFile)($errLine): error GDSHADER: $($global:shaderErrorMsg)"
                $global:anyErrors = $true
                $global:shaderErrorMsg = $null # Clear to avoid duplicate output for the same block
            }
        }
    }
}

# Set UTF-8 console output encoding to mitigate decoding errors
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch {}

# Temporarily relax ErrorActionPreference to prevent pipeline warning errors from terminating execution
$oldEap = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    & $godotPath @godotArgs 2>&1 | ForEach-Object {
        Process-Line $_
    }
} finally {
    $ErrorActionPreference = $oldEap
}

# Dump any trailing error if the process cut off
if ($global:currentFile -and $global:shaderErrorMsg) {
    Write-Host "$($global:currentFile)(1): error GDSHADER: $($global:shaderErrorMsg)"
    $global:anyErrors = $true
}

if ($global:anyErrors) {
    Write-Host "Shader validation failed. Build blocked." -ForegroundColor Red
    exit 1
} else {
    Write-Host "All shaders passed validation." -ForegroundColor Green
    exit 0
}
