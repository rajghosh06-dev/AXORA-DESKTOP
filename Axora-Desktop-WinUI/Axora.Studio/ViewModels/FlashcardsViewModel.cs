using System.Collections.ObjectModel;
using System.ComponentModel;
using Axora.Studio.Models;
using Axora.Studio.Services;
using Axora.Studio.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Axora.Studio.ViewModels;

public sealed partial class FlashcardsViewModel : ObservableObject
{
    private readonly FlashcardReviewPolicy _reviews;
    private readonly FlashcardTextGenerator _generator;
    private readonly TimeProvider _clock;
    private readonly ObservableCollection<FlashcardDeck> _decks = [];
    private FlashcardDeck? _activeDeck;
    public FlashcardDeck? ActiveDeck
    {
        get => _activeDeck;
        set
        {
            if (ReferenceEquals(_activeDeck, value)) return;
            if (value is not null && !_decks.Contains(value)) throw new ArgumentException("Select a deck from this session.");
            SetActiveDeck(value, value is null ? null : ReadStudyTime());
        }
    }
    [ObservableProperty] private string _notesText = "";
    [ObservableProperty] private string _sourceLabel = "";
    [ObservableProperty] private string _status = "";
    private int _index;
    private bool _flipped;
    public ReadOnlyObservableCollection<FlashcardDeck> Decks { get; }
    public int CurrentCardIndex => _index;
    public FlashCard? CurrentCard => ActiveDeck is { CardCount: > 0 } deck ? deck.Cards[_index] : null;
    public bool IsCardFlipped => _flipped;
    public bool HasCard => CurrentCard is not null;
    public string CardSide => _flipped ? "Answer" : "Question";
    public string CardText => CurrentCard is not { } card ? "Select a deck to begin." : _flipped ? card.Back : card.Front;
    public string CardAccessibleText => $"{CardSide}. {CardText}. Press Space or Enter to flip.";
    public string SessionProgress => HasCard ? $"{_index + 1} / {ActiveDeck!.CardCount}" : "";
    public string DeckStats => ActiveDeck is { } deck ? $"{deck.CardCount} {(deck.CardCount == 1 ? "card" : "cards")} · Cards marked Easy: {deck.EasyStatistic}" : "No deck selected";
    public string ReviewDetails => CurrentCard is { } card ? $"Reviews: {card.ReviewCount} · Interval: {card.IntervalDays} day(s) · Ease: {card.EaseFactor:0.##}" : "";
    public bool CanCreateDeck => _decks.Count < FlashcardLimits.SessionDecks;
    private StudioExportSession? _exports;
    private bool _isExportBusy;
    private string _exportStatus = "";
    private string _exportRecovery = "";
    public bool IsExportBusy
    {
        get => _isExportBusy;
        private set
        {
            if (!SetProperty(ref _isExportBusy, value)) return;
            if (!value && ReadAloudStatus == "Read aloud is unavailable while exporting.")
                ReadAloudStatus = _speechUnavailable ? "Read aloud isn't available on this device." : CanStopReading ? "Stopping read aloud…" : "";
            OnPropertyChanged(nameof(CanExport)); NotifyReadAloud();
        }
    }
    public bool CanExport => ActiveDeck is not null && !IsExportBusy;
    public string ExportStatus { get => _exportStatus; set => SetProperty(ref _exportStatus, value); }
    public string ExportRecovery { get => _exportRecovery; private set => SetProperty(ref _exportRecovery, value); }
    public void ConfigureExport(StudioExportSession exports)
    {
        if (_exports is not null && !ReferenceEquals(_exports, exports)) throw new InvalidOperationException("Export session already assigned.");
        _exports = exports;
    }
    public async Task ExportAsync(FlashcardExportFormat format, nint owner)
    {
        if (IsExportBusy) { ExportStatus = "An export is already in progress."; return; }
        if (_exports is null) { ExportStatus = "Export session unavailable."; return; }
        CancelReadAloudContext(); // Never await optional audio before A2 captures its snapshot.
        IsExportBusy = true; ExportRecovery = ""; ExportStatus = "Choose where to save your cards.";
        ReadAloudStatus = "Read aloud is unavailable while exporting.";
        try
        {
            var result = await _exports.ExportAsync(owner, format, CaptureExportSnapshot);
            ExportStatus = result.Message;
            ExportRecovery = result.Publication is { RecoveryPaths.Count: > 0 } publication
                ? "Retained recovery locations:\n" + string.Join("\n", publication.RecoveryPaths) : "";
        }
        catch (Exception) { ExportStatus = "Export could not settle normally; review any destination and recovery files."; }
        finally { IsExportBusy = false; }
    }

