namespace Axora.Desktop.Models;

/// <summary>
/// Represents a paired or discovered Axora peer device (mobile or desktop).
/// The JSON schema mirrors the QR pairing payload:
/// {"ip":"...","port":...,"token":"...","pubkey":"...","service":"Axora"}
/// </summary>
public sealed class AxoraDevice
{
    /// <summary>Unique session identifier for this pairing instance.</summary>
    public string DeviceId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Human-readable display name (e.g. "Raj's Pixel 9 Pro").</summary>
    public string DisplayName { get; set; } = "Unknown Device";

    /// <summary>LAN IPv4 address of the peer.</summary>
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>TCP port the peer is listening on.</summary>
    public int Port { get; init; }

    /// <summary>Base64-encoded NIST P-256 ECDH public key for handshake.</summary>
    public string PublicKeyBase64 { get; init; } = string.Empty;

    /// <summary>One-time pairing token (32-byte hex string).</summary>
    public string PairingToken { get; init; } = string.Empty;

    /// <summary>Service tag — always "Axora" for protocol parity checks.</summary>
    public string ServiceTag { get; init; } = "Axora";

    /// <summary>Whether the device is currently in an active WebSocket session.</summary>
    public bool IsConnected { get; set; }

    /// <summary>UTC timestamp of last successful data exchange.</summary>
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Derived AES-256 session key after ECDH handshake (in-memory only, never persisted).</summary>
    public byte[]? SessionKey { get; set; }
}
