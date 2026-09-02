using System;
using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for the streaming Argon2id + AES-256-GCM vault encryption service with TPM/DPAPI machine sealing.
/// Vault file header format: [16-byte Salt][12-byte Nonce][1MB Ciphertext Blocks...]
/// </summary>
public interface ISecurityVaultService
{
    /// <summary>
    /// Encrypts the source file using Argon2id key derivation and AES-256-GCM streaming.
    /// </summary>
    Task EncryptFileAsync(
        string inputPath,
        string outputPath,
        string password,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Decrypts an .axvault file. Reads the embedded salt, re-derives the key via Argon2id,
    /// and streams 1MB AES-256-GCM blocks back to plaintext.
    /// </summary>
    Task DecryptFileAsync(
        string inputPath,
        string outputPath,
        string password,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a hardware TPM/DPAPI machine-sealed master key exists on this device.
    /// </summary>
    bool IsMachineSealedKeyAvailable();

    /// <summary>
    /// Seals the master password to the current laptop's physical TPM/DPAPI boundary.
    /// </summary>
    Task SealMasterKeyToMachineAsync(string password);

    /// <summary>
    /// Unseals the master password using local machine TPM/DPAPI.
    /// </summary>
    Task<string?> UnsealMasterKeyFromMachineAsync();

    /// <summary>
    /// Clears the sealed key blob.
    /// </summary>
    void ClearSealedKey();
}
