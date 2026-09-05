namespace Axora.Desktop.Models;

/// <summary>
/// Policy governing EXIF, document author, GPS, and timestamp metadata during conversion.
/// </summary>
public enum MetadataHandling
{
    /// <summary>Retain all metadata in output file where format permits.</summary>
    Preserve,

    /// <summary>Strip privacy-sensitive metadata (GPS, author, hardware details).</summary>
    Strip
}
