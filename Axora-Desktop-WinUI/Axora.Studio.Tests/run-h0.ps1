[CmdletBinding()]
param([ValidateRange(5,300)][int]$TimeoutSeconds = 135)
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot 'bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64/Axora.Studio.Tests.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build Studio.Tests Debug/x64 before running H0.' }
$logRoot = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff')
$stdout = Join-Path $logRoot "h0-$stamp.stdout.log"
$stderr = Join-Path $logRoot "h0-$stamp.stderr.log"
$child = Start-Process -FilePath $executable -ArgumentList '--group=STUDIO-H0' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if (-not $child.WaitForExit($TimeoutSeconds * 1000)) {
    Write-Output "BLOCKED: STUDIO-H0 exceeded ${TimeoutSeconds}s; PID=$($child.Id). No forced termination performed. Logs: $stdout; $stderr"
    exit 1
}
$child.WaitForExit()
$output = Get-Content -LiteralPath $stdout -Raw
Write-Output $output
$errors = Get-Content -LiteralPath $stderr -Raw
if ($errors) { Write-Output $errors }
$lines = @($output -split '\r?\n' | Where-Object { $_.StartsWith('STUDIO-LEDGER ') })
if ($child.ExitCode -ne 0 -or $lines.Count -ne 1) { exit 1 }
$ledger = $lines[0].Substring('STUDIO-LEDGER '.Length) | ConvertFrom-Json
if ($ledger.group -ne 'STUDIO-H0' -or $ledger.registeredGroups -ne 1 -or $ledger.executedGroups -ne 1 -or
    $ledger.disposition -ne 'Pass' -or -not $ledger.complete -or $ledger.passedAssertions -le 0 -or
    $ledger.failedAssertions -ne 0 -or $ledger.missing -ne 0 -or $ledger.duplicate -ne 0 -or
    $ledger.unknown -ne 0 -or $ledger.blocked -ne 0) { exit 1 }
Write-Output "Runner PASS; child exit=$($child.ExitCode); evidence=$stdout"
exit 0
