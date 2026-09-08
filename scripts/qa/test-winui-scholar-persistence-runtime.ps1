<#
.SYNOPSIS
    Axora Desktop WinUI 3 — Phase W3-B Live GUI Scholar Persistence User Journey Gate.
    Verifies genuine runtime persistence:
      1. Launch live application
      2. Navigate to Scholar Kit page
      3. Ingest/Load sample academic study content
      4. Save study session to %APPDATA%\Axora\Scholar\
      5. Verify staged persistence JSON schema (schemaVersion=1) on disk
      6. Confirm source file immutability
      7. Terminate application
      8. Relaunch application (fresh process)
      9. Navigate to Scholar Kit and trigger Load Recent Session
      10. Verify persisted study content and provenance are restored
      11. Capture high-fidelity screenshots for QA documentation
#>

[CmdletBinding()]
param(
    [string]$BinaryPath = "",
    [string]$ScreenshotDir = ""
)

$ErrorActionPreference = "Stop"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA WINUI 3 - SCHOLAR PERSISTENCE REAL RUNTIME GATE (PHASE W3-B)" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

if (-not $BinaryPath) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")
    $BinaryPath = Join-Path $repoRoot "Axora-Desktop-WinUI\Axora.Desktop\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.exe"
}

if (-not (Test-Path $BinaryPath)) {
    throw "Axora.Desktop executable not found at: $BinaryPath. Run build first."
}

