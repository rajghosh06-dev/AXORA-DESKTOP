<#
.SYNOPSIS
    Terminates any active Axora desktop instances to prevent file lock errors during compilation.
#>
[CmdletBinding()]
param()

$ProcessNames = @("Axora.Desktop", "axora-desktop", "Axora.Desktop.Tests")
$KilledCount = 0

foreach ($procName in $ProcessNames) {
    $processes = Get-Process -Name $procName -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host "Terminating active process: $procName (PID: $($processes.Id -join ', '))" -ForegroundColor Yellow
        $processes | Stop-Process -Force -ErrorAction SilentlyContinue
        $KilledCount++
    }
}

if ($KilledCount -eq 0) {
    Write-Host "[OK] No locked Axora processes detected." -ForegroundColor Green
} else {
    Write-Host "[OK] Terminated $KilledCount locked process instances." -ForegroundColor Green
    Start-Sleep -Milliseconds 500
}
exit 0
