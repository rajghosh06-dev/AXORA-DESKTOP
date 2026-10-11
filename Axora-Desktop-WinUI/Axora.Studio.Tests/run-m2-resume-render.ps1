[CmdletBinding()]
param([ValidateRange(5,300)][int]$TimeoutSeconds = 135)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64/Axora.Studio.Tests.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build Studio.Tests Debug/x64 before running Resume-render.' }
$expected = @('STUDIO-H0','STUDIO-M1-FLASHCARDS','STUDIO-M1-EXPORT','STUDIO-M1-READALOUD','STUDIO-M2-RESUME','STUDIO-M2-RENDERER','STUDIO-M2-RESUME-RENDER')
$manifestText = & $exe --manifest
if ($LASTEXITCODE -ne 0) { throw 'Manifest failed.' }
$manifest = $manifestText | ConvertFrom-Json
if (@($manifest.groups).Count -ne 7 -or (Compare-Object $expected @($manifest.groups))) { throw 'Unknown or incomplete Studio manifest.' }
$logRoot = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff')
$stdout = Join-Path $logRoot "resume-render-$stamp.stdout.log"
$stderr = Join-Path $logRoot "resume-render-$stamp.stderr.log"
$child = Start-Process -FilePath $exe -ArgumentList '--group=STUDIO-M2-RESUME-RENDER' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if (-not $child.WaitForExit($TimeoutSeconds * 1000)) { Write-Output "BLOCKED: Resume-render PID=$($child.Id); logs=$stdout; $stderr"; exit 1 }
$child.WaitForExit()
$output = Get-Content -LiteralPath $stdout -Raw
Write-Output $output
Get-Content -LiteralPath $stderr
$lines = @($output -split '\r?\n' | Where-Object { $_.StartsWith('STUDIO-LEDGER ') })
if ($child.ExitCode -ne 0 -or $lines.Count -ne 1) { exit 1 }
$l = $lines[0].Substring('STUDIO-LEDGER '.Length) | ConvertFrom-Json
if ($l.group -ne 'STUDIO-M2-RESUME-RENDER' -or $l.totalRegisteredGroups -ne 7 -or $l.registeredGroups -ne 1 -or $l.executedGroups -ne 1 -or
    -not $l.complete -or $l.disposition -ne 'Pass' -or $l.passedAssertions -le 0 -or $l.failedAssertions -ne 0 -or
    $l.missing -ne 0 -or $l.duplicate -ne 0 -or $l.unknown -ne 0 -or $l.blocked -ne 0) { exit 1 }
exit 0
