using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Desktop.Models;

/// <summary>
/// A flashcard deck with Spaced Repetition (SM-2) scheduling metadata.
/// </summary>
public sealed partial class FlashcardDeck : ObservableObject
{
    public string DeckId { get; init; } = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _title = "Untitled Deck";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _colorTag = "#5B7DE8";

    public List<FlashCard> Cards { get; init; } = [];
    public int CardCount => Cards.Count;

    [ObservableProperty] private DateTimeOffset _lastStudied = DateTimeOffset.UtcNow;

    public double RetentionRate
    {
        get
        {
            if (Cards.Count == 0) return 100.0;
            int mastered = Cards.Count(c => c.Difficulty == CardDifficulty.Easy);
            return Math.Round(((double)mastered / Cards.Count) * 100, 1);
        }
    }

    public void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(CardCount));
        OnPropertyChanged(nameof(RetentionRate));
    }
}

/// <summary>
/// FIX-3: FlashCard is now ObservableObject so XAML bindings receive change notifications
/// when Difficulty, EaseFactor, IntervalDays, and ReviewCount are updated by RateCard().
/// </summary>
public sealed partial class FlashCard : ObservableObject
{
    public string CardId { get; init; } = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _front = string.Empty;
    [ObservableProperty] private string _back = string.Empty;
    [ObservableProperty] private CardDifficulty _difficulty = CardDifficulty.Medium;
    [ObservableProperty] private int _reviewCount;
    /// <summary>Standard SM-2 ease factor — starts at 2.5, adjusted by review ratings.</summary>
    [ObservableProperty] private double _easeFactor = 2.5;
    [ObservableProperty] private int _intervalDays = 1;
    [ObservableProperty] private DateTimeOffset _lastReviewed = DateTimeOffset.UtcNow;
    [ObservableProperty] private DateTimeOffset _nextReviewDate = DateTimeOffset.UtcNow.AddDays(1);
}

public enum CardDifficulty
{
    Easy,
    Medium,
    Hard
}
