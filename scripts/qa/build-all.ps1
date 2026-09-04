<#
.SYNOPSIS
    Builds the specified Axora Desktop application projects.
.PARAMETER Target
    The target to build: 'WinUI', 'MaterialUI', or 'All'. Defaults to 'WinUI'.
.PARAMETER Configuration
    Build configuration: 'Debug' or 'Release'. Defaults to 'Debug'.
#>
[CmdletBinding()]
param(
    [ValidateSet('WinUI', 'MaterialUI', 'All')]
    [string]$Target = 'WinUI',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA DESKTOP - BUILD PIPELINE ($Target - $Configuration)" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# 1. Clean Process Locks
& (Join-Path $ScriptRoot "pre-build-clean.ps1")

$BuildCount = 0

# Helper to find MSBuild
function Get-MSBuildPath {
    $candidates = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }
    # Check vswhere
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($vsPath -and (Test-Path $vsPath)) { return $vsPath }
    }
    return $null
}

# 2. Build WinUI
if ($Target -eq 'WinUI' -or $Target -eq 'All') {
    Write-Host "`n[1/2] Building WinUI 3 Application..." -ForegroundColor Yellow
    $MSBuild = Get-MSBuildPath
    $WinUIProj = Join-Path $WorkspaceRoot "Axora-Desktop-WinUI\Axora.Desktop\Axora.Desktop.csproj"
    $TestsProj = Join-Path $WorkspaceRoot "Axora-Desktop-WinUI\Axora.Desktop.Tests\Axora.Desktop.Tests.csproj"

    if ($MSBuild) {
        Write-Host "Using MSBuild: $MSBuild" -ForegroundColor Gray
        Write-Host "Compiling Axora.Desktop..." -ForegroundColor Gray
        $BuildsExecuted++
        & $MSBuild $WinUIProj -restore -p:Configuration=$Configuration -p:Platform=x64 -v:minimal -m
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[FAIL] Axora.Desktop compilation failed with exit code $LASTEXITCODE." -ForegroundColor Red
            $HasFailures = $true
        } else {
            Write-Host "[PASS] Axora.Desktop compiled successfully." -ForegroundColor Green
        }

        Write-Host "Compiling Axora.Desktop.Tests..." -ForegroundColor Gray
        $BuildsExecuted++
        & $MSBuild $TestsProj -restore -p:Configuration=$Configuration -p:Platform=x64 -v:minimal -m
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[FAIL] Axora.Desktop.Tests compilation failed with exit code $LASTEXITCODE." -ForegroundColor Red
            $HasFailures = $true
        } else {
            Write-Host "[PASS] Axora.Desktop.Tests compiled successfully." -ForegroundColor Green
        }
    } else {
        Write-Host "Visual Studio MSBuild not found; falling back to dotnet build CLI..." -ForegroundColor Yellow
        Write-Host "Compiling Axora.Desktop via dotnet build..." -ForegroundColor Gray
        $BuildsExecuted++
        dotnet build $WinUIProj -p:Configuration=$Configuration -p:Platform=x64 -v:minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[FAIL] Axora.Desktop dotnet build failed." -ForegroundColor Red
            $HasFailures = $true
        } else {
            Write-Host "[PASS] Axora.Desktop compiled successfully via dotnet build." -ForegroundColor Green
        }

        Write-Host "Compiling Axora.Desktop.Tests via dotnet build..." -ForegroundColor Gray
        $BuildsExecuted++
        dotnet build $TestsProj -p:Configuration=$Configuration -p:Platform=x64 -v:minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[FAIL] Axora.Desktop.Tests dotnet build failed." -ForegroundColor Red
            $HasFailures = $true
        } else {
            Write-Host "[PASS] Axora.Desktop.Tests compiled successfully via dotnet build." -ForegroundColor Green
        }
    }
}

# 3. Build MaterialUI
if ($Target -eq 'MaterialUI' -or $Target -eq 'All') {
    Write-Host "`n[2/2] Building MaterialUI (Tauri / React) Application..." -ForegroundColor Yellow
    $MatRoot = Join-Path $WorkspaceRoot "Axora-Desktop-MaterialUI"
    
    # Check npm
    $npmCmd = Get-Command "npm" -ErrorAction SilentlyContinue
        if ($npmCmd) {
        Push-Location $MatRoot
        try {
            Write-Host "Compiling React Frontend (npm run build)..." -ForegroundColor Gray
            $BuildsExecuted++
            npm run build
            if ($LASTEXITCODE -ne 0) {
                Write-Host "[FAIL] Frontend build failed." -ForegroundColor Red
                $HasFailures = $true
            } else {
                Write-Host "[PASS] Frontend built successfully." -ForegroundColor Green
            }
        } finally {
            Pop-Location
        }
    } else {
        Write-Host "[BLOCKED] npm is not in current PATH for frontend build." -ForegroundColor Yellow
    }

    # Check cargo
    if (-not (Get-Command "cargo" -ErrorAction SilentlyContinue) -and (Test-Path "$env:USERPROFILE\.cargo\bin\cargo.exe")) {
        $env:Path = "$env:USERPROFILE\.cargo\bin;" + $env:Path
    }
    # Initialize Visual Studio Developer environment for MSVC / Windows SDK if needed
    $devShell = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\Launch-VsDevShell.ps1"
    if (-not (Get-Command "cl.exe" -ErrorAction SilentlyContinue) -and (Test-Path $devShell)) {
        & $devShell -Arch amd64 -HostArch amd64 | Out-Null
    }
    $cargoCmd = Get-Command "cargo" -ErrorAction SilentlyContinue
    if ($cargoCmd) {
        $cargoToml = Join-Path $MatRoot "src-tauri\Cargo.toml"
        Write-Host "Compiling Tauri Rust Backend (cargo check)..." -ForegroundColor Gray
        $BuildsExecuted++
        cargo check --manifest-path $cargoToml
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[FAIL] Tauri backend compilation check failed." -ForegroundColor Red
            $HasFailures = $true
        } else {
            Write-Host "[PASS] Tauri backend compilation check passed." -ForegroundColor Green
        }
    } else {
        Write-Host "[BLOCKED] cargo is not in current PATH for Rust backend build." -ForegroundColor Yellow
    }
}

Write-Host "================================================================================" -ForegroundColor Cyan
if ($HasFailures) {
    Write-Host "  BUILD RESULT: ONE OR MORE TARGETS ENCOUNTERED COMPILATION ERRORS" -ForegroundColor Red
    Write-Host "================================================================================" -ForegroundColor Cyan
    exit 1
} elseif ($BuildsExecuted -gt 0) {
    Write-Host "  BUILD RESULT: ALL EXECUTED TARGETS COMPILED CLEANLY (0 ERRORS)" -ForegroundColor Green
    Write-Host "================================================================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "  BUILD RESULT: TARGET BLOCKED (Required toolchains not found on host)" -ForegroundColor Yellow
    Write-Host "================================================================================" -ForegroundColor Cyan
    exit 2
}
