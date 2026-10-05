[CmdletBinding()]
param([ValidateRange(5,300)][int]$TimeoutSeconds = 135)
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot 'bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64/Axora.Studio.Tests.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build Studio.Tests Debug/x64 before running M1.' }
$expected = @('STUDIO-H0','STUDIO-M1-FLASHCARDS','STUDIO-M1-EXPORT','STUDIO-M1-READALOUD')
$manifestText = & $executable --manifest
if ($LASTEXITCODE -ne 0) { throw 'Manifest failed.' }
$manifest = $manifestText | ConvertFrom-Json
if (@($manifest.groups).Count -ne $expected.Count -or (Compare-Object $expected @($manifest.groups))) { throw 'Unknown or incomplete Studio manifest.' }
$logRoot = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff')
foreach ($group in $expected) {
    $stdout = Join-Path $logRoot "m1-$stamp-$group.stdout.log"
    $stderr = Join-Path $logRoot "m1-$stamp-$group.stderr.log"
    $child = Start-Process -FilePath $executable -ArgumentList "--group=$group" -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if (-not $child.WaitForExit($TimeoutSeconds * 1000)) {
        Write-Output "BLOCKED: $group exceeded ${TimeoutSeconds}s; PID=$($child.Id). No forced termination. Logs: $stdout; $stderr"
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
    if ($ledger.group -ne $group -or $ledger.registeredGroups -ne 1 -or $ledger.executedGroups -ne 1 -or
        $ledger.totalRegisteredGroups -ne $expected.Count -or $ledger.disposition -ne 'Pass' -or -not $ledger.complete -or
        $ledger.passedAssertions -le 0 -or $ledger.failedAssertions -ne 0 -or $ledger.missing -ne 0 -or
        $ledger.duplicate -ne 0 -or $ledger.unknown -ne 0 -or $ledger.blocked -ne 0) { exit 1 }
    Write-Output "Runner PASS: $group; child exit=$($child.ExitCode); evidence=$stdout"
}
exit 0
