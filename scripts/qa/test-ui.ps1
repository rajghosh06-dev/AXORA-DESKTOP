<#
.SYNOPSIS
    AXORA Desktop - Unified Desktop UI Automation, Adversarial Stress & Chaos Test Runner
.DESCRIPTION
    Executes real UI interaction, visual state, navigation, adversarial stress, and accessibility tests:
    - MaterialUI: Chrome DevTools Protocol (CDP) WebSocket automation against Tauri WebView2
    - WinUI: Windows UI Automation (UIAutomationClient) against native WinUI 3 XAML Visual Tree
    - Adversarial Stress: Rapid bombardment, extreme input injection, window resizing
    - Mutation Self-Test: Verification that test harnesses catch injected defects
.PARAMETER Target
    'All', 'WinUI', or 'MaterialUI'. Defaults to 'All'.
.PARAMETER IncludeAdversarial
    Include adversarial stress testing. Defaults to $true.
.PARAMETER IncludeSelfTest
    Include QA mutation self-testing. Defaults to $false.
#>

[CmdletBinding()]
param(
    [ValidateSet('All', 'WinUI', 'MaterialUI')]
    [string]$Target = 'All',

    [switch]$IncludeAdversarial = $true,
    [switch]$IncludeSelfTest = $false
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA DESKTOP - UNIFIED UI AUTOMATION TEST RUNNER ($Target)" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

$allPassed = $true

# 1. MaterialUI CDP Interactive UI Tests
if ($Target -eq 'All' -or $Target -eq 'MaterialUI') {
    Write-Host "`n>>> [STAGE 1] EXECUTING MATERIALUI REAL CDP INTERACTIVE UI TESTS <<<" -ForegroundColor Yellow
    $cdpScript = Join-Path $ScriptRoot "test-materialui-cdp.mjs"
    & node $cdpScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] MaterialUI CDP UI tests failed." -ForegroundColor Red
        $allPassed = $false
    } else {
        Write-Host "[PASS] MaterialUI CDP UI tests passed cleanly." -ForegroundColor Green
    }
}

# 2. WinUI 3 Windows UI Automation Tests
if ($Target -eq 'All' -or $Target -eq 'WinUI') {
    Write-Host "`n>>> [STAGE 2] EXECUTING WINUI 3 REAL WINDOWS UI AUTOMATION TESTS <<<" -ForegroundColor Yellow
    $winuiScript = Join-Path $ScriptRoot "test-winui-ui.ps1"
    & powershell.exe -ExecutionPolicy Bypass -File $winuiScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] WinUI 3 UI Automation tests failed." -ForegroundColor Red
        $allPassed = $false
    } else {
        Write-Host "[PASS] WinUI 3 UI Automation tests passed cleanly." -ForegroundColor Green
    }
}

# 3. Adversarial Stress Testing (Phase 3 & 4)
if ($IncludeAdversarial) {
    if ($Target -eq 'All' -or $Target -eq 'MaterialUI') {
        Write-Host "`n>>> [STAGE 3A] EXECUTING MATERIALUI ADVERSARIAL STRESS SUITE <<<" -ForegroundColor Yellow
        $matAdv = Join-Path $ScriptRoot "test-adversarial-ui.mjs"
        & node $matAdv
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[FAIL] MaterialUI Adversarial tests failed." -ForegroundColor Red
            $allPassed = $false
        } else {
            Write-Host "[PASS] MaterialUI Adversarial tests passed cleanly." -ForegroundColor Green
        }
    }

    if ($Target -eq 'All' -or $Target -eq 'WinUI') {
        Write-Host "`n>>> [STAGE 3B] EXECUTING WINUI 3 ADVERSARIAL STRESS SUITE <<<" -ForegroundColor Yellow
        $winAdv = Join-Path $ScriptRoot "test-adversarial-winui.ps1"
        & powershell.exe -ExecutionPolicy Bypass -File $winAdv
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[FAIL] WinUI 3 Adversarial tests failed." -ForegroundColor Red
            $allPassed = $false
        } else {
            Write-Host "[PASS] WinUI 3 Adversarial tests passed cleanly." -ForegroundColor Green
        }
    }
}

# 4. QA Self-Test / Mutation Trials (Phase 6)
if ($IncludeSelfTest) {
    Write-Host "`n>>> [STAGE 4] EXECUTING QA MUTATION SELF-TEST (5 TRIALS) <<<" -ForegroundColor Yellow
    $mutScript = Join-Path $ScriptRoot "test-qa-mutations.mjs"
    & node $mutScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] QA Mutation Self-Test failed." -ForegroundColor Red
        $allPassed = $false
    } else {
        Write-Host "[PASS] QA Mutation Self-Test passed (5/5 defects detected)." -ForegroundColor Green
    }
}

Write-Host "`n================================================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "  ALL UI AUTOMATION & ADVERSARIAL STRESS SUITES PASSED CLEANLY" -ForegroundColor Green
} else {
    Write-Host "  ONE OR MORE UI AUTOMATION OR ADVERSARIAL SUITES FAILED" -ForegroundColor Red
}
Write-Host "================================================================================" -ForegroundColor Cyan

if (-not $allPassed) { exit 1 } else { exit 0 }
