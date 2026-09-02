// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod settings;
mod vault;
mod processor;
mod scanner;
mod network;
mod commands;

use network::ServerInfo;
use tauri::{
    AppHandle, Emitter, Manager,
    menu::{MenuBuilder, MenuItemBuilder},
    tray::{TrayIconBuilder, TrayIconEvent},
};

// ─────────────────────────────────────────────────────────────────────────────
// Network / Mobile Link commands exposed to the React frontend
// ─────────────────────────────────────────────────────────────────────────────

/// Toggle the Axum + mDNS server on or off.
/// When `enabled=true`: starts the server thread and returns ServerInfo.
/// When `enabled=false`: fires the CancellationToken and releases the port.
#[tauri::command]
async fn toggle_sync_server(enabled: bool) -> Result<Option<ServerInfo>, String> {
    if enabled {
        let info = network::start_server().await?;
        Ok(Some(info))
    } else {
        network::stop_server().await;
        Ok(None)
    }
}

/// Start the local network server (legacy — kept for backward compat).
#[tauri::command]
async fn start_ecosystem_server() -> Result<ServerInfo, String> {
    network::start_server().await
}

/// Get current server info (for refreshing QR code without restarting server).
#[tauri::command]
async fn get_server_info() -> Result<Option<ServerInfo>, String> {
    let cell = network::get_server_info_cell();
    let guard = cell.lock().await;
    Ok(guard.clone())
}

/// Generate QR code as an SVG data URL from the server pairing info.
/// The Android app scans this QR code to connect.
#[tauri::command]
async fn generate_pairing_qr() -> Result<String, String> {
    use qrcode::{QrCode, EcLevel};
    use qrcode::render::svg;

    let cell = network::get_server_info_cell();
    let guard = cell.lock().await;
    let info = guard.as_ref().ok_or("Server not started yet")?;

    // Encode pairing data as a compact JSON string for the QR code
    let payload = serde_json::json!({
        "ip": info.ip,
        "port": info.port,
        "token": info.auth_token,
        "pubkey": info.server_pubkey_b64,
        "service": "axora"
    })
    .to_string();

    let code = QrCode::with_error_correction_level(payload.as_bytes(), EcLevel::M)
        .map_err(|e| format!("QR generation error: {}", e))?;

    let svg_string = code
        .render::<svg::Color<'_>>()
        .min_dimensions(300, 300)
        .quiet_zone(true)
        .dark_color(svg::Color("#000000"))
        .light_color(svg::Color("#ffffff"))
        .build();

    Ok(format!("data:image/svg+xml;utf8,{}", urlencoding_encode(&svg_string)))
}

fn urlencoding_encode(s: &str) -> String {
    s.chars().map(|c| match c {
        ' ' => "+".to_string(),
        c if c.is_alphanumeric() || matches!(c, '-' | '_' | '.' | '~' | '<' | '>' | '=' | '/' | '"') => c.to_string(),
        c => format!("%{:02X}", c as u32),
    }).collect()
}

/// Simple backend ping for Workspace Hub health check
#[tauri::command]
fn ping_backend() -> String {
    "Axora Backend Online ✓".to_string()
}

// ─────────────────────────────────────────────────────────────────────────────
// System Tray helpers
// ─────────────────────────────────────────────────────────────────────────────

