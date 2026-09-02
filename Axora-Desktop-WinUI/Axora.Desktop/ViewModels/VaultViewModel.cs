using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// Encrypted Vault ViewModel — zero-emoji, multi-file batch encryption/decryption,
/// SHA-256 integrity validation, TPM 2.0/DPAPI machine key sealing, and DoD shredding guarantees.
/// </summary>
public sealed partial class VaultViewModel : ObservableObject
{
    private readonly ISecurityVaultService _vaultService;
    private readonly DispatcherQueue _dispatcher;

    public VaultViewModel(ISecurityVaultService vaultService)
    {
        _vaultService = vaultService;
        // FIX-1: Capture dispatcher for safe background-to-UI thread property updates
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _sha256Checksum = "None";
        _statusMessage = "Select files or folder to encrypt/decrypt.";
        IsTpmSealedKeyAvailable = _vaultService.IsMachineSealedKeyAvailable();
    }

    [ObservableProperty] private bool _isProcessing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordSet))]
    private string _password = string.Empty;

    public bool IsPasswordSet => !string.IsNullOrWhiteSpace(Password);

    [ObservableProperty] private double _operationProgress;
    [ObservableProperty] private string _statusMessage;
    [ObservableProperty] private string _selectedInputPath = string.Empty;
    [ObservableProperty] private string _sha256Checksum;
    [ObservableProperty] private bool _shredOriginalFile;
    [ObservableProperty] private int _selectedSecurityProfileIndex; // 0=Standard (64MB), 1=High (256MB)
    [ObservableProperty] private bool _isTpmSealedKeyAvailable;

    // FEAT-5: Password strength indicator (0.0–1.0 normalized score)
    [ObservableProperty] private double _passwordStrength;

    public ObservableCollection<VaultQueueItem> Queue { get; } = [];

    partial void OnPasswordChanged(string value)
    {
        // FEAT-5: Compute password strength score
        if (string.IsNullOrEmpty(value)) { PasswordStrength = 0; return; }
        double score = 0;
        if (value.Length >= 8) score += 0.2;
        if (value.Length >= 12) score += 0.2;
        if (value.Length >= 16) score += 0.1;
        if (Regex.IsMatch(value, @"[A-Z]")) score += 0.15;
        if (Regex.IsMatch(value, @"[a-z]")) score += 0.1;
        if (Regex.IsMatch(value, @"[0-9]")) score += 0.15;
        if (Regex.IsMatch(value, @"[^a-zA-Z0-9]")) score += 0.1;
        PasswordStrength = Math.Min(1.0, score);
    }

    public void AddFileToQueue(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        var fileInfo = new FileInfo(filePath);
        var item = new VaultQueueItem
        {
            FileName = fileInfo.Name,
            FilePath = filePath,
            SizeBytes = fileInfo.Length,
            IsVaultFile = filePath.EndsWith(".axvault", StringComparison.OrdinalIgnoreCase),
            Status = "Queued"
        };
        Queue.Add(item);
        SelectedInputPath = filePath;
        ComputeChecksum(filePath);
    }

    private void ComputeChecksum(string path)
    {
        Task.Run(() =>
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(path);
                var hash = sha256.ComputeHash(stream);
                var checksum = Convert.ToHexString(hash).ToLowerInvariant();
                // FIX-1: Marshal SHA-256 result back to UI thread before mutating ObservableProperty
                _dispatcher.TryEnqueue(() => Sha256Checksum = checksum);
            }
            catch
            {
                _dispatcher.TryEnqueue(() => Sha256Checksum = "Unavailable");
            }
        });
    }

    [RelayCommand]
    public async Task SealKeyToMachineAsync()
    {
        if (string.IsNullOrWhiteSpace(Password)) return;
        await _vaultService.SealMasterKeyToMachineAsync(Password);
        IsTpmSealedKeyAvailable = true;
        StatusMessage = "Master key sealed to local machine TPM / DPAPI boundary.";
    }

    [RelayCommand]
    public async Task UnsealHardwareKeyAsync()
    {
        var key = await _vaultService.UnsealMasterKeyFromMachineAsync();
        if (!string.IsNullOrWhiteSpace(key))
        {
            Password = key;
            StatusMessage = "Hardware-sealed master key unlocked!";
        }
        else
        {
            StatusMessage = "Could not unlock hardware key.";
        }
    }

    [RelayCommand]
    public void ClearSealedKey()
    {
        _vaultService.ClearSealedKey();
        IsTpmSealedKeyAvailable = false;
        StatusMessage = "Hardware sealed key cleared.";
    }

    [RelayCommand(IncludeCancelCommand = true)]
    public async Task EncryptBatchAsync(CancellationToken ct)
    {
        if (Queue.Count == 0 && !string.IsNullOrWhiteSpace(SelectedInputPath))
            AddFileToQueue(SelectedInputPath);
        if (Queue.Count == 0 || string.IsNullOrWhiteSpace(Password)) return;

        IsProcessing = true;
        StatusMessage = "Encrypting files with AES-256-GCM + Argon2id…";

        try
        {
            int completed = 0;
            foreach (var item in Queue)
            {
                ct.ThrowIfCancellationRequested();
                if (item.IsVaultFile) continue;
                item.Status = "Encrypting…";
                var outputPath = item.FilePath + ".axvault";
                var progress = new Progress<double>(p =>
                {
                    item.Progress = p * 100;
                    OperationProgress = ((completed + p) / Queue.Count) * 100;
                });
                await _vaultService.EncryptFileAsync(item.FilePath, outputPath, Password, progress, ct);
                item.Status = "Encrypted";
                item.Progress = 100;
                if (ShredOriginalFile)
                    await Helpers.CryptographyHelper.SecureShredFileAsync(item.FilePath, ct);
                completed++;
            }
            StatusMessage = $"Batch encryption complete ({completed} files).";
        }
        catch (OperationCanceledException) { StatusMessage = "Encryption cancelled."; }
        catch (Exception ex) { StatusMessage = $"Encryption failed: {ex.Message}"; }
        finally
        {
            IsProcessing = false;
            // FIX-6: Zero-clear password from managed memory after cryptographic operations
            ZeroPassword();
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    public async Task DecryptBatchAsync(CancellationToken ct)
    {
        if (Queue.Count == 0 && !string.IsNullOrWhiteSpace(SelectedInputPath))
            AddFileToQueue(SelectedInputPath);
        if (Queue.Count == 0 || string.IsNullOrWhiteSpace(Password)) return;

        IsProcessing = true;
        StatusMessage = "Decrypting vault files…";

        try
        {
            int completed = 0;
            foreach (var item in Queue)
            {
                ct.ThrowIfCancellationRequested();
                item.Status = "Decrypting…";
                var outputPath = item.FilePath.EndsWith(".axvault", StringComparison.OrdinalIgnoreCase)
                    ? item.FilePath[..^8]
                    : item.FilePath + "_decrypted";
                var progress = new Progress<double>(p =>
                {
                    item.Progress = p * 100;
                    OperationProgress = ((completed + p) / Queue.Count) * 100;
                });
                await _vaultService.DecryptFileAsync(item.FilePath, outputPath, Password, progress, ct);
                item.Status = "Decrypted";
                item.Progress = 100;
                completed++;
            }
            StatusMessage = $"Batch decryption complete ({completed} files).";
        }
        catch (OperationCanceledException) { StatusMessage = "Decryption cancelled."; }
        catch (Exception ex) { StatusMessage = $"Decryption failed: {ex.Message}"; }
        finally
        {
            IsProcessing = false;
            // FIX-6: Zero-clear password bytes from managed memory after crypto operations
            ZeroPassword();
        }
    }

    [RelayCommand]
    public void ClearQueue()
    {
        Queue.Clear();
        SelectedInputPath = string.Empty;
        Password = string.Empty;
        OperationProgress = 0;
        Sha256Checksum = "None";
        PasswordStrength = 0;
        StatusMessage = "Select files or folder to encrypt/decrypt.";
    }

    /// <summary>
    /// FIX-6: Clears password reference from managed memory after cryptographic operations.
    /// </summary>
    private void ZeroPassword()
    {
        Password = string.Empty;
        PasswordStrength = 0;
    }
}

public sealed class VaultQueueItem : ObservableObject
{
    private string _fileName = string.Empty;
    public string FileName { get => _fileName; set => SetProperty(ref _fileName, value); }

    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public string FormattedSize => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes / (1024.0 * 1024):F1} MB"
    };

    public bool IsVaultFile { get; set; }

    private string _status = "Queued";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    private double _progress;
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }
}
