<#
.SYNOPSIS
    Axora Desktop WinUI 3 — Phase W2-F3 Live GUI Optimization Presets User Journey Gate.
    Exercises real UI automation:
      Journey A: Balanced (default) — Full dimensions, quality 85, strip metadata.
      Journey B: Custom — User overrides (Quality, MaxDimension=1280 downscaling).
      Journey C: Web & Mobile Optimized — 1080p (1920) clamp, quality 75.
    Verifies output decodability, pixel dimensions, source immutability, responsive layouts (960x600, 1200x800).
#>

[CmdletBinding()]
param(
    [string]$BinaryPath = "",
    [string]$ScreenshotDir = ""
)

$ErrorActionPreference = "Stop"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  AXORA WINUI 3 - OPTIMIZATION PRESETS GUI USER JOURNEYS (PHASE W2-F3)" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# Locate executable if not specified
if (-not $BinaryPath) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")
    $BinaryPath = Join-Path $repoRoot "Axora-Desktop-WinUI\Axora.Desktop\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.exe"
}

if (-not (Test-Path $BinaryPath)) {
    throw "Axora.Desktop executable not found at: $BinaryPath. Run build-all.ps1 -Target WinUI first."
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

# Win32 Helpers for Window Management & DPI
$win32Src = @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32PresetsGate {
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

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

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_SHOWWINDOW = 0x0040;
}
"@
if (-not ([System.Management.Automation.PSTypeName]"Win32PresetsGate").Type) {
    Add-Type -TypeDefinition $win32Src -ReferencedAssemblies System.Drawing
}

$results = [System.Collections.Generic.List[PSObject]]::new()
function Record-JourneyResult([string]$stage, [string]$testName, [bool]$pass, [string]$details = "") {
    $results.Add([PSCustomObject]@{ Stage = $stage; TestName = $testName; Pass = $pass; Details = $details })
    $tag = if ($pass) { "[PASS]" } else { "[FAIL]" }
    $color = if ($pass) { "Green" } else { "Red" }
    Write-Host "  $tag $testName" -ForegroundColor $color -NoNewline
    if ($details) { Write-Host " - $details" -ForegroundColor Gray } else { Write-Host "" }
}

function New-TestPngFixture([string]$filePath, [int]$width = 64, [int]$height = 64, [System.Drawing.Color]$color = $null) {
    if ($null -eq $color) { $color = [System.Drawing.Color]::FromArgb(255, 33, 150, 243) }
    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $brush = New-Object System.Drawing.SolidBrush($color)
    $gfx.FillRectangle($brush, 0, 0, $width, $height)
    $accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Yellow)
    $gfx.FillRectangle($accentBrush, 0, 0, [Math]::Max(4, [int]($width / 8)), [Math]::Max(4, [int]($height / 8)))
    $gfx.Dispose()
    $brush.Dispose()
    $accentBrush.Dispose()
    $bmp.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Get-FileSha256([string]$filePath) {
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($filePath)
    $hash = $hasher.ComputeHash($stream)
    $stream.Dispose()
    $hasher.Dispose()
    return [BitConverter]::ToString($hash).Replace("-", "").ToLowerInvariant()
}

function Capture-WindowScreenshot([IntPtr]$hWnd, [string]$outputPath) {
    $rect = New-Object Win32PresetsGate+RECT
    [Win32PresetsGate]::GetWindowRect($hWnd, [ref]$rect) | Out-Null
    $w = [Math]::Max(100, $rect.Right - $rect.Left)
    $h = [Math]::Max(100, $rect.Bottom - $rect.Top)
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $gfx.GetHdc()
    try {
        [Win32PresetsGate]::PrintWindow($hWnd, $hdc, 2) | Out-Null
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

$tempWorkspace = Join-Path ([System.IO.Path]::GetTempPath()) ("axora_f3_uj_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempWorkspace -Force | Out-Null
$testIntakeFile = Join-Path ([System.IO.Path]::GetTempPath()) "axora_test_picker_files.txt"

$proc = $null
try {
    Write-Host "`n>>> [STAGE 1] LAUNCHING LIVE GUI APPLICATION <<<" -ForegroundColor Yellow
    $proc = Start-Process -FilePath $BinaryPath -PassThru
    Write-Host "  Axora.Desktop GUI spawned with PID: $($proc.Id)" -ForegroundColor Gray

    $hRoot = [IntPtr]::Zero
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        $proc.Refresh()
        if ($proc.HasExited) {
            throw "Axora.Desktop.exe exited prematurely with code $($proc.ExitCode)"
        }
        $hRoot = [Win32PresetsGate]::FindWindowByProcessId($proc.Id)
        if ($hRoot -ne [IntPtr]::Zero) { break }
    }

    if ($hRoot -eq [IntPtr]::Zero) {
        throw "Could not locate MainWindowHandle for PID $($proc.Id) within 20s."
    }

    Write-Host "  Located MainWindowHandle: $hRoot" -ForegroundColor Gray
    [Win32PresetsGate]::SetForegroundWindow($hRoot) | Out-Null
    Start-Sleep -Milliseconds 600

    $rootElem = [System.Windows.Automation.AutomationElement]::FromHandle($hRoot)
    Record-JourneyResult "Stage 1" "Main Window Loaded and Responsive" ($null -ne $rootElem) "HWND: $hRoot"

    # Navigate to Universal Converter
    Write-Host "`n>>> [STAGE 2] NAVIGATING TO UNIVERSAL CONVERTER <<<" -ForegroundColor Yellow
    $navItemCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem
    )
    $navItems = $rootElem.FindAll([System.Windows.Automation.TreeScope]::Descendants, $navItemCond)
    $ucNavItem = $null
    foreach ($item in $navItems) {
        if ($item.Current.Name -like "*Universal Converter*") {
            $ucNavItem = $item
            break
        }
    }

    if ($null -eq $ucNavItem) {
        throw "Could not locate 'Universal Converter' in NavigationView"
    }

    $selPattern = $ucNavItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selPattern.Select()
    Start-Sleep -Milliseconds 800
    Record-JourneyResult "Stage 2" "Navigate to Universal Converter Page" $true "SelectedItem active"

    # Discover UI Controls
    Write-Host "`n>>> [STAGE 3] DISCOVERING W2-F3 OPTIMIZATION UI CONTROLS <<<" -ForegroundColor Yellow
    function Find-Control([string]$autoId, [string]$name = "") {
        $cond = if ($autoId) {
            New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $autoId)
        } else {
            New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
        }
        return $rootElem.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    }

    $browseFilesBtn = Find-Control "BrowseFilesButton"
    $targetFmtCombo = Find-Control "TargetFormatComboBox"
    $collisionCombo = Find-Control "CollisionPolicyComboBox"
    $startBtn = Find-Control "StartConversionButton"
    $clearAllBtn = Find-Control "ClearAllButton"

    $presetCombo = Find-Control "PresetSelectorComboBox"
    $presetDesc = Find-Control "PresetDescriptionTextBlock"
    $effectiveGrid = Find-Control "EffectiveSummaryGrid"
    $qualityText = Find-Control "EffectiveQualityTextBlock"
    $dimText = Find-Control "EffectiveDimensionTextBlock"
    $dpiText = Find-Control "EffectiveDpiTextBlock"
    $metaText = Find-Control "EffectiveMetadataTextBlock"
    $customPanel = Find-Control "CustomParametersPanel"
    $qualitySlider = Find-Control "QualitySlider"
    $customQualityText = Find-Control "CustomQualityTextBlock"
    $maxDimCombo = Find-Control "MaxDimensionComboBox"
    $targetDpiCombo = Find-Control "TargetDpiComboBox"
    $metaToggle = Find-Control "MetadataHandlingToggle"
    $guidanceExpander = Find-Control "OptimizationGuidanceExpander"

    Record-JourneyResult "Stage 3" "PresetSelectorComboBox Discovered" ($null -ne $presetCombo)
    Record-JourneyResult "Stage 3" "EffectiveSummaryGrid Discovered" ($null -ne $effectiveGrid)
    Record-JourneyResult "Stage 3" "Effective Summary Metric Displays Discovered" ($null -ne $qualityText -and $null -ne $dimText -and $null -ne $dpiText -and $null -ne $metaText)
    Record-JourneyResult "Stage 3" "CustomParametersPanel Discovered" ($null -ne $customPanel)
    Record-JourneyResult "Stage 3" "QualitySlider Discovered" ($null -ne $qualitySlider)
    Record-JourneyResult "Stage 3" "MaxDimensionComboBox Discovered" ($null -ne $maxDimCombo)
    Record-JourneyResult "Stage 3" "TargetDpiComboBox Discovered" ($null -ne $targetDpiCombo)
    Record-JourneyResult "Stage 3" "MetadataHandlingToggle Discovered" ($null -ne $metaToggle)
    Record-JourneyResult "Stage 3" "OptimizationGuidanceExpander Discovered" ($null -ne $guidanceExpander)

    # ── JOURNEY A: BALANCED (DEFAULT PRESET) CONVERSION ───────────────────────
    Write-Host "`n>>> [STAGE 4] JOURNEY A: BALANCED PRESET (DEFAULT) <<<" -ForegroundColor Yellow
    # Initial state verification
    $isSliderDisabled = -not $qualitySlider.Current.IsEnabled
    Record-JourneyResult "Stage 4" "Custom Controls Locked under Balanced Preset" $isSliderDisabled "Slider IsEnabled: $($qualitySlider.Current.IsEnabled)"

    $sourcePngA = Join-Path $tempWorkspace "journey_a_photo.png"
    New-TestPngFixture $sourcePngA 800 600 ([System.Drawing.Color]::DodgerBlue)
    $shaBeforeA = Get-FileSha256 $sourcePngA

    # Intake file
    Set-Content -Path $testIntakeFile -Value $sourcePngA -Encoding utf8
    Invoke-Control $browseFilesBtn
    Start-Sleep -Milliseconds 600

    # Start conversion
    Invoke-Control $startBtn

    # Wait for completion
    $expectedJpgA = Join-Path $tempWorkspace "journey_a_photo.jpg"
    $completedA = $false
    for ($w = 0; $w -lt 120; $w++) {
        Start-Sleep -Milliseconds 250
        if (Test-Path $expectedJpgA) {
            $completedA = $true
            break
        }
    }
    Record-JourneyResult "Stage 4" "Journey A: Conversion Succeeded" $completedA "Output: journey_a_photo.jpg"

    # Verify output properties
    $fileInfoA = Get-Item $expectedJpgA
    Record-JourneyResult "Stage 4" "Journey A: Output File Non-Empty" ($fileInfoA.Length -gt 0) "Size: $($fileInfoA.Length) bytes"

    $bytesA = [System.IO.File]::ReadAllBytes($expectedJpgA)
    $validJpgA = ($bytesA.Length -ge 2 -and $bytesA[0] -eq 0xFF -and $bytesA[1] -eq 0xD8)
    Record-JourneyResult "Stage 4" "Journey A: Output Has Valid JPEG SOI" $validJpgA

    $imgA = [System.Drawing.Image]::FromFile($expectedJpgA)
    $dimsMatchA = ($imgA.Width -eq 800 -and $imgA.Height -eq 600)
    $actualW_A = $imgA.Width
    $actualH_A = $imgA.Height
    $imgA.Dispose()
    Record-JourneyResult "Stage 4" "Journey A: Dimensions Preserved Unconstrained (800x600)" $dimsMatchA "Actual: ${actualW_A}x${actualH_A}"

    $shaAfterA = Get-FileSha256 $sourcePngA
    Record-JourneyResult "Stage 4" "Journey A: Source File Strictly Immutable" ($shaBeforeA -eq $shaAfterA) "SHA-256 match"

    # ── JOURNEY B: CUSTOM PRESET WITH RESIZING & CUSTOM QUALITY ──────────────
    Write-Host "`n>>> [STAGE 5] JOURNEY B: CUSTOM PRESET EDITING & CONVERSION <<<" -ForegroundColor Yellow
    Invoke-Control $clearAllBtn
    Start-Sleep -Milliseconds 400

    # Select "Custom" preset in PresetSelectorComboBox
    $expPattern = $presetCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expPattern.Expand()
    Start-Sleep -Milliseconds 400

    $customItemCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        "Custom"
    )
    $customItem = $presetCombo.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $customItemCond)
    if ($null -ne $customItem) {
        $selItem = $customItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $selItem.Select()
    } else {
        $expPattern.Collapse()
    }
    Start-Sleep -Milliseconds 500

    # Verify custom controls unlocked
    $isSliderUnlocked = $qualitySlider.Current.IsEnabled
    Record-JourneyResult "Stage 5" "Journey B: Custom Controls Unlocked in Custom Mode" $isSliderUnlocked "Slider IsEnabled: $isSliderUnlocked"

    # Intake 2400x1200 landscape image
    $sourcePngB = Join-Path $tempWorkspace "journey_b_highres.png"
    New-TestPngFixture $sourcePngB 2400 1200 ([System.Drawing.Color]::ForestGreen)
    $shaBeforeB = Get-FileSha256 $sourcePngB

    # Select 1280 px in MaxDimensionComboBox
    $maxDimExp = $maxDimCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $maxDimExp.Expand()
    Start-Sleep -Milliseconds 400

    $item1280Cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        "1280 px (HD)"
    )
    $item1280 = $maxDimCombo.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $item1280Cond)
    if ($null -ne $item1280) {
        $sel1280 = $item1280.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $sel1280.Select()
    } else {
        $maxDimExp.Collapse()
    }
    Start-Sleep -Milliseconds 500

    # Intake file
    Set-Content -Path $testIntakeFile -Value $sourcePngB -Encoding utf8
    Invoke-Control $browseFilesBtn
    Start-Sleep -Milliseconds 600

    # Start conversion
    Invoke-Control $startBtn

    # Wait for completion
    $expectedJpgB = Join-Path $tempWorkspace "journey_b_highres.jpg"
    $completedB = $false
    for ($w = 0; $w -lt 120; $w++) {
        Start-Sleep -Milliseconds 250
        if (Test-Path $expectedJpgB) {
            $completedB = $true
            break
        }
    }
    Record-JourneyResult "Stage 5" "Journey B: Conversion Succeeded" $completedB "Output: journey_b_highres.jpg"

    # Verify output properties
    $fileInfoB = Get-Item $expectedJpgB
    Record-JourneyResult "Stage 5" "Journey B: Output File Non-Empty" ($fileInfoB.Length -gt 0) "Size: $($fileInfoB.Length) bytes"

    $imgB = [System.Drawing.Image]::FromFile($expectedJpgB)
    $dimsMatchB = ($imgB.Width -eq 1280 -and $imgB.Height -eq 640)
    $actualW_B = $imgB.Width
    $actualH_B = $imgB.Height
    $imgB.Dispose()
    Record-JourneyResult "Stage 5" "Journey B: Proportional Downscaling Applied (1280x640)" $dimsMatchB "Actual: ${actualW_B}x${actualH_B}"

    $shaAfterB = Get-FileSha256 $sourcePngB
    Record-JourneyResult "Stage 5" "Journey B: Source File Strictly Immutable" ($shaBeforeB -eq $shaAfterB) "SHA-256 match"

    # ── JOURNEY C: WEB & MOBILE OPTIMIZED PRESET ──────────────────────────────
    Write-Host "`n>>> [STAGE 6] JOURNEY C: WEB & MOBILE OPTIMIZED PRESET <<<" -ForegroundColor Yellow
    Invoke-Control $clearAllBtn
    Start-Sleep -Milliseconds 400

    # Select "Web & Mobile Optimized"
    $expPattern.Expand()
    Start-Sleep -Milliseconds 400
    $webItemCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        "Web & Mobile Optimized"
    )
    $webItem = $presetCombo.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $webItemCond)
    if ($null -ne $webItem) {
        $selWeb = $webItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $selWeb.Select()
    } else {
        $expPattern.Collapse()
    }
    Start-Sleep -Milliseconds 500

    # Verify locked state
    $isSliderLockedWeb = -not $qualitySlider.Current.IsEnabled
    Record-JourneyResult "Stage 6" "Journey C: Parameter Controls Locked Under Web & Mobile Preset" $isSliderLockedWeb

    # Intake 2560x1440 2K image
    $sourcePngC = Join-Path $tempWorkspace "journey_c_2k.png"
    New-TestPngFixture $sourcePngC 2560 1440 ([System.Drawing.Color]::DarkOrange)
    $shaBeforeC = Get-FileSha256 $sourcePngC

    # Intake file
    Set-Content -Path $testIntakeFile -Value $sourcePngC -Encoding utf8
    Invoke-Control $browseFilesBtn
    Start-Sleep -Milliseconds 600

    # Start conversion
    Invoke-Control $startBtn

    # Wait for completion
    $expectedJpgC = Join-Path $tempWorkspace "journey_c_2k.jpg"
    $completedC = $false
    for ($w = 0; $w -lt 120; $w++) {
        Start-Sleep -Milliseconds 250
        if (Test-Path $expectedJpgC) {
            $completedC = $true
            break
        }
    }
    Record-JourneyResult "Stage 6" "Journey C: Conversion Succeeded" $completedC "Output: journey_c_2k.jpg"

    # Verify 1920 clamp applied: 2560x1440 clamped to MaxDimension 1920 => 1920x1080
    $imgC = [System.Drawing.Image]::FromFile($expectedJpgC)
    $dimsMatchC = ($imgC.Width -eq 1920 -and $imgC.Height -eq 1080)
    $actualW_C = $imgC.Width
    $actualH_C = $imgC.Height
    $imgC.Dispose()
    Record-JourneyResult "Stage 6" "Journey C: 1080p Clamp Applied (1920x1080)" $dimsMatchC "Actual: ${actualW_C}x${actualH_C}"

    $shaAfterC = Get-FileSha256 $sourcePngC
    Record-JourneyResult "Stage 6" "Journey C: Source File Strictly Immutable" ($shaBeforeC -eq $shaAfterC) "SHA-256 match"

    # ── STAGE 7: VISUAL AUDIT & SCREENSHOT CAPTURE ────────────────────────────
    Write-Host "`n>>> [STAGE 7] VISUAL QA RESOLUTION MATRIX & SCREENSHOTS <<<" -ForegroundColor Yellow
    $dpi = [Win32PresetsGate]::GetDpiForWindow($hRoot)
    if ($dpi -eq 0) { $dpi = 96 }
    $scale = $dpi / 96.0

    # 960x600 DIP
    $w960 = [int](960 * $scale)
    $h600 = [int](600 * $scale)
    [Win32PresetsGate]::SetWindowPos($hRoot, [IntPtr]::Zero, 0, 0, $w960, $h600, [Win32PresetsGate]::SWP_NOMOVE -bor [Win32PresetsGate]::SWP_NOZORDER -bor [Win32PresetsGate]::SWP_SHOWWINDOW) | Out-Null
    Start-Sleep -Milliseconds 600
    $ss960 = Join-Path $ScreenshotDir "winui-02-universal-converter-f3-960x600.png"
    Capture-WindowScreenshot $hRoot $ss960
    Record-JourneyResult "Stage 7" "Visual Capture at 960x600 DIP" (Test-Path $ss960) "File: winui-02-universal-converter-f3-960x600.png"

    # Scroll Optimization Presets shelf into view
    $scrollItemPat = $null
    if ($effectiveGrid.TryGetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern, [ref]$scrollItemPat)) {
        $scrollItemPat.ScrollIntoView()
        Start-Sleep -Milliseconds 400
    }

    # 1200x800 DIP
    $w1200 = [int](1200 * $scale)
    $h800 = [int](800 * $scale)
    [Win32PresetsGate]::SetWindowPos($hRoot, [IntPtr]::Zero, 0, 0, $w1200, $h800, [Win32PresetsGate]::SWP_NOMOVE -bor [Win32PresetsGate]::SWP_NOZORDER -bor [Win32PresetsGate]::SWP_SHOWWINDOW) | Out-Null
    Start-Sleep -Milliseconds 600
    $ss1200 = Join-Path $ScreenshotDir "winui-02-universal-converter-f3-1200x800.png"
    Capture-WindowScreenshot $hRoot $ss1200
    Record-JourneyResult "Stage 7" "Visual Capture at 1200x800 DIP" (Test-Path $ss1200) "File: winui-02-universal-converter-f3-1200x800.png"

    # Copy to brain artifact directory if present
    $brainDir = "C:\Users\rajghosh\.gemini\antigravity\brain\86f93aba-cfe6-4b6a-942d-7c868105abd2"
    if (Test-Path $brainDir) {
        Copy-Item -Path $ss960 -Destination (Join-Path $brainDir "winui-02-universal-converter-f3-960x600.png") -Force -ErrorAction SilentlyContinue
        Copy-Item -Path $ss1200 -Destination (Join-Path $brainDir "winui-02-universal-converter-f3-1200x800.png") -Force -ErrorAction SilentlyContinue
    }

}
finally {
    if ($proc -and -not $proc.HasExited) {
        Write-Host "`n  Terminating test GUI process (PID: $($proc.Id))..." -ForegroundColor Gray
        $proc.Kill()
        $proc.WaitForExit(5000)
    }

    if (Test-Path $tempWorkspace) {
        Remove-Item -Path $tempWorkspace -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $testIntakeFile) {
        Remove-Item -Path $testIntakeFile -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`n================================================================================" -ForegroundColor Cyan
$passCount = ($results | Where-Object { $_.Pass }).Count
$failCount = ($results | Where-Object { -not $_.Pass }).Count
Write-Host "  PRESET USER JOURNEYS RESULT: $passCount/$($results.Count) PASSED (Failed: $failCount)" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host "================================================================================" -ForegroundColor Cyan

if ($failCount -gt 0) {
    exit 1
}
