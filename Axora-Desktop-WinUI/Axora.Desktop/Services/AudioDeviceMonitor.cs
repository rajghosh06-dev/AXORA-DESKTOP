using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Monitors microphone audio capture endpoints and Windows microphone privacy permissions.
/// Gracefully handles permission denial (0x80070005) and missing audio endpoints without crashing.
/// </summary>
public sealed class AudioDeviceMonitor : IAudioDeviceMonitor
{
    private readonly ILogger<AudioDeviceMonitor>? _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _hasMicrophone;
    private bool _isPermissionGranted = true;
    private string? _defaultDeviceName;
    private AudioCaptureHealth _currentHealth = AudioCaptureHealth.Healthy;
    private DeviceWatcher? _deviceWatcher;
    private int _disposed;

    public bool HasMicrophone => _hasMicrophone;
    public bool IsPermissionGranted => _isPermissionGranted;
    public string? DefaultCaptureDeviceName => _defaultDeviceName;
    public AudioCaptureHealth CurrentHealth => _currentHealth;

    public event EventHandler<AudioDeviceStatusChangedEventArgs>? DeviceStatusChanged;

    public AudioDeviceMonitor(ILogger<AudioDeviceMonitor>? logger = null)
    {
        _logger = logger;
        InitializeWatcher();
    }

    private void InitializeWatcher()
    {
        try
        {
            _deviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.AudioCapture);
            _deviceWatcher.Added += OnDeviceAdded;
            _deviceWatcher.Removed += OnDeviceRemoved;
            _deviceWatcher.Updated += OnDeviceUpdated;
            _deviceWatcher.Start();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize audio capture DeviceWatcher. Falling back to polling.");
        }
    }

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args) => QueueRefresh();
    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args) => QueueRefresh();
    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args) => QueueRefresh();
    private void QueueRefresh()
    {
        if (Volatile.Read(ref _disposed) == 0) _ = RefreshStatusAsync();
    }

    public async Task RefreshStatusAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        await _refreshLock.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            try
            {
                var devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
                if (devices.Count == 0)
                {
                    _hasMicrophone = false;
                    _defaultDeviceName = null;
                    _currentHealth = AudioCaptureHealth.NoMicrophoneDetected;
                    _logger?.LogInformation("No microphone detected on the system.");
                }
                else
                {
                    _hasMicrophone = true;
                    _isPermissionGranted = true;
                    string defaultId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);
                    var defaultDev = devices.FirstOrDefault(d => d.Id == defaultId) ?? devices[0];
                    _defaultDeviceName = defaultDev.Name;
                    _currentHealth = AudioCaptureHealth.Healthy;
                    _logger?.LogInformation("Microphone available: {DeviceName}", _defaultDeviceName);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _isPermissionGranted = false;
                _currentHealth = AudioCaptureHealth.PermissionDenied;
                _logger?.LogWarning(ex, "Microphone access denied by Windows Privacy Settings (0x80070005).");
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80070005)
            {
                _isPermissionGranted = false;
                _currentHealth = AudioCaptureHealth.PermissionDenied;
                _logger?.LogWarning(ex, "Microphone access denied (0x80070005).");
            }
            catch (Exception ex)
            {
                _currentHealth = AudioCaptureHealth.RecognitionUnavailable;
                _logger?.LogWarning(ex, "Unexpected error inspecting audio capture devices.");
            }

            if (Volatile.Read(ref _disposed) == 0)
                DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusChangedEventArgs(_currentHealth, _defaultDeviceName));
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            if (_deviceWatcher != null)
            {
                _deviceWatcher.Added -= OnDeviceAdded;
                _deviceWatcher.Removed -= OnDeviceRemoved;
                _deviceWatcher.Updated -= OnDeviceUpdated;
                _deviceWatcher.Stop();
                _deviceWatcher = null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error stopping DeviceWatcher.");
        }
        // An in-flight WinRT device query may still hold this managed semaphore.
        // Disposing it here would race its finally/release path.
    }
}
