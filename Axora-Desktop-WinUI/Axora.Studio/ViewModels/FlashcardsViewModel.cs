using System.Collections.ObjectModel;
using System.ComponentModel;
using Axora.Studio.Models;
using Axora.Studio.Services;
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
        RefreshCard();
    }
    private void ActiveDeckChanged(object? sender, PropertyChangedEventArgs args) => OnPropertyChanged(nameof(DeckStats));
    [RelayCommand] public void SelectDeck(FlashcardDeck? deck) => ActiveDeck = deck;
    [RelayCommand(CanExecute = nameof(HasCard))] public void FlipCard() { if (!HasCard) return; _flipped = !_flipped; RefreshCard(); }
    [RelayCommand(CanExecute = nameof(HasCard))] public void NextCard() => Move(1);
    [RelayCommand(CanExecute = nameof(HasCard))] public void PreviousCard() => Move(-1);
    private void Move(int delta)
    {
        if (!HasCard) return;
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
    }
}
