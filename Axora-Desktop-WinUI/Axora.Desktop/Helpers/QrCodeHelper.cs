using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using Windows.Storage.Streams;

namespace Axora.Desktop.Helpers;

/// <summary>
/// Generates crisp, offline QR Code Bitmaps for zero-cloud peer device pairing.
/// </summary>
public static class QrCodeHelper
{
    public static async Task<BitmapImage?> GenerateQrCodeBitmapAsync(string plainText, int pixelsPerModule = 10)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return null;

        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(plainText, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(pixelsPerModule);

            var bitmapImage = new BitmapImage();
            using var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(qrCodeBytes);
            await writer.StoreAsync();
            stream.Seek(0);
            await bitmapImage.SetSourceAsync(stream);
            return bitmapImage;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrCodeHelper] Error generating QR code: {ex.Message}");
            return null;
        }
    }
}
