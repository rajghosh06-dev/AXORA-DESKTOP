<#
.SYNOPSIS
    Runs automated test suites for Axora Desktop.
.PARAMETER Target
    'WinUI', 'MaterialUI', or 'All'. Defaults to 'All'.
#>
[CmdletBinding()]
param(
    [ValidateSet('WinUI', 'MaterialUI', 'All')]
    [string]$Target = 'All'
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA DESKTOP - AUTOMATED TEST RUNNER ($Target)" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

$TotalPassed = 0
$TotalFailed = 0
$TotalSkipped = 0

# 1. Run WinUI Tests
if ($Target -eq 'WinUI' -or $Target -eq 'All') {
    Write-Host "`n[1] Running WinUI 3 Adversarial Stress Test Suite..." -ForegroundColor Yellow
    $candidates = @(
        (Join-Path $WorkspaceRoot "Axora-Desktop-WinUI\Axora.Desktop.Tests\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.Tests.exe"),
        (Join-Path $WorkspaceRoot "Axora-Desktop-WinUI\Axora.Desktop.Tests\bin\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.Tests.exe")
    )
    $TestExe = $null
    foreach ($cand in $candidates) {
        if (Test-Path $cand) { $TestExe = $cand; break }
    }

    if (-not $TestExe) {
        Write-Host "Test runner executable not found. Attempting build first..." -ForegroundColor Gray
        & (Join-Path $ScriptRoot "build-all.ps1") -Target WinUI
        foreach ($cand in $candidates) {
            if (Test-Path $cand) { $TestExe = $cand; break }
        }
    }

    if ($TestExe -and (Test-Path $TestExe)) {
        $output = & $TestExe 2>&1
        $output | ForEach-Object { Write-Host "  $_" }

        $summaryMatched = $false
        foreach ($line in $output) {
            if ($line -match "Total:\s*(\d+)\s*\|\s*Passed:\s*(\d+)\s*\|\s*Failed:\s*(\d+)") {
                $passed = [int]$matches[2]
                $failed = [int]$matches[3]
                $TotalPassed += $passed
                $TotalFailed += $failed
                $summaryMatched = $true
                break
            }
        }

        if (-not $summaryMatched) {
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[PASS] Test executable completed with exit code 0." -ForegroundColor Green
                $TotalPassed += 1
            } else {
                Write-Host "[FAIL] Test executable exited with code $LASTEXITCODE." -ForegroundColor Red
                $TotalFailed += 1
            }
        } elseif ($LASTEXITCODE -eq 0 -and $failed -eq 0) {
            Write-Host "[PASS] WinUI Stress Suite ($passed/$passed assertions passed)." -ForegroundColor Green
        } else {
            Write-Host "[FAIL] WinUI Stress Suite encountered $failed failure(s)." -ForegroundColor Red
        }
    } else {
        Write-Host "[ERROR] Could not build or find test executable." -ForegroundColor Red
        $TotalFailed += 1
    }
}

# 2. Run MaterialUI Tests
if ($Target -eq 'MaterialUI' -or $Target -eq 'All') {
    Write-Host "`n[2] Running MaterialUI Tests..." -ForegroundColor Yellow
    $cargoCmd = Get-Command "cargo" -ErrorAction SilentlyContinue
    if ($cargoCmd) {
        $cargoToml = Join-Path $WorkspaceRoot "Axora-Desktop-MaterialUI\src-tauri\Cargo.toml"
        $output = cargo test --manifest-path $cargoToml -- --nocapture 2>&1
        $output | ForEach-Object { Write-Host "  $_" }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[PASS] MaterialUI Rust Backend Unit Tests." -ForegroundColor Green
            # Count passed tests from cargo output
            $rustPassed = 0
            foreach ($line in $output) {
                if ($line -match "test result: ok\.\s*(\d+)\s*passed") {
                    $rustPassed = [int]$matches[1]
                }
            }
            $TotalPassed += $rustPassed
        } else {
            Write-Host "[FAIL] MaterialUI Rust Unit Tests failed." -ForegroundColor Red
            $TotalFailed += 1
        }
    } else {
        Write-Host "[BLOCKED] Cargo toolchain is not available in PATH to execute Rust unit tests." -ForegroundColor Yellow
        $TotalSkipped += 1
    }
}

Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY: $TotalPassed Passed | $TotalFailed Failed | $TotalSkipped Skipped" -ForegroundColor $(if ($TotalFailed -gt 0) { "Red" } elseif ($TotalPassed -gt 0) { "Green" } else { "Yellow" })
Write-Host "================================================================================" -ForegroundColor Cyan

if ($TotalFailed -gt 0) {
    exit 1
} elseif ($TotalPassed -gt 0) {
    exit 0
} else {
    Write-Host "  RESULT: TEST EXECUTION BLOCKED (No tests were able to execute)" -ForegroundColor Yellow
    exit 2
}
