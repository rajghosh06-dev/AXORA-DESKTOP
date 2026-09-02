<#
.SYNOPSIS
    Performs runtime smoke launch and startup log validation.
.PARAMETER Target
    'WinUI' or 'MaterialUI'. Defaults to 'WinUI'.
.PARAMETER DurationSeconds
    Number of seconds to keep the process running before graceful termination. Defaults to 3.
#>
[CmdletBinding()]
param(
    [ValidateSet('WinUI', 'MaterialUI')]
    [string]$Target = 'WinUI',
    [int]$DurationSeconds = 3
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA DESKTOP - RUNTIME SMOKE TEST ($Target)" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

& (Join-Path $ScriptRoot "pre-build-clean.ps1")

if ($Target -eq 'WinUI') {
    # Check potential output locations (x64\Debug or Debug)
    $candidates = @(
        (Join-Path $WorkspaceRoot "Axora-Desktop-WinUI\Axora.Desktop\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.exe"),
        (Join-Path $WorkspaceRoot "Axora-Desktop-WinUI\Axora.Desktop\bin\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.exe")
    )
    $ExePath = $null
    foreach ($cand in $candidates) {
        if (Test-Path $cand) { $ExePath = $cand; break }
    }

    if (-not $ExePath) {
        Write-Host "[ERROR] Axora.Desktop.exe not found in expected output directories." -ForegroundColor Red
        Write-Host "Please compile the project first using .\scripts\qa\build-all.ps1 -Target WinUI" -ForegroundColor Yellow
        exit 1
    }

    $ExeDir = Split-Path -Parent $ExePath
    $LogPath = Join-Path $ExeDir "startup.log"

    # Backup / Clear old startup.log
    if (Test-Path $LogPath) {
        Remove-Item $LogPath -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Launching $ExePath in background..." -ForegroundColor Yellow
    $process = Start-Process -FilePath $ExePath -PassThru

    Write-Host "Waiting $DurationSeconds seconds for window initialization..." -ForegroundColor Gray
    Start-Sleep -Seconds $DurationSeconds

    # Check if process is still alive
    if ($process.HasExited) {
        Write-Host "[FAIL] Process exited prematurely with ExitCode: $($process.ExitCode)" -ForegroundColor Red
        if (Test-Path $LogPath) {
            Write-Host "`n--- STARTUP LOG ---" -ForegroundColor Yellow
            Get-Content $LogPath | ForEach-Object { Write-Host "  $_" }
        }
        exit 1
    } else {
        Write-Host "[PASS] Process is active and running (PID: $($process.Id))." -ForegroundColor Green

        # Verify startup.log
        if (Test-Path $LogPath) {
            $logContent = Get-Content $LogPath -Raw
            Write-Host "`n--- STARTUP DIAGNOSTICS ---" -ForegroundColor Cyan
            Write-Host $logContent -ForegroundColor Gray

            if ($logContent -match "UnhandledException") {
                Write-Host "[WARNING] Unhandled exception detected in startup log!" -ForegroundColor Red
            } elseif ($logContent -match "MainWindow activated" -or $logContent -match "AppHost built") {
                Write-Host "[PASS] Application host and window initialized successfully." -ForegroundColor Green
            }
        } else {
            Write-Host "[WARNING] No startup.log was written." -ForegroundColor Yellow
        }

        # Graceful cleanup
        Write-Host "`nTerminating smoke test instance (PID: $($process.Id))..." -ForegroundColor Gray
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
} elseif ($Target -eq 'MaterialUI') {
    $MatExe = Join-Path $WorkspaceRoot "Axora-Desktop-MaterialUI\src-tauri\target\debug\axora-desktop.exe"
    if (Test-Path $MatExe) {
        Write-Host "Launching MaterialUI binary ($MatExe)..." -ForegroundColor Yellow
        $process = Start-Process -FilePath $MatExe -PassThru
        Start-Sleep -Seconds $DurationSeconds
        if ($process.HasExited) {
            Write-Host "[FAIL] MaterialUI process exited prematurely (ExitCode: $($process.ExitCode))." -ForegroundColor Red
            exit 1
        } else {
            Write-Host "[PASS] MaterialUI process is running (PID: $($process.Id))." -ForegroundColor Green
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    } else {
        Write-Host "[BLOCKED] MaterialUI executable not found at $MatExe." -ForegroundColor Yellow
        Write-Host "Rust/Cargo and Node.js toolchains are required to build the native binary." -ForegroundColor Gray
        Write-Host "================================================================================" -ForegroundColor Cyan
        Write-Host "  SMOKE TEST BLOCKED: TARGET BINARY NOT FOUND" -ForegroundColor Yellow
        Write-Host "================================================================================" -ForegroundColor Cyan
        exit 2
    }
}

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  SMOKE TEST COMPLETE: RUNTIME LAUNCH VERIFIED" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan
exit 0
