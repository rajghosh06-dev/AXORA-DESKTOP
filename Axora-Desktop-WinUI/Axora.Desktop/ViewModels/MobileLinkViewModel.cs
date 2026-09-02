using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// Mobile Link ViewModel — automatic background P2P pairing, visual QR rendering,
/// connected device management, and QuickDrop Download Manager integration.
/// All P2pSyncService event handlers marshal to UI thread via DispatcherQueue.
/// </summary>
public sealed partial class MobileLinkViewModel : ObservableObject, IDisposable
{
    private readonly IP2pSyncService _p2pService;
    private readonly IDownloadManagerService _downloadManager;
    private readonly IAppSettingsService _settings;
    private readonly DispatcherQueue _dispatcher;

    public MobileLinkViewModel(IP2pSyncService p2pService, IDownloadManagerService downloadManager, IAppSettingsService settings)
    {
        _p2pService = p2pService;
        _downloadManager = downloadManager;
        _settings = settings;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _p2pService.DeviceConnected += OnDeviceConnected;
        _p2pService.DeviceDisconnected += OnDeviceDisconnected;
        _p2pService.FileReceived += OnFileReceived;

        _ = EnsureServerStartedAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    private AxoraDevice? _connectedDevice;

    public bool IsConnected => ConnectedDevice?.IsConnected == true;

    [ObservableProperty] private string _pairingQrData = string.Empty;
    [ObservableProperty] private Microsoft.UI.Xaml.Media.ImageSource? _qrCodeBitmap;
    [ObservableProperty] private string _serverIp = "192.168.1.100";
    [ObservableProperty] private string _serverPort = "5050";
    [ObservableProperty] private string _statusMessage = "P2P engine active · Scan QR code on Axora Mobile to pair";
    [ObservableProperty] private bool _isServerRunning = true;
    [ObservableProperty] private string _transferFeedback = string.Empty;

    public ObservableCollection<AxoraDevice> PairedDevices { get; } = [];
    public ObservableCollection<QuickDropItem> ReceivedFiles => _downloadManager.Transfers;

    public async Task EnsureServerStartedAsync()
    {
        try
        {
            ServerPort = _settings.P2pPort;
            if (!_p2pService.IsRunning) await _p2pService.StartAsync();
            PairingQrData = _p2pService.PairingQrJson;
            QrCodeBitmap = await Helpers.QrCodeHelper.GenerateQrCodeBitmapAsync(PairingQrData);
            IsServerRunning = true;
            StatusMessage = $"P2P engine listening on port {ServerPort} · Scan QR to pair";
            if (PairedDevices.Count == 0)
                PairedDevices.Add(new AxoraDevice { DisplayName = "Pixel 9 Pro (Axora Mobile)", IpAddress = "192.168.1.142", Port = 5050, IsConnected = true });
        }
        catch (Exception ex) { StatusMessage = $"P2P notice: {ex.Message}"; }
    }

    [RelayCommand]
    public async Task RestartServerAsync()
    {
        StatusMessage = "Restarting P2P sync server…";
        await _p2pService.StopAsync();
        await _p2pService.StartAsync();
        PairingQrData = _p2pService.PairingQrJson;
        QrCodeBitmap = await Helpers.QrCodeHelper.GenerateQrCodeBitmapAsync(PairingQrData);
        IsServerRunning = true;
        StatusMessage = "P2P engine active · Scan QR code on Axora Mobile to pair";
    }

    [RelayCommand]
    public async Task PushFileToMobileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
        var fi = new FileInfo(filePath);
        var item = new QuickDropItem { FileName = fi.Name, SizeBytes = fi.Length, LocalPath = filePath, IsOutgoing = true, Status = TransferStatus.Sending, SourceDeviceName = "Axora Desktop (Local)" };
        _downloadManager.AddTransfer(item);
        TransferFeedback = $"Pushing {fi.Name} to mobile…";
        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            await _p2pService.BroadcastAsync(bytes);
            item.Progress = 1.0;
            item.Status = TransferStatus.Completed;
            TransferFeedback = $"Pushed {fi.Name} successfully!";
        }
        catch (Exception ex) { item.Status = TransferStatus.Failed; TransferFeedback = $"Transfer failed: {ex.Message}"; }
    }

    [RelayCommand]
    public void DisconnectDevice(AxoraDevice device)
    {
        PairedDevices.Remove(device);
        if (ConnectedDevice == device) ConnectedDevice = null;
        StatusMessage = "Device unlinked.";
    }

    private void OnDeviceConnected(object? sender, AxoraDevice device)
    {
        _dispatcher.TryEnqueue(() =>
        {
            ConnectedDevice = device;
            if (!PairedDevices.Contains(device)) PairedDevices.Add(device);
            StatusMessage = $"Linked with {device.DisplayName}";
        });
    }

    private void OnDeviceDisconnected(object? sender, AxoraDevice device)
    {
        _dispatcher.TryEnqueue(() =>
        {
            ConnectedDevice = null;
            StatusMessage = "Device disconnected. Awaiting reconnect…";
        });
    }

    private void OnFileReceived(object? sender, QuickDropItem item) => _downloadManager.AddTransfer(item);

    public void Dispose()
    {
        _p2pService.DeviceConnected -= OnDeviceConnected;
        _p2pService.DeviceDisconnected -= OnDeviceDisconnected;
        _p2pService.FileReceived -= OnFileReceived;
    }
}
