<#
.SYNOPSIS
    Performs comprehensive diagnostic health checks across all toolchains, runtimes, and repository integrity.
.OUTPUTS
    Exit code 0: System healthy and fully ready
    Exit code 2: Host environment limitations detected (e.g. missing optional toolchains)
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA DESKTOP - SYSTEM & TOOLCHAIN DOCTOR" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

$IssuesFound = 0
$BlockedCount = 0

# 1. Host OS & Architecture
Write-Host "`n[1] Operating System Environment:" -ForegroundColor Yellow
$os = Get-CimInstance Win32_OperatingSystem
Write-Host "  OS Name:     $($os.Caption)" -ForegroundColor Gray
Write-Host "  Version:     $($os.Version) (Build $($os.BuildNumber))" -ForegroundColor Gray
Write-Host "  Arch:        $($os.OSArchitecture)" -ForegroundColor Gray

# 2. Git Environment
Write-Host "`n[2] Git Repository Health:" -ForegroundColor Yellow
$gitCmd = Get-Command "git" -ErrorAction SilentlyContinue
if ($gitCmd) {
    $gitVer = git --version
    Write-Host "  Git Binary:  $gitVer" -ForegroundColor Gray
    Push-Location $WorkspaceRoot
    try {
        $branch = git rev-parse --abbrev-ref HEAD 2>$null
        $remote = git remote get-url origin 2>$null
        Write-Host "  Active Branch: $branch" -ForegroundColor Gray
        Write-Host "  Origin URL:    $remote" -ForegroundColor Gray
        if ($remote -match "rajghosh06-dev/AXORA-DESKTOP") {
            Write-Host "  [PASS] Origin repository matches private target." -ForegroundColor Green
        } else {
            Write-Host "  [WARN] Unexpected origin URL: $remote" -ForegroundColor Yellow
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "  [FAIL] Git command not found in PATH." -ForegroundColor Red
    $IssuesFound++
}

# 3. .NET Toolchain
Write-Host "`n[3] .NET Toolchain (WinUI):" -ForegroundColor Yellow
$dotnetCmd = Get-Command "dotnet" -ErrorAction SilentlyContinue
if ($dotnetCmd) {
    $dotnetVer = dotnet --version
    Write-Host "  .NET SDK:    $dotnetVer" -ForegroundColor Gray
    if ($dotnetVer -match "^(9|10)\.") {
        Write-Host "  [PASS] Modern .NET SDK detected ($dotnetVer)." -ForegroundColor Green
    } else {
        Write-Host "  [WARN] .NET SDK $dotnetVer detected; net9.0-windows recommended." -ForegroundColor Yellow
    }
} else {
    Write-Host "  [FAIL] .NET SDK not found in PATH." -ForegroundColor Red
    $IssuesFound++
}

# 4. Visual Studio MSBuild
Write-Host "`n[4] Visual Studio MSBuild (WinUI):" -ForegroundColor Yellow
$msBuildCandidates = @(
    "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "D:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)

$foundMsBuild = $null
foreach ($path in $msBuildCandidates) {
    if (Test-Path $path) {
        $foundMsBuild = $path
        break
    }
}

if ($foundMsBuild) {
    Write-Host "  MSBuild Path: $foundMsBuild" -ForegroundColor Gray
    Write-Host "  [PASS] Visual Studio MSBuild available for WinUI Pass 2 compilation." -ForegroundColor Green
} else {
    Write-Host "  [WARN] Visual Studio MSBuild not found in standard paths; dotnet build fallback will be used." -ForegroundColor Yellow
}

# 5. Web & Rust Toolchains (MaterialUI)
Write-Host "`n[5] Web & Rust Toolchains (MaterialUI):" -ForegroundColor Yellow
$nodeCmd = Get-Command "node" -ErrorAction SilentlyContinue
$npmCmd = Get-Command "npm" -ErrorAction SilentlyContinue
$cargoCmd = Get-Command "cargo" -ErrorAction SilentlyContinue
$rustcCmd = Get-Command "rustc" -ErrorAction SilentlyContinue

if ($nodeCmd -and $npmCmd) {
    Write-Host "  Node.js:     $(node --version)" -ForegroundColor Gray
    Write-Host "  npm:         $(npm --version)" -ForegroundColor Gray
    Write-Host "  [PASS] Node.js toolchain ready." -ForegroundColor Green
} else {
    Write-Host "  [BLOCKED] Node.js / npm not found on host PATH. MaterialUI frontend compilation unavailable." -ForegroundColor Yellow
    $BlockedCount++
}

if ($cargoCmd -and $rustcCmd) {
    Write-Host "  Cargo:       $(cargo --version)" -ForegroundColor Gray
    Write-Host "  Rustc:       $(rustc --version)" -ForegroundColor Gray
    Write-Host "  [PASS] Rust toolchain ready." -ForegroundColor Green
} else {
    Write-Host "  [BLOCKED] Cargo / Rust not found on host PATH. MaterialUI backend compilation unavailable." -ForegroundColor Yellow
    $BlockedCount++
}

# 6. Antigravity Customizations
Write-Host "`n[6] Antigravity Workspace Integrity:" -ForegroundColor Yellow
$agentsDir = Join-Path $WorkspaceRoot ".agents"
$rulesCount = (Get-ChildItem -Path (Join-Path $agentsDir "rules") -Filter "*.md" -ErrorAction SilentlyContinue).Count
$skillsCount = (Get-ChildItem -Path (Join-Path $agentsDir "skills") -Directory -ErrorAction SilentlyContinue).Count
$workflowsCount = (Get-ChildItem -Path (Join-Path $agentsDir "workflows") -Filter "*.md" -ErrorAction SilentlyContinue).Count
$subagentsCount = (Get-ChildItem -Path (Join-Path $agentsDir "agents") -Filter "*.md" -ErrorAction SilentlyContinue).Count

Write-Host "  Rules:       $rulesCount" -ForegroundColor Gray
Write-Host "  Skills:      $skillsCount" -ForegroundColor Gray
Write-Host "  Workflows:   $workflowsCount" -ForegroundColor Gray
Write-Host "  Subagents:   $subagentsCount" -ForegroundColor Gray

if ($rulesCount -ge 6 -and $skillsCount -ge 6 -and $workflowsCount -ge 7) {
    Write-Host "  [PASS] Antigravity customization structure complete." -ForegroundColor Green
} else {
    Write-Host "  [WARN] Some Antigravity customizations appear missing or incomplete." -ForegroundColor Yellow
}

# 7. Locked Processes
Write-Host "`n[7] Process Lock Check:" -ForegroundColor Yellow
$lockedProcs = Get-Process -Name "Axora.Desktop*", "axora-desktop*" -ErrorAction SilentlyContinue
if ($lockedProcs) {
    Write-Host "  [WARN] Active Axora process(es) detected: $($lockedProcs.Name -join ', ')" -ForegroundColor Yellow
} else {
    Write-Host "  [PASS] No locked Axora processes." -ForegroundColor Green
}

Write-Host "`n================================================================================" -ForegroundColor Cyan
if ($IssuesFound -gt 0) {
    Write-Host "  DOCTOR RESULT: $IssuesFound CRITICAL ISSUE(S) DETECTED" -ForegroundColor Red
    Write-Host "================================================================================" -ForegroundColor Cyan
    exit 1
} elseif ($BlockedCount -gt 0) {
    Write-Host "  DOCTOR RESULT: PASS WITH LIMITATIONS ($BlockedCount optional toolchain(s) missing)" -ForegroundColor Yellow
    Write-Host "================================================================================" -ForegroundColor Cyan
    exit 2
} else {
    Write-Host "  DOCTOR RESULT: ALL SYSTEMS OPERATIONAL (0 ISSUES)" -ForegroundColor Green
    Write-Host "================================================================================" -ForegroundColor Cyan
    exit 0
}
