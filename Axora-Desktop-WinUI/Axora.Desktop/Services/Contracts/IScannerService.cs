namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for hardware document scanner acquisition via the Windows Image Acquisition (WIA) COM API.
/// </summary>
public interface IScannerService
{
    /// <summary>Returns display names of all connected WIA-compatible flatbed scanners.</summary>
    Task<IReadOnlyList<string>> GetConnectedScannersAsync(CancellationToken ct = default);

    /// <summary>
    /// Captures a scan from the specified device at the requested DPI.
    /// </summary>
    /// <param name="deviceName">Device display name from <see cref="GetConnectedScannersAsync"/>.</param>
    /// <param name="dpi">Resolution (100, 200, 300, or 600 recommended).</param>
    /// <param name="colorMode">Scan color mode.</param>
    /// <returns>MemoryStream containing the raw JPEG image data.</returns>
    Task<Stream> CaptureAsync(
        string deviceName,
        int dpi = 300,
        ScanColorMode colorMode = ScanColorMode.Color,
        CancellationToken ct = default);
}

public enum ScanColorMode
{
    Color = 1,
    Grayscale = 2,
    BlackAndWhite = 4
}
