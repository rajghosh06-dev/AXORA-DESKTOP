using Axora.Studio.Models;

namespace Axora.Studio.Services;

public sealed record FlashcardReview(CardDifficulty Difficulty, double EaseFactor, int IntervalDays,
    int ReviewCount, DateTimeOffset LastReviewed, DateTimeOffset NextReviewDate);

/// <summary>AXORA's custom review intervals. Not an implementation of standard SM-2.</summary>
public sealed class FlashcardReviewPolicy
{
    public FlashcardReview Calculate(FlashCard card, CardDifficulty rating, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!Enum.IsDefined(rating)) throw new ArgumentOutOfRangeException(nameof(rating));
        FlashCard.Validate(card.Difficulty, card.EaseFactor, card.IntervalDays, card.ReviewCount);
        FlashcardLimits.Timestamp(now);
        double ease = rating switch
        {
            CardDifficulty.Easy => Math.Min(3.0, card.EaseFactor + 0.15),
            CardDifficulty.Hard => Math.Max(1.3, card.EaseFactor - 0.2),
            _ => card.EaseFactor
        };
        int interval = rating switch
        {
            CardDifficulty.Easy => Math.Min(36500, Math.Max(2, (int)Math.Floor(card.IntervalDays * ease))),
            CardDifficulty.Medium => Math.Min(36500, Math.Max(1, (int)Math.Floor(card.IntervalDays * 1.2))),
            _ => 1
        };
        return new(rating, ease, interval, checked(card.ReviewCount + 1), now, now.AddDays(interval));
    }
}
