# ================================================================================
# AXORA WINUI 3 - NATIVE PDF RENDERING POC VALIDATION SCRIPT (W2-C)
# Tests Windows.Data.Pdf rendering directly inside the live desktop application
# ================================================================================

param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
$exePath = Join-Path $repoRoot "Axora-Desktop-WinUI\Axora.Desktop\bin\x64\$Configuration\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.exe"
$screenshotDir = Join-Path $repoRoot "docs\qa\screenshots"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA WINUI 3 - NATIVE PDF RENDERING POC RUNNER (Phase W2-C)" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

if (-not (Test-Path $exePath)) {
    Write-Host "[FAIL] Executable not found at: $exePath" -ForegroundColor Red
    exit 1
}

Write-Host "[1/3] Launching Axora.Desktop.exe with --poc-pdf-render..." -ForegroundColor Yellow
$proc = Start-Process -FilePath $exePath -ArgumentList "--poc-pdf-render" -NoNewWindow -PassThru -Wait

if ($proc.ExitCode -ne 0) {
    Write-Host "[FAIL] Axora.Desktop.exe exited with non-zero exit code: $($proc.ExitCode)" -ForegroundColor Red
    exit 1
}
Write-Host "[PASS] Desktop process completed execution cleanly (ExitCode: 0)." -ForegroundColor Green

Write-Host "`n[2/3] Verifying generated visual evidence screenshots in docs/qa/screenshots/..." -ForegroundColor Yellow
$requiredScreenshots = @(
    "pdf-poc-portrait.png",
    "pdf-poc-landscape.png",
    "pdf-poc-image.png",
    "pdf-poc-unicode.png"
)

$allScreenshotsValid = $true
foreach ($name in $requiredScreenshots) {
    $fullPath = Join-Path $screenshotDir $name
    if (-not (Test-Path $fullPath)) {
        Write-Host "  [FAIL] Missing screenshot: $name" -ForegroundColor Red
        $allScreenshotsValid = $false
        continue
    }

    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    if ($bytes.Length -lt 100) {
        Write-Host "  [FAIL] Screenshot is unexpectedly small ($($bytes.Length) bytes): $name" -ForegroundColor Red
        $allScreenshotsValid = $false
        continue
    }

    # Verify PNG magic bytes: 0x89, 0x50, 0x4E, 0x47
    if ($bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or $bytes[3] -ne 0x47) {
        Write-Host "  [FAIL] Screenshot does not have valid PNG header: $name" -ForegroundColor Red
        $allScreenshotsValid = $false
        continue
    }

    Write-Host "  [PASS] $name ($($bytes.Length) bytes, valid PNG)" -ForegroundColor Green
}

if (-not $allScreenshotsValid) {
    Write-Host "`n[FAIL] One or more visual evidence screenshots failed validation." -ForegroundColor Red
    exit 1
}

Write-Host "`n[3/3] Inspecting startup.log diagnostics..." -ForegroundColor Yellow
$logPath = Join-Path (Split-Path $exePath) "startup.log"
if (Test-Path $logPath) {
    $pocLines = Get-Content $logPath | Where-Object { $_ -match "\[POC\]" -or $_ -match "PDF Renderer POC" }
    foreach ($line in $pocLines) {
        Write-Host "  $line" -ForegroundColor Gray
    }
}

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  W2-C PDF RENDERING POC VALIDATION RESULT: ALL GATES PASSED" -ForegroundColor Green
Write-Host "  DECISION: APPROVED FOR W2 CORE" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan
exit 0
