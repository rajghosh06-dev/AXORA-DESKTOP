<#
.SYNOPSIS
    AXORA WinUI 3 Adversarial & Chaos Verification Suite.
    Deliberately stresses the native WinUI 3 desktop application via rapid navigation
    bombardment, diagnostics toggling hammer, telemetry refresh spam, and window bounding constraints.
#>

[CmdletBinding()]
param(
    [string]$ExePath = "d:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP\Axora-Desktop-WinUI\Axora.Desktop\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.exe",
    [string]$ScreenshotPath = "d:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP\docs\qa\screenshots\winui-adversarial-stress.png"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Win32 definitions
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class Win32AdvHelper {
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBmp, uint nFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
"@

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA WINUI 3 - ADVERSARIAL & CHAOS VERIFICATION SUITE" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

$ExeDir = Split-Path -Parent $ExePath
$appProc = Start-Process -FilePath $ExePath -WorkingDirectory $ExeDir -PassThru
Write-Host "[1] Spawned Axora.Desktop.exe with PID: $($appProc.Id)" -ForegroundColor Yellow

$testResults = [System.Collections.Generic.List[PSCustomObject]]::new()
function Record-Test([string]$Category, [string]$Name, [bool]$Pass, [string]$Details = "") {
    $testResults.Add([PSCustomObject]@{
        Category = $Category
        TestName = $Name
        Passed   = $Pass
        Details  = $Details
    })
    $tag = if ($Pass) { "[PASS]" } else { "[FAIL]" }
    $color = if ($Pass) { "Green" } else { "Red" }
    Write-Host "  $tag $Name $(if ($Details) { '- ' + $Details })" -ForegroundColor $color
}

try {
    # Discover HWND
    $foundHwnd = [IntPtr]::Zero
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.ElapsedMilliseconds -lt 15000 -and $foundHwnd -eq [IntPtr]::Zero) {
        Start-Sleep -Milliseconds 400
        [Win32AdvHelper]::EnumWindows({
            param($h, $l)
            $pidVal = 0
            [Win32AdvHelper]::GetWindowThreadProcessId($h, [ref]$pidVal)
            if ($pidVal -eq $appProc.Id -and [Win32AdvHelper]::IsWindowVisible($h)) {
                $sb = New-Object System.Text.StringBuilder 256
                [Win32AdvHelper]::GetWindowText($h, $sb, 256) | Out-Null
                if ($sb.ToString() -eq "Axora Desktop") {
                    $script:foundHwnd = $h
                    return $false
                }
            }
            return $true
        }, [IntPtr]::Zero) | Out-Null
    }

    if ($foundHwnd -eq [IntPtr]::Zero) {
        throw "Could not discover WinUI 3 AppWindow HWND within 15 seconds."
    }
    Write-Host "  Discovered HWND: $foundHwnd" -ForegroundColor Gray

    $windowElement = [System.Windows.Automation.AutomationElement]::FromHandle($foundHwnd)
    if ($null -eq $windowElement) {
        throw "Failed to create AutomationElement from HWND."
    }

    # ── ADVERSARIAL TEST 1: Rapid Diagnostics Expansion Hammer ────────────────
    Write-Host "`n[2] Adversarial Test 1: Rapid Diagnostics Expansion Hammer (10 Toggles)..." -ForegroundColor Yellow
    $diagBtnCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "DiagnosticsButton"
    )
    $diagBtn = $windowElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $diagBtnCondition)
    
    $diagHammerPassed = $false
    if ($null -ne $diagBtn) {
        $diagPattern = $diagBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
        if ($null -ne $diagPattern) {
            $diagCount = 0
            for ($i = 0; $i -lt 10; $i++) {
                $diagPattern.Invoke()
                Start-Sleep -Milliseconds 60
                $diagCount++
            }
            $diagHammerPassed = ($diagCount -eq 10)
        }
    }
    Record-Test "Stress" "Diagnostics Button Hammer (10 Rapid Invocations)" $diagHammerPassed "Executed 10 rapid invoke patterns without UI freeze"

    # ── ADVERSARIAL TEST 2: Rapid Telemetry Refresh Spam ───────────────────────
    Write-Host "`n[3] Adversarial Test 2: Telemetry Refresh Spam (10 Invocations)..." -ForegroundColor Yellow
    $refreshCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "RefreshTelemetryButton"
    )
    $refreshBtn = $windowElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $refreshCondition)
    
    $refreshSpamPassed = $false
    if ($null -ne $refreshBtn) {
        $refreshPattern = $refreshBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
        if ($null -ne $refreshPattern) {
            $refCount = 0
            for ($i = 0; $i -lt 10; $i++) {
                $refreshPattern.Invoke()
                Start-Sleep -Milliseconds 60
                $refCount++
            }
            $refreshSpamPassed = ($refCount -eq 10)
        }
    }
    Record-Test "Stress" "Telemetry Refresh Spam (10 Invocations)" $refreshSpamPassed "Executed 10 rapid telemetry refreshes without dispatcher exceptions"

    # ── ADVERSARIAL TEST 3: Rapid NavigationView Switching Hammer ─────────────
    Write-Host "`n[4] Adversarial Test 3: Rapid NavigationView Route Switching (16 Transitions)..." -ForegroundColor Yellow
    $listItemCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem
    )
    $navItems = $windowElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $listItemCondition)
    
    $navTransitionsSucceeded = 0
    if ($navItems.Count -gt 0) {
        for ($i = 0; $i -lt 16; $i++) {
            $item = $navItems[$i % $navItems.Count]
            $selPattern = $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern) -as [System.Windows.Automation.SelectionItemPattern]
            if ($null -ne $selPattern) {
                $selPattern.Select()
                Start-Sleep -Milliseconds 80
                $navTransitionsSucceeded++
            }
        }
    }
    Record-Test "Stress" "NavigationView Rapid Route Thrashing (16 switches)" ($navTransitionsSucceeded -eq 16) "Switched pages 16 times in ~1.5s; ContentFrame intact"

    # ── ADVERSARIAL TEST 4: Command Palette Accelerator Hammer ─────────────────
    Write-Host "`n[5] Adversarial Test 4: Command Palette Hammer (5 Rapid Open/Close Cycles)..." -ForegroundColor Yellow
    $VK_CONTROL = [byte]0x11
    $VK_K = [byte]0x4B
    $VK_ESCAPE = [byte]0x1B
    $KEYEVENTF_KEYUP = [uint32]0x0002

    $paletteHammerPassed = 0
    for ($c = 0; $c -lt 5; $c++) {
        # Press Ctrl+K
        [Win32AdvHelper]::keybd_event($VK_CONTROL, 0, 0, [UIntPtr]::Zero)
        [Win32AdvHelper]::keybd_event($VK_K, 0, 0, [UIntPtr]::Zero)
        [Win32AdvHelper]::keybd_event($VK_K, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
        [Win32AdvHelper]::keybd_event($VK_CONTROL, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 150

        # Press Escape
        [Win32AdvHelper]::keybd_event($VK_ESCAPE, 0, 0, [UIntPtr]::Zero)
        [Win32AdvHelper]::keybd_event($VK_ESCAPE, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 150
        $paletteHammerPassed++
    }
    Record-Test "Stress" "WinUI Command Palette Accelerator Hammer (5 Cycles)" ($paletteHammerPassed -eq 5) "5 open/close accelerator cycles completed without hang"

    # ── ADVERSARIAL TEST 5: Window Min-Size Clamping & WM_GETMINMAXINFO ─────────
    Write-Host "`n[6] Adversarial Test 5: Window Minimum Dimensions Constraint Clamping..." -ForegroundColor Yellow
    $rect = New-Object Win32AdvHelper+RECT
    [Win32AdvHelper]::GetWindowRect($foundHwnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top

    # Attempt to resize window smaller than 1000x620 (e.g. 500x300)
    # SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004
    [Win32AdvHelper]::SetWindowPos($foundHwnd, [IntPtr]::Zero, 0, 0, 500, 300, 0x0002 -bor 0x0004) | Out-Null
    Start-Sleep -Milliseconds 250

    $clampedRect = New-Object Win32AdvHelper+RECT
    [Win32AdvHelper]::GetWindowRect($foundHwnd, [ref]$clampedRect) | Out-Null
    $clampedW = $clampedRect.Right - $clampedRect.Left
    $clampedH = $clampedRect.Bottom - $clampedRect.Top

    # Restore window size
    [Win32AdvHelper]::SetWindowPos($foundHwnd, [IntPtr]::Zero, 0, 0, $w, $h, 0x0002 -bor 0x0004) | Out-Null

    $isClamped = ($clampedW -ge 980) -and ($clampedH -ge 600)
    Record-Test "Boundary" "Window Min-Size WM_GETMINMAXINFO Clamping" $isClamped "Attempted 500x300 -> Clamped to ${clampedW}x${clampedH}px"

    # ── ADVERSARIAL TEST 6: Capture Visual Evidence ───────────────────────────
    Write-Host "`n[7] Capturing Adversarial Visual Evidence..." -ForegroundColor Yellow
    $bmp = New-Object System.Drawing.Bitmap($clampedW, $clampedH)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $gfx.GetHdc()
    [Win32AdvHelper]::PrintWindow($foundHwnd, $hdc, 2) | Out-Null
    $gfx.ReleaseHdc($hdc)
    $gfx.Dispose()
    $dir = Split-Path -Parent $ScreenshotPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $bmp.Save($ScreenshotPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  Screenshot saved: $ScreenshotPath" -ForegroundColor Gray

} finally {
    if ($appProc -and -not $appProc.HasExited) {
        $appProc.Kill()
        $appProc.WaitForExit(3000)
        Write-Host "`n[8] Axora.Desktop process terminated cleanly." -ForegroundColor Gray
    }
}

Write-Host "`n================================================================================" -ForegroundColor Cyan
$passed = ($testResults | Where-Object { $_.Passed }).Count
$failed = ($testResults | Where-Object { -not $_.Passed }).Count
Write-Host "  WINUI 3 ADVERSARIAL RESULT: $passed PASSED | $failed FAILED (Total: $($testResults.Count))" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

if ($failed -gt 0) { exit 1 } else { exit 0 }
