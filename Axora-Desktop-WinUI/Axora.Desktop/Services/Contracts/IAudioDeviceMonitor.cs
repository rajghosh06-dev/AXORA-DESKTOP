using System;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models.Voice;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Monitors microphone presence, audio endpoint changes, and Windows microphone privacy permissions.
/// </summary>
public interface IAudioDeviceMonitor : IDisposable
{
    bool HasMicrophone { get; }
    bool IsPermissionGranted { get; }
    string? DefaultCaptureDeviceName { get; }
    AudioCaptureHealth CurrentHealth { get; }

    Task RefreshStatusAsync(CancellationToken ct = default);
    event EventHandler<AudioDeviceStatusChangedEventArgs>? DeviceStatusChanged;
}
