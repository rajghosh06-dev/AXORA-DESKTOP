/// scanner.rs — Windows 11 Hardware Scanner Integration
///
/// Primary strategy: WIA (Windows Image Acquisition) via PowerShell COM.
/// This is the native Windows 11 scanner API and requires no external dependencies.
///
/// WIA is available on all Windows 11 machines that have a scanner driver installed.
/// The PowerShell COM approach is identical to how the document converter works,
/// making it stable and consistent with the rest of the backend.

use serde::{Deserialize, Serialize};
use std::path::Path;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ScannerDevice {
    pub id: String,
    pub name: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ScanResult {
    pub path: String,
    pub page: u32,
}

// ─────────────────────────────────────────────────────────────────────────────
// List all WIA-compatible scanners connected to this Windows 11 machine
// ─────────────────────────────────────────────────────────────────────────────
#[tauri::command]
pub fn list_scanners() -> Result<Vec<ScannerDevice>, String> {
    let ps_script = r#"
$wia = New-Object -ComObject WIA.DeviceManager
$devices = @()
foreach ($deviceInfo in $wia.DeviceInfos) {
    if ($deviceInfo.Type -eq 1) {  # 1 = Scanner
        $devices += [PSCustomObject]@{
            id   = $deviceInfo.DeviceID
            name = $deviceInfo.Properties['Name'].Value
        }
    }
}
$devices | ConvertTo-Json -Depth 2
"#;

    let output = run_ps_script(ps_script)?;
    let stdout = String::from_utf8_lossy(&output.stdout).to_string();

    if stdout.trim().is_empty() || stdout.trim() == "null" {
        return Ok(vec![]);
    }

    // Parse JSON output — WIA returns either array or single object
    let parsed: Result<Vec<ScannerDevice>, _> = serde_json::from_str(&stdout);
    if let Ok(devices) = parsed {
        return Ok(devices);
    }
    // Single device case
    let single: Result<ScannerDevice, _> = serde_json::from_str(&stdout);
    if let Ok(device) = single {
        return Ok(vec![device]);
    }

    Ok(vec![])
}

// ─────────────────────────────────────────────────────────────────────────────
// Perform a scan using WIA COM on Windows 11
// Parameters:
//   device_id  - WIA device ID from list_scanners()
//   output_dir - directory to save the scanned image
//   dpi        - scan resolution (100, 200, 300, 600)
//   color_mode - "Color", "Grayscale", or "BlackAndWhite"
// ─────────────────────────────────────────────────────────────────────────────
#[tauri::command]
pub fn scan_document(
    device_id: String,
    output_dir: String,
    dpi: u32,
    color_mode: String,
    page_number: u32,
) -> Result<ScanResult, String> {
    let out_dir = Path::new(&output_dir);
    if !out_dir.exists() {
        std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    }

    let clean_out_dir = out_dir
        .canonicalize()
        .unwrap_or(out_dir.to_path_buf())
        .to_string_lossy()
        .replace(r"\\?\", "");

    let timestamp = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_secs();
    let output_filename = format!("scan_{}_{:04}.jpg", timestamp, page_number);
    let output_path = format!("{}\\{}", clean_out_dir, output_filename);

    // WIA color mode constants: 1=Color, 2=Grayscale, 4=Black&White
    let wia_color_mode = match color_mode.to_lowercase().as_str() {
        "grayscale" => 2,
        "blackandwhite" | "bw" => 4,
        _ => 1, // Default: Color
    };

    let ps_script = format!(
        r#"
$deviceId = '{device_id}'
$outputPath = '{output_path}'
$dpi = {dpi}
$colorMode = {color_mode}

$wia = New-Object -ComObject WIA.DeviceManager
$device = $null
foreach ($di in $wia.DeviceInfos) {{
    if ($di.DeviceID -eq $deviceId) {{
        $device = $di.Connect()
        break
    }}
}}

if ($null -eq $device) {{
    Write-Error "Scanner device not found"
    exit 1
}}

# Configure scanner settings
$scanner = $device.Items[1]
foreach ($prop in $scanner.Properties) {{
    switch ($prop.PropertyID) {{
        6146 {{ $prop.Value = $colorMode }}  # Color Mode
        6147 {{ $prop.Value = $dpi }}         # Horizontal Resolution
        6148 {{ $prop.Value = $dpi }}         # Vertical Resolution
    }}
}}

# Perform the scan
$image = $scanner.Transfer()
$image.SaveFile($outputPath)
Write-Output "OK:$outputPath"
"#,
        device_id = device_id.replace('\'', "''"),
        output_path = output_path.replace('\'', "''"),
        dpi = dpi,
        color_mode = wia_color_mode
    );

    let output = run_ps_script(&ps_script)?;
    let stdout = String::from_utf8_lossy(&output.stdout).to_string();

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&output.stderr).to_string();
        return Err(format!(
            "Scanner error: {}",
            if stderr.is_empty() { "Unknown scanner error".to_string() } else { stderr }
        ));
    }

    if stdout.contains("OK:") {
        return Ok(ScanResult {
            path: output_path,
            page: page_number,
        });
    }

    Err("Scan failed: no output received from scanner".to_string())
}

// ─────────────────────────────────────────────────────────────────────────────
// Helper: run a PowerShell script via temp file (avoids command-line parsing issues)
// ─────────────────────────────────────────────────────────────────────────────
fn run_ps_script(script: &str) -> Result<std::process::Output, String> {
    let temp_dir = std::env::temp_dir();
    let rand_id = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_micros();
    let script_path = temp_dir.join(format!("axora_scan_{}.ps1", rand_id));

    std::fs::write(&script_path, script).map_err(|e| format!("Failed to write scan script: {}", e))?;

    let output = std::process::Command::new("powershell")
        .args(&["-ExecutionPolicy", "Bypass", "-File", script_path.to_str().unwrap()])
        .output()
        .map_err(|e| format!("Failed to run scanner script: {}", e))?;

    let _ = std::fs::remove_file(&script_path);
    Ok(output)
}
