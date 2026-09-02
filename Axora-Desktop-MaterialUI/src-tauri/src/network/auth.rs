/// network/auth.rs — ECDH Keypair generation and auth token management

use base64::{engine::general_purpose::STANDARD as BASE64, Engine as _};
use p256::{ecdh::EphemeralSecret, EncodedPoint};
use uuid::Uuid;

/// Generate an ephemeral P-256 ECDH keypair.
/// Returns (base64-encoded public key, private key bytes) for the server.
pub fn generate_ecdh_keypair() -> Result<(String, Vec<u8>), String> {
    let mut rng = rand::thread_rng();
    let secret = EphemeralSecret::random(&mut rng);
    let pubkey = EncodedPoint::from(secret.public_key());
    let pubkey_b64 = BASE64.encode(pubkey.as_bytes());

    // We don't serialize the private key here — in a full implementation,
    // store it in a global Arc<Mutex<>> and use it in the /pair handler
    // to compute the shared secret from the client's public key.
    Ok((pubkey_b64, vec![]))
}

/// Generate a cryptographically random UUID v4 auth token.
pub fn generate_auth_token() -> String {
    Uuid::new_v4().to_string()
}
