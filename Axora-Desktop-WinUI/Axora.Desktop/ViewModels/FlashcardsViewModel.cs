using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// Flashcard Studio ViewModel — Spaced Repetition (SM-2), 1-click AI generation from Scholar Kit,
/// neural offline audio pronunciation (WinRT SpeechSynthesis), review analytics, and CSV/JSON export.
/// </summary>
public sealed partial class FlashcardsViewModel : ObservableObject, IDisposable
{
    private readonly ISpeechSynthesisService _speechService;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty] private FlashcardDeck? _activeDeck;
    [ObservableProperty] private FlashCard? _currentCard;
    [ObservableProperty] private bool _isCardFlipped;
    [ObservableProperty] private int _currentCardIndex;
    [ObservableProperty] private string _sessionProgress = string.Empty;
    [ObservableProperty] private string _deckStats = "3 Cards · 100% Retention";
    [ObservableProperty] private string _exportStatus = string.Empty;
    [ObservableProperty] private bool _isSpeaking;

    public ObservableCollection<FlashcardDeck> Decks { get; } = [];

    public FlashcardsViewModel(ISpeechSynthesisService speechService)
    {
        _speechService = speechService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        var sdkDeck = new FlashcardDeck
        {
            Title = "Windows App SDK & WinUI 3",
            Description = "Core architectural concepts for Windows Desktop apps",
            ColorTag = "#5B7DE8",
        };
        sdkDeck.Cards.Add(new FlashCard { Front = "What is the DispatcherQueue in WinUI 3?", Back = "The native mechanism for marshalling background asynchronous thread executions safely back to the XAML UI thread.", Difficulty = CardDifficulty.Easy });
        sdkDeck.Cards.Add(new FlashCard { Front = "What backdrop material does Axora Desktop render?", Back = "MicaBackdrop with Kind=BaseAlt — integrating seamlessly with the Windows 11 Fluent Desktop Window Manager (DWM).", Difficulty = CardDifficulty.Easy });
        sdkDeck.Cards.Add(new FlashCard { Front = "What cryptographic primitives are used in Axora Vault?", Back = "Argon2id (64MB memory hardening, 3 iterations) for key derivation + AES-256-GCM streaming 1MB blocks.", Difficulty = CardDifficulty.Medium });
        sdkDeck.Cards.Add(new FlashCard { Front = "How does Axora Mobile pair with Axora Desktop?", Back = "Via ECDH NIST P-256 key exchange over WebSocket frames, pairing through an offline QR code token.", Difficulty = CardDifficulty.Easy });

        var aiDeck = new FlashcardDeck
        {
            Title = "DirectML & On-Device AI",
            Description = "Local neural network acceleration",
            ColorTag = "#7C4DFF",
        };
        aiDeck.Cards.Add(new FlashCard { Front = "What is DirectML?", Back = "A low-level DirectX 12 hardware acceleration API for running ONNX machine learning models locally on any GPU/NPU.", Difficulty = CardDifficulty.Easy });
        aiDeck.Cards.Add(new FlashCard { Front = "What embedding dimension does all-MiniLM-L6-v2 produce?", Back = "384-dimensional dense floating-point vector representations for local semantic document search.", Difficulty = CardDifficulty.Easy });

        Decks.Add(sdkDeck);
        Decks.Add(aiDeck);
        SelectDeck(sdkDeck);
    }

    // FIX-4: OnActiveDeckChanged partial method — called by CommunityToolkit source generator when
    // ActiveDeck property changes (e.g., via TwoWay binding SelectedItem on the deck ListView).
    partial void OnActiveDeckChanged(FlashcardDeck? oldValue, FlashcardDeck? newValue)
    {
        if (newValue is null) return;
        _currentCardIndex = 0;
        IsCardFlipped = false;
        UpdateCurrentCard();
        newValue.LastStudied = DateTimeOffset.UtcNow;
    }

    [RelayCommand]
    public async Task SpeakCurrentCardAsync()
    {
        if (CurrentCard is null) return;
        string text = IsCardFlipped ? CurrentCard.Back : CurrentCard.Front;
        IsSpeaking = true;
        try { await _speechService.SpeakTextAsync(text); }
        finally { IsSpeaking = false; }
    }

    [RelayCommand]
    public void SelectDeck(FlashcardDeck deck)
    {
        ActiveDeck = deck;
        CurrentCardIndex = 0;
        IsCardFlipped = false;
        UpdateCurrentCard();
        deck.LastStudied = DateTimeOffset.UtcNow;
    }

    [RelayCommand] public void FlipCard() { if (CurrentCard is null) return; IsCardFlipped = !IsCardFlipped; }

    [RelayCommand]
    public void NextCard()
    {
        if (ActiveDeck is null || ActiveDeck.Cards.Count == 0) return;
        CurrentCardIndex = (CurrentCardIndex + 1) % ActiveDeck.Cards.Count;
        IsCardFlipped = false;
        UpdateCurrentCard();
    }

    [RelayCommand]
    public void PreviousCard()
    {
        if (ActiveDeck is null || ActiveDeck.Cards.Count == 0) return;
        CurrentCardIndex = CurrentCardIndex > 0 ? CurrentCardIndex - 1 : ActiveDeck.Cards.Count - 1;
        IsCardFlipped = false;
        UpdateCurrentCard();
    }

    [RelayCommand]
    public void CreateDeck()
    {
        var deck = new FlashcardDeck { Title = $"New Study Deck {Decks.Count + 1}", Description = "Custom generated deck" };
        deck.Cards.Add(new FlashCard { Front = "Enter Question / Term", Back = "Enter Answer / Definition" });
        Decks.Add(deck);
        SelectDeck(deck);
    }

    [RelayCommand]
    public void RateCard(string difficulty)
    {
        if (CurrentCard is null) return;

        CurrentCard.Difficulty = difficulty switch
        {
            "Easy" => CardDifficulty.Easy,
            "Hard" => CardDifficulty.Hard,
            _      => CardDifficulty.Medium
        };

        if (difficulty == "Easy")
        {
            CurrentCard.EaseFactor = Math.Min(3.0, CurrentCard.EaseFactor + 0.15);
            CurrentCard.IntervalDays = Math.Min(36500, Math.Max(2, (int)(CurrentCard.IntervalDays * CurrentCard.EaseFactor)));
        }
        else if (difficulty == "Hard")
        {
            CurrentCard.EaseFactor = Math.Max(1.3, CurrentCard.EaseFactor - 0.2);
            CurrentCard.IntervalDays = 1;
        }
        else
        {
            CurrentCard.IntervalDays = Math.Min(36500, Math.Max(1, (int)(CurrentCard.IntervalDays * 1.2)));
        }

        CurrentCard.ReviewCount++;
        CurrentCard.LastReviewed = DateTimeOffset.UtcNow;
        CurrentCard.NextReviewDate = DateTimeOffset.UtcNow.AddDays(CurrentCard.IntervalDays);
        ActiveDeck?.NotifyStatsChanged();
        NextCard();
    }

    public void GenerateCardsFromText(string text, string sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var deckTitle = !string.IsNullOrWhiteSpace(sourceFileName)
            ? $"Summary: {Path.GetFileNameWithoutExtension(sourceFileName)}"
            : $"Extracted Notes ({DateTime.Now:MM-dd})";
        var newDeck = new FlashcardDeck { Title = deckTitle, Description = "Generated from Scholar Kit document extraction", ColorTag = "#4CAF50" };
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length < 10) continue;
            if (line.Contains(':') && line.IndexOf(':') < 40)
            {
                var parts = line.Split(':', 2);
                newDeck.Cards.Add(new FlashCard { Front = parts[0].Trim(), Back = parts[1].Trim() });
            }
            else if (i + 1 < lines.Length && lines[i + 1].Trim().Length > 5)
            {
                newDeck.Cards.Add(new FlashCard { Front = line, Back = lines[i + 1].Trim() });
                i++;
            }
        }
        if (newDeck.Cards.Count == 0)
            newDeck.Cards.Add(new FlashCard { Front = "Document Summary", Back = text.Length > 200 ? text[..200] + "…" : text });
        Decks.Insert(0, newDeck);
        SelectDeck(newDeck);
    }

    [RelayCommand]
    public async Task ExportDeckToCsvAsync()
    {
        if (ActiveDeck is null) return;
        try
        {
            var suggestedName = $"{ActiveDeck.Title.Replace(" ", "_")}_deck.csv";
            var savePath = await Helpers.NativeFilePickerHelper.PickSaveFileAsync("Export Deck to CSV", "csv", "CSV File (*.csv)\0*.csv\0", suggestedName);
            if (string.IsNullOrWhiteSpace(savePath)) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Front,Back,Difficulty,IntervalDays");
            foreach (var card in ActiveDeck.Cards)
                sb.AppendLine($"\"{card.Front.Replace("\"", "\"\"")}\",\"{card.Back.Replace("\"", "\"\"")}\",{card.Difficulty},{card.IntervalDays}");
            await File.WriteAllTextAsync(savePath, sb.ToString());
            ExportStatus = $"Exported to {Path.GetFileName(savePath)}";
        }
        catch (Exception ex) { ExportStatus = $"Export failed: {ex.Message}"; }
    }

    [RelayCommand]
    public async Task ExportDeckToAnkiTxtAsync()
    {
        if (ActiveDeck is null) return;
        try
        {
            var suggestedName = $"{ActiveDeck.Title.Replace(" ", "_")}_anki.txt";
            var savePath = await Helpers.NativeFilePickerHelper.PickSaveFileAsync("Export Deck for Anki", "txt", "Anki Import Text (*.txt)\0*.txt\0", suggestedName);
            if (string.IsNullOrWhiteSpace(savePath)) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("#separator:tab");
            sb.AppendLine("#html:true");
            sb.AppendLine($"#deck:{ActiveDeck.Title}");
            sb.AppendLine("#tags:Axora SpacedRepetition");
            foreach (var card in ActiveDeck.Cards)
            {
                var front = card.Front.Replace("\t", " ").Replace("\r\n", "<br>").Replace("\n", "<br>");
                var back = card.Back.Replace("\t", " ").Replace("\r\n", "<br>").Replace("\n", "<br>");
                sb.AppendLine($"{front}\t{back}\t{card.Difficulty}");
            }
            await File.WriteAllTextAsync(savePath, sb.ToString());
            ExportStatus = $"Exported Anki deck to {Path.GetFileName(savePath)}";
        }
        catch (Exception ex) { ExportStatus = $"Export failed: {ex.Message}"; }
    }

    [RelayCommand]
    public async Task ExportDeckToJsonAsync()
    {
        if (ActiveDeck is null) return;
        try
        {
            var suggestedName = $"{ActiveDeck.Title.Replace(" ", "_")}_deck.json";
            var savePath = await Helpers.NativeFilePickerHelper.PickSaveFileAsync("Export Deck to JSON", "json", "JSON File (*.json)\0*.json\0", suggestedName);
            if (string.IsNullOrWhiteSpace(savePath)) return;
            var json = System.Text.Json.JsonSerializer.Serialize(ActiveDeck, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(savePath, json);
            ExportStatus = $"Exported JSON to {Path.GetFileName(savePath)}";
        }
        catch (Exception ex) { ExportStatus = $"Export failed: {ex.Message}"; }
    }

    private void UpdateCurrentCard()
    {
        if (ActiveDeck is null || ActiveDeck.Cards.Count == 0)
        {
            CurrentCard = null; SessionProgress = string.Empty; DeckStats = "0 Cards"; return;
        }
        CurrentCard = ActiveDeck.Cards[CurrentCardIndex];
        SessionProgress = $"{CurrentCardIndex + 1} / {ActiveDeck.Cards.Count}";
        DeckStats = $"{ActiveDeck.Cards.Count} Cards · {ActiveDeck.RetentionRate:F0}% Retention Rate";
        ActiveDeck.NotifyStatsChanged();
    }

    public void Dispose() => _speechService.Stop();
}
