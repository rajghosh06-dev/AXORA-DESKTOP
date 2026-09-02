use serde::{Deserialize, Serialize};
use std::fs;
use std::path::PathBuf;
use tauri::AppHandle;
use tauri_plugin_autostart::ManagerExt;

#[derive(Debug, Serialize, Deserialize)]
pub struct Settings {
    pub theme: Option<String>,
    pub theme_accent: Option<String>,
    pub hardware_concurrency: Option<u32>,
    pub output_directory: Option<String>,
    pub minimize_to_tray: Option<bool>,
    pub enable_splash: Option<bool>,
    pub splash_duration: Option<u32>,
    pub default_ocr_lang: Option<String>,
    pub clear_metadata: Option<bool>,
    pub image_quality: Option<u32>,
    pub argon_memory: Option<u32>,
    pub argon_iterations: Option<u32>,
    pub auto_lock_vault: Option<u32>,
}

pub fn get_settings_path() -> PathBuf {
    dirs::config_dir()
        .expect("Failed to get config dir")
        .join("com.axora.desktop")
        .join("settings.json")
}

#[tauri::command]
pub fn save_settings(settings: Settings) -> Result<(), String> {
    let path = get_settings_path();

    if let Some(parent) = path.parent() {
        if !parent.exists() {
            fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        }
    }

    let json = serde_json::to_string_pretty(&settings).map_err(|e| e.to_string())?;
    fs::write(path, json).map_err(|e| e.to_string())?;

    Ok(())
}

#[tauri::command]
pub fn update_theme_settings(theme: String, accent: String) -> Result<(), String> {
    let mut settings = load_settings().unwrap_or_else(|_| Settings {
        theme: Some("system".to_string()),
        theme_accent: Some("blue".to_string()),
        hardware_concurrency: Some(8),
        output_directory: None,
        minimize_to_tray: Some(true),
        enable_splash: Some(true),
        splash_duration: Some(1800),
        default_ocr_lang: Some("en".to_string()),
        clear_metadata: Some(true),
        image_quality: Some(85),
        argon_memory: Some(65536),
        argon_iterations: Some(3),
        auto_lock_vault: Some(15),
    });
    settings.theme = Some(theme);
    settings.theme_accent = Some(accent);
    save_settings(settings)?;
    Ok(())
}

#[tauri::command]
pub fn load_settings() -> Result<Settings, String> {
    let path = get_settings_path();
    let mut settings = if path.exists() {
        let json = fs::read_to_string(&path).map_err(|e| e.to_string())?;
        let loaded: Settings = serde_json::from_str(&json).map_err(|e| e.to_string())?;
        loaded
    } else {
        Settings {
            theme: Some("system".to_string()),
            theme_accent: Some("blue".to_string()),
            hardware_concurrency: Some(8),
            output_directory: None,
            minimize_to_tray: Some(true),
            enable_splash: Some(true),
            splash_duration: Some(1800),
            default_ocr_lang: Some("en".to_string()),
            clear_metadata: Some(true),
            image_quality: Some(85),
            argon_memory: Some(65536),
            argon_iterations: Some(3),
            auto_lock_vault: Some(15),
        }
    };

    // Ensure all options are filled with fallback defaults
    if settings.theme.is_none() { settings.theme = Some("system".to_string()); }
    if settings.theme_accent.is_none() { settings.theme_accent = Some("blue".to_string()); }
    if settings.hardware_concurrency.is_none() { settings.hardware_concurrency = Some(8); }
    if settings.minimize_to_tray.is_none() { settings.minimize_to_tray = Some(true); }
    if settings.enable_splash.is_none() { settings.enable_splash = Some(true); }
    if settings.splash_duration.is_none() { settings.splash_duration = Some(1800); }
    if settings.default_ocr_lang.is_none() { settings.default_ocr_lang = Some("en".to_string()); }
    if settings.clear_metadata.is_none() { settings.clear_metadata = Some(true); }
    if settings.image_quality.is_none() { settings.image_quality = Some(85); }
    if settings.argon_memory.is_none() { settings.argon_memory = Some(65536); }
    if settings.argon_iterations.is_none() { settings.argon_iterations = Some(3); }
    if settings.auto_lock_vault.is_none() { settings.auto_lock_vault = Some(15); }

    Ok(settings)
}

#[tauri::command]
pub fn get_download_dir() -> Result<String, String> {
    if let Some(dir) = dirs::download_dir() {
        Ok(dir.to_string_lossy().to_string())
    } else {
        Err("Download directory not found".to_string())
    }
}

/// Check if the app is configured to launch at Windows startup
#[tauri::command]
pub fn get_autostart_enabled(app: AppHandle) -> Result<bool, String> {
    let autostart_manager = app.autolaunch();
    autostart_manager
        .is_enabled()
        .map_err(|e| format!("Autostart check failed: {}", e))
}

