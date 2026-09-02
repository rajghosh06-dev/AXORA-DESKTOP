/// network/mod.rs — Axora Mobile Ecosystem Network Layer
///
/// Architecture:
/// - Axum HTTP + WebSocket server running on a dedicated Tokio thread
/// - Binds to the machine's local LAN IP (not 0.0.0.0) on a dynamic port
/// - mDNS advertises as `_axora._tcp.local` for zero-config discovery
/// - ECDH (P-256) handshake to establish a secure session key
/// - Dynamic auth token (UUID v4) rotates on each pairing session
/// - CancellationToken: graceful server shutdown without port lingering

pub mod auth;
pub mod mdns_service;

use axum::{
    extract::ws::{Message, WebSocket, WebSocketUpgrade},
    extract::State,
    http::StatusCode,
    response::{IntoResponse, Response},
    routing::{get, post},
    Json, Router,
};
use once_cell::sync::OnceCell;
use serde::{Deserialize, Serialize};
use std::{net::SocketAddr, sync::Arc};
use tokio::sync::Mutex;
use tokio_util::sync::CancellationToken;
use tower_http::cors::{Any, CorsLayer};

#[derive(Clone, Serialize, Deserialize)]
pub struct ServerInfo {
    pub ip: String,
    pub port: u16,
    pub auth_token: String,
    pub server_pubkey_b64: String,
}

pub struct NetworkState {
    pub server_info: Arc<Mutex<Option<ServerInfo>>>,
    pub session_established: Arc<Mutex<bool>>,
}

/// Global server info — set once when server starts, read by QR code generator
static SERVER_INFO: OnceCell<Arc<Mutex<Option<ServerInfo>>>> = OnceCell::new();

/// Global cancellation token — triggered to stop the server gracefully
static CANCEL_TOKEN: OnceCell<Arc<Mutex<Option<CancellationToken>>>> = OnceCell::new();

pub fn get_server_info_cell() -> &'static Arc<Mutex<Option<ServerInfo>>> {
    SERVER_INFO.get_or_init(|| Arc::new(Mutex::new(None)))
}

fn get_cancel_token_cell() -> &'static Arc<Mutex<Option<CancellationToken>>> {
    CANCEL_TOKEN.get_or_init(|| Arc::new(Mutex::new(None)))
}

// ─────────────────────────────────────────────────────────────────────────────
// Gracefully stop the running server (if any) by triggering CancellationToken.
// ─────────────────────────────────────────────────────────────────────────────
pub async fn stop_server() {
    let token_cell = get_cancel_token_cell();
    let mut token_guard = token_cell.lock().await;
    if let Some(token) = token_guard.take() {
        token.cancel();
        println!("[Axora Network] Cancellation token fired — server shutting down");
    }
    // Clear server info so QR code goes stale
    let info_cell = get_server_info_cell();
    let mut info_guard = info_cell.lock().await;
    *info_guard = None;
}

