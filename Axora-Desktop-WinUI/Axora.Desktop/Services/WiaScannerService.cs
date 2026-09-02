using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// WIA (Windows Image Acquisition) scanner service using COM interop.
/// Enumerates connected flatbed scanners and captures raw JPEG image streams.
///
/// COM Safety: All dynamic COM objects are released via Marshal.ReleaseComObject in finally
/// blocks to prevent scanner driver handle leaks and WIA COM server deadlocks.
/// Temp files are always cleaned up in finally guards regardless of transfer success/failure.
/// </summary>
public sealed class WiaScannerService : IScannerService
{
    private readonly ILogger<WiaScannerService> _logger;

    // WIA Property IDs (from WIA Automation spec)
    private const int WIA_IPS_XRES = 6147;            // Horizontal DPI
    private const int WIA_IPS_YRES = 6148;            // Vertical DPI
    private const int WIA_IPS_PHOTOMETRIC_INTERP = 6146; // Color intent

    public WiaScannerService(ILogger<WiaScannerService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetConnectedScannersAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var scanners = new List<string>();
            dynamic? deviceManager = null;

            try
            {
                var deviceManagerType = Type.GetTypeFromProgID("WIA.DeviceManager");
                if (deviceManagerType is null)
                {
                    _logger.LogWarning("WIA DeviceManager COM class not registered on this system.");
                    return (IReadOnlyList<string>)scanners;
                }

                deviceManager = Activator.CreateInstance(deviceManagerType)!;

                // Enumerate via local variable so we can release the deviceInfos COM object
                var deviceInfos = deviceManager.DeviceInfos;
                try
                {
                    foreach (dynamic info in deviceInfos)
                    {
                        dynamic? capturedInfo = info;
                        try
                        {
                            // WiaDeviceType.ScannerDeviceType = 1
                            if ((int)capturedInfo.Type == 1)
                            {
                                scanners.Add((string)capturedInfo.Properties["Name"].Value);
                            }
                        }
                        finally
                        {
                            if (capturedInfo is not null)
                                Marshal.ReleaseComObject(capturedInfo);
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(deviceInfos);
                }
            }
            catch (COMException ex)
            {
                _logger.LogError(ex, "COM error enumerating WIA devices.");
            }
            finally
            {
                if (deviceManager is not null)
                    Marshal.ReleaseComObject(deviceManager);
            }

            return (IReadOnlyList<string>)scanners;
        }, ct);
    }

    /// <inheritdoc/>
    public async Task<Stream> CaptureAsync(
        string deviceName,
        int dpi = 300,
        ScanColorMode colorMode = ScanColorMode.Color,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            dynamic? deviceManager = null;
            dynamic? targetDevice = null;
            dynamic? scannedImage = null;
            string? tempPath = null;

            try
            {
                var deviceManagerType = Type.GetTypeFromProgID("WIA.DeviceManager")
                    ?? throw new InvalidOperationException("WIA not available on this system.");
                deviceManager = Activator.CreateInstance(deviceManagerType)!;

                // Locate the named scanner — release each info COM object
                var deviceInfos = deviceManager.DeviceInfos;
                try
                {
                    foreach (dynamic info in deviceInfos)
                    {
                        dynamic? capturedInfo = info;
                        try
                        {
                            if ((int)capturedInfo.Type == 1 &&
                                string.Equals((string)capturedInfo.Properties["Name"].Value, deviceName,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                targetDevice = capturedInfo.Connect();
                                break;
                            }
                        }
                        finally
                        {
                            // Only release info if we didn't connect from it (connect returns a different object)
                            if (capturedInfo is not null && targetDevice is null)
                                Marshal.ReleaseComObject(capturedInfo);
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(deviceInfos);
                }

                if (targetDevice is null)
                    throw new InvalidOperationException($"Scanner '{deviceName}' not found or not connected.");

                var scannerItem = targetDevice.Items[1];

                // Set DPI and color mode
                SetWiaProperty(scannerItem.Properties, WIA_IPS_XRES, dpi);
                SetWiaProperty(scannerItem.Properties, WIA_IPS_YRES, dpi);
                SetWiaProperty(scannerItem.Properties, WIA_IPS_PHOTOMETRIC_INTERP, (int)colorMode);

                // WIA FormatID for JPEG: {B96B3CAE-0728-11D3-9D7B-0000F81EF32E}
                const string wiaFormatJpeg = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
                scannedImage = scannerItem.Transfer(wiaFormatJpeg);

                // Save to temp path, read into MemoryStream — temp file always cleaned up in finally
                tempPath = Path.GetTempFileName() + ".jpg";
                scannedImage.SaveFile(tempPath);

                var memStream = new MemoryStream(File.ReadAllBytes(tempPath));
                _logger.LogInformation("WIA scan complete: {DPI} DPI, color mode {Mode}", dpi, colorMode);
                return (Stream)memStream;
            }
            catch (COMException ex)
            {
                _logger.LogError(ex, "WIA COM error during scan capture.");
                throw new InvalidOperationException($"Scanner error: {ex.Message}", ex);
            }
            finally
            {
                // Always clean up temp file regardless of success or failure
                if (tempPath is not null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* Best-effort cleanup */ }
                }

                // Release COM objects in reverse acquisition order
                if (scannedImage is not null)
                    Marshal.ReleaseComObject(scannedImage);
                if (targetDevice is not null)
                    Marshal.ReleaseComObject(targetDevice);
                if (deviceManager is not null)
                    Marshal.ReleaseComObject(deviceManager);
            }
        }, ct);
    }

    private static void SetWiaProperty(dynamic properties, int propId, int value)
    {
        foreach (dynamic prop in properties)
        {
            if ((int)prop.PropertyID == propId)
            {
                prop.Value = value;
                return;
            }
        }
    }
}
