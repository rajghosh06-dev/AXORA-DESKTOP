[CmdletBinding()]
param(
    [ValidateSet('Studio','Desktop')][string]$Product = 'Studio',
    [Parameter(Mandatory)][string]$FixtureRoot,
    [ValidateRange(5,900)][int]$ExitBudgetSeconds = 600
)
$ErrorActionPreference = 'Stop'
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'logs')) + [IO.Path]::DirectorySeparatorChar
$fixture = [IO.Path]::GetFullPath($FixtureRoot)
if (-not $fixture.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Runtime fixtures must be under the Studio.Tests logs directory.'
}
$roaming = Join-Path $fixture 'roaming'
$local = Join-Path $fixture 'local'
New-Item -ItemType Directory -Path $roaming,$local -Force | Out-Null
$legacyRoot = Join-Path $roaming 'Axora'
New-Item -ItemType Directory -Path $legacyRoot -Force | Out-Null
$legacySettings = Join-Path $legacyRoot 'settings.json'
if (-not (Test-Path -LiteralPath $legacySettings)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Fixtures/legacy-runtime-settings.json') -Destination $legacySettings
}
$legacyBefore = (Get-FileHash -LiteralPath $legacySettings -Algorithm SHA256).Hash
$projectRoot = Split-Path $PSScriptRoot
$exe = Join-Path $projectRoot "Axora.$Product/bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64/Axora.$Product.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "Missing $Product binary." }
$start = [Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute = $false
# The synthetic legacy fixture's relative download directory stays test-owned.
$start.WorkingDirectory = $fixture
# This is a visible interactive QA window, not a background helper/service.
$start.Environment['APPDATA'] = $roaming
$start.Environment['LOCALAPPDATA'] = $local
$child = [Diagnostics.Process]::Start($start)
$deadline = [DateTime]::UtcNow.AddSeconds(25)
do {
    Start-Sleep -Milliseconds 100
    $child.Refresh()
} while (-not $child.HasExited -and $child.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
$handle = if($child.HasExited) { 0 } else { $child.MainWindowHandle.ToInt64() }
$title = if($child.HasExited) { '' } else { $child.MainWindowTitle }
Write-Output ('RUNTIME-LAUNCH ' + ([pscustomobject]@{product=$Product;pid=$child.Id;handle=$handle;title=$title;fixture=$fixture;exited=$child.HasExited} | ConvertTo-Json -Compress))
# Keep the original launch Process object so its actual exit code is observable.
# The driver issues normal CloseMainWindow separately; no forced termination exists.
if (-not $child.WaitForExit($ExitBudgetSeconds * 1000)) {
    Write-Output "BLOCKED: $Product PID=$($child.Id) has not exited within ${ExitBudgetSeconds}s. Close normally; no forced termination performed."
    exit 1
}
$exitCode = $child.ExitCode
$legacyAfter = (Get-FileHash -LiteralPath $legacySettings -Algorithm SHA256).Hash
Write-Output ('RUNTIME-EXIT ' + ([pscustomobject]@{product=$Product;pid=$child.Id;exitCode=$exitCode;legacyCanaryUnchanged=($legacyBefore -eq $legacyAfter)} | ConvertTo-Json -Compress))
if ($Product -eq 'Studio') {
    $log = Join-Path $roaming "Axora/Studio/startup.$($child.Id).log"
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log }
}
exit $exitCode
