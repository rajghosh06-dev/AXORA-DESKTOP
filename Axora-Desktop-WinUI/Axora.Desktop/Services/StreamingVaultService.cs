using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// High-performance streaming vault encryption and decryption service.
/// Features zero-allocation buffer pooling (ArrayPool), hardware AES-NI GCM acceleration,
/// Argon2id memory hardening, TPM 2.0 / DPAPI hardware key sealing (DataProtectionProvider),
/// and cryptographically secure memory wiping.
///
/// File format: [16-byte Salt][12-byte Nonce][Block_0][Block_1]...[Block_N]
/// Block format: [4-byte LE Length][AES-256-GCM Ciphertext][16-byte Tag]
///
/// Edge-Case Hardening:
///   - 0-byte input files: encrypts to header-only format; progress reports 100% explicitly.
///   - Corrupted block headers: blockLen validation rejects malformed ciphertext.
///   - Failed decryption: output file deleted before re-throwing to prevent partial plaintext.
/// </summary>
public sealed class StreamingVaultService : ISecurityVaultService, IDisposable
{
    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int BlockSize = 1024 * 1024; // 1 MB streaming blocks
    private const int Argon2Memory = 65536;    // 64 MB RAM cost
    private const int Argon2Iterations = 3;
    private const int Argon2Parallelism = 1;

    private readonly ILogger<StreamingVaultService> _logger;
    private readonly string _sealedKeyPath;

    public StreamingVaultService(ILogger<StreamingVaultService> logger)
    {
        _logger = logger;
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Axora");
        Directory.CreateDirectory(appData);
        _sealedKeyPath = Path.Combine(appData, "vault_sealed.dat");
    }

    public bool IsMachineSealedKeyAvailable() => File.Exists(_sealedKeyPath);

    public async Task SealMasterKeyToMachineAsync(string password)
    {
        try
        {
            var provider = new DataProtectionProvider("LOCAL=user");
            var plainBuffer = CryptographicBuffer.ConvertStringToBinary(password, BinaryStringEncoding.Utf8);
            var protectedBuffer = await provider.ProtectAsync(plainBuffer);

            byte[] protectedBytes = new byte[protectedBuffer.Length];
            using (var reader = DataReader.FromBuffer(protectedBuffer))
            {
                reader.ReadBytes(protectedBytes);
            }

            await File.WriteAllBytesAsync(_sealedKeyPath, protectedBytes);
            _logger.LogInformation("Vault master key sealed to local machine TPM / DPAPI boundary.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to seal key with DataProtectionProvider");
        }
    }

