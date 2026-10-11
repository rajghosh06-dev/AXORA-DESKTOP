using System.Globalization;
using Axora.Studio.Models;
using Axora.Studio.Models.ResumeRendering;

namespace Axora.Studio.Services.ResumeRendering;

public static class ResumeRenderFormatting
{
    public static ResumeResolvedFormatting Resolve(ResumeRenderSnapshot snapshot,
        ResumeRenderFontChoice fontChoice = ResumeRenderFontChoice.HonorSavedPreference)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!Enum.IsDefined(fontChoice)) throw new ResumeRenderException(ResumeRenderFailure.InvalidFormattingSelection, "InvalidFontChoice");
        if (fontChoice == ResumeRenderFontChoice.HonorSavedPreference)
            throw new ResumeRenderException(ResumeRenderFailure.UnsupportedFormattingPreference, "FontUnavailable");
        var f = snapshot.Document.Formatting;
        int? target = f.TargetLength switch
        {
            ResumeTargetLength.OnePage => 1, ResumeTargetLength.TwoPages => 2, ResumeTargetLength.ThreePages => 3,
            ResumeTargetLength.FourPlusPages => null, _ => throw new InvalidOperationException("Validated target required.")
        };
        (double body, double leading, double gap) = f.SpacingMode switch
        {
            ResumeDensity.Compact => (10, .55, .35), ResumeDensity.Standard => (11, .65, .5),
            ResumeDensity.Relaxed => (12, .8, .7), _ => throw new InvalidOperationException("Validated density required.")
        };
        return new(fontChoice, f.FontFamily, "Libertinus Serif", true, target, f.SpacingMode, body, leading, gap,
            f.MarginInches, f.ShowDividers, f.CenterHeader, f.UppercaseSectionTitles,
            byte.Parse(f.AccentHexColor.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(f.AccentHexColor.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(f.AccentHexColor.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
