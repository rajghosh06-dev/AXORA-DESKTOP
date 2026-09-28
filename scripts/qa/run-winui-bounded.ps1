<#
.SYNOPSIS
    Execute each registered WinUI test group in an isolated, bounded child process.
#>
[CmdletBinding()]
param(
    [ValidateRange(60, 7200)][int]$TimeoutSeconds = 2700,
    [switch]$PhysicalVoice,
    [string[]]$GroupIds = @()
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspaceRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path
$candidates = @(
    (Join-Path $workspaceRoot 'Axora-Desktop-WinUI\Axora.Desktop.Tests\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.Tests.exe'),
    (Join-Path $workspaceRoot 'Axora-Desktop-WinUI\Axora.Desktop.Tests\bin\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.Tests.exe')
)
$testExe = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $testExe) { Write-Error 'WinUI test executable not found; build before running.'; exit 2 }

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$runRoot = Join-Path $tempRoot ('axora-p3a-' + [Guid]::NewGuid().ToString('N'))
$appDataRoot = Join-Path $runRoot 'appdata'
$localAppDataRoot = Join-Path $runRoot 'localappdata'
[IO.Directory]::CreateDirectory($appDataRoot) | Out-Null
[IO.Directory]::CreateDirectory($localAppDataRoot) | Out-Null
$oldAppData = $env:APPDATA
$oldLocalAppData = $env:LOCALAPPDATA
$oldMarker = $env:AXORA_TEST_APPDATA_ROOT
$script:activeChild = $null
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$groups = @()
$results = [System.Collections.Generic.List[object]]::new()
$fatal = $null
$cleanupFailed = $false

function Invoke-BoundedChild {
    param([string]$Arguments, [string]$Stdout, [string]$Stderr, [int]$LimitSeconds)
    $process = Start-Process -FilePath $testExe -ArgumentList $Arguments -WorkingDirectory (Split-Path -Parent $testExe) -RedirectStandardOutput $Stdout -RedirectStandardError $Stderr -WindowStyle Hidden -PassThru
    $script:activeChild = $process
    $finished = $process.WaitForExit($LimitSeconds * 1000)
    $timedOut = -not $finished
    if ($timedOut) {
        Write-Host "[P3A-TIMEOUT] test PID $($process.Id), argument $Arguments, limit ${LimitSeconds}s"
        try { $process.Kill() } catch { Write-Host "[P3A-WARNING] Exact PID kill failed: $($_.Exception.Message)" }
        $finished = $process.WaitForExit(10000)
    }
    $exitCode = if ($finished) { $process.ExitCode } else { -1 }
    if ($finished) { $script:activeChild = $null }
    return [pscustomobject]@{ ExitCode = $exitCode; TimedOut = $timedOut; StillRunning = (-not $finished); Pid = $process.Id }
}