fn setup_system_tray(app: &AppHandle) -> tauri::Result<()> {
    let open_item = MenuItemBuilder::new("Open Axora")
        .id("open")
        .build(app)?;

    let toggle_sync_item = MenuItemBuilder::new("Toggle Clipboard Sync")
        .id("toggle_sync")
        .build(app)?;

    let quit_item = MenuItemBuilder::new("Exit Application")
        .id("quit")
        .build(app)?;

    let menu = MenuBuilder::new(app)
        .item(&open_item)
        .separator()
        .item(&toggle_sync_item)
        .separator()
        .item(&quit_item)
        .build()?;

    TrayIconBuilder::new()
        .icon(app.default_window_icon().unwrap().clone())
        .tooltip("Axora Desktop")
        .menu(&menu)
        .show_menu_on_left_click(false)
        .on_menu_event(move |app, event| match event.id.as_ref() {
            "open" => {
                if let Some(window) = app.get_webview_window("main") {
                    let _ = window.show();
                    let _ = window.set_focus();
                }
            }
            "toggle_sync" => {
                let _ = app.emit("toggle-clipboard-sync", ());
            }
            "quit" => {
                std::process::exit(0);
            }
            _ => {}
        })
        .on_tray_icon_event(|tray, event| {
            if let TrayIconEvent::Click { button, .. } = event {
                if button == tauri::tray::MouseButton::Left {
                    if let Some(window) = tray.app_handle().get_webview_window("main") {
                        let _ = window.show();
                        let _ = window.set_focus();
                    }
                }
            }
        })
        .build(app)?;

    Ok(())
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Tauri entry point
// ─────────────────────────────────────────────────────────────────────────────
fn main() {
    let runtime = tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()
        .expect("Failed to build Tokio runtime");

    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_autostart::init(
            tauri_plugin_autostart::MacosLauncher::LaunchAgent,
            Some(vec![]),
        ))
        .plugin(tauri_plugin_global_shortcut::Builder::new().build())
        .setup(|app| {
            // Set up system tray
            let handle = app.handle().clone();
            setup_system_tray(&handle)?;

            // Get the main window (starts hidden per tauri.conf.json visible:false)
            let window = app.get_webview_window("main")
                .expect("main window not found");

            // Show window after a brief delay — gives React time to render
            // the SplashScreen before the window becomes visible.
            // This completely eliminates the transparent-window flash.
            let win_for_show = window.clone();
            std::thread::spawn(move || {
                std::thread::sleep(std::time::Duration::from_millis(120));
                let _ = win_for_show.show();
                let _ = win_for_show.set_focus();
            });

            // Intercept close → minimize to tray instead of quitting
            let win_clone = window.clone();
            window.on_window_event(move |event| {
                if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                    api.prevent_close();
                    let _ = win_clone.hide();
                }
            });

            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            // Settings
            settings::get_download_dir,
            settings::save_settings,
            settings::load_settings,
            settings::update_theme_settings,
            settings::get_system_info,
            // Vault
            vault::encrypt_file,
            vault::decrypt_file,
            // Processor
            processor::batch_process_images,
            processor::convert_files,
            // Scanner
            scanner::list_scanners,
            scanner::scan_document,
            // Network / Mobile Link
            toggle_sync_server,
            start_ecosystem_server,
            get_server_info,
            generate_pairing_qr,
            // Workspace Hub
            ping_backend,
            // ── Form Studio Suite ──
            commands::bureaucrat::resize_to_target_kb,
            commands::bureaucrat::extract_signature,
            commands::bureaucrat::stitch_id_card_pdf,
            commands::bureaucrat::compile_ordered_pdf,
            commands::bureaucrat::remove_photo_background,
            commands::bureaucrat::extract_official_stamp,
            // ── Scholar Kit Suite ──
            commands::academic::ocr_image_windows,
            commands::academic::redact_pdf,
            commands::academic::get_pdf_page_count,
            commands::academic::reorder_pdf_pages,
            commands::academic::rotate_pdf_pages,
            commands::academic::extract_pdf_pages,
            commands::academic::compress_pdf_multi_tier,
            // ── Anki SM-2 Studio ──
            commands::anki::export_flashcard_deck,
            commands::anki::calculate_sm2_desktop,
            // ── Vector RAG & Semantic Search ──
            commands::rag::semantic_search_docs,
            // ── Media Forge Suite ──
            commands::media::extract_audio,
            commands::media::transcribe_audio_file,
            commands::media::save_snippet,
            commands::media::load_snippets,
            commands::media::delete_snippet,
            // ── Agentic Containment & Intune Sandboxing ──
            commands::sandbox::validate_mxc_policy,
            commands::sandbox::spawn_sandboxed_command,
            // ── Autostart ──
            settings::get_autostart_enabled,
            settings::set_autostart_enabled,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");

    drop(runtime);
}
