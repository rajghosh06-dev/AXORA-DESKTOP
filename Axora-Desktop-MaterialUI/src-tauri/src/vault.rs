use aes_gcm::{
    aead::{KeyInit, stream::{EncryptorBE32, DecryptorBE32}, generic_array::GenericArray},
    Aes256Gcm, Key,
};
use argon2::{Argon2, Params, Version, Algorithm};
use rand::RngCore;
use std::fs::File;
use std::io::{Read, Write};
use std::path::PathBuf;

// Argon2id parameters tuned for Windows 11 desktop (fast enough, hard to brute-force)
// Memory: 64 MB, Iterations: 3, Parallelism: 1
const ARGON2_MEM_COST: u32 = 65536; // 64 MB in KB
const ARGON2_TIME_COST: u32 = 3;
const ARGON2_PARALLELISM: u32 = 1;

/// Derives a 32-byte AES-256 key from a user password and per-file salt using Argon2id.
fn derive_key(password: &str, salt: &[u8; 16]) -> Result<[u8; 32], String> {
    let params = Params::new(ARGON2_MEM_COST, ARGON2_TIME_COST, ARGON2_PARALLELISM, Some(32))
        .map_err(|e| format!("Argon2 param error: {}", e))?;

    let argon2 = Argon2::new(Algorithm::Argon2id, Version::V0x13, params);
    let mut key = [0u8; 32];
    argon2
        .hash_password_into(password.as_bytes(), salt, &mut key)
        .map_err(|e| format!("Key derivation failed: {}", e))?;
    Ok(key)
}

fn read_exact_or_eof(file: &mut File, mut buf: &mut [u8]) -> std::io::Result<usize> {
    let mut total_read = 0;
    while !buf.is_empty() {
        let n = file.read(buf)?;
        if n == 0 {
            break;
        }
        total_read += n;
        let tmp = buf;
        buf = &mut tmp[n..];
    }
    Ok(total_read)
}

#[tauri::command]
pub fn encrypt_file(path: String, password: String) -> Result<String, String> {
    if password.is_empty() {
        return Err("Password cannot be empty".to_string());
    }
    if password.len() < 6 {
        return Err("Password must be at least 6 characters".to_string());
    }

    let file_path = PathBuf::from(&path);
    if !file_path.exists() {
        return Err("File does not exist".to_string());
    }

    // Generate random 16-byte salt and 7-byte stream nonce per file
    let mut salt_bytes = [0u8; 16];
    let mut nonce_bytes = [0u8; 7];
    let mut rng = rand::thread_rng();
    rng.fill_bytes(&mut salt_bytes);
    rng.fill_bytes(&mut nonce_bytes);

    // Derive key from password and random salt using Argon2id
    let raw_key = derive_key(&password, &salt_bytes)?;
    let key = Key::<Aes256Gcm>::from_slice(&raw_key);
    let cipher = Aes256Gcm::new(key);

    let mut source_file = File::open(&file_path).map_err(|e| e.to_string())?;
    let out_path = file_path.with_extension("axora");
    let mut dest_file = File::create(&out_path).map_err(|e| e.to_string())?;

    // Write file header: [16-byte salt][7-byte nonce]
    dest_file.write_all(&salt_bytes).map_err(|e| e.to_string())?;
    dest_file.write_all(&nonce_bytes).map_err(|e| e.to_string())?;

    let nonce = GenericArray::from(nonce_bytes);
    let mut encryptor = EncryptorBE32::from_aead(cipher, &nonce);

    let mut buffer = [0u8; 1024 * 1024]; // 1MB streaming chunks
    loop {
        let read_count =
            read_exact_or_eof(&mut source_file, &mut buffer).map_err(|e| e.to_string())?;

        if read_count < buffer.len() {
            let ciphertext = encryptor
                .encrypt_last(&buffer[..read_count])
                .map_err(|e| format!("Encryption error: {:?}", e))?;
            dest_file.write_all(&ciphertext).map_err(|e| e.to_string())?;
            break;
        } else {
            let ciphertext = encryptor
                .encrypt_next(&buffer[..read_count])
                .map_err(|e| format!("Encryption error: {:?}", e))?;
            dest_file.write_all(&ciphertext).map_err(|e| e.to_string())?;
        }
    }

    Ok(out_path.to_string_lossy().to_string())
}

