using System.Collections.ObjectModel;

namespace Axora.Studio.Models;

public enum FlashcardExportFormat { Csv, AnkiText, AxoraJson }
public enum ExportResultState
{
    Published, Canceled, Rejected, InvalidDestination, DestinationChanged, SerializationFailed,
    StageValidationFailed, PublicationFailed, PublishedButVerificationFailed, Indeterminate
}
public enum ExportBoundary
{
    StageOpened, AfterStageWrite, BeforeDurableFlush, BeforeStageValidation, AfterStageValidation, BeforeDestinationRecheck,
    CommitAdmitted, AfterNativePublication, BeforeFinalVerification, Cleanup
}

/// <summary>Value-only schema v1 card DTO. No live domain objects are retained.</summary>
public sealed record FlashcardExportCard(string CardId, string Front, string Back, CardDifficulty Difficulty,
    int ReviewCount, double EaseFactor, int IntervalDays, DateTimeOffset? LastReviewedUtc, DateTimeOffset? NextReviewUtc)
{
    public void Validate()
    {
        if (CardId is null) throw new ArgumentException("Missing card identity.");
        FlashcardLimits.Id(CardId); FlashcardLimits.Text(Front); FlashcardLimits.Text(Back);
        FlashCard.Validate(Difficulty, EaseFactor, IntervalDays, ReviewCount);
        if (LastReviewedUtc.HasValue != NextReviewUtc.HasValue) throw new ArgumentException("Incomplete review history.");
        if (LastReviewedUtc is { } last && NextReviewUtc is { } next)
        {
            FlashcardLimits.Timestamp(last); FlashcardLimits.Timestamp(next);
            if (ReviewCount == 0 || last.AddDays(IntervalDays) != next) throw new ArgumentException("Incoherent review history.");
        }
        // A1 permits positive counts with unknown timestamps. Preserve those nulls.
    }
}

public sealed record FlashcardExportDeck
{
    public string DeckId { get; }
    public string Title { get; }
    public string Description { get; }
    public DateTimeOffset? LastStudiedUtc { get; }
    public ReadOnlyCollection<FlashcardExportCard> Cards { get; }
    public FlashcardExportDeck(string deckId, string title, string description, DateTimeOffset? lastStudiedUtc,
        IEnumerable<FlashcardExportCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (deckId is null) throw new ArgumentException("Missing deck identity.");
        FlashcardLimits.Id(deckId); FlashcardLimits.Text(title, 256); FlashcardLimits.Text(description);
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Missing deck title.");
        if (lastStudiedUtc is { } time) FlashcardLimits.Timestamp(time);
        var owned = new List<FlashcardExportCard>(); var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards)
        {
            ArgumentNullException.ThrowIfNull(card); card.Validate();
            if (owned.Count >= FlashcardLimits.CardsPerDeck || !ids.Add(card.CardId)) throw new ArgumentException("Invalid card membership.");
            owned.Add(card);
        }
        (DeckId, Title, Description, LastStudiedUtc, Cards) = (deckId, title, description, lastStudiedUtc, owned.AsReadOnly());
    }
}

public sealed record FlashcardExportSnapshot
{
    public const string Format = "axora.flashcards";
    public const int SchemaVersion = 1;
    public DateTimeOffset ExportedAtUtc { get; }
    public FlashcardExportDeck Deck { get; }
    public FlashcardExportSnapshot(DateTimeOffset exportedAtUtc, FlashcardExportDeck deck)
    {
        FlashcardLimits.Timestamp(exportedAtUtc); ArgumentNullException.ThrowIfNull(deck);
        (ExportedAtUtc, Deck) = (exportedAtUtc, deck);
    }
}

public sealed record ExportSnapshotResult(FlashcardExportSnapshot? Snapshot, ExportResultState? Failure, string ReasonCode);
public sealed record ExportFileIdentity(uint Volume, ulong FileId);
public sealed record ExportFingerprint(string CanonicalPath, ExportFileIdentity Identity, long Length, long LastWrite, string Sha256);
public sealed record ExportDestinationPlan
{
    public string Destination { get; }
    public FlashcardExportFormat Format { get; }
    public ExportFileIdentity ParentIdentity { get; }
    public ExportFingerprint? Existing { get; }
    public bool ReplacementApproved { get; }
    internal ExportDestinationPlan(string destination, FlashcardExportFormat format, ExportFileIdentity parent,
        ExportFingerprint? existing, bool approved = false) =>
        (Destination, Format, ParentIdentity, Existing, ReplacementApproved) = (destination, format, parent, existing, approved);
    /// <summary>Future consent owner calls this only after approval bound to this exact fingerprint.</summary>
    public ExportDestinationPlan ApproveReplacement() => Existing is null
        ? throw new InvalidOperationException("There is no replacement to approve.")
        : new(Destination, Format, ParentIdentity, Existing, true);
}
public sealed record ExportPreparation(ExportDestinationPlan? Plan, ExportResultState? Failure, string ReasonCode);
public sealed record ExportOperationContext(Guid OperationId, string Destination, string? Stage, string? Backup);
public sealed record ExportPublicationResult(Guid OperationId, FlashcardExportFormat Format, ExportResultState State,
    string ReasonCode, bool CommitAdmitted, bool CommitAttempted, IReadOnlyList<string> RecoveryPaths, IReadOnlyList<string> CleanupWarnings,
    string DestinationObservation, string StageObservation, string BackupObservation);
