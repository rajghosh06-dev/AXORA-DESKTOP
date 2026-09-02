# Axora Desktop — Build & Launch Utility
# PowerShell Core 7+ | Windows 11 optimized
# Usage:
#   .\build-utility.ps1           → Launches dev server (default)
#   .\build-utility.ps1 -Mode dev → Launch dev server with hot reload
#   .\build-utility.ps1 -Mode build → Compile production Windows installer

param(
    [ValidateSet("dev", "build", "check")]
    [string]$Mode = "dev"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

# ─── Color helpers ────────────────────────────────────────────────────────────
function Write-Header([string]$Text) {
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║  $($Text.PadRight(44))║" -ForegroundColor Cyan
    Write-Host "  ╚══════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}
function Write-Step([string]$Text) { Write-Host "  ▶ $Text" -ForegroundColor Blue }
function Write-Success([string]$Text) { Write-Host "  ✓ $Text" -ForegroundColor Green }
function Write-Warn([string]$Text) { Write-Host "  ⚠ $Text" -ForegroundColor Yellow }
function Write-Fail([string]$Text) { Write-Host "  ✗ $Text" -ForegroundColor Red }

# ─── Dependency Validation ────────────────────────────────────────────────────
function Test-Dependencies {
    Write-Step "Validating dependencies..."

    $missing = @()

    if (-not (Get-Command node -ErrorAction SilentlyContinue)) { $missing += "Node.js (https://nodejs.org)" }
    if (-not (Get-Command npm  -ErrorAction SilentlyContinue)) { $missing += "npm (bundled with Node.js)" }
    if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) { $missing += "Rust toolchain (https://rustup.rs)" }
    if (-not (Get-Command tauri -ErrorAction SilentlyContinue)) {
        # tauri CLI might be in node_modules/.bin
        $tauriBin = Join-Path $ProjectRoot "node_modules\.bin\tauri.cmd"
        if (-not (Test-Path $tauriBin)) {
            $missing += "@tauri-apps/cli (run: npm install)"
        }
    }

    if ($missing.Count -gt 0) {
        Write-Fail "Missing dependencies:"
        $missing | ForEach-Object { Write-Host "    • $_" -ForegroundColor Red }
        exit 1
    }

    # Check node_modules
    if (-not (Test-Path (Join-Path $ProjectRoot "node_modules"))) {
        Write-Warn "node_modules not found — running npm install..."
        Set-Location $ProjectRoot
        npm install
        if ($LASTEXITCODE -ne 0) { Write-Fail "npm install failed"; exit 1 }
    }

    Write-Success "All dependencies verified"
}

# ─── Retrieve Node / Rust versions ────────────────────────────────────────────
function Write-Versions {
    $nodeVer  = (node --version 2>&1)
    $npmVer   = (npm --version 2>&1)
    $rustVer  = (rustc --version 2>&1) -replace "rustc ", ""
    $cargoVer = (cargo --version 2>&1) -replace "cargo ", ""

    Write-Host "  Node  $nodeVer  |  npm $npmVer  |  Rust $rustVer" -ForegroundColor DarkGray
}

# ─── MODE: dev ────────────────────────────────────────────────────────────────
function Start-DevServer {
    Write-Header "Axora Desktop — Dev Server"
    Write-Versions
    Test-Dependencies

    Write-Step "Starting Tauri dev server (Vite + Cargo)..."
    Write-Host "  Tip: App window appears after Rust compiles. First run may take 60–90s." -ForegroundColor DarkGray
    Write-Host ""

    Set-Location $ProjectRoot
    npm run tauri dev
}

# ─── MODE: build ──────────────────────────────────────────────────────────────
function Start-ProductionBuild {
    Write-Header "Axora Desktop — Production Build"
    Write-Versions
    Test-Dependencies

    Write-Step "Running TypeScript type check..."
    Set-Location $ProjectRoot
    npx tsc --noEmit
    if ($LASTEXITCODE -ne 0) { Write-Fail "TypeScript errors — fix before building"; exit 1 }
    Write-Success "TypeScript: no errors"

    Write-Step "Compiling production bundle (Vite + Cargo release)..."
    Write-Host "  This may take 3–5 minutes on first build." -ForegroundColor DarkGray
    Write-Host ""

    npm run tauri build

    if ($LASTEXITCODE -eq 0) {
        $installerDir = Join-Path $ProjectRoot "src-tauri\target\release\bundle"
        Write-Host ""
        Write-Success "Build complete!"
        Write-Host "  Installer location: $installerDir" -ForegroundColor Cyan

        # Open output folder automatically
        if (Test-Path $installerDir) {
            Start-Process explorer.exe $installerDir
        }
    } else {
        Write-Fail "Build failed. Check errors above."
        exit 1
    }
}

# ─── MODE: check ──────────────────────────────────────────────────────────────
function Start-Check {
    Write-Header "Axora Desktop — Validation Check"
    Test-Dependencies

    Write-Step "TypeScript type check..."
    Set-Location $ProjectRoot
    npx tsc --noEmit
    if ($LASTEXITCODE -eq 0) { Write-Success "TypeScript: OK" } else { Write-Fail "TypeScript: errors found" }

    Write-Step "Rust build check..."
    Set-Location (Join-Path $ProjectRoot "src-tauri")
    cargo check 2>&1 | Select-String -Pattern "(error|warning)" | Select-Object -First 10
    if ($LASTEXITCODE -eq 0) { Write-Success "Rust: OK" } else { Write-Fail "Rust: errors found" }

    Write-Host ""
    Write-Success "Check complete"
}

# ─── Dispatch ─────────────────────────────────────────────────────────────────
switch ($Mode) {
    "dev"   { Start-DevServer }
    "build" { Start-ProductionBuild }
    "check" { Start-Check }
}
