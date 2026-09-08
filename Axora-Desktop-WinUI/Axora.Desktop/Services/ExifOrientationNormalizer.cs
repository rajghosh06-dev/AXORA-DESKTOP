using System;
using SkiaSharp;

namespace Axora.Desktop.Services;

/// <summary>
/// Normalizes EXIF orientation tags (1 through 8) by applying physical pixel transforms
/// (rotations and reflections) to ensure decoded image buffers are visually upright.
/// </summary>
public static class ExifOrientationNormalizer
{
    /// <summary>
    /// Determines whether the specified EXIF orientation transposes the width and height dimensions.
    /// Orientations 5 (LeftTop), 6 (RightTop), 7 (RightBottom), and 8 (LeftBottom) swap X and Y axes.
    /// </summary>
    public static bool IsAxesSwapped(SKEncodedOrigin origin) => origin switch
    {
        SKEncodedOrigin.LeftTop or
        SKEncodedOrigin.RightTop or
        SKEncodedOrigin.RightBottom or
        SKEncodedOrigin.LeftBottom => true,
        _ => false
    };

    /// <summary>
    /// Maps an EXIF orientation value (1–8) into an upright SKBitmap.
    /// If origin is TopLeft (1) or unspecified (0), returns a copy of the source bitmap.
    /// </summary>
    public static SKBitmap NormalizeOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (origin is SKEncodedOrigin.TopLeft or (SKEncodedOrigin)0)
        {
            return source.Copy();
        }

        int w = source.Width;
        int h = source.Height;
        bool swapped = IsAxesSwapped(origin);
        int targetW = swapped ? h : w;
        int targetH = swapped ? w : h;

        var targetInfo = new SKImageInfo(targetW, targetH, source.ColorType, source.AlphaType);
        var targetBitmap = new SKBitmap(targetInfo);
        using var canvas = new SKCanvas(targetBitmap);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // 2: Mirror horizontal (flip X)
                canvas.Translate(targetW, 0);
                canvas.Scale(-1, 1);
                canvas.DrawBitmap(source, 0, 0);
                break;

            case SKEncodedOrigin.BottomRight: // 3: Rotate 180 degrees
                canvas.Translate(targetW, targetH);
                canvas.RotateDegrees(180);
                canvas.DrawBitmap(source, 0, 0);
                break;

            case SKEncodedOrigin.BottomLeft: // 4: Mirror vertical (flip Y)
                canvas.Translate(0, targetH);
                canvas.Scale(1, -1);
                canvas.DrawBitmap(source, 0, 0);
                break;

            case SKEncodedOrigin.LeftTop: // 5: Transpose (swap X and Y: x' = y, y' = x)
                var matrix5 = new SKMatrix
                {
                    ScaleX = 0, SkewX = 1, TransX = 0,
                    SkewY = 1, ScaleY = 0, TransY = 0,
                    Persp2 = 1
                };
                canvas.SetMatrix(matrix5);
                canvas.DrawBitmap(source, 0, 0);
                break;

            case SKEncodedOrigin.RightTop: // 6: Rotate 90 CW
                canvas.Translate(targetW, 0);
                canvas.RotateDegrees(90);
                canvas.DrawBitmap(source, 0, 0);
                break;

            case SKEncodedOrigin.RightBottom: // 7: Transverse (rotate 90 CW and flip horizontal)
                var matrix7 = new SKMatrix
                {
                    ScaleX = 0, SkewX = -1, TransX = targetW,
                    SkewY = -1, ScaleY = 0, TransY = targetH,
                    Persp2 = 1
                };
                canvas.SetMatrix(matrix7);
                canvas.DrawBitmap(source, 0, 0);
                break;

            case SKEncodedOrigin.LeftBottom: // 8: Rotate 270 CW (90 CCW)
                canvas.Translate(0, targetH);
                canvas.RotateDegrees(270);
                canvas.DrawBitmap(source, 0, 0);
                break;

            default:
                canvas.DrawBitmap(source, 0, 0);
                break;
        }

        canvas.Flush();
        return targetBitmap;
    }
}
