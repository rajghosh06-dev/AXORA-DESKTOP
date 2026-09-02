using System;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for the local P2P sync service that pairs with Axora Mobile
/// via ECDH P-256 key exchange and AES-256-GCM binary-framed WebSocket protocol.
/// </summary>
public interface IP2pSyncService
{
    /// <summary>Gets the JSON QR pairing payload string once the server is running.</summary>
    string PairingQrJson { get; }

    /// <summary>Whether the TCP listener is actively accepting connections.</summary>
    bool IsRunning { get; }

    /// <summary>Count of currently connected and active mobile peers.</summary>
    int ConnectedDeviceCount { get; }

    /// <summary>Fires when a mobile device completes ECDH pairing and is authenticated.</summary>
    event EventHandler<AxoraDevice>? DeviceConnected;

    /// <summary>Fires when an authenticated device disconnects.</summary>
    event EventHandler<AxoraDevice>? DeviceDisconnected;

    /// <summary>Fires when a QuickDrop file transfer is completed and saved locally.</summary>
    event EventHandler<QuickDropItem>? FileReceived;

    /// <summary>Starts the TCP listener and begins advertising via mDNS.</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Gracefully shuts down all connections and the listener.</summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Encrypts and broadcasts raw bytes to all connected peers.</summary>
    Task BroadcastAsync(byte[] payload, CancellationToken ct = default);
}
