using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Axora.Desktop.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility v && v == Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility v && v != Visibility.Visible;
}

/// <summary>
/// Converts an integer ATS match score to a SolidColorBrush:
/// ≥ 80 → Green (#4CAF50), ≥ 60 → Amber (#FF9800), &lt; 60 → Red (#E53935)
/// </summary>
public sealed class ScoreToColorBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush  = new(Color.FromArgb(255, 76,  175,  80));
    private static readonly SolidColorBrush AmberBrush  = new(Color.FromArgb(255, 255, 152,   0));
    private static readonly SolidColorBrush RedBrush    = new(Color.FromArgb(255, 229,  57,  53));
    private static readonly SolidColorBrush NeutralBrush= new(Color.FromArgb(255, 150, 150, 150));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int score)
        {
            return score == 0  ? NeutralBrush
                 : score >= 80 ? GreenBrush
                 : score >= 60 ? AmberBrush
                 :               RedBrush;
        }
        return NeutralBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a double page-budget percentage (0–120+) to a progress-bar foreground colour.
/// </summary>
public sealed class BudgetToColorBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromArgb(255,  76, 175,  80));
    private static readonly SolidColorBrush AmberBrush = new(Color.FromArgb(255, 255, 152,   0));
    private static readonly SolidColorBrush RedBrush   = new(Color.FromArgb(255, 229,  57,  53));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
            return d <= 90 ? GreenBrush : d <= 100 ? AmberBrush : RedBrush;
        return GreenBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a normalized password strength (0.0–1.0) to a colored SolidColorBrush:
/// Red (&lt;0.4), Amber (0.4–0.7), Green (≥0.7).
/// </summary>
public sealed class StrengthToColorBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush RedBrush    = new(Color.FromArgb(255, 244, 67, 54));
    private static readonly SolidColorBrush AmberBrush  = new(Color.FromArgb(255, 255, 152, 0));
    private static readonly SolidColorBrush GreenBrush  = new(Color.FromArgb(255, 76, 175, 80));
    private static readonly SolidColorBrush NeutralBrush= new(Color.FromArgb(255, 120, 120, 120));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double score)
        {
            if (score <= 0.0) return NeutralBrush;
            if (score < 0.4) return RedBrush;
            if (score < 0.7) return AmberBrush;
            return GreenBrush;
        }
        return NeutralBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a normalized password strength (0.0–1.0) to human-readable strength feedback.
/// </summary>
public sealed class StrengthToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double score)
        {
            if (score <= 0.0) return "Enter master passphrase";
            if (score < 0.3) return "Strength: Weak (Add length & special characters)";
            if (score < 0.6) return "Strength: Fair (Mix uppercase, digits & symbols)";
            if (score < 0.8) return "Strength: Good (High entropy)";
            return "Strength: Excellent (High security & brute-force resistant)";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