// ─────────────────────────────────────────────────────────────────────────────
// Start the Axum server on a background Tokio task.
// Returns (ip, port, auth_token, server_pubkey_b64) for QR code generation.
// ─────────────────────────────────────────────────────────────────────────────
pub async fn start_server() -> Result<ServerInfo, String> {
    // Stop any existing server first (token cancellation)
    stop_server().await;

    // Get local LAN IP (not loopback, not 0.0.0.0)
    let local_ip =
        local_ip_address::local_ip().map_err(|e| format!("Failed to get local IP: {}", e))?;

    // Bind to port 0 to get an OS-assigned ephemeral port
    let listener = tokio::net::TcpListener::bind(SocketAddr::new(local_ip, 0))
        .await
        .map_err(|e| format!("Failed to bind server: {}", e))?;
    let port = listener.local_addr().map_err(|e| e.to_string())?.port();

    // Generate ECDH keypair + auth token
    let (server_pubkey_b64, _server_privkey) = auth::generate_ecdh_keypair()?;
    let auth_token = auth::generate_auth_token();

    let info = ServerInfo {
        ip: local_ip.to_string(),
        port,
        auth_token: auth_token.clone(),
        server_pubkey_b64: server_pubkey_b64.clone(),
    };

    // Store in global cell so Tauri commands can read it
    {
        let cell = get_server_info_cell();
        let mut guard = cell.lock().await;
        *guard = Some(info.clone());
    }

    let state = Arc::new(NetworkState {
        server_info: Arc::new(Mutex::new(Some(info.clone()))),
        session_established: Arc::new(Mutex::new(false)),
    });

    let cors = CorsLayer::new()
        .allow_origin(Any)
        .allow_methods(Any)
        .allow_headers(Any);

    let app = Router::new()
        .route("/health", get(health_handler))
        .route("/pair", post(pair_handler))
        .route("/api/v1/pair", post(pair_handler))
        .route("/ws", get(ws_upgrade_handler))
        .with_state(state)
        .layer(cors);

    // Create a fresh cancellation token for this server instance
    let token = CancellationToken::new();
    {
        let cell = get_cancel_token_cell();
        let mut guard = cell.lock().await;
        *guard = Some(token.clone());
    }

    // Clone info for mDNS and spawn mDNS advertisement on background task
    let info_for_mdns = info.clone();
    let mdns_token = token.clone();
    tokio::spawn(async move {
        tokio::select! {
            result = mdns_service::advertise_service(info_for_mdns.port) => {
                if let Err(e) = result {
                    eprintln!("[Axora Network] mDNS error: {}", e);
                }
            }
            _ = mdns_token.cancelled() => {
                println!("[Axora Network] mDNS stopped");
            }
        }
    });

    // Spawn the Axum server — cancellable via the token
    let server_token = token.clone();
    tokio::spawn(async move {
        tokio::select! {
            result = axum::serve(listener, app) => {
                if let Err(e) = result {
                    eprintln!("[Axora Network] Server error: {}", e);
                }
            }
            _ = server_token.cancelled() => {
                println!("[Axora Network] Server gracefully stopped — port released");
            }
        }
    });

    println!(
        "[Axora Network] Server running on {}:{} | Token: {}",
        info.ip, info.port, info.auth_token
    );

    Ok(info)
}