    public async Task<string?> UnsealMasterKeyFromMachineAsync()
    {
        if (!File.Exists(_sealedKeyPath)) return null;

        try
        {
            byte[] protectedBytes = await File.ReadAllBytesAsync(_sealedKeyPath);
            var provider = new DataProtectionProvider();

            var dataWriter = new DataWriter();
            dataWriter.WriteBytes(protectedBytes);
            var protectedBuffer = dataWriter.DetachBuffer();

            var plainBuffer = await provider.UnprotectAsync(protectedBuffer);
            return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, plainBuffer);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unseal key from TPM / DPAPI");
            return null;
        }
    }

    public void ClearSealedKey()
    {
        if (File.Exists(_sealedKeyPath))
        {
            try { File.Delete(_sealedKeyPath); } catch { /* Ignore */ }
        }
    }

    /// <inheritdoc/>
    public async Task EncryptFileAsync(
        string inputPath,
        string outputPath,
        string password,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        await Task.Run(async () =>
        {
            var fileInfo = new FileInfo(inputPath);
            long totalBytes = fileInfo.Length;
            long processedBytes = 0;

            var salt = RandomNumberGenerator.GetBytes(SaltLength);
            var key = DeriveKey(password, salt);

            var pool = ArrayPool<byte>.Shared;
            byte[] inputBuffer  = pool.Rent(BlockSize);
            byte[] cipherBuffer = pool.Rent(BlockSize);
            byte[] tagBuffer    = pool.Rent(TagLength);

            try
            {
                using var inputStream  = new FileStream(inputPath,  FileMode.Open,   FileAccess.Read,  FileShare.Read,  BlockSize, useAsync: true);
                using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BlockSize, useAsync: true);

                await outputStream.WriteAsync(salt.AsMemory(), ct);

                var baseNonce = RandomNumberGenerator.GetBytes(NonceLength);
                await outputStream.WriteAsync(baseNonce.AsMemory(), ct);

                using var aes = new AesGcm(key, TagLength);
                uint blockIndex = 0;
                int bytesRead;

                while ((bytesRead = await inputStream.ReadAsync(inputBuffer.AsMemory(0, BlockSize), ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    var blockIv = DeriveBlockIv(baseNonce, blockIndex);

                    aes.Encrypt(
                        blockIv,
                        inputBuffer.AsSpan(0, bytesRead),
                        cipherBuffer.AsSpan(0, bytesRead),
                        tagBuffer.AsSpan(0, TagLength));

                    var lenBytes = BitConverter.GetBytes((uint)bytesRead);
                    await outputStream.WriteAsync(lenBytes.AsMemory(), ct);
                    await outputStream.WriteAsync(cipherBuffer.AsMemory(0, bytesRead), ct);
                    await outputStream.WriteAsync(tagBuffer.AsMemory(0, TagLength), ct);

                    processedBytes += bytesRead;
                    progress?.Report(totalBytes > 0 ? (double)processedBytes / totalBytes : 1.0);
                    blockIndex++;
                }

                await outputStream.FlushAsync(ct);

                // FIX W-6: Explicitly report 100% for 0-byte files where the loop never executes
                if (totalBytes == 0)
                    progress?.Report(1.0);

                _logger.LogInformation("Vault Encrypted {Input} -> {Output} ({Blocks} blocks)", inputPath, outputPath, blockIndex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(inputBuffer);
                CryptographicOperations.ZeroMemory(cipherBuffer);
                CryptographicOperations.ZeroMemory(tagBuffer);
                pool.Return(inputBuffer);
                pool.Return(cipherBuffer);
                pool.Return(tagBuffer);
            }
        }, ct);
    }

    /// <inheritdoc/>
    public async Task DecryptFileAsync(
        string inputPath,
        string outputPath,
        string password,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        await Task.Run(async () =>
        {
            var fileInfo = new FileInfo(inputPath);
            long totalBytes = fileInfo.Length;
            long processedBytes = SaltLength + NonceLength;

            // Guard: minimum valid vault file size is salt + nonce header
            if (totalBytes < SaltLength + NonceLength)
                throw new InvalidDataException("Vault file is too small to be a valid encrypted archive.");

            using var inputStream  = new FileStream(inputPath,  FileMode.Open,   FileAccess.Read,  FileShare.Read,  BlockSize, useAsync: true);
            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BlockSize, useAsync: true);

            var salt = new byte[SaltLength];
            await inputStream.ReadExactlyAsync(salt.AsMemory(), ct);

            var baseNonce = new byte[NonceLength];
            await inputStream.ReadExactlyAsync(baseNonce.AsMemory(), ct);

            var key = DeriveKey(password, salt);

            var pool = ArrayPool<byte>.Shared;
            byte[] cipherBuffer = pool.Rent(BlockSize);
            byte[] plainBuffer  = pool.Rent(BlockSize);
            byte[] tagBuffer    = pool.Rent(TagLength);
            byte[] lenBytes     = new byte[4];

            try
            {
                using var aes = new AesGcm(key, TagLength);
                uint blockIndex = 0;

                while (inputStream.Position < inputStream.Length)
                {
                    ct.ThrowIfCancellationRequested();

                    await inputStream.ReadExactlyAsync(lenBytes.AsMemory(), ct);
                    int blockLen = (int)BitConverter.ToUInt32(lenBytes, 0);

                    if (blockLen <= 0 || blockLen > BlockSize)
                        throw new InvalidDataException(
                            $"Vault file block header at offset {inputStream.Position - 4} is malformed (declared length: {blockLen}). File may be corrupted.");

                    await inputStream.ReadExactlyAsync(cipherBuffer.AsMemory(0, blockLen), ct);
                    await inputStream.ReadExactlyAsync(tagBuffer.AsMemory(0, TagLength), ct);

                    var blockIv = DeriveBlockIv(baseNonce, blockIndex);

                    aes.Decrypt(
                        blockIv,
                        cipherBuffer.AsSpan(0, blockLen),
                        tagBuffer.AsSpan(0, TagLength),
                        plainBuffer.AsSpan(0, blockLen));

                    await outputStream.WriteAsync(plainBuffer.AsMemory(0, blockLen), ct);

                    processedBytes += 4 + blockLen + TagLength;
                    progress?.Report(totalBytes > 0 ? (double)processedBytes / totalBytes : 1.0);
                    blockIndex++;
                }

                await outputStream.FlushAsync(ct);

                // Report full completion for 0-block (encrypted-empty-file) vaults
                if (blockIndex == 0)
                    progress?.Report(1.0);

                _logger.LogInformation("Vault Decrypted {Input} -> {Output} ({Blocks} blocks)", inputPath, outputPath, blockIndex);
            }
            catch (CryptographicException ex)
            {
                // Close stream before deleting to release file handle
                outputStream.Close();
                try { File.Delete(outputPath); } catch { /* Ignore */ }
                throw new InvalidOperationException(
                    "Decryption authentication failed: incorrect passphrase or modified ciphertext.", ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(cipherBuffer);
                CryptographicOperations.ZeroMemory(plainBuffer);
                CryptographicOperations.ZeroMemory(tagBuffer);
                pool.Return(cipherBuffer);
                pool.Return(plainBuffer);
                pool.Return(tagBuffer);
            }
        }, ct);
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt              = salt,
                MemorySize        = Argon2Memory,
                Iterations        = Argon2Iterations,
                DegreeOfParallelism = Argon2Parallelism
            };
            return argon2.GetBytes(32);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static byte[] DeriveBlockIv(byte[] baseNonce, uint blockIndex)
    {
        // Counter-mode IV: XOR last 4 bytes of baseNonce with big-endian block counter.
        // This guarantees each block uses a unique nonce under the same derived key.
        var iv = (byte[])baseNonce.Clone();
        var counterBytes = BitConverter.GetBytes(blockIndex);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);
        for (int i = 0; i < 4; i++)
            iv[NonceLength - 4 + i] ^= counterBytes[i];
        return iv;
    }

    public void Dispose() { }
}
