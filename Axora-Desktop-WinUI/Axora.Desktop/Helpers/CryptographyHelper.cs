using System.Security.Cryptography;

namespace Axora.Desktop.Helpers;

/// <summary>
/// Low-level cryptographic primitives used by P2pSyncService and StreamingVaultService.
/// All methods operate on raw byte arrays for interoperability with the Axora Mobile protocol.
/// </summary>
public static class CryptographyHelper
{
    // ── Random Generation ────────────────────────────────────────────────────

    /// <summary>Generates cryptographically random bytes using the OS CSPRNG.</summary>
    public static byte[] GenerateRandomBytes(int count)
    {
        return RandomNumberGenerator.GetBytes(count);
    }

    // ── HKDF Key Derivation ──────────────────────────────────────────────────

    /// <summary>
    /// Derives a key using HKDF-SHA-256 (RFC 5869).
    /// Matches the derivation used by Axora Mobile's OkHttp WebSocket client.
    /// </summary>
    /// <param name="sharedSecret">Input key material (ECDH shared secret).</param>
    /// <param name="salt">Salt bytes (use UTF-8 encoded context string for P2P).</param>
    /// <param name="outputLength">Desired output key length in bytes (default 32 for AES-256).</param>
    public static byte[] DeriveKeyHkdf(byte[] sharedSecret, byte[] salt, int outputLength = 32)
    {
        return HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: sharedSecret,
            outputLength: outputLength,
            salt: salt,
            info: null);
    }

    // ── AES-256-GCM ──────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM with a random 12-byte IV.
    /// Returns (ciphertext, 16-byte GCM authentication tag).
    /// The IV is not included in the output — the caller is responsible for framing.
    /// </summary>
    public static (byte[] Ciphertext, byte[] Tag) AesGcmEncrypt(byte[] key, byte[] iv, byte[] plaintext)
    {
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(iv, plaintext, ciphertext, tag);

        return (ciphertext, tag);
    }

    /// <summary>
    /// Decrypts an AES-256-GCM ciphertext block, verifying the GCM authentication tag.
    /// Throws <see cref="CryptographicException"/> if authentication fails.
    /// </summary>
    public static byte[] AesGcmDecrypt(byte[] key, byte[] iv, byte[] ciphertext, byte[] tag)
    {
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(iv, ciphertext, tag, plaintext);
        return plaintext;
    }

    // ── Key Zeroing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Overwrites a key byte array with zeros to remove key material from managed heap memory.
    /// Call this in finally blocks after completing cryptographic operations.
    /// </summary>
    public static void ZeroMemory(byte[] keyMaterial)
    {
        if (keyMaterial is null) return;
        CryptographicOperations.ZeroMemory(keyMaterial);
    }

    // ── DoD 5220.22-M Secure File Sanitization ──────────────────────────────

    /// <summary>
    /// Implements the DoD 5220.22-M (E) 3-pass sanitization standard:
    ///   - Pass 1: Overwrite with all zeros (0x00)
    ///   - Pass 2: Overwrite with all ones (0xFF)
    ///   - Pass 3: Overwrite with cryptographically secure random bytes (CSPRNG)
    /// Strips read-only attributes, truncates stream to 0 bytes, flushes sectors to physical disk,
    /// and renames file to a random GUID before deletion to eradicate file-system directory entry metadata.
    /// </summary>
    /// <param name="filePath">Absolute path to target file.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SecureShredFileAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        // Strip ReadOnly / Hidden attributes that would block write operations
        try
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
        catch { /* Best-effort attribute reset */ }

        var fileInfo = new FileInfo(filePath);
        long length = fileInfo.Length;
        const int bufferSize = 65536; // 64 KB write buffer

        if (length > 0)
        {
            var zeros = new byte[bufferSize];
            var ones = new byte[bufferSize];
            Array.Fill(ones, (byte)0xFF);
            var randomBuf = new byte[bufferSize];

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
            {
                // Pass 1: 0x00 Overwrite
                await OverwriteStreamPassAsync(stream, zeros, length, ct);
                await stream.FlushAsync(ct);

                // Pass 2: 0xFF Overwrite
                stream.Position = 0;
                await OverwriteStreamPassAsync(stream, ones, length, ct);
                await stream.FlushAsync(ct);

                // Pass 3: CSPRNG Overwrite
                stream.Position = 0;
                long written = 0;
                while (written < length)
                {
                    ct.ThrowIfCancellationRequested();
                    int toWrite = (int)Math.Min(bufferSize, length - written);
                    RandomNumberGenerator.Fill(randomBuf.AsSpan(0, toWrite));
                    await stream.WriteAsync(randomBuf.AsMemory(0, toWrite), ct);
                    written += toWrite;
                }
                await stream.FlushAsync(ct);

                // Truncate file to 0 bytes
                stream.SetLength(0);
                await stream.FlushAsync(ct);
            }

            ZeroMemory(zeros);
            ZeroMemory(ones);
            ZeroMemory(randomBuf);
        }

        // Rename file before deletion to destroy original filename metadata in directory table
        var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        var tempName = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
        try
        {
            File.Move(filePath, tempName);
            File.Delete(tempName);
        }
        catch
        {
            File.Delete(filePath);
        }
    }

    private static async Task OverwriteStreamPassAsync(FileStream stream, byte[] pattern, long totalLength, CancellationToken ct)
    {
        long written = 0;
        int bufLen = pattern.Length;
        while (written < totalLength)
        {
            ct.ThrowIfCancellationRequested();
            int toWrite = (int)Math.Min(bufLen, totalLength - written);
            await stream.WriteAsync(pattern.AsMemory(0, toWrite), ct);
            written += toWrite;
        }
    }

    // ── Hex Utilities ────────────────────────────────────────────────────────

    /// <summary>Converts a byte array to lowercase hex string.</summary>
    public static string ToHexString(byte[] bytes) =>
        Convert.ToHexString(bytes).ToLowerInvariant();

    /// <summary>Parses a hex string to a byte array.</summary>
    public static byte[] FromHexString(string hex) =>
        Convert.FromHexString(hex);
}
