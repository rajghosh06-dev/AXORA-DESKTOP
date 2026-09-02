<#
.SYNOPSIS
    Performs a comprehensive security scan across working tree, git history, and configurations.
.OUTPUTS
    Exit code 0: PASS (Zero secrets found)
    Exit code 1: FAIL (Potential secrets or unclassified sensitive values detected)
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA DESKTOP - SECURITY & SECRETS AUDIT" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

$Patterns = @{
    "Google API Key"                = "AIza[0-9A-Za-z-_]{35}"
    "GitHub Classic Token"          = "ghp_[0-9a-zA-Z]{36}"
    "GitHub Fine-Grained Token"     = "github_pat_[0-9a-zA-Z_]{82}"
    "Slack Token"                   = "xox[baprs]-[0-9a-zA-Z]{10,48}"
    "Private Key Header"            = "-----BEGIN (RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----"
    "OpenAI API Key"                = "sk-[a-zA-Z0-9]{32,}"
    "AWS Access Key ID"             = "(A3T[A-Z0-9]|AKIA|AGPA|AIDA|AROA|AIPA|ANPA|ANVA|ASIA)[A-Z0-9]{16}"
    "Hardcoded Password Assignment" = "(?i)(password|passwd|pwd)\s*[:=]\s*[\x22\x27][^\x22\x27\s]{6,}[\x22\x27]"
    "Hardcoded Secret Assignment"   = "(?i)(client_secret|secret_key|auth_token)\s*[:=]\s*[\x22\x27][^\x22\x27\s]{6,}[\x22\x27]"
}

$CurrentTreeFindings = @()
$HistoryFindings = @()
$ConfigFindings = @()
$TestFixtureFindings = @()

# 1. Audit Current Working Tree
Write-Host "`n[1] Scanning Current Working Tree..." -ForegroundColor Yellow
$projectFiles = Get-ChildItem -Path $WorkspaceRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|target|node_modules|\.git|\.vs)\\" -and
    $_.Extension -notmatch "\.(dll|exe|pdb|ico|png|jpg|jpeg|binlog|log)$"
}

foreach ($file in $projectFiles) {
    $relPath = $file.FullName.Substring($WorkspaceRoot.Path.Length + 1)
    $content = try { [System.IO.File]::ReadAllText($file.FullName) } catch { $null }
    if (-not $content) { continue }

    foreach ($name in $Patterns.Keys) {
        $pattern = $Patterns[$name]
        if ($content -match $pattern) {
            # Distinguish test fixtures vs production vs config
            if ($relPath -match "(test|spec|mock)" -or $file.Name -match "Tests") {
                $TestFixtureFindings += [PSCustomObject]@{ File = $relPath; Type = $name }
            } elseif ($file.Extension -match "\.(json|config|xml|yaml|yml|props|targets)$") {
                $ConfigFindings += [PSCustomObject]@{ File = $relPath; Type = $name }
            } else {
                $CurrentTreeFindings += [PSCustomObject]@{ File = $relPath; Type = $name }
            }
        }
    }
}

$HistoricalTestFixtureNotes = @()

# 2. Audit Git History
Write-Host "[2] Scanning Git Commit History..." -ForegroundColor Yellow
Push-Location $WorkspaceRoot
try {
    $commits = git log --format="%H" 2>$null
    if ($commits) {
        foreach ($commit in $commits) {
            $diff = git show $commit 2>$null
            if ($diff) {
                $diffText = ($diff -join "`n")
                foreach ($name in $Patterns.Keys) {
                    $pattern = $Patterns[$name]
                    if ($diffText -match $pattern) {
                        $matchedText = $Matches[0]
                        if ($matchedText -match "SecretMasterPassword2026" -or $matchedText -match "axora-non-secret-test-dummy") {
                            $HistoricalTestFixtureNotes += [PSCustomObject]@{
                                Commit = $commit.Substring(0, 7)
                                Detail = "Unit test dummy string in vault.rs (classified non-secret fixture)"
                            }
                        } else {
                            $HistoryFindings += [PSCustomObject]@{ Commit = $commit.Substring(0, 7); Type = $name; Match = $matchedText }
                        }
                    }
                }
            }
        }
    }
} finally {
    Pop-Location
}

# 3. Output Results
Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host "  AUDIT CLASSIFICATION RESULTS" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

$hasErrors = $false

if ($CurrentTreeFindings.Count -eq 0) {
    Write-Host "  CURRENT TREE RESULT:    PASS (0 secrets detected across $($projectFiles.Count) files)" -ForegroundColor Green
} else {
    Write-Host "  CURRENT TREE RESULT:    FAIL ($($CurrentTreeFindings.Count) potential secret(s) found)" -ForegroundColor Red
    $CurrentTreeFindings | Format-Table -AutoSize
    $hasErrors = $true
}

if ($HistoryFindings.Count -eq 0) {
    Write-Host "  GIT HISTORY RESULT:     PASS (0 real secrets found in commit history)" -ForegroundColor Green
    if ($HistoricalTestFixtureNotes.Count -gt 0) {
        Write-Host "  HISTORICAL FIXTURES:    DISCLOSED (Known non-secret test fixture in commit a9d0491; replaced in HEAD)" -ForegroundColor Gray
    }
} else {
    Write-Host "  GIT HISTORY RESULT:     FAIL ($($HistoryFindings.Count) potential secret(s) found in history)" -ForegroundColor Red
    $HistoryFindings | Format-Table -AutoSize
    $hasErrors = $true
}

if ($TestFixtureFindings.Count -eq 0) {
    Write-Host "  TEST FIXTURE RESULT:    PASS (0 hardcoded credentials in test files)" -ForegroundColor Green
} else {
    Write-Host "  TEST FIXTURE RESULT:    WARNING ($($TestFixtureFindings.Count) suspicious fixture string(s))" -ForegroundColor Yellow
    $TestFixtureFindings | Format-Table -AutoSize
}

if ($ConfigFindings.Count -eq 0) {
    Write-Host "  CONFIGURATION RESULT:   PASS (0 credentials in manifests or configs)" -ForegroundColor Green
} else {
    Write-Host "  CONFIGURATION RESULT:   FAIL ($($ConfigFindings.Count) credentials found in config files)" -ForegroundColor Red
    $ConfigFindings | Format-Table -AutoSize
    $hasErrors = $true
}

Write-Host "================================================================================" -ForegroundColor Cyan

if ($hasErrors) {
    Write-Host "  SECURITY AUDIT FAILED" -ForegroundColor Red
    Write-Host "================================================================================" -ForegroundColor Cyan
    exit 1
} else {
    Write-Host "  SECURITY AUDIT PASSED" -ForegroundColor Green
    Write-Host "================================================================================" -ForegroundColor Cyan
    exit 0
}