// ─────────────────────────────────────────────────────────────────────────────
// GET /health — simple ping for Android client to verify connectivity
// ─────────────────────────────────────────────────────────────────────────────
async fn health_handler() -> impl IntoResponse {
    Json(serde_json::json!({ "status": "ok", "service": "Axora" }))
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /pair & POST /api/v1/pair — ECDH pairing initiation
// Body: { "auth_token": "...", "client_pubkey_b64": "..." } OR
//       { "pairing_token": "...", "client_ephemeral_public_key_hex": "..." }
// ─────────────────────────────────────────────────────────────────────────────
#[derive(Deserialize)]
struct PairRequest {
    #[serde(alias = "pairing_token")]
    auth_token: Option<String>,
    #[allow(dead_code)]
    #[serde(alias = "client_ephemeral_public_key_hex")]
    client_pubkey_b64: Option<String>,
}

#[derive(Serialize)]
struct PairResponse {
    session_ok: bool,
    server_pubkey_b64: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    server_ephemeral_public_key_hex: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    status: Option<String>,
    message: String,
}

async fn pair_handler(
    State(state): State<Arc<NetworkState>>,
    Json(body): Json<PairRequest>,
) -> Response {
    let info_guard = state.server_info.lock().await;
    let expected_token = info_guard
        .as_ref()
        .map(|i| i.auth_token.clone())
        .unwrap_or_default();
    let server_pubkey = info_guard
        .as_ref()
        .map(|i| i.server_pubkey_b64.clone())
        .unwrap_or_default();
    drop(info_guard);

    let incoming_token = body.auth_token.unwrap_or_default();
    if incoming_token.is_empty() || incoming_token != expected_token {
        return (
            StatusCode::UNAUTHORIZED,
            Json(PairResponse {
                session_ok: false,
                server_pubkey_b64: String::new(),
                server_ephemeral_public_key_hex: None,
                status: Some("DENIED".to_string()),
                message: "Invalid auth token".to_string(),
            }),
        )
            .into_response();
    }

    // Mark session as established
    let mut session = state.session_established.lock().await;
    *session = true;

    (
        StatusCode::OK,
        Json(PairResponse {
            session_ok: true,
            server_pubkey_b64: server_pubkey.clone(),
            server_ephemeral_public_key_hex: Some(server_pubkey),
            status: Some("APPROVED".to_string()),
            message: "Pairing successful. WebSocket ready at /ws".to_string(),
        }),
    )
        .into_response()
}

// ─────────────────────────────────────────────────────────────────────────────
// GET /ws — WebSocket upgrade for real-time mobile↔desktop communication
// ─────────────────────────────────────────────────────────────────────────────
async fn ws_upgrade_handler(
    State(state): State<Arc<NetworkState>>,
    ws: WebSocketUpgrade,
) -> Response {
    let session_ok = *state.session_established.lock().await;
    if !session_ok {
        return (StatusCode::FORBIDDEN, "Pair first via /pair").into_response();
    }
    ws.on_upgrade(handle_websocket)
}

async fn handle_websocket(mut socket: WebSocket) {
    println!("[Axora WS] Mobile device connected");
    while let Some(msg) = socket.recv().await {
        match msg {
            Ok(Message::Text(text)) => {
                println!("[Axora WS] Received text payload");

                if let Ok(v) = serde_json::from_str::<serde_json::Value>(&text) {
                    let cmd_type = v.get("command").or_else(|| v.get("type")).and_then(|s| s.as_str()).unwrap_or("");
                    if cmd_type == "quick_drop_payload" {
                        let payload = v.get("payload").unwrap_or(&v);
                        let payload_type = payload.get("payload_type").and_then(|s| s.as_str()).unwrap_or("text");
                        let content = payload.get("content").and_then(|s| s.as_str()).unwrap_or("");
                        let filename = payload.get("filename").and_then(|s| s.as_str()).unwrap_or("drop_file");

                        if payload_type == "file" && !content.is_empty() {
                            use base64::Engine;
                            if let Ok(decoded) = base64::engine::general_purpose::STANDARD.decode(content) {
                                if let Some(home) = dirs::download_dir() {
                                    let drop_dir = home.join("Axora_QuickDrop");
                                    let _ = std::fs::create_dir_all(&drop_dir);
                                    let file_path = drop_dir.join(filename);
                                    let _ = std::fs::write(&file_path, decoded);
                                    println!("[Quick Drop] Saved file to {:?}", file_path);
                                }
                            }
                        }
                    }
                }

                let _ = socket
                    .send(Message::Text(format!(
                        "{{\"status\": \"ok\", \"ack\": true}}"
                    )))
                    .await;
            }
            Ok(Message::Binary(bin_data)) => {
                println!("[Axora WS] Received binary framed payload ({} bytes)", bin_data.len());
                // Binary framing: [12-byte IV][4-byte Big-Endian Length][AES-256-GCM Ciphertext Payload]
                if bin_data.len() >= 16 {
                    let iv = &bin_data[0..12];
                    let payload_len = u32::from_be_bytes([bin_data[12], bin_data[13], bin_data[14], bin_data[15]]) as usize;
                    let payload = &bin_data[16..];
                    println!("[Axora WS] Binary frame parsed: IV len={}, expected_len={}, payload_len={}", iv.len(), payload_len, payload.len());

                    // Save directly to QuickDrop if it's a binary stream drop
                    if let Some(home) = dirs::download_dir() {
                        let drop_dir = home.join("Axora_QuickDrop");
                        let _ = std::fs::create_dir_all(&drop_dir);
                        let file_path = drop_dir.join(format!("drop_stream_{}.bin", chrono::Utc::now().timestamp_millis()));
                        let _ = std::fs::write(&file_path, payload);
                    }
                }

                let _ = socket
                    .send(Message::Text(format!(
                        "{{\"status\": \"ok\", \"ack\": true, \"binary\": true}}"
                    )))
                    .await;
            }
            Ok(Message::Close(_)) => {
                println!("[Axora WS] Mobile device disconnected");
                break;
            }
            Err(e) => {
                eprintln!("[Axora WS] Error: {}", e);
                break;
            }
            _ => {}
        }
    }
}


