using Axora.Studio.Models;
using Axora.Studio.Services;
using Axora.Studio.ViewModels;
using Axora.Studio.Views;
using Microsoft.Extensions.DependencyInjection;
using Windows.System;

namespace Axora.Studio.Tests;

internal static class FlashcardsTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[]
    {
        "model-defaults", "model-invalid", "notifications", "selection", "selection-clock-atomic", "empty", "wrap-flip", "review-math",
        "review-boundaries", "review-failure-atomic", "review-clock", "easy-statistic", "starter-session",
        "session-bounds", "text-branches", "text-boundaries", "text-newlines-metadata", "text-unicode",
        "text-limits", "cancel-no-insert", "lazy-composition-retention", "keyboard-routing"
    });
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
    private sealed class Clock : TimeProvider
    {
        public int Calls;
        public Action? OnRead;
        public override DateTimeOffset GetUtcNow() { Calls++; OnRead?.Invoke(); return Now; }
    }
    private static FlashcardsViewModel Vm(Clock? clock = null) => new(new(), new(), clock ?? new());
    private static Task Sync(Action run) { run(); return Task.CompletedTask; }
    private static void Reject<T>(Checks c, Action run, string name) where T : Exception
    {
        try { run(); c.That(false, name); } catch (T) { c.That(true, name); }
    }
    public static async Task RunAsync(Checks c)
    {
        await c.CaseAsync("model-defaults", () => Sync(() =>
        {
            var a = new FlashCard(); var b = new FlashCard(); var d = new FlashcardDeck(); var e = new FlashcardDeck();
            c.That(a.CardId != b.CardId && d.DeckId != e.DeckId && Guid.TryParseExact(a.CardId, "N", out _), "Unique typed identifiers");
            c.That(a.EaseFactor == 2.5 && a.IntervalDays == 1 && a.ReviewCount == 0 && a.Difficulty == CardDifficulty.Medium, "Unreviewed defaults");
            c.That(a.LastReviewed is null && a.NextReviewDate is null && d.LastStudied is null, "No fabricated review/study timestamps");
            c.That(d.Cards.Count == 0 && d.CardsMarkedEasyPercentage is null && d.EasyStatistic == "—", "Empty model statistic is unknown");
        }));
        await c.CaseAsync("model-invalid", () => Sync(() =>
        {
            foreach (double ease in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1.29, 3.01 })
                Reject<ArgumentException>(c, () => new FlashCard(easeFactor: ease), "Invalid ease rejected " + ease);
            foreach (int interval in new[] { 0, -1, 36501 }) Reject<ArgumentException>(c, () => new FlashCard(intervalDays: interval), "Invalid interval " + interval);
            Reject<ArgumentException>(c, () => new FlashCard(reviewCount: -1), "Negative count rejected");
            Reject<ArgumentException>(c, () => new FlashCard(difficulty: (CardDifficulty)99), "Unknown difficulty rejected");
            Reject<ArgumentException>(c, () => new FlashCard(cardId: ""), "Empty ID rejected");
            Reject<ArgumentException>(c, () => new FlashcardDeck(deckId: Guid.Empty.ToString("N")), "Zero ID rejected");
            Reject<ArgumentException>(c, () => new FlashCard("\uD800"), "Malformed UTF-16 rejected");
            Reject<ArgumentException>(c, () => new FlashCard(new string('q', 4097)), "Field bound rejected");
            var card = new FlashCard("Question", "Answer");
            Reject<ArgumentException>(c, () => new FlashcardDeck(cards: [card, card]), "Duplicate membership rejected");
            Reject<ArgumentException>(c, () => new FlashcardDeck(" "), "Blank title rejected");
            Reject<ArgumentException>(c, () => new FlashcardDeck().MarkStudied(default), "Invalid timestamp rejected");
            Reject<ArgumentException>(c, () => new FlashcardDeck().MarkStudied(Now.ToOffset(TimeSpan.FromHours(1))), "Non-UTC timestamp rejected");
        }));
        await c.CaseAsync("notifications", () => Sync(() =>
        {
            var card = new FlashCard("Question", "Answer"); var deck = new FlashcardDeck(cards: [card]);
            var names = new HashSet<string>(); var deckNames = new HashSet<string>();
            card.PropertyChanged += (_, e) => { names.Add(e.PropertyName!); c.That(card.ReviewCount == 1 && card.NextReviewDate == Now.AddDays(2), "Notification sees committed state"); };
            deck.PropertyChanged += (_, e) => deckNames.Add(e.PropertyName!);
            card.ApplyReview(new FlashcardReviewPolicy().Calculate(card, CardDifficulty.Easy, Now));
            c.That(names.SetEquals(new[] { "Difficulty", "EaseFactor", "IntervalDays", "ReviewCount", "LastReviewed", "NextReviewDate" }), "All review properties notify");
            c.That(deckNames.Contains(nameof(FlashcardDeck.CardsMarkedEasyPercentage)) && deckNames.Contains(nameof(FlashcardDeck.EasyStatistic)), "Deck stats notify");
        }));
        await c.CaseAsync("selection", () => Sync(() =>
        {
            var vm = Vm(); var deck = vm.Decks[0]; vm.NextCard(); vm.FlipCard();
            int resets = 0; vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.CurrentCardIndex)) resets++; };
            vm.SelectDeck(deck);
            c.That(vm.CurrentCardIndex == 1 && vm.IsCardFlipped && resets == 0, "Repeated selected item does not reset");
            vm.SelectDeck(vm.Decks[1]);
            c.That(vm.CurrentCardIndex == 0 && !vm.IsCardFlipped && resets == 1, "New selection resets exactly once");
            vm.ActiveDeck = null;
            c.That(vm.CurrentCard is null && vm.SessionProgress == "" && !vm.HasCard && vm.CardText == "Select a deck to begin.", "Null clears stale display");
            c.That(!vm.FlipCardCommand.CanExecute(null) && !vm.RateCardCommand.CanExecute(CardDifficulty.Easy), "Null disables commands");
            Reject<ArgumentException>(c, () => vm.SelectDeck(new FlashcardDeck()), "Foreign selection rejected");
            c.That(vm.ActiveDeck is null, "Rejected selection does not mutate");
        }));
        await c.CaseAsync("selection-clock-atomic", () => Sync(() =>
        {
            var clock = new Clock(); var vm = Vm(clock); clock.Calls = 0;
            clock.OnRead = () => { if (clock.Calls > 1) throw new ArgumentException("Unexpected second clock read."); };
            var added = new FlashcardDeck("Atomic", [new("Q", "A")]);
            vm.AddDeck(added);
            c.That(clock.Calls == 1 && vm.ActiveDeck == added && added.LastStudied == Now, "Insertion and selection share one validated timestamp");
            clock.Calls = 0; vm.SelectDeck(vm.Decks[0]);
            c.That(clock.Calls == 1 && vm.CurrentCardIndex == 0 && !vm.IsCardFlipped, "Selection reads clock once");
            vm.NextCard(); vm.FlipCard(); var active = vm.ActiveDeck; int before = vm.Decks.Count;
            clock.OnRead = () => throw new ArgumentException("Clock unavailable.");
            Reject<ArgumentException>(c, () => vm.AddDeck(new("Rejected", [new("Q", "A")])), "Clock failure rejects before insertion");
            Reject<ArgumentException>(c, () => vm.SelectDeck(added), "Clock failure rejects before selection");
            c.That(vm.Decks.Count == before && vm.ActiveDeck == active && vm.CurrentCardIndex == 1 && vm.IsCardFlipped,
                "Clock failures preserve complete session state");
        }));
        await c.CaseAsync("empty", () => Sync(() =>
        {
            var vm = Vm(); vm.AddDeck(new("Empty")); vm.FlipCard(); vm.NextCard(); vm.PreviousCard(); vm.RateCard(CardDifficulty.Hard);
            c.That(vm.CurrentCard is null && !vm.IsCardFlipped && vm.CurrentCardIndex == 0 && vm.DeckStats.Contains("—"), "Empty actions are safe and statistic honest");
        }));
        await c.CaseAsync("wrap-flip", () => Sync(() =>
        {
            var vm = Vm(); vm.PreviousCard(); c.That(vm.CurrentCardIndex == 1, "Previous wraps");
            vm.FlipCard(); c.That(vm.CardText == vm.CurrentCard!.Back && vm.IsCardFlipped, "Flip shows back");
            vm.NextCard(); c.That(vm.CurrentCardIndex == 0 && !vm.IsCardFlipped, "Next wraps and clears flip");
            vm.AddDeck(new("Single", [new("Q", "A")])); vm.FlipCard(); vm.NextCard(); vm.PreviousCard();
            c.That(vm.CurrentCardIndex == 0 && !vm.IsCardFlipped && vm.SessionProgress == "1 / 1", "Single-card navigation stable");
        }));
        await c.CaseAsync("review-math", () => Sync(() =>
        {
            var p = new FlashcardReviewPolicy(); var card = new FlashCard();
            var easy = p.Calculate(card, CardDifficulty.Easy, Now);
            var medium = p.Calculate(card, CardDifficulty.Medium, Now); var hard = p.Calculate(card, CardDifficulty.Hard, Now);
            c.That(easy.IntervalDays == 2 && Math.Abs(easy.EaseFactor - 2.65) < 1e-10, "First Easy custom math");
            c.That(medium.IntervalDays == 1 && medium.EaseFactor == 2.5, "Medium floors at one");
            c.That(hard.IntervalDays == 1 && hard.EaseFactor == 2.3, "Hard resets interval");
            card.ApplyReview(easy); var next = p.Calculate(card, CardDifficulty.Easy, Now);
            c.That(next.IntervalDays == 5 && Math.Abs(next.EaseFactor - 2.8) < 1e-10, "Easy uses updated ease before floor");
            c.That(p.Calculate(new(intervalDays: 10), CardDifficulty.Medium, Now).IntervalDays == 12, "Medium 1.2 multiplication");
            var vm = Vm(); var first = vm.CurrentCard!; vm.RateCard(CardDifficulty.Easy);
            c.That(first.ReviewCount == 1 && vm.CurrentCardIndex == 1 && vm.CurrentCard != first && !vm.IsCardFlipped, "Review commits and advances");
        }));
        await c.CaseAsync("review-boundaries", () => Sync(() =>
        {
            var p = new FlashcardReviewPolicy();
            c.That(p.Calculate(new(easeFactor: 3, intervalDays: 36500), CardDifficulty.Easy, Now).IntervalDays == 36500, "Easy upper interval bound");
            c.That(p.Calculate(new(easeFactor: 3), CardDifficulty.Easy, Now).EaseFactor == 3, "Easy ceiling");
            c.That(p.Calculate(new(easeFactor: 1.3), CardDifficulty.Hard, Now).EaseFactor == 1.3, "Hard floor");
            c.That(p.Calculate(new(intervalDays: 36500), CardDifficulty.Medium, Now).IntervalDays == 36500, "Medium upper interval bound");
            var card = new FlashCard();
            for (int i = 0; i < 1000; i++) card.ApplyReview(p.Calculate(card, CardDifficulty.Easy, Now));
            c.That(card.IntervalDays == 36500 && card.EaseFactor == 3 && card.ReviewCount == 1000, "Repeated Easy remains bounded");
            for (int i = 0; i < 50; i++) card.ApplyReview(p.Calculate(card, CardDifficulty.Hard, Now));
            c.That(card.IntervalDays == 1 && card.EaseFactor == 1.3, "Repeated Hard floor");
            var med = new FlashCard(); for (int i = 0; i < 200; i++) med.ApplyReview(p.Calculate(med, CardDifficulty.Medium, Now));
            c.That(med.IntervalDays == 1, "Medium-one behavior preserved without false +3d claim");
        }));
        await c.CaseAsync("review-failure-atomic", () => Sync(() =>
        {
            var p = new FlashcardReviewPolicy(); var card = new FlashCard();
            Reject<ArgumentException>(c, () => p.Calculate(card, (CardDifficulty)99, Now), "Unknown rating rejected");
            Reject<ArgumentException>(c, () => p.Calculate(card, CardDifficulty.Easy, DateTimeOffset.MaxValue), "Date overflow rejected before mutation");
            Reject<OverflowException>(c, () => p.Calculate(new(reviewCount: int.MaxValue), CardDifficulty.Easy, Now), "Count overflow rejected");
            Reject<ArgumentException>(c, () => card.ApplyReview(new(CardDifficulty.Easy, 2.65, 2, 1, Now, Now.AddDays(3))), "Incoherent update rejected");
            c.That(card.ReviewCount == 0 && card.Difficulty == CardDifficulty.Medium && card.LastReviewed is null, "Failure leaves state untouched");
            var vm = Vm(); var prior = vm.CurrentCard;
            Reject<ArgumentException>(c, () => vm.RateCard((CardDifficulty)99), "VM validates typed input");
            c.That(vm.CurrentCard == prior && prior!.ReviewCount == 0, "VM rejection does not advance");
        }));
        await c.CaseAsync("review-clock", () => Sync(() =>
        {
            var clock = new Clock(); var vm = Vm(clock); var card = vm.CurrentCard!; clock.Calls = 0;
            vm.RateCard(CardDifficulty.Easy);
            c.That(clock.Calls == 1 && card.LastReviewed == Now && card.NextReviewDate == Now.AddDays(2), "One clock observation for review timestamps");
        }));
        await c.CaseAsync("easy-statistic", () => Sync(() =>
        {
            var vm = Vm(); var deck = vm.ActiveDeck!;
            c.That(deck.CardsMarkedEasyPercentage == 0, "Examples do not imply mastery");
            vm.RateCard(CardDifficulty.Easy); c.That(deck.CardsMarkedEasyPercentage == 50, "Easy share 1 of 2");
            vm.RateCard(CardDifficulty.Hard); c.That(deck.CardsMarkedEasyPercentage == 50, "Hard is not counted Easy");
            vm.RateCard(CardDifficulty.Medium); c.That(deck.CardsMarkedEasyPercentage == 0 && vm.DeckStats.Contains("Cards marked Easy"), "Easy share changes honestly");
        }));
        await c.CaseAsync("starter-session", () => Sync(() =>
        {
            var vm = Vm(); var old = vm.Decks.Count; vm.CreateDeck();
            c.That(vm.Decks.Count == old + 1 && vm.ActiveDeck!.CardCount == 1 && vm.ActiveDeck.Title.StartsWith("New Study Deck"), "One selected starter deck");
            c.That(vm.ActiveDeck!.Description.Contains("not a deck editor"), "Starter is not an editor claim");
            c.That(Vm().Decks.Count == 2, "New process/session reseeds rather than restoring");
        }));
        await c.CaseAsync("session-bounds", () => Sync(() =>
        {
            var vm = Vm(); while (vm.Decks.Count < 100) vm.AddDeck(new("Empty"), select: false);
            Reject<ArgumentException>(c, () => vm.AddDeck(new("Over limit")), "100-deck bound enforced");
            c.That(vm.Decks.Count == 100 && !vm.CanCreateDeck && !vm.CreateDeckCommand.CanExecute(null), "No partial insertion at deck bound");
            var bytesVm = Vm(); string field = new('x', 4096);
            FlashcardDeck Large() => new("Large", Enumerable.Range(0, 500).Select(_ => new FlashCard(field, field)));
            for (int i = 0; i < 4; i++) bytesVm.AddDeck(Large(), select: false);
            int before = bytesVm.Decks.Count; var active = bytesVm.ActiveDeck;
            Reject<ArgumentException>(c, () => bytesVm.AddDeck(Large()), "16-MiB aggregate bound enforced");
            c.That(bytesVm.Decks.Count == before && bytesVm.ActiveDeck == active, "Text-budget failure preserves session");
            Reject<ArgumentException>(c, () => bytesVm.AddDeck(active!), "Duplicate deck rejected");
            Reject<ArgumentException>(c, () => bytesVm.AddDeck(new("Other", active!.Cards)), "Duplicate card across decks rejected");
        }));
        await c.CaseAsync("text-branches", () => Sync(() =>
        {
            var g = new FlashcardTextGenerator();
            c.That(g.Generate(" \t\r\n ", "").Deck is null, "Whitespace no-op");
            var colon = g.Generate("TermName: Definition here", "").Deck!;
            c.That(colon.CardCount == 1 && colon.Cards[0].Front == "TermName" && colon.Cards[0].Back == "Definition here", "Colon pair");
            var adjacent = g.Generate("Question line\nAnswer line", "").Deck!;
            c.That(adjacent.Cards[0].Front == "Question line" && adjacent.Cards[0].Back == "Answer line", "Adjacent pair");
            var fallback = g.Generate("An unstructured paragraph", "").Deck!;
            c.That(fallback.Cards[0].Front == "Document excerpt" && fallback.Cards[0].Back == "An unstructured paragraph", "Excerpt not semantic summary");
            var empty = g.Generate(": sufficient answer\nLongQuestion:", "");
            c.That(empty.IgnoredLines == 2 && empty.Deck!.Cards.All(x => x.Front.Length > 0 && x.Back.Length > 0), "Empty pairs skipped and reported");
        }));
        await c.CaseAsync("text-boundaries", () => Sync(() =>
        {
            var g = new FlashcardTextGenerator();
            c.That(g.Generate("Term: abc", "").Deck!.Cards[0].Front == "Document excerpt", "Nine-character line skipped");
            c.That(g.Generate("Term: abcd", "").Deck!.Cards[0].Front == "Term", "Ten-character line parsed");
            c.That(g.Generate(new string('q', 39) + ": answer", "").Deck!.Cards[0].Front.Length == 39, "Colon index 39 accepted");
            c.That(g.Generate(new string('q', 40) + ": answer", "").Deck!.Cards[0].Front == "Document excerpt", "Colon index 40 not parsed");
            c.That(g.Generate("Question line\n12345", "").Deck!.Cards[0].Front == "Document excerpt", "Five-character next line not paired");
            c.That(g.Generate("Question line\n123456", "").Deck!.Cards[0].Back == "123456", "Six-character next line paired");
        }));
        await c.CaseAsync("text-newlines-metadata", () => Sync(() =>
        {
            var g = new FlashcardTextGenerator();
            foreach (string newline in new[] { "\r", "\n", "\r\n" })
                c.That(g.Generate("TermOne: answer" + newline + "TermTwo: another", "").Deck!.CardCount == 2, "Newline form " + newline.Length);
            c.That(g.Generate("TermOne: answer", "C:\\unopened\\notes.pdf").Deck!.Title == "Notes: notes.pdf", "Filename display metadata only");
            Reject<ArgumentException>(c, () => g.Generate("TermOne: answer", new string('x', 257)), "Metadata limit rejected");
        }));
        await c.CaseAsync("text-unicode", () => Sync(() =>
        {
            var g = new FlashcardTextGenerator(); string emojis = string.Concat(Enumerable.Repeat("😀", 201));
            var excerpt = g.Generate(emojis, "").Deck!.Cards[0].Back;
            c.That(excerpt == string.Concat(Enumerable.Repeat("😀", 200)) + "…", "Excerpt cuts Unicode scalars, not surrogate halves");
            var full = g.Generate(string.Concat(Enumerable.Repeat("😀", 200)), "").Deck!.Cards[0].Back;
            c.That(!full.EndsWith('…'), "Exact 200-scalar excerpt not falsely truncated");
            c.That(new FlashCard(string.Concat(Enumerable.Repeat("😀", 4096))).Front.Length == 8192, "4096-scalar field allowed");
        }));
        await c.CaseAsync("text-limits", () => Sync(() =>
        {
            var g = new FlashcardTextGenerator();
            c.That(g.Generate(new string('x', 1048576), "").Deck!.CardCount == 1, "Exact ASCII input budget accepted");
            Reject<ArgumentException>(c, () => g.Generate(new string('x', 1048577), ""), "Oversized ASCII rejected");
            c.That(g.Generate(new string('é', 524288), "").Deck!.CardCount == 1, "Exact multibyte budget accepted");
            Reject<ArgumentException>(c, () => g.Generate(new string('é', 524289), ""), "UTF-8 byte bound rather than character count");
            string Lines(int count) => string.Join('\n', Enumerable.Range(0, count).Select(i => $"Term{i}: answer here"));
            c.That(g.Generate(Lines(500), "").Deck!.CardCount == 500, "500-card budget accepted");
            Reject<ArgumentException>(c, () => g.Generate(Lines(501), ""), "501st card rejected");
            Reject<ArgumentException>(c, () => g.Generate("Question: " + new string('a', 4097), ""), "Generated field limit enforced");
            Reject<ArgumentException>(c, () => g.Generate("Question: \uD800", ""), "Strict Unicode input validation");
        }));
        await c.CaseAsync("cancel-no-insert", () => Sync(() =>
        {
            var clock = new Clock(); var vm = Vm(clock); int before = vm.Decks.Count; var active = vm.ActiveDeck;
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Reject<OperationCanceledException>(c, () => vm.GenerateCardsFromText("Question: answer here", "", canceled.Token), "Pre-canceled generation rejected");
            using var atCommit = new CancellationTokenSource(); clock.OnRead = atCommit.Cancel;
            Reject<OperationCanceledException>(c, () => vm.GenerateCardsFromText("Question: answer here", "", atCommit.Token), "Cancellation before insertion rejected");
            clock.OnRead = null;
            Reject<ArgumentException>(c, () => vm.GenerateCardsFromText(new string('x', 1048577), ""), "Invalid generation rejected");
            c.That(vm.Decks.Count == before && vm.ActiveDeck == active, "Cancellation/failure never partially inserts");
        }));
        await c.CaseAsync("lazy-composition-retention", async () =>
        {
            int vms = 0, policies = 0, generators = 0;
            var clock = new Clock();
            using var host = StudioBootstrap.BuildHost(new StudioPathService(Path.Combine(Path.GetTempPath(), "axora-m1-no-io")), services =>
            {
                services.AddSingleton<FlashcardReviewPolicy>(_ => { policies++; return new(); });
                services.AddSingleton<FlashcardTextGenerator>(_ => { generators++; return new(); });
                services.AddSingleton<FlashcardsViewModel>(sp => { vms++; return new(sp.GetRequiredService<FlashcardReviewPolicy>(), sp.GetRequiredService<FlashcardTextGenerator>(), clock); });
            });
            await host.StartAsync();
            var lazy = new Lazy<FlashcardsViewModel>(host.Services.GetRequiredService<Func<FlashcardsViewModel>>());
            c.That(vms == 0 && policies == 0 && generators == 0 && !lazy.IsValueCreated, "Bootstrap/delegate resolution constructs no feature");
            var shell = host.Services.GetRequiredService<ShellViewModel>();
            c.That(shell.SelectedRoute == StudioRoute.Home && (int)StudioRoute.Flashcards == 3, "Home default and append-only route");
            foreach (var route in new[] { StudioRoute.Home, StudioRoute.Settings, StudioRoute.About })
            {
                shell.Navigate(route);
                c.That(MainWindow.ResolveFlashcards(shell.SelectedRoute, lazy) is null && vms == 0 && policies == 0 && generators == 0, "Non-feature route stays lazy " + route);
            }
            shell.Navigate(StudioRoute.Flashcards); var vm = MainWindow.ResolveFlashcards(shell.SelectedRoute, lazy)!;
            c.That(vms == 1 && policies == 1 && generators == 1, "First Flashcards route constructs once");
            vm.CreateDeck(); vm.RateCard(CardDifficulty.Easy); vm.FlipCard();
            var deck = vm.ActiveDeck; var card = vm.CurrentCard; var count = vm.Decks.Count;
            shell.Navigate(StudioRoute.Home); _ = MainWindow.ResolveFlashcards(shell.SelectedRoute, lazy);
            shell.Navigate(StudioRoute.Flashcards); var reused = MainWindow.ResolveFlashcards(shell.SelectedRoute, lazy);
            c.That(ReferenceEquals(vm, reused) && ReferenceEquals(deck, reused!.ActiveDeck) && ReferenceEquals(card, reused.CurrentCard)
                && reused.IsCardFlipped && reused.Decks.Count == count && card!.ReviewCount == 1, "Production resolver retains created deck/review/card/flip session");
            c.That(vms == 1 && policies == 1 && generators == 1, "Returning never reconstructs feature");
            Reject<ArgumentException>(c, () => MainWindow.ResolveFlashcards((StudioRoute)999, lazy), "Invalid route never resolves feature");
            c.That(!typeof(FlashcardsViewModel).Assembly.GetReferencedAssemblies().Any(a => a.Name!.StartsWith("Axora.Desktop")), "No legacy assembly dependency");
            await host.StopAsync();
        });
        await c.CaseAsync("keyboard-routing", () => Sync(() =>
        {
            foreach (var key in new[] { VirtualKey.Space, VirtualKey.Enter, VirtualKey.Left, VirtualKey.A, VirtualKey.Right, VirtualKey.D,
                VirtualKey.Number1, VirtualKey.Number2, VirtualKey.Number3, VirtualKey.NumberPad1, VirtualKey.NumberPad2, VirtualKey.NumberPad3 })
            {
                c.That(FlashcardsPage.ShouldHandleStudyKey(key, false, false, false, false), "Study key admitted " + key);
                c.That(!FlashcardsPage.ShouldHandleStudyKey(key, true, false, false, false), "Handled key ignored " + key);
                c.That(!FlashcardsPage.ShouldHandleStudyKey(key, false, true, false, false), "Editing/list focus protected " + key);
                c.That(!FlashcardsPage.ShouldHandleStudyKey(key, false, false, false, true), "Modified shortcut protected " + key);
            }
            c.That(!FlashcardsPage.ShouldHandleStudyKey(VirtualKey.Space, false, false, true, false)
                && !FlashcardsPage.ShouldHandleStudyKey(VirtualKey.Enter, false, false, true, false), "Buttons own Space/Enter; no double activation");
            c.That(FlashcardsPage.ShouldHandleStudyKey(VirtualKey.Number1, false, false, true, false), "Card button focus permits rating shortcut");
            c.That(!FlashcardsPage.ShouldHandleStudyKey(VirtualKey.F1, false, false, false, false), "Unrelated key ignored");
        }));
    }
}