    /// <summary>Call synchronously on the owning UI thread, before any future dialog/await.</summary>
    public ExportSnapshotResult CaptureExportSnapshot()
    {
        if (ActiveDeck is not { } deck) return new(null, ExportResultState.Rejected, "NoActiveDeck");
        try
        {
            var now = _clock.GetUtcNow();
            var owned = new FlashcardExportDeck(deck.DeckId, deck.Title, deck.Description, deck.LastStudied,
                deck.Cards.Select(card => new FlashcardExportCard(card.CardId, card.Front, card.Back, card.Difficulty,
                    card.ReviewCount, card.EaseFactor, card.IntervalDays, card.LastReviewed, card.NextReviewDate)));
            return new(new(now, owned), null, "SnapshotCaptured");
        }
        catch (ArgumentException) { return new(null, ExportResultState.Rejected, "InvalidSnapshot"); }
    }

    public FlashcardsViewModel(FlashcardReviewPolicy reviews, FlashcardTextGenerator generator, TimeProvider clock)
    {
        (_reviews, _generator, _clock) = (reviews, generator, clock);
        Decks = new(_decks);
        AddDeck(new("Study techniques", [
            new("What is active recall?", "Try retrieving an answer before checking your notes."),
            new("Why revisit material over time?", "Repeated retrieval over separated study sessions provides practice.")]), select: false);
        AddDeck(new("Windows concepts", [
            new("What is a UI thread?", "The thread responsible for processing a window's user-interface work."),
            new("What does a dispatcher queue do?", "It schedules work on the thread that owns the queue.")]), select: false);
        SelectDeck(_decks[0]);
    }
    private DateTimeOffset ReadStudyTime()
    {
        var now = _clock.GetUtcNow();
        FlashcardLimits.Timestamp(now);
        return now;
    }
    private void SetActiveDeck(FlashcardDeck? newValue, DateTimeOffset? studiedAt)
    {
        if (ReferenceEquals(_activeDeck, newValue)) return;
        CancelReadAloudContext();
        OnPropertyChanging(nameof(ActiveDeck));
        if (_activeDeck is not null) _activeDeck.PropertyChanged -= ActiveDeckChanged;
        (_index, _flipped) = (0, false);
        _activeDeck = newValue;
        if (newValue is not null)
        {
            newValue.PropertyChanged += ActiveDeckChanged;
            newValue.MarkStudied(studiedAt!.Value);
        }
        OnPropertyChanged(nameof(ActiveDeck));
        OnPropertyChanged(nameof(CanExport));
        RefreshCard();
    }
    private void ActiveDeckChanged(object? sender, PropertyChangedEventArgs args) => OnPropertyChanged(nameof(DeckStats));
    [RelayCommand] public void SelectDeck(FlashcardDeck? deck) => ActiveDeck = deck;
    [RelayCommand(CanExecute = nameof(HasCard))] public void FlipCard() { if (!HasCard) return; CancelReadAloudContext(); _flipped = !_flipped; RefreshCard(); }
    [RelayCommand(CanExecute = nameof(HasCard))] public void NextCard() => Move(1);
    [RelayCommand(CanExecute = nameof(HasCard))] public void PreviousCard() => Move(-1);
    private void Move(int delta)
    {
        if (!HasCard) return;
        CancelReadAloudContext();
        _index = (_index + delta + ActiveDeck!.CardCount) % ActiveDeck.CardCount;
        _flipped = false;
        RefreshCard();
    }
    [RelayCommand(CanExecute = nameof(HasCard))]
    public void RateCard(CardDifficulty rating)
    {
        if (!Enum.IsDefined(rating)) throw new ArgumentOutOfRangeException(nameof(rating));
        if (CurrentCard is not { } card) return;
        FlashcardReview update = _reviews.Calculate(card, rating, _clock.GetUtcNow());
        card.ApplyReview(update);
        NextCard();
    }
    [RelayCommand(CanExecute = nameof(CanCreateDeck))]
    public void CreateDeck()
    {
        try
        {
            AddDeck(new($"New Study Deck {_decks.Count + 1}", [
                new("How can I practise active recall?", "Answer before flipping this starter card, then rate your recall.")],
                "One educational starter card; not a deck editor"));
            Status = "Starter deck added for this session.";
        }
        catch (ArgumentException ex) { Status = ex.Message; }
    }
    public void AddDeck(FlashcardDeck deck, bool select = true, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(deck);
        if (_decks.Count >= FlashcardLimits.SessionDecks) throw new ArgumentException("Session limit: 100 decks.");
        if (_decks.Any(d => string.Equals(d.DeckId, deck.DeckId, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Duplicate deck identifier.");
        var existingIds = _decks.SelectMany(d => d.Cards).Select(c => c.CardId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (deck.Cards.Any(c => existingIds.Contains(c.CardId))) throw new ArgumentException("Card identifiers must be unique within this session.");
        if (_decks.Sum(d => (long)d.TextBytes) + deck.TextBytes > FlashcardLimits.SessionTextBytes)
            throw new ArgumentException("Session card text exceeds 16 MiB.");
        // Validate one clock observation before insertion; selection cannot fail on a second read.
        DateTimeOffset? studiedAt = select ? ReadStudyTime() : null;
        token.ThrowIfCancellationRequested();
        _decks.Add(deck);
        OnPropertyChanged(nameof(CanCreateDeck));
        CreateDeckCommand.NotifyCanExecuteChanged();
        if (select) SetActiveDeck(deck, studiedAt);
    }
    public bool GenerateCardsFromText(string text, string sourceLabel, CancellationToken token = default)
    {
        var result = _generator.Generate(text, sourceLabel, token);
        if (result.Deck is null) return false;
        AddDeck(result.Deck, token: token);
        Status = $"Created {result.Deck.CardCount} rule-based card(s); ignored {result.IgnoredLines} line(s). Session only.";
        return true;
    }
    [RelayCommand]
    private void CreateCardsFromNotes()
    {
        try { if (!GenerateCardsFromText(NotesText, SourceLabel)) Status = "Enter structured notes first."; }
        catch (ArgumentException ex) { Status = ex.Message; }
    }
    private void RefreshCard()
    {
        foreach (var name in new[] { nameof(CurrentCard), nameof(CurrentCardIndex), nameof(IsCardFlipped), nameof(HasCard),
            nameof(CardSide), nameof(CardText), nameof(CardAccessibleText), nameof(SessionProgress), nameof(DeckStats), nameof(ReviewDetails) })
            OnPropertyChanged(name);
        FlipCardCommand.NotifyCanExecuteChanged(); NextCardCommand.NotifyCanExecuteChanged();
        PreviousCardCommand.NotifyCanExecuteChanged(); RateCardCommand.NotifyCanExecuteChanged();
        NotifyReadAloud();
    }

    private StudioReadAloudSession? _readAloud;
    private Action<Action> _dispatchReadAloud = action => action();
    private Action<string>? _readAloudLog;
    private long _studyContext, _readUiGeneration;
    private Guid _readOperation;
    private bool _speechUnavailable, _onFlashcards = true;
    private Task? _readCancellation;
    public Task ReadAloudCancellationSettlement => _readCancellation ?? Task.CompletedTask;
    private ReadAloudPhase _readAloudPhase;
    private string _readAloudStatus = "";
    public ReadAloudPhase ReadAloudPhase { get => _readAloudPhase; private set { if (SetProperty(ref _readAloudPhase, value)) NotifyReadAloud(); } }
    public string ReadAloudStatus { get => _readAloudStatus; private set => SetProperty(ref _readAloudStatus, value); }
    public bool CanReadAloud => HasCard && _readAloud is not null && !_speechUnavailable && _onFlashcards && !IsExportBusy;
    public bool CanStopReading => ReadAloudPhase is ReadAloudPhase.Preparing or ReadAloudPhase.Reading or ReadAloudPhase.Stopping;
    public bool IsReadAloudBusy => CanStopReading;
    public void ConfigureReadAloud(StudioReadAloudSession session, Action<Action> dispatch, Action<string>? log = null)
    {
        if (_readAloud is not null && !ReferenceEquals(_readAloud, session)) throw new InvalidOperationException("Read aloud session already assigned.");
        _readAloud = session; _dispatchReadAloud = dispatch; _readAloudLog = log;
        _onFlashcards = true;
        if (!session.IsActive && CanStopReading) { ReadAloudPhase = ReadAloudPhase.Idle; ReadAloudStatus = "Read aloud stopped."; }
        NotifyReadAloud();
    }
    [RelayCommand(AllowConcurrentExecutions = true, CanExecute = nameof(CanReadAloud))]
    public async Task ReadAloudAsync()
    {
        if (!CanReadAloud) return;
        string text = CardText, side = CardSide; // Immutable UI-owned capture before the first await.
        long context = _studyContext, ui = ++_readUiGeneration;
        Guid id = _readOperation = Guid.NewGuid();
        ReadAloudPhase = ReadAloudPhase.Preparing; ReadAloudStatus = "Preparing read aloud…";
        try { _readAloudLog?.Invoke($"ReadAloud operation={id:N}; phase=Captured; side={side}; context={context}"); } catch (Exception) { }
        var result = await _readAloud!.ReadAsync(new(id, text), progress => DispatchCurrent(ui, context, id, () =>
        {
            ReadAloudPhase = progress.Phase;
            ReadAloudStatus = progress.Phase == ReadAloudPhase.Stopping ? "Stopping read aloud…" : progress.Phase == ReadAloudPhase.Reading
                ? side == "Answer" ? "Reading answer…" : "Reading question…" : "Preparing read aloud…";
        })).ConfigureAwait(false);
        DispatchCurrent(ui, context, id, () => ApplyReadAloudResult(result));
    }
    private void DispatchCurrent(long ui, long context, Guid id, Action update) => _dispatchReadAloud(() =>
    {
        // Fence inside the UI callback, after any queueing delay or intervening navigation.
        if (_onFlashcards && ui == _readUiGeneration && context == _studyContext && id == _readOperation) update();
    });
    private void ApplyReadAloudResult(ReadAloudResult result)
    {
        if (result.Outcome == ReadAloudOutcome.Replaced) return;
        _readOperation = Guid.Empty; ++_readUiGeneration; // Terminal UI settlement fences even progress already queued for this request.
        _speechUnavailable |= result.Outcome == ReadAloudOutcome.Unavailable || result.Cleanup == ReadAloudCleanup.Deferred || result.ReasonCode == "CleanupFailure";
        ReadAloudPhase = result.Outcome switch
        {
            ReadAloudOutcome.Completed => ReadAloudPhase.Finished,
            ReadAloudOutcome.Canceled => ReadAloudPhase.Idle,
            ReadAloudOutcome.Unavailable => ReadAloudPhase.Unavailable,
            _ => ReadAloudPhase.Failed
        };
        ReadAloudStatus = result.Outcome switch
        {
            ReadAloudOutcome.Completed => "Read aloud finished.",
            ReadAloudOutcome.Canceled => "Read aloud stopped.",
            ReadAloudOutcome.Unavailable => "Read aloud isn't available on this device.",
            ReadAloudOutcome.InvalidText => "This card has no valid text to read aloud.",
            ReadAloudOutcome.SynthesisFailed => "Couldn't prepare read aloud.",
            _ => "Couldn't play read aloud."
        };
        NotifyReadAloud();
    }
    [RelayCommand(CanExecute = nameof(CanStopReading))]
    public void StopReading() => CancelReadAloudContext();
    public void OnFlashcardsDeparture()
    {
        CancelReadAloudContext(); _onFlashcards = false; NotifyReadAloud();
    }
    private void CancelReadAloudContext()
    {
        ++_studyContext;
        long ui = ++_readUiGeneration, context = _studyContext;
        _readOperation = Guid.Empty;
        if (_readAloud is null || !_readAloud.IsActive)
        {
            // Native settlement can precede its queued terminal UI callback. The context fence discards
            // that callback, so clear any stale busy state here rather than waiting for an update we reject.
            if (CanStopReading) { ReadAloudPhase = ReadAloudPhase.Idle; ReadAloudStatus = "Read aloud stopped."; }
            return;
        }
        ReadAloudPhase = ReadAloudPhase.Stopping; ReadAloudStatus = "Stopping read aloud…";
        _readCancellation = ObserveReadCancellationAsync(_readAloud.CancelCurrentAsync(), ui, context);
    }
    private async Task ObserveReadCancellationAsync(Task<ReadAloudResult> cancellation, long ui, long context)
    {
        var result = await cancellation.ConfigureAwait(false);
        DispatchCurrent(ui, context, Guid.Empty, () =>
        {
            _speechUnavailable |= result.Outcome == ReadAloudOutcome.Unavailable || result.Cleanup == ReadAloudCleanup.Deferred;
            if (IsExportBusy) { ReadAloudPhase = ReadAloudPhase.Idle; ReadAloudStatus = "Read aloud is unavailable while exporting."; }
            else ApplyReadAloudResult(result);
        });
    }
    private void NotifyReadAloud()
    {
        OnPropertyChanged(nameof(CanReadAloud)); OnPropertyChanged(nameof(CanStopReading)); OnPropertyChanged(nameof(IsReadAloudBusy));
        ReadAloudCommand.NotifyCanExecuteChanged(); StopReadingCommand.NotifyCanExecuteChanged();
    }
}