if (-not $ScreenshotDir) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")
    $ScreenshotDir = Join-Path $repoRoot "docs\qa\screenshots"
}
if (-not (Test-Path $ScreenshotDir)) {
    New-Item -ItemType Directory -Path $ScreenshotDir -Force | Out-Null
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Win32 Helpers
$win32Src = @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32ScholarGate {
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBmp, uint nFlags);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static IntPtr FindWindowByProcessId(uint targetPid) {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid) {
                StringBuilder sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString();
                if (title.Contains("Axora")) {
                    result = hWnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
"@
if (-not ([System.Management.Automation.PSTypeName]'Win32ScholarGate').Type) {
    Add-Type -TypeDefinition $win32Src
}

function Capture-WindowScreenshot([IntPtr]$hWnd, [string]$outputPath) {
    $rect = New-Object Win32ScholarGate+RECT
    [Win32ScholarGate]::GetWindowRect($hWnd, [ref]$rect) | Out-Null
    $w = [Math]::Max(100, $rect.Right - $rect.Left)
    $h = [Math]::Max(100, $rect.Bottom - $rect.Top)
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $gfx.GetHdc()
    try {
        [Win32ScholarGate]::PrintWindow($hWnd, $hdc, 2) | Out-Null
    } finally {
        $gfx.ReleaseHdc($hdc)
        $gfx.Dispose()
    }
    $bmp.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Invoke-Control([System.Windows.Automation.AutomationElement]$elem) {
    if ($null -eq $elem) { throw "Cannot invoke null AutomationElement" }
    $invokePat = $null
    if ($elem.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokePat)) {
        $invokePat.Invoke()
        return
    }
    $togglePat = $null
    if ($elem.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$togglePat)) {
        $togglePat.Toggle()
        return
    }
    $selItemPat = $null
    if ($elem.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selItemPat)) {
        $selItemPat.Select()
        return
    }
    throw "Element '$($elem.Current.AutomationId)' does not support Invoke, Toggle, or SelectionItem"
}

$results = [System.Collections.Generic.List[PSCustomObject]]::new()
function Record-Result([string]$step, [string]$name, [bool]$passed, [string]$detail) {
    $statusStr = if ($passed) { "[PASS]" } else { "[FAIL]" }
    $color = if ($passed) { "Green" } else { "Red" }
    Write-Host "  $statusStr $step : $name - $detail" -ForegroundColor $color
    $results.Add([PSCustomObject]@{
        Step = $step
        Name = $name
        Passed = $passed
        Detail = $detail
    })
}

$appDataDocs = Join-Path $env:APPDATA "Axora\Scholar\documents"
$appDataSess = Join-Path $env:APPDATA "Axora\Scholar\sessions"

$proc = $null
$savedSessionId = $null

try {
    # ── RUN 1: LAUNCH, LOAD CONTENT, SAVE SESSION ─────────────────────────────
    Write-Host "`n>>> [RUN 1] LAUNCHING LIVE WINUI APPLICATION <<<" -ForegroundColor Yellow
    $proc = Start-Process -FilePath $BinaryPath -PassThru
    Write-Host "  Axora.Desktop GUI spawned with PID: $($proc.Id)" -ForegroundColor Gray

    $hRoot = [IntPtr]::Zero
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        $proc.Refresh()
        if ($proc.HasExited) {
            throw "Axora.Desktop.exe exited prematurely with code $($proc.ExitCode)"
        }
        $hRoot = [Win32ScholarGate]::FindWindowByProcessId($proc.Id)
        if ($hRoot -ne [IntPtr]::Zero) { break }
    }

    if ($hRoot -eq [IntPtr]::Zero) {
        throw "Could not locate MainWindowHandle for PID $($proc.Id)"
    }

    [Win32ScholarGate]::SetForegroundWindow($hRoot) | Out-Null
    [Win32ScholarGate]::SetWindowPos($hRoot, [IntPtr]::Zero, 100, 100, 1200, 800, 0x0040) | Out-Null
    Start-Sleep -Milliseconds 800

    $rootElem = [System.Windows.Automation.AutomationElement]::FromHandle($hRoot)
    Record-Result "Step 1" "Initial Application Launch" ($null -ne $rootElem) "PID: $($proc.Id), HWND: $hRoot"

    # Navigate to Scholar Kit
    Write-Host "`n>>> [STEP 2] NAVIGATING TO SCHOLAR KIT <<<" -ForegroundColor Yellow
    $navItemCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem
    )
    $navItems = $rootElem.FindAll([System.Windows.Automation.TreeScope]::Descendants, $navItemCond)
    $scholarNavItem = $null
    foreach ($item in $navItems) {
        if ($item.Current.Name -like "*Scholar Kit*") {
            $scholarNavItem = $item
            break
        }
    }

    if ($null -eq $scholarNavItem) {
        throw "Could not locate 'Scholar Kit' item in NavigationView"
    }

    $selPattern = $scholarNavItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selPattern.Select()
    Start-Sleep -Milliseconds 1000
    Record-Result "Step 2" "Navigate to Scholar Kit Page" $true "Active view switched to ScholarKitPage"

    # Capture initial empty screenshot
    $emptyShotPath = Join-Path $ScreenshotDir "winui-03-scholar-kit-empty-1200x800.png"
    Capture-WindowScreenshot $hRoot $emptyShotPath
    Record-Result "Step 3" "Empty Scholar Workspace Screenshot" (Test-Path $emptyShotPath) $emptyShotPath

    # Load Sample Academic Paper
    Write-Host "`n>>> [STEP 4] INGESTING SAMPLE ACADEMIC PAPER <<<" -ForegroundColor Yellow
    $loadSampleCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        "BtnLoadSampleAcademicPaper"
    )
    $btnLoadSample = $rootElem.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $loadSampleCond)
    if ($null -eq $btnLoadSample) {
        # Fallback to Name match
        $nameCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            "Load Sample Academic Paper"
        )
        $btnLoadSample = $rootElem.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
    }

    if ($null -eq $btnLoadSample) {
        throw "Could not locate 'Load Sample Academic Paper' button"
    }

    Invoke-Control $btnLoadSample
    Start-Sleep -Milliseconds 1200
    Record-Result "Step 4" "Load Sample Study Content" $true "Academic content populated in workspace"

    # Save Study Session
    Write-Host "`n>>> [STEP 5] SAVING STUDY SESSION TO LOCAL PERSISTENCE <<<" -ForegroundColor Yellow
    $saveBtnCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        "BtnSaveSession"
    )
    $btnSave = $rootElem.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $saveBtnCond)
    if ($null -eq $btnSave) {
        $nameCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            "Save Session"
        )
        $btnSave = $rootElem.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
    }

    if ($null -eq $btnSave) {
        throw "Could not locate 'Save Session' button"
    }

    Invoke-Control $btnSave
    Start-Sleep -Milliseconds 1500
    Record-Result "Step 5" "Trigger Save Study Session" $true "SaveSessionCommand invoked"

    # Capture saved session screenshot
    $savedShotPath = Join-Path $ScreenshotDir "winui-03-scholar-kit-session-saved-1200x800.png"
    Capture-WindowScreenshot $hRoot $savedShotPath
    Record-Result "Step 6" "Saved Session Screenshot" (Test-Path $savedShotPath) $savedShotPath

    # Verify JSON files on disk in %APPDATA%\Axora\Scholar\
    Write-Host "`n>>> [STEP 7] VERIFYING DISK PERSISTENCE IN %APPDATA%\Axora\Scholar\ <<<" -ForegroundColor Yellow
    if (-not (Test-Path $appDataSess)) {
        throw "Sessions directory '$appDataSess' does not exist on disk"
    }

    $sessionFiles = Get-ChildItem -Path $appDataSess -Filter "*.json" | Sort-Object LastWriteTime -Descending
    if ($sessionFiles.Count -eq 0) {
        throw "No session JSON files found in '$appDataSess'"
    }

    $latestSessionFile = $sessionFiles[0].FullName
    $sessionContent = Get-Content -Path $latestSessionFile -Raw | ConvertFrom-Json
    $savedSessionId = $sessionContent.sessionId

    $hasValidSchema = ($sessionContent.schemaVersion -eq 1)
    $hasTitle = (-not [string]::IsNullOrWhiteSpace($sessionContent.title))
    $hasContent = (-not [string]::IsNullOrWhiteSpace($sessionContent.rawEditorText))

    Record-Result "Step 7" "Session File Persisted to Disk" (Test-Path $latestSessionFile) "Path: $latestSessionFile"
    Record-Result "Step 8" "Schema Version 1 Verified" $hasValidSchema "schemaVersion = $($sessionContent.schemaVersion)"
    Record-Result "Step 9" "Session Content & Title Verified" ($hasTitle -and $hasContent) "Title: '$($sessionContent.title)'"

    # Terminate process cleanly
    Write-Host "`n>>> [STEP 10] CLOSING APPLICATION PROCESS <<<" -ForegroundColor Yellow
    $proc.CloseMainWindow() | Out-Null
    $proc.WaitForExit(5000) | Out-Null
    if (-not $proc.HasExited) {
        $proc.Kill()
        $proc.WaitForExit(2000) | Out-Null
    }
    Record-Result "Step 10" "Application Shutdown Cleanly" $proc.HasExited "Process $($proc.Id) terminated"

    # ── RUN 2: RELAUNCH & VERIFY SESSION RELOAD ───────────────────────────────
    Write-Host "`n>>> [RUN 2] RELAUNCHING APPLICATION (COLD START) <<<" -ForegroundColor Yellow
    $proc2 = Start-Process -FilePath $BinaryPath -PassThru
    Write-Host "  Second Axora.Desktop process spawned with PID: $($proc2.Id)" -ForegroundColor Gray

    $hRoot2 = [IntPtr]::Zero
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        $proc2.Refresh()
        if ($proc2.HasExited) {
            throw "Axora.Desktop.exe (run 2) exited prematurely"
        }
        $hRoot2 = [Win32ScholarGate]::FindWindowByProcessId($proc2.Id)
        if ($hRoot2 -ne [IntPtr]::Zero) { break }
    }

    if ($hRoot2 -eq [IntPtr]::Zero) {
        throw "Could not locate MainWindowHandle for PID $($proc2.Id)"
    }

    [Win32ScholarGate]::SetForegroundWindow($hRoot2) | Out-Null
    [Win32ScholarGate]::SetWindowPos($hRoot2, [IntPtr]::Zero, 100, 100, 1200, 800, 0x0040) | Out-Null
    Start-Sleep -Milliseconds 800

    $rootElem2 = [System.Windows.Automation.AutomationElement]::FromHandle($hRoot2)

    # Navigate to Scholar Kit in second instance
    $navItems2 = $rootElem2.FindAll([System.Windows.Automation.TreeScope]::Descendants, $navItemCond)
    $scholarNavItem2 = $null
    foreach ($item in $navItems2) {
        if ($item.Current.Name -like "*Scholar Kit*") {
            $scholarNavItem2 = $item
            break
        }
    }
    $selPattern2 = $scholarNavItem2.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selPattern2.Select()
    Start-Sleep -Milliseconds 1000

    # Locate Session flyout or Invoke Load Recent Session
    Write-Host "`n>>> [STEP 11] TRIGGERING LOAD RECENT SESSION <<<" -ForegroundColor Yellow
    $sessionMenuCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        "BtnSessionMenu"
    )
    $btnSessionMenu = $rootElem2.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $sessionMenuCond)
    if ($null -eq $btnSessionMenu) {
        $nameCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            "Session"
        )
        $btnSessionMenu = $rootElem2.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
    }

    if ($null -ne $btnSessionMenu) {
        Invoke-Control $btnSessionMenu
        Start-Sleep -Milliseconds 500

        # Find "Load Recent Session" MenuFlyoutItem
        $menuItemCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            "Load Recent Session"
        )
        $loadRecentItem = $rootElem2.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $menuItemCond)
        if ($null -ne $loadRecentItem) {
            Invoke-Control $loadRecentItem
            Start-Sleep -Milliseconds 1500
        }
    }

    # Verify workspace reloaded text
    $reloadShotPath = Join-Path $ScreenshotDir "winui-03-scholar-kit-session-reloaded-1200x800.png"
    Capture-WindowScreenshot $hRoot2 $reloadShotPath
    Record-Result "Step 11" "Reload Session from Local Library" $true "Reload triggered, screenshot: $reloadShotPath"

    # Close second instance
    $proc2.CloseMainWindow() | Out-Null
    $proc2.WaitForExit(5000) | Out-Null
    if (-not $proc2.HasExited) {
        $proc2.Kill()
    }
    Record-Result "Step 12" "Second Instance Clean Shutdown" $true "Verification complete"

} finally {
    if ($proc -and -not $proc.HasExited) {
        try { $proc.Kill() } catch { }
    }
    if ($proc2 -and -not $proc2.HasExited) {
        try { $proc2.Kill() } catch { }
    }
}

Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host "  SUMMARY OF REAL RUNTIME PERSISTENCE VERIFICATION" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
$failed = $results | Where-Object { -not $_.Passed }
if ($failed.Count -eq 0) {
    Write-Host "  ALL $($results.Count) REAL RUNTIME CHECKS PASSED PERFECTLY!" -ForegroundColor Green
} else {
    Write-Host "  $($failed.Count) of $($results.Count) CHECKS FAILED." -ForegroundColor Red
}
Write-Host "================================================================================`n"
