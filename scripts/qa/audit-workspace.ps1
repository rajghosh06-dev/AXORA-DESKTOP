<#
.SYNOPSIS
    Performs a fast repository health and environment audit across Axora Desktop.
#>
[CmdletBinding()]
param()

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA DESKTOP - ENVIRONMENT & HEALTH AUDIT" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# 1. Check Git Status
Write-Host "`n[1] Git Repository Status:" -ForegroundColor Yellow
$gitStatus = git status 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  Git Repository: Active on $(git branch --show-current)" -ForegroundColor Green
} else {
    Write-Host "  Git Repository: Not initialized or error ($gitStatus)" -ForegroundColor Red
}

# 2. Check .NET SDK
Write-Host "`n[2] .NET Toolchain:" -ForegroundColor Yellow
$dotnetVer = dotnet --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  .NET SDK Version: $dotnetVer" -ForegroundColor Green
} else {
    Write-Host "  .NET SDK: Not found" -ForegroundColor Red
}

# 3. Check MSBuild
Write-Host "`n[3] Visual Studio MSBuild:" -ForegroundColor Yellow
$msbuildCandidates = @(
    "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)
$foundMsbuild = $false
foreach ($mb in $msbuildCandidates) {
    if (Test-Path $mb) {
        Write-Host "  MSBuild Path: $mb" -ForegroundColor Green
        $foundMsbuild = $true
        break
    }
}
if (-not $foundMsbuild) {
    Write-Host "  MSBuild: Visual Studio MSBuild not found at default candidate locations." -ForegroundColor Yellow
}

# 4. Check Web & Rust Toolchains (MaterialUI)
Write-Host "`n[4] Web & Rust Toolchains (MaterialUI):" -ForegroundColor Yellow
$nodeCmd = Get-Command "node" -ErrorAction SilentlyContinue
if ($nodeCmd) {
    Write-Host "  Node.js: $(node --version)" -ForegroundColor Green
} else {
    Write-Host "  Node.js: Not in PATH" -ForegroundColor DarkYellow
}
$cargoCmd = Get-Command "cargo" -ErrorAction SilentlyContinue
if ($cargoCmd) {
    Write-Host "  Cargo / Rust: $(cargo --version)" -ForegroundColor Green
} else {
    Write-Host "  Cargo / Rust: Not in PATH" -ForegroundColor DarkYellow
}

# 5. Check Antigravity Customizations
Write-Host "`n[5] Antigravity Customizations (.agents/):" -ForegroundColor Yellow
$rulesCount = (Get-ChildItem (Join-Path $WorkspaceRoot ".agents\rules") -Filter *.md -ErrorAction SilentlyContinue).Count
$skillsCount = (Get-ChildItem (Join-Path $WorkspaceRoot ".agents\skills") -Directory -ErrorAction SilentlyContinue).Count
$workflowsCount = (Get-ChildItem (Join-Path $WorkspaceRoot ".agents\workflows") -Filter *.md -ErrorAction SilentlyContinue).Count
$agentsCount = (Get-ChildItem (Join-Path $WorkspaceRoot ".agents\agents") -Filter *.md -ErrorAction SilentlyContinue).Count

Write-Host "  Rules Defined:      $rulesCount" -ForegroundColor Green
Write-Host "  Skills Installed:   $skillsCount" -ForegroundColor Green
Write-Host "  Workflows Defined:  $workflowsCount" -ForegroundColor Green
Write-Host "  Subagents Defined:  $agentsCount" -ForegroundColor Green
Write-Host "  Hooks Config:       $(Test-Path (Join-Path $WorkspaceRoot '.agents\hooks.json'))" -ForegroundColor Green
Write-Host "  MCP Config:         $(Test-Path (Join-Path $WorkspaceRoot '.agents\mcp_config.json'))" -ForegroundColor Green

Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host "  AUDIT COMPLETE" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan
exit 0
