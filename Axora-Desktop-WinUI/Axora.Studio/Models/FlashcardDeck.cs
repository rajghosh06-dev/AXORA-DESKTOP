using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Axora.Studio.Services;

namespace Axora.Studio.Models;

public enum CardDifficulty { Easy, Medium, Hard }

public static class FlashcardLimits
{
    public const int InputBytes = 1024 * 1024;
    public const int CardsPerDeck = 500;
    public const int FieldScalars = 4096;
    public const int SessionDecks = 100;
    public const int SessionTextBytes = 16 * 1024 * 1024;
    public static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static string Text(string value, int maxScalars = FieldScalars)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > maxScalars * 2) throw new ArgumentException("Text exceeds the field limit.");
        _ = StrictUtf8.GetByteCount(value); // Reject malformed UTF-16; never substitute replacement characters.
        if (value.EnumerateRunes().Count() > maxScalars) throw new ArgumentException("Text exceeds the field limit.");
        return value;
    }
    public static string Id(string? value)
    {
        value ??= Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty)
            throw new ArgumentException("A nonempty N-format GUID is required.");
        return value;
    }
    public static void Timestamp(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new ArgumentException("A nondefault UTC timestamp is required.");
    }
}

/// <summary>Session-only deck. Card text/identity and membership are immutable after construction.</summary>
public sealed class FlashcardDeck : ObservableObject
{
    public string DeckId { get; }
    public string Title { get; }
    public string Description { get; }
    public IReadOnlyList<FlashCard> Cards { get; }
    public int CardCount => Cards.Count;
    public int TextBytes { get; }
    public double? CardsMarkedEasyPercentage => CardCount == 0 ? null
        : Math.Round(100.0 * Cards.Count(c => c.Difficulty == CardDifficulty.Easy) / CardCount, 1);
    public string EasyStatistic => CardsMarkedEasyPercentage is double value ? $"{value:0.#}%" : "—";
    private DateTimeOffset? _lastStudied;
    public DateTimeOffset? LastStudied => _lastStudied;

    public FlashcardDeck(string title = "Untitled Deck", IEnumerable<FlashCard>? cards = null,
        string description = "", string? deckId = null)
    {
        Title = FlashcardLimits.Text(title, 256);
        if (string.IsNullOrWhiteSpace(Title)) throw new ArgumentException("A deck title is required.");
        Description = FlashcardLimits.Text(description);
        DeckId = FlashcardLimits.Id(deckId);
        var owned = new List<FlashCard>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards ?? [])
        {
            ArgumentNullException.ThrowIfNull(card);
            if (owned.Count == FlashcardLimits.CardsPerDeck) throw new ArgumentException("Deck exceeds 500 cards.");
            if (!ids.Add(card.CardId)) throw new ArgumentException("Duplicate card identifier.");
            owned.Add(card);
        }
        Cards = new ReadOnlyCollection<FlashCard>(owned);
        TextBytes = owned.Sum(c => c.TextBytes);
        foreach (var card in owned) card.PropertyChanged += CardChanged;
    }
    public void MarkStudied(DateTimeOffset now)
    {
        FlashcardLimits.Timestamp(now);
        SetProperty(ref _lastStudied, now, nameof(LastStudied));
    }
    private void CardChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(FlashCard.Difficulty)) return;
        OnPropertyChanged(nameof(CardsMarkedEasyPercentage));
        OnPropertyChanged(nameof(EasyStatistic));
    }
}

public sealed class FlashCard : ObservableObject
{
    public string CardId { get; }
    public string Front { get; }
    public string Back { get; }
    public int TextBytes { get; }
    private CardDifficulty _difficulty;
    private double _easeFactor;
    private int _intervalDays;
    private int _reviewCount;
    public CardDifficulty Difficulty => _difficulty;
    public double EaseFactor => _easeFactor;
    public int IntervalDays => _intervalDays;
    public int ReviewCount => _reviewCount;
    public DateTimeOffset? LastReviewed { get; private set; }
    public DateTimeOffset? NextReviewDate { get; private set; }

    public FlashCard(string front = "", string back = "", string? cardId = null,
        CardDifficulty difficulty = CardDifficulty.Medium, double easeFactor = 2.5,
        int intervalDays = 1, int reviewCount = 0)
    {
        CardId = FlashcardLimits.Id(cardId);
        Front = FlashcardLimits.Text(front);
        Back = FlashcardLimits.Text(back);
        Validate(difficulty, easeFactor, intervalDays, reviewCount);
        (_difficulty, _easeFactor, _intervalDays, _reviewCount) = (difficulty, easeFactor, intervalDays, reviewCount);
        TextBytes = FlashcardLimits.StrictUtf8.GetByteCount(Front) + FlashcardLimits.StrictUtf8.GetByteCount(Back);
    }
    public static void Validate(CardDifficulty difficulty, double ease, int interval, int reviews)
    {
        if (!Enum.IsDefined(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (!double.IsFinite(ease) || ease < 1.3 || ease > 3.0) throw new ArgumentOutOfRangeException(nameof(ease));
        if (interval is < 1 or > 36500) throw new ArgumentOutOfRangeException(nameof(interval));
        if (reviews < 0) throw new ArgumentOutOfRangeException(nameof(reviews));
    }
    public void ApplyReview(FlashcardReview update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Validate(update.Difficulty, update.EaseFactor, update.IntervalDays, update.ReviewCount);
        FlashcardLimits.Timestamp(update.LastReviewed);
        FlashcardLimits.Timestamp(update.NextReviewDate);
        if (update.ReviewCount != checked(ReviewCount + 1) || update.NextReviewDate != update.LastReviewed.AddDays(update.IntervalDays))
            throw new ArgumentException("Incoherent review update.");
        // Commit complete state before notifications: observers never see a half-applied review.
        (_difficulty, _easeFactor, _intervalDays, _reviewCount) =
            (update.Difficulty, update.EaseFactor, update.IntervalDays, update.ReviewCount);
        LastReviewed = update.LastReviewed;
        NextReviewDate = update.NextReviewDate;
        foreach (var name in new[] { nameof(Difficulty), nameof(EaseFactor), nameof(IntervalDays),
            nameof(ReviewCount), nameof(LastReviewed), nameof(NextReviewDate) }) OnPropertyChanged(name);
    }
}