#[tauri::command]
pub fn decrypt_file(path: String, password: String) -> Result<String, String> {
    if password.is_empty() {
        return Err("Password cannot be empty".to_string());
    }

    let file_path = PathBuf::from(&path);
    if !file_path.exists() {
        return Err("File does not exist".to_string());
    }

    let mut source_file = File::open(&file_path).map_err(|e| e.to_string())?;

    // Determine output filename: strip .axora, restore original extension
    let out_path = if file_path.extension().and_then(|e| e.to_str()) == Some("axora") {
        file_path.with_extension("")
    } else {
        file_path.with_extension("decrypted")
    };
    let mut dest_file = File::create(&out_path).map_err(|e| e.to_string())?;

    // Read the 23-byte header: [16-byte salt][7-byte nonce]
    let mut salt_bytes = [0u8; 16];
    let mut nonce_bytes = [0u8; 7];
    source_file
        .read_exact(&mut salt_bytes)
        .map_err(|_| "File appears corrupted or is not an .axora file (missing salt header)".to_string())?;
    source_file
        .read_exact(&mut nonce_bytes)
        .map_err(|_| "File appears corrupted or is not an .axora file (missing nonce header)".to_string())?;

    // Derive key from password using salt extracted from file header
    let raw_key = derive_key(&password, &salt_bytes)?;
    let key = Key::<Aes256Gcm>::from_slice(&raw_key);
    let cipher = Aes256Gcm::new(key);

    let nonce = GenericArray::from(nonce_bytes);
    let mut decryptor = DecryptorBE32::from_aead(cipher, &nonce);

    let mut buffer = [0u8; 1024 * 1024 + 16]; // 1MB + 16-byte auth tag
    loop {
        let read_count =
            read_exact_or_eof(&mut source_file, &mut buffer).map_err(|e| e.to_string())?;

        if read_count < buffer.len() {
            let plaintext = decryptor
                .decrypt_last(&buffer[..read_count])
                .map_err(|_| "Decryption failed — wrong password or corrupted file".to_string())?;
            dest_file.write_all(&plaintext).map_err(|e| e.to_string())?;
            break;
        } else {
            let plaintext = decryptor
                .decrypt_next(&buffer[..read_count])
                .map_err(|_| "Decryption failed — wrong password or corrupted file".to_string())?;
            dest_file.write_all(&plaintext).map_err(|e| e.to_string())?;
        }
    }

    Ok(out_path.to_string_lossy().to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_derive_key_deterministic() {
        let salt = [42u8; 16];
        let key1 = derive_key("MySecretPass123", &salt).unwrap();
        let key2 = derive_key("MySecretPass123", &salt).unwrap();
        assert_eq!(key1, key2);
        assert_eq!(key1.len(), 32);
    }

    #[test]
    fn test_derive_key_diff_passwords() {
        let salt = [42u8; 16];
        let key1 = derive_key("PassA", &salt).unwrap();
        let key2 = derive_key("PassB", &salt).unwrap();
        assert_ne!(key1, key2);
    }

    #[test]
    fn test_encrypt_decrypt_roundtrip() {
        let temp_dir = std::env::temp_dir();
        let src_path = temp_dir.join("axora_test_vault_input.txt");
        let test_data = b"Axora Zero-Trust Encrypted Vault Test Payload 2026!";
        std::fs::write(&src_path, test_data).unwrap();

        let dummy_fixture_passphrase = String::from("axora-non-secret-test-dummy");
        let enc_res = encrypt_file(src_path.to_str().unwrap().to_string(), dummy_fixture_passphrase.clone());
        assert!(enc_res.is_ok(), "Encryption should succeed");

        let enc_path = enc_res.unwrap();
        assert!(std::path::Path::new(&enc_path).exists());

        // Decrypt
        let dec_res = decrypt_file(enc_path.clone(), dummy_fixture_passphrase);
        assert!(dec_res.is_ok(), "Decryption should succeed");

        let dec_data = std::fs::read(&src_path).unwrap();
        assert_eq!(dec_data, test_data);

        // Cleanup
        let _ = std::fs::remove_file(&src_path);
        let _ = std::fs::remove_file(&enc_path);
    }
}


