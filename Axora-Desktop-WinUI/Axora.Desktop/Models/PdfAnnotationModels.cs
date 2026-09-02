using System;
using System.Collections.Generic;

namespace Axora.Desktop.Models;

public enum AnnotationType
{
    Highlighter,
    InkFreehand,
    VectorText,
    DigitalSignature,
    BlackoutRedaction
}

public sealed class AnnotationPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class AnnotationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public AnnotationType Type { get; set; }
    public int PageIndex { get; set; } = 0;

    // Bounds on the PDF page coordinate space (points: 1/72 inch)
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    // Appearance
    public string ColorHex { get; set; } = "#FFFF00"; // Yellow default for highlighter
    public double Opacity { get; set; } = 0.5;
    public double StrokeThickness { get; set; } = 2.0;

    // Freehand stroke points
    public List<AnnotationPoint> StrokePoints { get; set; } = [];

    // Text & Signature metadata
    public string TextContent { get; set; } = string.Empty;
    public string FontSize { get; set; } = "14";
    public string SignerName { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RedactionRegion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int PageIndex { get; set; } = 0;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>DoD / Privacy reason code printed over redaction (e.g. "[PII REDACTED]", "[CONFIDENTIAL]").</summary>
    public string ExemptionCode { get; set; } = "[REDACTED]";

    /// <summary>True once text and raster underlay have been permanently purged from the document object stream.</summary>
    public bool IsPermanentPurged { get; set; }
}

public sealed class RedactionExportOptions
{
    public int RasterDpi { get; set; } = 150;
    public bool FlattenVectorAnnotations { get; set; } = true;
    public bool BurnBlackoutBoxes { get; set; } = true;
    public bool StripMetadataAndXmp { get; set; } = true;
    public string ComplianceStandard { get; set; } = "DoD 5220.22-M / HIPAA Compliant";
}