try {
    $env:APPDATA = $appDataRoot
    $env:LOCALAPPDATA = $localAppDataRoot
    $env:AXORA_TEST_APPDATA_ROOT = $appDataRoot
    Write-Host "[P3A] test=$testExe isolated-appdata=$appDataRoot overall-timeout=${TimeoutSeconds}s physical-voice=$PhysicalVoice"

    $manifestOut = Join-Path $runRoot 'manifest.stdout.log'
    $manifestErr = Join-Path $runRoot 'manifest.stderr.log'
    $manifestRun = Invoke-BoundedChild '--manifest-only' $manifestOut $manifestErr 30
    if ($manifestRun.TimedOut -or $manifestRun.StillRunning -or $manifestRun.ExitCode -ne 0) {
        throw "Manifest discovery failed: exit=$($manifestRun.ExitCode) timed-out=$($manifestRun.TimedOut) still-running=$($manifestRun.StillRunning)"
    }
    $manifestLines = @(Get-Content -LiteralPath $manifestOut)
    foreach ($line in $manifestLines) {
        if ($line -match '^\[MANIFEST-GROUP\] id=(\S+) timeout-seconds=(\d+) category=(\S+) evidence=(\S+) environment=') {
            $groups += [pscustomobject]@{ Id = $Matches[1]; Timeout = [int]$Matches[2]; Category = $Matches[3]; Evidence = $Matches[4] }
        }
    }
    $manifestSummary = @($manifestLines | Where-Object { $_.StartsWith('[MANIFEST-SUMMARY]') }) | Select-Object -Last 1
    if (-not $manifestSummary -or $manifestSummary -notmatch '^\[MANIFEST-SUMMARY\] expected=(\d+) duplicates=0$') {
        throw "Manifest summary invalid: $manifestSummary"
    }
    $manifestExpected = [int]$Matches[1]
    $duplicateManifestIds = @($groups | Group-Object Id | Where-Object { $_.Count -gt 1 })
    if ($groups.Count -ne $manifestExpected -or $groups.Count -eq 0 -or $duplicateManifestIds.Count -gt 0) {
        throw "Manifest mismatch or duplicate IDs: parsed=$($groups.Count), expected=$manifestExpected, duplicates=$($duplicateManifestIds.Count)"
    }
    if ($GroupIds.Count -gt 0) {
        $allIds = @($groups | ForEach-Object { $_.Id })
        $unknownRequested = @($GroupIds | Where-Object { $allIds -notcontains $_ })
        if ($unknownRequested.Count -gt 0 -or @($GroupIds | Select-Object -Unique).Count -ne $GroupIds.Count) {
            throw "Unknown or duplicate targeted group IDs: $($GroupIds -join ',')"
        }
        $groups = @($groups | Where-Object { $GroupIds -contains $_.Id })
        Write-Host "[P3A-TARGETED] requested=$($GroupIds -join ',') manifest-total=$manifestExpected; this is not full-suite evidence"
    }

    foreach ($group in $groups) {
        $remaining = [int][Math]::Floor(($deadline - [DateTime]::UtcNow).TotalSeconds)
        $status = 'Blocked'
        $reason = 'Overall suite deadline reached before group launch'
        $passed = 0; $failed = 0; $observations = 0; $gaps = 0; $partialPassLines = 0
        if ($remaining -gt 0) {
            $safeId = $group.Id -replace '[^A-Za-z0-9._-]', '_'
            $stdout = Join-Path $runRoot ($safeId + '.stdout.log')
            $stderr = Join-Path $runRoot ($safeId + '.stderr.log')
            $arguments = '--group=' + $group.Id
            if ($PhysicalVoice) { $arguments += ' --physical-voice' }
            $limit = [Math]::Max(1, [Math]::Min($group.Timeout + 20, $remaining))
            try {
                $run = Invoke-BoundedChild $arguments $stdout $stderr $limit
                $lines = if (Test-Path -LiteralPath $stdout) { @(Get-Content -LiteralPath $stdout) } else { @() }
                $partialPassLines = @($lines | Where-Object { $_ -match '^\s*\[PASS\]' }).Count
                $ends = @($lines | Where-Object { $_.StartsWith('[GROUP-END]') })
                $summaries = @($lines | Where-Object { $_.StartsWith('[LEDGER-SUMMARY]') })
                if ($run.StillRunning) {
                    $reason = "Exact test PID $($run.Pid) did not exit after timeout/kill; aborting later launches"
                } elseif ($run.TimedOut) {
                    $reason = "Process watchdog expired after ${limit}s; exact test PID $($run.Pid) terminated"
                } elseif ($ends.Count -ne 1 -or $summaries.Count -ne 1) {
                    $reason = "Missing or duplicate child ledger: group-ends=$($ends.Count), summaries=$($summaries.Count), exit=$($run.ExitCode)"
                } elseif ($ends[0] -match '^\[GROUP-END\] (\S+) \| (Pass|Fail|EnvironmentNotAvailable|SkippedByPolicy|Blocked) \| pass=(\d+) fail=(\d+) observations=(\d+) static-gaps=(\d+) elapsed=') {
                    $reportedId = $Matches[1]; $reportedStatus = $Matches[2]
                    $reportedPassed = [int]$Matches[3]; $reportedFailed = [int]$Matches[4]
                    $reportedObservations = [int]$Matches[5]; $reportedGaps = [int]$Matches[6]
                    $expectedExit = if ($reportedStatus -in @('Pass', 'EnvironmentNotAvailable', 'SkippedByPolicy')) { 0 } else { 1 }
                    if ($reportedId -ne $group.Id -or $run.ExitCode -ne $expectedExit -or
                        $summaries[0] -notmatch 'expected=1 executed=1 .* missing=0 duplicates=0 unknown=0 .*') {
                        $reason = "Child ledger/exit mismatch: expected=$($group.Id), reported=$reportedId, exit=$($run.ExitCode), summary=$($summaries[0])"
                    } else {
                        $status = $reportedStatus; $passed = $reportedPassed; $failed = $reportedFailed
                        $observations = $reportedObservations; $gaps = $reportedGaps
                        $reason = 'Child ledger verified'
                    }
                } else {
                    $reason = "Unparseable child group result: $($ends[0])"
                }
            } catch {
                $reason = "Group launch/collection error: $($_.Exception.Message)"
            }
        }
        $results.Add([pscustomobject]@{
            Id = $group.Id; Status = $status; Passed = $passed; Failed = $failed
            Observations = $observations; StaticGaps = $gaps; PartialPassLines = $partialPassLines
            Category = $group.Category; Evidence = $group.Evidence; Reason = $reason
        })
        Write-Host "[SUITE-GROUP] $($group.Id) | $status | pass=$passed fail=$failed observations=$observations static-gaps=$gaps partial-pass-lines=$partialPassLines | $reason"
        if ($script:activeChild -and -not $script:activeChild.HasExited) { break }
    }
} catch {
    $fatal = $_.Exception.Message
    Write-Host "[P3A-HARNESS-ERROR] $fatal"
} finally {
    $env:APPDATA = $oldAppData
    $env:LOCALAPPDATA = $oldLocalAppData
    $env:AXORA_TEST_APPDATA_ROOT = $oldMarker
    if (-not $script:activeChild -or $script:activeChild.HasExited) {
        foreach ($owned in @($appDataRoot, $localAppDataRoot)) {
            $resolved = [IO.Path]::GetFullPath($owned)
            if ($resolved.StartsWith($runRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
                ([IO.Path]::GetFileName($resolved) -in @('appdata', 'localappdata'))) {
                Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
                if (Test-Path -LiteralPath $resolved) {
                    $cleanupFailed = $true
                    Write-Host "[P3A-WARNING] Test-owned settings cleanup incomplete: $resolved"
                }
            }
        }
    } else {
        $cleanupFailed = $true
        Write-Host '[P3A-WARNING] A test child is still live; isolated settings were preserved rather than deleted underneath it.'
    }
}

# Any unvisited registered group is explicitly blocked, not silently omitted.
foreach ($group in $groups) {
    $seen = @($results | Where-Object { $_.Id -eq $group.Id }).Count
    if ($seen -eq 0) {
        $results.Add([pscustomobject]@{ Id = $group.Id; Status = 'Blocked'; Passed = 0; Failed = 0; Observations = 0; StaticGaps = 0; PartialPassLines = 0; Category = $group.Category; Evidence = $group.Evidence; Reason = 'Harness aborted before launch' })
        Write-Host "[SUITE-GROUP] $($group.Id) | Blocked | Harness aborted before launch"
    }
}
$expected = $groups.Count
$executed = $results.Count
$resultIds = @($results | ForEach-Object { $_.Id })
$manifestIds = @($groups | ForEach-Object { $_.Id })
$missing = @($groups | Where-Object { $resultIds -notcontains $_.Id }).Count
$duplicates = @($results | Group-Object Id | Where-Object { $_.Count -gt 1 }).Count
$unknown = @($results | Where-Object { $manifestIds -notcontains $_.Id }).Count
function Sum-Field([string]$field) { return [int](($results | Measure-Object -Property $field -Sum).Sum) }
function Count-Status([string]$status) { return @($results | Where-Object { $_.Status -eq $status }).Count }
$pass = Count-Status 'Pass'; $fail = Count-Status 'Fail'
$unavailable = Count-Status 'EnvironmentNotAvailable'; $skipped = Count-Status 'SkippedByPolicy'; $blocked = Count-Status 'Blocked'
$assertionsPassed = Sum-Field 'Passed'; $assertionsFailed = Sum-Field 'Failed'
$observations = Sum-Field 'Observations'; $gaps = Sum-Field 'StaticGaps'; $partial = Sum-Field 'PartialPassLines'
function Sum-Evidence([string]$evidence, [string]$field) {
    return [int](($results | Where-Object { $_.Evidence -eq $evidence } | Measure-Object -Property $field -Sum).Sum)
}
$detPass = Sum-Evidence 'Deterministic' 'Passed'; $detFail = Sum-Evidence 'Deterministic' 'Failed'
$intPass = Sum-Evidence 'Integration' 'Passed'; $intFail = Sum-Evidence 'Integration' 'Failed'
$envPass = Sum-Evidence 'EnvironmentRuntime' 'Passed'; $envFail = Sum-Evidence 'EnvironmentRuntime' 'Failed'
$invalid = $fatal -or $cleanupFailed -or $expected -eq 0 -or $missing -gt 0 -or $duplicates -gt 0 -or $unknown -gt 0 -or $fail -gt 0 -or $blocked -gt 0 -or $assertionsPassed -eq 0
$exitDisposition = if ($invalid) { 'FAIL' } elseif ($GroupIds.Count -gt 0) { 'TARGETED-PASS' } else { 'PASS-INCOMPLETE-PHYSICAL-COVERAGE' }
$elapsed = [Math]::Round($TimeoutSeconds - ($deadline - [DateTime]::UtcNow).TotalSeconds, 1)
Write-Host "[SUITE-LEDGER] mode=$(if ($GroupIds.Count -gt 0) { 'targeted' } else { 'full' }) manifest-total=$manifestExpected expected=$expected executed=$executed pass=$pass fail=$fail environment-not-available=$unavailable skipped-by-policy=$skipped blocked=$blocked missing=$missing duplicates=$duplicates unknown=$unknown assertions-passed=$assertionsPassed assertions-failed=$assertionsFailed environment-observations=$observations static-gaps=$gaps partial-pass-lines=$partial elapsed-seconds=$elapsed exit=$exitDisposition"
Write-Host "[EVIDENCE-LEDGER] deterministic-pass=$detPass deterministic-fail=$detFail integration-pass=$intPass integration-fail=$intFail environment-runtime-pass=$envPass environment-runtime-fail=$envFail observations=$observations static-gaps=$gaps"
Write-Host "[P3A] run-root=$runRoot retained-logs=true cleanup-failed=$cleanupFailed fatal=$fatal"
if ($invalid) { exit 1 }
exit 0