/// Enable or disable launching the app at Windows startup
#[tauri::command]
pub fn set_autostart_enabled(app: AppHandle, enabled: bool) -> Result<(), String> {
    let autostart_manager = app.autolaunch();
    if enabled {
        autostart_manager
            .enable()
            .map_err(|e| format!("Failed to enable autostart: {}", e))
    } else {
        autostart_manager
            .disable()
            .map_err(|e| format!("Failed to disable autostart: {}", e))
    }
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct SystemInfo {
    pub os_name: String,
    pub os_version: String,
    pub cpu_model: String,
    pub cpu_cores: u32,
    pub total_memory_gb: f64,
    pub free_disk_space_gb: f64,
    pub is_tpm_available: bool,
    pub is_webview2_installed: bool,
}

#[tauri::command]
pub fn get_system_info() -> Result<SystemInfo, String> {
    // 1. Get OS Info
    let (os_name, os_version) = {
        let output = std::process::Command::new("cmd")
            .args(&["/c", "ver"])
            .output();
        if let Ok(out) = output {
            let stdout = String::from_utf8_lossy(&out.stdout).trim().to_string();
            let mut is_win11 = false;
            if let Some(version_start) = stdout.find("Version ") {
                let version_str = &stdout[version_start + 8..];
                let parts: Vec<&str> = version_str.split('.').collect();
                if parts.len() >= 3 {
                    let build_str: String = parts[2].chars().filter(|c| c.is_ascii_digit()).collect();
                    if let Ok(build_num) = build_str.parse::<u32>() {
                        if build_num >= 22000 {
                            is_win11 = true;
                        }
                    }
                }
            }
            if is_win11 {
                ("Windows 11".to_string(), stdout)
            } else if stdout.contains("Version 10.0") {
                ("Windows 10".to_string(), stdout)
            } else {
                ("Windows".to_string(), stdout)
            }
        } else {
            ("Windows 11".to_string(), "Microsoft Windows [Version 10.0.22631]".to_string())
        }
    };

    // 2. Get CPU Model
    let cpu_model = {
        let output = std::process::Command::new("reg")
            .args(&["query", "HKLM\\HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0", "/v", "ProcessorNameString"])
            .output();
        if let Ok(out) = output {
            let stdout = String::from_utf8_lossy(&out.stdout);
            let mut model = None;
            for line in stdout.lines() {
                if line.contains("ProcessorNameString") {
                    let parts: Vec<&str> = line.split("REG_SZ").collect();
                    if parts.len() > 1 {
                        model = Some(parts[1].trim().to_string());
                        break;
                    }
                }
            }
            model.unwrap_or_else(|| std::env::var("PROCESSOR_IDENTIFIER").unwrap_or_else(|_| "Intel/AMD Processor".to_string()))
        } else {
            std::env::var("PROCESSOR_IDENTIFIER").unwrap_or_else(|_| "Intel/AMD Processor".to_string())
        }
    };

    // 3. Get CPU Cores
    let cpu_cores = std::thread::available_parallelism()
        .map(|n| n.get())
        .unwrap_or(8) as u32;

    // 4. Get Total RAM
    let total_memory_gb = {
        let output = std::process::Command::new("wmic")
            .args(&["ComputerSystem", "get", "TotalPhysicalMemory"])
            .output();
        if let Ok(out) = output {
            let stdout = String::from_utf8_lossy(&out.stdout);
            let mut mem = 16.0;
            for line in stdout.lines() {
                let line = line.trim();
                if !line.is_empty() && line.chars().all(|c| c.is_ascii_digit()) {
                    if let Ok(bytes) = line.parse::<u64>() {
                        mem = bytes as f64 / (1024.0 * 1024.0 * 1024.0);
                        break;
                    }
                }
            }
            mem
        } else {
            16.0
        }
    };

    // 5. Get Free Disk Space
    let free_disk_space_gb = {
        let output = std::process::Command::new("wmic")
            .args(&["logicaldisk", "where", "DeviceID='C:'", "get", "FreeSpace"])
            .output();
        if let Ok(out) = output {
            let stdout = String::from_utf8_lossy(&out.stdout);
            let mut space = 100.0;
            for line in stdout.lines() {
                let line = line.trim();
                if !line.is_empty() && line.chars().all(|c| c.is_ascii_digit()) {
                    if let Ok(bytes) = line.parse::<u64>() {
                        space = bytes as f64 / (1024.0 * 1024.0 * 1024.0);
                        break;
                    }
                }
            }
            space
        } else {
            100.0
        }
    };

    // 6. Check TPM 2.0 (Driver service enumeration status)
    let is_tpm_available = {
        let output = std::process::Command::new("reg")
            .args(&["query", "HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tpm\\Enum"])
            .output();
        if let Ok(out) = output {
            let stdout = String::from_utf8_lossy(&out.stdout);
            out.status.success() && (stdout.contains("ACPI\\") || stdout.contains("PCI\\") || stdout.contains("ROOT\\"))
        } else {
            false
        }
    };

    Ok(SystemInfo {
        os_name,
        os_version,
        cpu_model,
        cpu_cores,
        total_memory_gb,
        free_disk_space_gb,
        is_tpm_available,
        is_webview2_installed: true,
    })
}
