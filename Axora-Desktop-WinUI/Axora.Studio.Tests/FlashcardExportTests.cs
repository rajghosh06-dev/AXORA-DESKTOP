using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Axora.Studio.Models;
using Axora.Studio.Services;
using Axora.Studio.ViewModels;
using Axora.Studio.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Axora.Studio.Tests;

internal static class FlashcardExportTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[]
    {
        "snapshot-unavailable", "snapshot-values", "snapshot-isolation", "snapshot-validation", "filename",
        "csv-roundtrip", "csv-reject", "anki-roundtrip", "anki-reject", "json-schema", "json-reject", "codec-size-memory",
        "publish-new", "publish-empty-formats", "publish-existing", "replacement-unapproved", "destination-appears",
        "destination-changes", "destination-disappears", "final-window-backup", "locked-readonly-denied", "write-flush-fault",
        "stage-validation-fault", "stage-identity-swap", "publication-no-change", "native-publication-exception", "publication-partial", "publication-ambiguous", "post-verify-fault",
        "cancel-precommit", "cancel-postadmission", "cleanup-canary", "cleanup-warning", "cleanup-identity-swap",
        "oversized-stage-target", "protected-boundaries", "unsupported-paths", "hardlink-reparse", "publisher-idle",
        "precommit-backup-original", "precommit-backup-cancel", "precommit-backup-occupied", "final-stage-destination-change",
        "final-window-backup-occupied", "backup-reservation-required", "reservation-creation-occupied",
        "reservation-identity-swap", "reservation-disappears", "reservation-cancel", "reservation-postcheck-race",
        "coordinator-lazy", "coordinator-busy", "coordinator-picker-terminals", "coordinator-consent", "coordinator-destination-change",
        "coordinator-status", "coordinator-navigation", "coordinator-stop-picker", "coordinator-stop-staging",
        "coordinator-stop-admitted", "coordinator-stop-verification", "coordinator-recovery",
        "picker-adapter-contracts", "picker-adapter-cancel-release", "picker-adapter-failure", "picker-owner-guard", "export-performance",
        "session-shutdown-context", "picker-adapter-ui-close", "consent-partial-button-init",
        "consent-first-button-fault", "consent-success-control", "consent-taskdialog-failure"
    });
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
    private sealed class Clock : TimeProvider
    {
        public int Calls;
        public override DateTimeOffset GetUtcNow() { Calls++; return Now.AddTicks(1234567); }
    }
    private static FlashcardsViewModel Vm(Clock? clock = null) => new(new(), new(), clock ?? new());
    private static FlashcardExportSnapshot Snapshot(bool empty = false)
    {
        var cards = empty ? Array.Empty<FlashcardExportCard>() : new[]
        {
            new FlashcardExportCard(Guid.NewGuid().ToString("N"), "=SUM(1,2)\t\"Q\"\r\nline\nनमस्ते 😀", "<b>&literal</b>\ranswer\t", CardDifficulty.Easy, 1, 2.65, 2, Now, Now.AddDays(2)),
            new FlashcardExportCard(Guid.NewGuid().ToString("N"), "", "+@-formula", CardDifficulty.Hard, int.MaxValue, 1.3, 36500, null, null)
        };
        return new(Now, new(Guid.NewGuid().ToString("N"), "Unicode café 😀", "Description\n&<>", null, cards));
    }
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "axora-export-tests-" + Guid.NewGuid().ToString("N"));
        public string Target => Path.Combine(Root, "cards.json");
        public StudioPathService Paths { get; }
        public Fixture() { Directory.CreateDirectory(Root); Paths = new(Path.Combine(Root, "operational")); }
        public ExportFilePublisher Publisher(Action<ExportBoundary, ExportOperationContext>? boundary = null,
            Action<ExportOperationContext>? finalStageValidated = null, Action<ExportOperationContext>? reservationPreparing = null,
            Action<ExportOperationContext>? beforeNativeReplacement = null) => new(Paths, boundary, finalStageValidated, reservationPreparing, beforeNativeReplacement);
        public ExportDestinationPlan Plan(ExportFilePublisher p, bool approve = true, string? destination = null, FlashcardExportFormat format = FlashcardExportFormat.AxoraJson)
        {
            var result = p.Prepare(destination ?? Target, format);
            if (result.Plan is null) throw new InvalidOperationException("Fixture prepare failed: " + result.ReasonCode);
            return result.Plan.Existing is not null && approve ? result.Plan.ApproveReplacement() : result.Plan;
        }
        public void Dispose()
        {
            // Exclusive test-owned root, no user inputs. Links are removed in their own test before root cleanup.
            foreach (string path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(Root, recursive: true);
        }
    }
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }
    private static void Reject(Checks c, Action run, string name)
    { try { run(); c.That(false, name); } catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or JsonException) { c.That(true, name); } }
    private static byte[] Encode(FlashcardExportSnapshot snapshot, FlashcardExportFormat format)
    { using var stream = new MemoryStream(); FlashcardExportCodec.Write(stream, snapshot, format); return stream.ToArray(); }
    private static void Validate(byte[] bytes, FlashcardExportSnapshot snapshot, FlashcardExportFormat format)
    { using var stream = new MemoryStream(bytes); FlashcardExportCodec.Validate(stream, snapshot, format); }
    private static void ValidFile(Checks c, string path, FlashcardExportSnapshot snapshot, FlashcardExportFormat format)
    { using var file = File.OpenRead(path); FlashcardExportCodec.Validate(file, snapshot, format); c.That(true, "Independent reopened artifact matches snapshot"); }
    public static async Task RunAsync(Checks c)
    {
        await RunIntegrationAsync(c);
        await c.CaseAsync("snapshot-unavailable", () => Sync(() =>
        {
            var clock = new Clock(); var vm = Vm(clock); vm.ActiveDeck = null; clock.Calls = 0;
            var status = vm.Status; var result = vm.CaptureExportSnapshot();
            c.That(result.Failure == ExportResultState.Rejected && result.Snapshot is null && clock.Calls == 0 && vm.Status == status, "No active deck typed rejection without mutation/clock");
        }));
        await c.CaseAsync("snapshot-values", () => Sync(() =>
        {
            var clock = new Clock(); var vm = Vm(clock); vm.FlipCard(); var deck = vm.ActiveDeck!; var card = vm.CurrentCard!;
            var studied = deck.LastStudied; int index = vm.CurrentCardIndex; string status = vm.Status; clock.Calls = 0;
            var s = vm.CaptureExportSnapshot().Snapshot!;
            c.That(clock.Calls == 1 && s.ExportedAtUtc == Now.AddTicks(1234567), "One exportedAt observation");
            c.That(s.Deck.DeckId == deck.DeckId && s.Deck.Title == deck.Title && s.Deck.Description == deck.Description && s.Deck.LastStudiedUtc == studied,
                "Deck metadata and study observation copied");
            c.That(s.Deck.Cards.Select(x => x.CardId).SequenceEqual(deck.Cards.Select(x => x.CardId)), "Card order preserved");
            c.That(deck.LastStudied == studied && card.ReviewCount == 0 && vm.CurrentCardIndex == index && vm.IsCardFlipped && vm.Status == status,
                "Capture changes no study, review, selection, flip, index or status");
            vm.AddDeck(new("Empty")); s = vm.CaptureExportSnapshot().Snapshot!;
            c.That(s.Deck.Cards.Count == 0, "Empty active deck snapshot available");
        }));
        await c.CaseAsync("snapshot-isolation", () => Sync(() =>
        {
            var vm = Vm(); var s = vm.CaptureExportSnapshot().Snapshot!; var bytes = Encode(s, FlashcardExportFormat.AxoraJson);
            vm.RateCard(CardDifficulty.Easy); vm.NextCard(); vm.FlipCard(); vm.PreviousCard(); vm.ActiveDeck = vm.Decks[1]; vm.CreateDeck();
            c.That(bytes.SequenceEqual(Encode(s, FlashcardExportFormat.AxoraJson)) && s.Deck.Cards[0].ReviewCount == 0, "Snapshot survives review, navigation, flip, switch and deck creation");
            c.That(((IList<FlashcardExportCard>)s.Deck.Cards).IsReadOnly, "Owned read-only membership");
            Reject(c, () => new FlashcardExportDeck(s.Deck.DeckId, "Title", "", null, [s.Deck.Cards[0], s.Deck.Cards[0]]), "Duplicate membership rejected");
        }));
        await c.CaseAsync("snapshot-validation", () => Sync(() =>
        {
            var s = Snapshot(); var a = s.Deck.Cards[0];
            foreach (var bad in new[] { a with { CardId = "bad" }, a with { Front = "\uD800" }, a with { EaseFactor = double.NaN },
                a with { IntervalDays = 36501 }, a with { ReviewCount = -1 }, a with { Difficulty = (CardDifficulty)99 },
                a with { NextReviewUtc = null }, a with { NextReviewUtc = Now.AddDays(4) } })
                Reject(c, () => new FlashcardExportDeck(s.Deck.DeckId, "Title", "", null, [bad]), "Invalid snapshot card rejected");
            Reject(c, () => new FlashcardExportSnapshot(default, s.Deck), "Default export timestamp rejected");
            var clock = new BadClock(); var vm = new FlashcardsViewModel(new(), new(), clock); clock.Bad = true;
            c.That(vm.CaptureExportSnapshot().Failure == ExportResultState.Rejected, "Invalid clock returns typed rejection");
        }));
        await c.CaseAsync("filename", () => Sync(() =>
        {
            foreach (var (title, expected) in new[] { ("", "Flashcards.csv"), ("...  ", "Flashcards.csv"), ("a<>:\"/\\|?*\t\n\u007fb", "a_b.csv"),
                ("CON", "_CON.csv"), ("nul.csv", "_nul.csv"), ("CoM1", "_CoM1.csv"), ("LPT³", "_LPT³.csv"), ("COM¹.foo", "_COM¹.foo.csv"),
                (" title.  ", "title.csv"), ("deck.CSV.csv", "deck.csv"), ("cafe\u0301 😀", "café 😀.csv") })
                c.That(ExportFileNameSanitizer.Suggest(title, FlashcardExportFormat.Csv) == expected, "Filename " + title.Replace('\n', ' '));
            foreach (var format in Enum.GetValues<FlashcardExportFormat>())
            {
                string name = ExportFileNameSanitizer.Suggest(string.Concat(Enumerable.Repeat("😀", 200)), format);
                c.That(name.Length <= 120 && name.EndsWith(ExportFileNameSanitizer.Extension(format)) && !name.Contains('�'), "Scalar-safe total filename bound " + format);
                _ = new UTF8Encoding(false, true).GetBytes(name);
            }
            Reject(c, () => ExportFileNameSanitizer.Suggest("\uD800", FlashcardExportFormat.Csv), "Malformed Unicode rejected");
        }));
        await c.CaseAsync("csv-roundtrip", () => Sync(() =>
        {
            var s = Snapshot(); byte[] bytes = Encode(s, FlashcardExportFormat.Csv); Validate(bytes, s, FlashcardExportFormat.Csv);
            string text = Encoding.UTF8.GetString(bytes[3..]);
            c.That(bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 }) && text.StartsWith("Front,Back,Difficulty,IntervalDays\r\n"), "CSV exact BOM/header");
            c.That(text.Contains("\"=SUM(1,2)\t\"\"Q\"\"\r\nline\nनमस्ते 😀\"") && text.EndsWith("\"Hard\",\"36500\"\r\n"), "CSV preserves formula, comma, tab, Unicode, embedded newlines and quoted data");
            var empty = Snapshot(true); bytes = Encode(empty, FlashcardExportFormat.Csv); Validate(bytes, empty, FlashcardExportFormat.Csv);
            c.That(Encoding.UTF8.GetString(bytes[3..]) == "Front,Back,Difficulty,IntervalDays\r\n", "Empty CSV header only");
        }));
        await c.CaseAsync("csv-reject", () => Sync(() =>
        {
            var s = Snapshot(); var bytes = Encode(s, FlashcardExportFormat.Csv);
            foreach (byte[] bad in new[] { bytes[3..], bytes[..^1], bytes.Concat(new byte[] { 10 }).ToArray(),
                Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes[3..]).Replace("\"Easy\"", "Easy"))).ToArray() })
                Reject(c, () => Validate(bad, s, FlashcardExportFormat.Csv), "CSV independent grammar rejects malformed input");
        }));
        await c.CaseAsync("anki-roundtrip", () => Sync(() =>
        {
            var s = Snapshot(); byte[] bytes = Encode(s, FlashcardExportFormat.AnkiText); Validate(bytes, s, FlashcardExportFormat.AnkiText);
            string text = Encoding.UTF8.GetString(bytes);
            c.That(text.StartsWith("#separator:Tab\n#html:false\n#columns:Front\tBack\n") && bytes[0] == '#', "Exact Anki headers, real TAB, no BOM");
            c.That(text.Contains("\"<b>&literal</b>\ranswer\t\"") && !text.Contains("\"Easy\"") && text.EndsWith("\"\"\t\"+@-formula\"\n"), "Two fields; literal HTML/tabs/newlines/empty/Unicode preserved");
            var empty = Snapshot(true); Validate(Encode(empty, FlashcardExportFormat.AnkiText), empty, FlashcardExportFormat.AnkiText);
            c.That(Encoding.UTF8.GetString(Encode(empty, FlashcardExportFormat.AnkiText)) == "#separator:Tab\n#html:false\n#columns:Front\tBack\n", "Empty Anki headers only; no physical import claim");
        }));
        await c.CaseAsync("anki-reject", () => Sync(() =>
        {
            var s = Snapshot(); string text = Encoding.UTF8.GetString(Encode(s, FlashcardExportFormat.AnkiText));
            foreach (string bad in new[] { "\uFEFF" + text, text.Replace("#html:false", "#html:true"), text.Replace("Front\tBack", "Front,Back"), text + "\"extra\"\n", text[..^1] })
                Reject(c, () => Validate(Encoding.UTF8.GetBytes(bad), s, FlashcardExportFormat.AnkiText), "Anki grammar rejects mismatch");
        }));
        await c.CaseAsync("json-schema", () => Sync(() =>
        {
            var s = Snapshot(); byte[] bytes = Encode(s, FlashcardExportFormat.AxoraJson); Validate(bytes, s, FlashcardExportFormat.AxoraJson);
            using var doc = JsonDocument.Parse(bytes); var root = doc.RootElement; var deck = root.GetProperty("deck"); var card = deck.GetProperty("cards")[0];
            c.That(root.EnumerateObject().Select(p => p.Name).SequenceEqual(new[] { "format", "schemaVersion", "exportedAtUtc", "deck" }), "Stable envelope order/property set");
            c.That(deck.EnumerateObject().Select(p => p.Name).SequenceEqual(new[] { "deckId", "title", "description", "lastStudiedUtc", "cards" }), "Stable deck properties without colorTag");
            c.That(card.EnumerateObject().Select(p => p.Name).SequenceEqual(new[] { "cardId", "front", "back", "difficulty", "reviewCount", "easeFactor", "intervalDays", "lastReviewedUtc", "nextReviewUtc" }), "Stable complete card schema");
            c.That(root.GetProperty("format").GetString() == "axora.flashcards" && root.GetProperty("schemaVersion").GetInt32() == 1
                && root.GetProperty("exportedAtUtc").GetString() == "2026-10-03T00:00:00.0000000Z", "Exact schema and seven-digit UTC");
            c.That(deck.GetProperty("lastStudiedUtc").ValueKind == JsonValueKind.Null && deck.GetProperty("cards")[1].GetProperty("lastReviewedUtc").ValueKind == JsonValueKind.Null,
                "Unknown history retained as explicit null including positive review count");
            c.That(bytes[0] == '{' && bytes.SequenceEqual(Encode(s, FlashcardExportFormat.AxoraJson)), "No BOM and deterministic bytes");
        }));
        await c.CaseAsync("json-reject", () => Sync(() =>
        {
            var s = Snapshot(); string text = Encoding.UTF8.GetString(Encode(s, FlashcardExportFormat.AxoraJson));
            var bads = new[] { text.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), text.Replace("\"schemaVersion\":1", "\"schemaVersion\":1.0"),
                text.Replace("\"schemaVersion\":1", "\"schemaVersion\":\"1\""), text.Replace("axora.flashcards", "other"),
                text.Insert(1, "\"extra\":0,"), text.Insert(1, "\"schemaVersion\":1,"), text.Replace("\"schemaVersion\":1,", ""),
                text.Replace(s.Deck.DeckId, "bad"), text.Replace(s.Deck.Cards[1].CardId, s.Deck.Cards[0].CardId),
                text.Replace("\"easeFactor\":2.65", "\"easeFactor\":1e999"), text.Replace("\"easeFactor\":2.65", "\"easeFactor\":0"),
                text.Replace("\"intervalDays\":2", "\"intervalDays\":36501"), text.Replace("\"reviewCount\":1", "\"reviewCount\":-1"),
                text.Replace("2026-10-03T00:00:00.0000000Z", "2026-10-03T00:00:00+00:00"),
                text.Replace("\"nextReviewUtc\":\"2026-10-05T00:00:00.0000000Z\"", "\"nextReviewUtc\":null"),
                text.Replace("\"difficulty\":\"Easy\"", "\"difficulty\":\"easy\""),
                text.Replace("\"deck\":{", "\"deck\":{\"title\":\"duplicate\","),
                text.Replace("\"deck\":{", "\"deck\":{\"unknown\":0,"),
                text.Replace("\"reviewCount\":1,", ""), text.Replace("\"reviewCount\":1", "\"reviewCount\":1,\"reviewCount\":1"),
                text.Replace("\"reviewCount\":1", "\"reviewCount\":1,\"unknown\":0"),
                text.Replace("\"front\":", "\"frontUnknown\":"), text.Replace("\"lastStudiedUtc\":null", "\"lastStudiedUtc\":0"),
                "\uFEFF" + text, text + "{}" };
            foreach (string bad in bads) Reject(c, () => Validate(Encoding.UTF8.GetBytes(bad), s, FlashcardExportFormat.AxoraJson), "JSON schema/value violation rejected independently");
        }));
        await c.CaseAsync("codec-size-memory", () => Sync(() =>
        {
            string field = string.Concat(Enumerable.Repeat("😀", 4096));
            var s = new FlashcardExportSnapshot(Now, new(Guid.NewGuid().ToString("N"), "Maximum", "", null,
                Enumerable.Range(0, 500).Select(_ => new FlashcardExportCard(Guid.NewGuid().ToString("N"), field, field, CardDifficulty.Medium, 0, 2.5, 1, null, null))));
            using var f = new Fixture(); string path = Path.Combine(f.Root, "maximum.json");
            var timing = System.Diagnostics.Stopwatch.StartNew();
            using (var file = File.Create(path)) FlashcardExportCodec.Write(file, s, FlashcardExportFormat.AxoraJson);
            ValidFile(c, path, s, FlashcardExportFormat.AxoraJson);
            c.That(new FileInfo(path).Length > 49_000_000 && new FileInfo(path).Length < FlashcardExportCodec.ArtifactByteLimit, "500-card worst escaped scalar output fits ceiling with streaming validation");
            Console.WriteLine("ARTIFACT: 500-card supplementary-scalar JSON bytes=" + new FileInfo(path).Length);
            Console.WriteLine($"PERFORMANCE: near-limit codec write+independent-validation ms={timing.Elapsed.TotalMilliseconds:0.00}; peakWorkingSet={System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64}");
            string oversized = Path.Combine(f.Root, "oversized.json"); using (var file = File.Create(oversized)) file.SetLength(FlashcardExportCodec.ArtifactByteLimit + 1);
            using var huge = new PaddingStream(FlashcardExportCodec.ArtifactByteLimit + 1);
            Reject(c, () => FlashcardExportCodec.Validate(huge, Snapshot(true), FlashcardExportFormat.AxoraJson), "Reader byte ceiling rejects huge whitespace without DOM");
        }));
        await c.CaseAsync("publish-new", async () =>
        {
            using var f = new Fixture(); var p = f.Publisher(); var s = Snapshot(); var plan = f.Plan(p);
            c.That(plan.Existing is null && !File.Exists(f.Target), "Read-only preparation leaves new target absent");
            var result = await p.PublishAsync(plan, s);
            c.That(result.State == ExportResultState.Published && result.CommitAttempted && result.RecoveryPaths.Count == 0 && result.CleanupWarnings.Count == 0, "New publication verified and settled");
            ValidFile(c, f.Target, s, FlashcardExportFormat.AxoraJson);
            c.That(Directory.GetFileSystemEntries(f.Root).Length == 1, "New success leaves only destination");
        });
        await c.CaseAsync("publish-empty-formats", async () =>
        {
            using var f = new Fixture(); var s = Snapshot(true); var p = f.Publisher();
            foreach (var format in Enum.GetValues<FlashcardExportFormat>())
            {
                string path = Path.Combine(f.Root, "empty" + ExportFileNameSanitizer.Extension(format));
                var r = await p.PublishAsync(f.Plan(p, destination: path, format: format), s);
                c.That(r.State == ExportResultState.Published, "Empty deck publication " + format); ValidFile(c, path, s, format);
            }
        });
        await c.CaseAsync("backup-reservation-required", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved reservation transition");
            string? backup = null; bool reserved = false, seenAtAdmission = false, transformed = false;
            var originalId = TestIdentity(f.Target); ExportFileIdentity? reservationId = null;
            var p = f.Publisher((b, x) =>
            {
                if (b == ExportBoundary.BeforeDestinationRecheck)
                {
                    backup = x.Backup; reserved = backup is not null && File.Exists(backup) && new FileInfo(backup).Length == 0;
                    c.That(reserved, "Production created an empty reservation before final destination recheck");
                    if (reserved) reservationId = TestIdentity(backup!);
                }
                if (b == ExportBoundary.CommitAdmitted)
                {
                    seenAtAdmission = backup is not null && File.Exists(backup) && new FileInfo(backup).Length == 0;
                    c.That(seenAtAdmission, "Native admission sees the existing reservation");
                    if (seenAtAdmission) c.That(TestIdentity(backup!) == reservationId, "Exact reservation identity survives until native admission");
                }
                if (b == ExportBoundary.AfterNativePublication)
                {
                    transformed = backup is not null && File.ReadAllText(backup) == "approved reservation transition" && !File.Exists(x.Stage);
                    c.That(transformed, "Real File.Replace transforms existing reservation into displaced original");
                    c.That(TestIdentity(backup!) == originalId && originalId != reservationId, "Native backup inherits approved original identity rather than placeholder identity");
                }
            });
            var s = Snapshot(); var r = await p.PublishAsync(f.Plan(p), s);
            c.That(reserved && seenAtAdmission && transformed && r.State == ExportResultState.Published && r.RecoveryPaths.Count == 0 && r.CleanupWarnings.Count == 0,
                "Owned reservation transitions to verified backup and complete cleanup");
            ValidFile(c, f.Target, s, FlashcardExportFormat.AxoraJson);
            c.That(backup is not null && !File.Exists(backup) && Directory.GetFileSystemEntries(f.Root).Length == 1, "Verified success removes only operation recovery artifacts");
        });
        await c.CaseAsync("reservation-creation-occupied", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved"); string? slot = null, stage = null;
            var p = f.Publisher(reservationPreparing: x => { slot = x.Backup; stage = x.Stage; File.WriteAllText(slot!, "unexpected before CreateNew"); });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(!r.CommitAdmitted && !r.CommitAttempted && r.State == ExportResultState.InvalidDestination && File.ReadAllText(f.Target) == "approved",
                "Occupied CreateNew reservation fails before native admission and preserves destination");
            c.That(slot is not null && File.ReadAllText(slot) == "unexpected before CreateNew" && r.RecoveryPaths.Contains(slot) && !File.Exists(stage),
                "Unowned reservation occupant survives with disclosure and owned stage cleanup");
        });
        await c.CaseAsync("reservation-identity-swap", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved"); string? slot = null;
            var p = f.Publisher((b, x) => { if (b == ExportBoundary.BeforeDestinationRecheck) { slot = x.Backup; File.Move(slot!, Path.Combine(f.Root, "moved-reservation")); File.WriteAllBytes(slot!, []); } });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(!r.CommitAdmitted && !r.CommitAttempted && r.State == ExportResultState.InvalidDestination && File.ReadAllText(f.Target) == "approved",
                "Even identical empty replacement fails tracked reservation identity check");
            c.That(slot is not null && File.Exists(slot) && r.RecoveryPaths.Contains(slot), "Externally replaced reservation never cleaned");
        });
        await c.CaseAsync("reservation-disappears", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved");
            var p = f.Publisher((b, x) => { if (b == ExportBoundary.BeforeDestinationRecheck) File.Delete(x.Backup!); });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(!r.CommitAdmitted && !r.CommitAttempted && r.State == ExportResultState.InvalidDestination && File.ReadAllText(f.Target) == "approved", "Missing reservation cannot become absent-slot publication");
        });
        await c.CaseAsync("reservation-cancel", async () =>
        {
            using var f = new Fixture(); using var cancel = new CancellationTokenSource(); File.WriteAllText(f.Target, "approved");
            var p = f.Publisher((b, _) => { if (b == ExportBoundary.BeforeDestinationRecheck) cancel.Cancel(); });
            var r = await p.PublishAsync(f.Plan(p), Snapshot(), cancel.Token);
            c.That(r.State == ExportResultState.Canceled && !r.CommitAttempted && r.RecoveryPaths.Count == 0 && r.CleanupWarnings.Count == 0
                && File.ReadAllText(f.Target) == "approved" && Directory.GetFileSystemEntries(f.Root).Length == 1, "Precommit cancellation deletes exact owned placeholder/stage only");
        });
        await c.CaseAsync("reservation-postcheck-race", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved"); bool reservationSeen = false, nativeSeen = false;
            var p = f.Publisher((b, x) => { if (b == ExportBoundary.AfterNativePublication) nativeSeen = File.ReadAllText(x.Backup!) == "approved"; },
                beforeNativeReplacement: x => { reservationSeen = File.Exists(x.Backup) && new FileInfo(x.Backup!).Length == 0;
                    File.Move(x.Backup!, Path.Combine(f.Root, "externally-moved-reservation")); File.WriteAllText(x.Backup!, "deliberate same-user intrusion"); });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(reservationSeen && nativeSeen && r.State == ExportResultState.Published, "Controlled post-check intrusion demonstrates native existing-backup overwrite; no universal CAS guarantee");
        });
        await c.CaseAsync("publish-existing", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "original exact bytes"); var p = f.Publisher(); var plan = f.Plan(p); var s = Snapshot();
            c.That(plan.Existing is { Length: 20 } && plan.ReplacementApproved && File.ReadAllText(f.Target) == "original exact bytes", "Read-only fingerprint and explicit approved plan");
            var r = await p.PublishAsync(plan, s);
            c.That(r.State == ExportResultState.Published && r.CleanupWarnings.Count == 0 && r.RecoveryPaths.Count == 0, "Replace verified displaced original then exact backup cleanup");
            ValidFile(c, f.Target, s, FlashcardExportFormat.AxoraJson); c.That(Directory.GetFileSystemEntries(f.Root).Length == 1, "Verified backup/container removed");
        });
        await c.CaseAsync("replacement-unapproved", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "original"); var p = f.Publisher(); var r = await p.PublishAsync(f.Plan(p, approve: false), Snapshot());
            c.That(r.State == ExportResultState.Rejected && !r.CommitAttempted && File.ReadAllText(f.Target) == "original" && Directory.GetFileSystemEntries(f.Root).Length == 1, "No consent plan never stages/replaces");
        });
        await c.CaseAsync("precommit-backup-original", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved original exact bytes 😀");
            byte[] original = File.ReadAllBytes(f.Target); string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original));
            string? backup = null, stage = null; int nativeAdmissions = 0;
            var p = f.Publisher((b, x) =>
            {
                if (b == ExportBoundary.BeforeDestinationRecheck) { backup = x.Backup; stage = x.Stage; File.Move(x.Destination, backup!, overwrite: true); }
                if (b == ExportBoundary.CommitAdmitted) nativeAdmissions++;
            });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(r.State == ExportResultState.DestinationChanged && !r.CommitAdmitted && !r.CommitAttempted && nativeAdmissions == 0,
                "Original moved to reserved backup rejects before native admission");
            c.That(backup is not null && File.Exists(backup) && original.SequenceEqual(File.ReadAllBytes(backup))
                && hash == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(backup))), "Unexpected moved original retains exact bytes/hash");
            c.That(backup is not null && r.RecoveryPaths.Contains(backup) && r.BackupObservation == "PresentInspected" && r.DestinationObservation == "Absent",
                "Moved original disclosed as recovery with truthful destination/backup observations");
            c.That(stage is not null && !File.Exists(stage), "Exclusively created stage cleaned without deleting unexpected backup");
        });
        await c.CaseAsync("precommit-backup-cancel", async () =>
        {
            using var f = new Fixture(); using var cancel = new CancellationTokenSource(); File.WriteAllText(f.Target, "approved");
            string? backup = null, stage = null; int nativeAdmissions = 0;
            var p = f.Publisher((b, x) =>
            {
                if (b == ExportBoundary.BeforeDestinationRecheck) { backup = x.Backup; stage = x.Stage; File.Move(x.Destination, backup!, overwrite: true); cancel.Cancel(); }
                if (b == ExportBoundary.CommitAdmitted) nativeAdmissions++;
            });
            var r = await p.PublishAsync(f.Plan(p), Snapshot(), cancel.Token);
            c.That(r.State == ExportResultState.Canceled && !r.CommitAdmitted && !r.CommitAttempted && nativeAdmissions == 0,
                "Cancellation wins precommit arbitration with occupied backup");
            c.That(backup is not null && File.Exists(backup) && File.ReadAllText(backup) == "approved" && r.RecoveryPaths.Contains(backup),
                "Cancellation preserves and discloses unexpected original backup");
            c.That(r.DestinationObservation == "Absent" && r.BackupObservation == "PresentInspected" && stage is not null && !File.Exists(stage),
                "Canceled result settles observations and owned stage cleanup");
        });
        await c.CaseAsync("precommit-backup-occupied", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved"); string? backup = null, stage = null; int nativeAdmissions = 0;
            var p = f.Publisher((b, x) =>
            {
                if (b == ExportBoundary.BeforeDestinationRecheck) { backup = x.Backup; stage = x.Stage; File.WriteAllText(backup!, "unrelated occupant"); }
                if (b == ExportBoundary.CommitAdmitted) nativeAdmissions++;
            });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(r.State == ExportResultState.InvalidDestination && !r.CommitAdmitted && !r.CommitAttempted && nativeAdmissions == 0,
                "Occupied reserved backup blocks native replacement");
            c.That(File.ReadAllText(f.Target) == "approved" && backup is not null && File.ReadAllText(backup) == "unrelated occupant",
                "Approved destination and unrelated backup occupant remain exact");
            c.That(backup is not null && r.RecoveryPaths.Contains(backup) && stage is not null && !File.Exists(stage),
                "Occupied backup disclosed while owned stage is cleaned");
        });
        await c.CaseAsync("final-stage-destination-change", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved"); string? stage = null; int completions = 0, nativeAdmissions = 0;
            var s = Snapshot();
            var p = f.Publisher((b, _) => { if (b == ExportBoundary.CommitAdmitted) nativeAdmissions++; }, x =>
            {
                completions++; stage = x.Stage;
                c.That(File.ReadAllText(x.Destination) == "approved", "Destination still approved at final stage-validation completion");
                ValidFile(c, x.Stage!, s, FlashcardExportFormat.AxoraJson);
                File.WriteAllText(x.Destination, "actor changed during final validation");
            });
            var r = await p.PublishAsync(f.Plan(p), s);
            c.That(completions == 1 && r.State == ExportResultState.DestinationChanged && !r.CommitAdmitted && !r.CommitAttempted && nativeAdmissions == 0,
                "Final destination recheck follows completed stage validation and prevents native publication");
            c.That(File.ReadAllText(f.Target) == "actor changed during final validation", "Final-validation actor bytes remain untouched");
            c.That(stage is not null && !File.Exists(stage) && r.RecoveryPaths.Count == 0 && Directory.GetFileSystemEntries(f.Root).Length == 1,
                "Rejected final-validation mutation cleans only owned stage/empty recovery");
        });
        await c.CaseAsync("destination-appears", async () =>
        {
            using var f = new Fixture(); var p = f.Publisher((b, x) => { if (b == ExportBoundary.BeforeDestinationRecheck) File.WriteAllText(x.Destination, "new outsider"); });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(r.State == ExportResultState.DestinationChanged && !r.CommitAttempted && File.ReadAllText(f.Target) == "new outsider", "Appearance fails closed without reinterpreting overwrite");
            c.That(Directory.GetFileSystemEntries(f.Root).Length == 1, "Exact stage cleaned after collision");
        });
        await c.CaseAsync("destination-changes", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "original"); var p = f.Publisher((b, x) => { if (b == ExportBoundary.BeforeDestinationRecheck) File.WriteAllText(x.Destination, "changed"); });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(r.State == ExportResultState.DestinationChanged && !r.CommitAttempted && File.ReadAllText(f.Target) == "changed", "Fingerprint mutation rejected before commit");
            c.That(Directory.GetFileSystemEntries(f.Root).Length == 1, "Stage and empty recovery cleaned");
            foreach (bool identitySwap in new[] { false, true })
            {
                using var f2 = new Fixture(); File.WriteAllText(f2.Target, "original"); var timestamp = File.GetLastWriteTimeUtc(f2.Target);
                var p2 = f2.Publisher((b, x) =>
                {
                    if (b != ExportBoundary.BeforeDestinationRecheck) return;
                    if (identitySwap) File.Move(x.Destination, Path.Combine(f2.Root, "relocated-original"));
                    File.WriteAllText(x.Destination, identitySwap ? "original" : "modified"); File.SetLastWriteTimeUtc(x.Destination, timestamp);
                });
                var r2 = await p2.PublishAsync(f2.Plan(p2), Snapshot());
                c.That(r2.State == ExportResultState.DestinationChanged && !r2.CommitAdmitted, "Identity/hash catches same-length same-time change " + identitySwap);
            }
        });
        await c.CaseAsync("destination-disappears", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "original"); var p = f.Publisher((b, x) => { if (b == ExportBoundary.BeforeDestinationRecheck) File.Delete(x.Destination); });
            var r = await p.PublishAsync(f.Plan(p), Snapshot()); c.That(r.State == ExportResultState.DestinationChanged && !File.Exists(f.Target), "Disappearance does not downgrade replacement into new-file publication");
        });
        await c.CaseAsync("final-window-backup", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved"); var s = Snapshot();
            var p = f.Publisher((b, x) => { if (b == ExportBoundary.CommitAdmitted) File.WriteAllText(x.Destination, "late writer"); });
            var r = await p.PublishAsync(f.Plan(p), s);
            c.That(r.State == ExportResultState.Indeterminate && r.CommitAttempted && r.RecoveryPaths.Any(File.Exists), "Late displaced fingerprint mismatch is indeterminate with retained backup");
            c.That(r.RecoveryPaths.Where(File.Exists).Any(x => File.ReadAllText(x) == "late writer"), "Actual displaced late-writer bytes retained"); ValidFile(c, f.Target, s, FlashcardExportFormat.AxoraJson);
            string note = r.RecoveryPaths.Single(x => Path.GetFileName(x) == "recovery.json");
            c.That(new FileInfo(note).Length <= 4096 && !File.ReadAllText(note).Contains(s.Deck.Title) && !File.ReadAllText(note).Contains(s.Deck.Cards[1].Back), "Bounded disclosed recovery note excludes deck/card content");
        });
        await c.CaseAsync("final-window-backup-occupied", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved"); string? backup = null, stage = null;
            var p = f.Publisher((b, x) =>
            {
                if (b == ExportBoundary.CommitAdmitted) { backup = x.Backup; stage = x.Stage; File.WriteAllText(backup!, "late unrelated occupant"); }
            });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(r.State == ExportResultState.Indeterminate && r.CommitAdmitted && !r.CommitAttempted,
                "Final-window occupied backup is inspected before native invocation");
            c.That(File.ReadAllText(f.Target) == "approved" && backup is not null && File.ReadAllText(backup) == "late unrelated occupant",
                "Final-window backup guard prevents overwrite of unrelated occupant");
            c.That(backup is not null && stage is not null && r.RecoveryPaths.Contains(backup) && r.RecoveryPaths.Contains(stage) && File.Exists(stage),
                "Admitted ambiguous recovery retains stage and unexpected backup through settlement");
        });
        await c.CaseAsync("locked-readonly-denied", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "original"); var p = f.Publisher();
            using (var locked = new FileStream(f.Target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                c.That(p.Prepare(f.Target, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Sharing violation never establishes absence");
            File.SetAttributes(f.Target, FileAttributes.ReadOnly);
            c.That(p.Prepare(f.Target, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Read-only rejected without truncation");
            File.SetAttributes(f.Target, FileAttributes.Normal);
            var file = new FileInfo(f.Target); var denied = file.GetAccessControl();
            var rule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
            denied.AddAccessRule(rule);
            try { file.SetAccessControl(denied); c.That(p.Prepare(f.Target, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Actual ACL denial never establishes absence"); }
            finally { denied.RemoveAccessRuleSpecific(rule); file.SetAccessControl(denied); }
            var plan = f.Plan(p); using var hold = new FileStream(f.Target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var r = await p.PublishAsync(plan, Snapshot()); c.That(r.State == ExportResultState.InvalidDestination && !r.CommitAttempted, "Lock acquired after preparation fails before staging");
        });
        await c.CaseAsync("write-flush-fault", async () =>
        {
            foreach (var point in new[] { ExportBoundary.StageOpened, ExportBoundary.AfterStageWrite, ExportBoundary.BeforeDurableFlush })
            {
                using var f = new Fixture(); var p = f.Publisher((b, _) => { if (b == point) throw new IOException("Injected boundary failure"); });
                var r = await p.PublishAsync(f.Plan(p), Snapshot());
                c.That(r.State == ExportResultState.SerializationFailed && !r.CommitAttempted && Directory.GetFileSystemEntries(f.Root).Length == 0, "Write/flush failure removes exact stage " + point);
            }
        });
        await c.CaseAsync("stage-validation-fault", async () =>
        {
            foreach (var point in new[] { ExportBoundary.BeforeStageValidation, ExportBoundary.AfterStageValidation })
            {
                using var f = new Fixture(); var p = f.Publisher((b, x) => { if (b == point) File.WriteAllText(x.Stage!, "malformed"); });
                var r = await p.PublishAsync(f.Plan(p), Snapshot()); c.That(r.State == ExportResultState.StageValidationFailed && !File.Exists(f.Target), "Corrupted stage rejected " + point);
            }
        });
        await c.CaseAsync("stage-identity-swap", async () =>
        {
            using var f = new Fixture(); string moved = Path.Combine(f.Root, "relocated-stage");
            var p = f.Publisher((b, x) => { if (b == ExportBoundary.AfterStageValidation) { File.Move(x.Stage!, moved); File.WriteAllText(x.Stage!, "outsider stage"); } });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(r.State == ExportResultState.StageValidationFailed && !r.CommitAdmitted && File.Exists(moved), "Changed stage identity rejected without sweeping moved artifact");
            c.That(r.CleanupWarnings.Count > 0 && r.RecoveryPaths.Where(File.Exists).Any(x => File.ReadAllText(x) == "outsider stage"), "Stage cleanup refuses replacement identity and discloses retained location");
        });
        await c.CaseAsync("publication-no-change", async () =>
        {
            foreach (bool existing in new[] { false, true })
            {
                using var f = new Fixture(); if (existing) File.WriteAllText(f.Target, "original");
                var p = f.Publisher((b, _) => { if (b == ExportBoundary.CommitAdmitted) throw new IOException("Before native operation"); });
                var r = await p.PublishAsync(f.Plan(p), Snapshot());
                c.That(r.State == ExportResultState.PublicationFailed && r.CommitAdmitted && !r.CommitAttempted && (existing ? File.ReadAllText(f.Target) == "original" : !File.Exists(f.Target)), "Exception inspected and proven no committed change before native attempt");
            }
        });
        await c.CaseAsync("publication-partial", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "original");
            var p = f.Publisher((b, x) => { if (b == ExportBoundary.CommitAdmitted) { File.Move(x.Destination, x.Backup!, overwrite: true); throw new IOException("Partial native-like move"); } });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(r.State == ExportResultState.Indeterminate && !File.Exists(f.Target) && r.RecoveryPaths.Where(File.Exists).Any(x => File.ReadAllText(x) == "original"), "Real partial move retains original backup and stage; no restore");
        });
        await c.CaseAsync("native-publication-exception", async () =>
        {
            foreach (bool existing in new[] { false, true })
            {
                using var f = new Fixture(); if (existing) File.WriteAllText(f.Target, "original"); FileStream? held = null;
                var p = f.Publisher((b, x) => { if (b == ExportBoundary.CommitAdmitted) held = new(existing ? x.Destination : x.Stage!, FileMode.Open, FileAccess.Read, FileShare.Read); });
                try
                {
                    var r = await p.PublishAsync(f.Plan(p), Snapshot());
                    c.That(r.State == ExportResultState.PublicationFailed && r.CommitAdmitted && r.CommitAttempted,
                        "Actual native sharing-violation exception inspected as no change " + existing);
                    c.That(existing ? File.ReadAllText(f.Target) == "original" : !File.Exists(f.Target), "Real API exception preserves destination");
                }
                finally { held?.Dispose(); }
            }
            using var f2 = new Fixture(); File.WriteAllText(f2.Target, "original"); var s = Snapshot();
            var p2 = f2.Publisher((b, _) => { if (b == ExportBoundary.AfterNativePublication) throw new IOException("Post-native exception"); });
            var r2 = await p2.PublishAsync(f2.Plan(p2), s);
            c.That(r2.State == ExportResultState.PublishedButVerificationFailed && r2.RecoveryPaths.Where(File.Exists).Any(x => File.ReadAllText(x) == "original"), "Exception after successful native replacement retains known original");
            ValidFile(c, f2.Target, s, FlashcardExportFormat.AxoraJson);
        });
        await c.CaseAsync("publication-ambiguous", async () =>
        {
            using var f = new Fixture(); FileStream? held = null;
            var p = f.Publisher((b, x) => { if (b == ExportBoundary.AfterNativePublication) { held = new(x.Destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None); throw new IOException("Verification unavailable"); } });
            try { var r = await p.PublishAsync(f.Plan(p), Snapshot()); c.That(r.State == ExportResultState.Indeterminate && r.DestinationObservation == "UnknownInspectionFailed", "Unknown post-exception state cannot be mistaken for absence"); }
            finally { held?.Dispose(); }
        });
        await c.CaseAsync("post-verify-fault", async () =>
        {
            foreach (bool existing in new[] { false, true })
            {
                using var f = new Fixture(); if (existing) File.WriteAllText(f.Target, "original");
                var p = f.Publisher((b, x) => { if (b == ExportBoundary.BeforeFinalVerification) File.WriteAllText(x.Destination, "corrupted after publication"); });
                var r = await p.PublishAsync(f.Plan(p), Snapshot());
                c.That(r.State == ExportResultState.PublishedButVerificationFailed && r.CommitAttempted && File.Exists(f.Target), "Post-publication failure reported truthfully");
                c.That(!existing || r.RecoveryPaths.Where(File.Exists).Any(x => File.ReadAllText(x) == "original"), "Replacement failure retains original recovery");
            }
        });
        await c.CaseAsync("cancel-precommit", async () =>
        {
            foreach (var point in new[] { ExportBoundary.StageOpened, ExportBoundary.AfterStageWrite, ExportBoundary.AfterStageValidation, ExportBoundary.BeforeDestinationRecheck })
            {
                using var f = new Fixture(); File.WriteAllText(f.Target, "original"); using var cancel = new CancellationTokenSource();
                var p = f.Publisher((b, _) => { if (b == point) cancel.Cancel(); }); var r = await p.PublishAsync(f.Plan(p), Snapshot(), cancel.Token);
                c.That(r.State == ExportResultState.Canceled && !r.CommitAttempted && File.ReadAllText(f.Target) == "original" && Directory.GetFileSystemEntries(f.Root).Length == 1, "Precommit cancellation fully settled " + point);
            }
            using var f2 = new Fixture(); using var pre = new CancellationTokenSource(); pre.Cancel(); var p2 = f2.Publisher();
            c.That((await p2.PublishAsync(f2.Plan(p2), Snapshot(), pre.Token)).State == ExportResultState.Canceled, "Pre-canceled worker returns typed settled result");
        });
        await c.CaseAsync("cancel-postadmission", async () =>
        {
            foreach (var point in new[] { ExportBoundary.CommitAdmitted, ExportBoundary.AfterNativePublication, ExportBoundary.BeforeFinalVerification })
            {
                using var f = new Fixture(); File.WriteAllText(f.Target, "original"); using var cancel = new CancellationTokenSource();
                var p = f.Publisher((b, _) => { if (b == point) cancel.Cancel(); }); var s = Snapshot(); var r = await p.PublishAsync(f.Plan(p), s, cancel.Token);
                c.That(r.State == ExportResultState.Published && r.CleanupWarnings.Count == 0 && Directory.GetFileSystemEntries(f.Root).Length == 1, "Post-admission cancellation waits for verification and backup cleanup " + point);
                ValidFile(c, f.Target, s, FlashcardExportFormat.AxoraJson);
            }
            using var f2 = new Fixture(); using var cancel2 = new CancellationTokenSource();
            using var release = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var p2 = f2.Publisher((b, _) => { if (b == ExportBoundary.AfterNativePublication) { entered.SetResult(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new IOException("Test barrier timeout"); } });
            var work = p2.PublishAsync(f2.Plan(p2), Snapshot(), cancel2.Token);
            try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancel2.Cancel(); c.That(!work.IsCompleted, "Cancellation cannot settle task while admitted publication verification is pending"); }
            finally { release.Set(); }
            c.That((await work).State == ExportResultState.Published, "Awaited worker includes post-admission settlement");
        });
        await c.CaseAsync("cleanup-canary", async () =>
        {
            using var f = new Fixture(); string canary = Path.Combine(f.Root, ".axora-export-unrelated.stage"); File.WriteAllText(canary, "canary");
            var p = f.Publisher((b, _) => { if (b == ExportBoundary.AfterStageWrite) throw new IOException(); });
            var r = await p.PublishAsync(f.Plan(p), Snapshot()); c.That(r.State == ExportResultState.SerializationFailed && File.ReadAllText(canary) == "canary" && Directory.GetFileSystemEntries(f.Root).Length == 1, "Cleanup never sweeps similar-name canary");
        });
        await c.CaseAsync("cleanup-warning", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "original");
            var p = f.Publisher((b, _) => { if (b == ExportBoundary.Cleanup) throw new IOException("Cleanup failure"); });
            var s = Snapshot(); var r = await p.PublishAsync(f.Plan(p), s);
            c.That(r.State == ExportResultState.Published && r.CleanupWarnings.Count > 0 && r.RecoveryPaths.Where(File.Exists).Any(x => File.ReadAllText(x) == "original"), "Verified publication remains Published with recovery cleanup warnings"); ValidFile(c, f.Target, s, FlashcardExportFormat.AxoraJson);
        });
        await c.CaseAsync("cleanup-identity-swap", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "original"); bool swapped = false;
            var p = f.Publisher((b, x) => { if (b == ExportBoundary.Cleanup && !swapped && x.Backup is not null) { swapped = true; File.Move(x.Backup, Path.Combine(f.Root, "rescued-original")); File.WriteAllText(x.Backup, "outsider"); } });
            var r = await p.PublishAsync(f.Plan(p), Snapshot());
            c.That(r.State == ExportResultState.Published && r.CleanupWarnings.Count > 0 && r.RecoveryPaths.Where(File.Exists).Any(x => File.ReadAllText(x) == "outsider"), "Identity-swapped backup is never deleted");
            c.That(File.ReadAllText(Path.Combine(f.Root, "rescued-original")) == "original", "Unrelated relocated original preserved");
        });
        await c.CaseAsync("oversized-stage-target", async () =>
        {
            using var f = new Fixture(); using (var file = File.Create(f.Target)) file.SetLength(FlashcardExportCodec.ArtifactByteLimit + 1);
            var p = f.Publisher(); c.That(p.Prepare(f.Target, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Oversized existing file cannot be replacement"); File.Delete(f.Target);
            p = f.Publisher((b, x) => { if (b == ExportBoundary.BeforeStageValidation) { using var file = File.OpenWrite(x.Stage!); file.SetLength(FlashcardExportCodec.ArtifactByteLimit + 1); } });
            var r = await p.PublishAsync(f.Plan(p), Snapshot()); c.That(r.State == ExportResultState.StageValidationFailed && !File.Exists(f.Target), "Oversized stage never admitted");
        });
        await c.CaseAsync("protected-boundaries", () => Sync(() =>
        {
            using var f = new Fixture(); Directory.CreateDirectory(f.Paths.Root); var p = f.Publisher();
            c.That(p.Prepare(Path.Combine(f.Paths.Root, "any.json"), FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Entire operational root protected");
            var shortRoot = new StringBuilder(32768); uint shortCount = GetShortPathName(f.Paths.Root, shortRoot, shortRoot.Capacity);
            if (shortCount > 0 && shortCount < shortRoot.Capacity)
                c.That(p.Prepare(Path.Combine(shortRoot.ToString(), "any.json"), FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Handle canonicalization rejects protected-root short-name alias");
            c.That(p.Prepare(Path.Combine(AppContext.BaseDirectory, "any.json"), FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Current application directory protected");
            string neighbor = f.Paths.Root + "-neighbor"; Directory.CreateDirectory(neighbor);
            c.That(p.Prepare(Path.Combine(neighbor, "cards.json"), FlashcardExportFormat.AxoraJson).Plan is not null, "Canonical boundary does not overblock similarly prefixed sibling");
        }));
        await c.CaseAsync("unsupported-paths", () => Sync(() =>
        {
            using var f = new Fixture(); var p = f.Publisher();
            foreach (string path in new[] { "\\\\server\\share\\cards.json", "\\\\?\\C:\\cards.json", "\\\\.\\NUL", "relative.json", f.Target + ":ads", Path.Combine(f.Root, "NUL.json"),
                Path.Combine(f.Root, "bad?.json"), Path.Combine(f.Root, "trailing .\\cards.json"), f.Target + " ", Path.ChangeExtension(f.Target, "csv"),
                Path.Combine(f.Root, new string('x', 252) + ".json"), Path.Combine(f.Root, "missing-parent", "cards.json") })
                c.That(p.Prepare(path, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Unsupported path rejected");
            Directory.CreateDirectory(f.Target); c.That(p.Prepare(f.Target, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Directory target rejected");
            Directory.Delete(f.Target); File.WriteAllText(f.Target, "placeholder fixture"); File.SetAttributes(f.Target, FileAttributes.Offline);
            c.That(p.Prepare(f.Target, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Actual offline attribute rejected before reading placeholder content");
            File.SetAttributes(f.Target, FileAttributes.Normal);
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && (d.DriveType != DriveType.Fixed || d.DriveFormat != "NTFS")))
                c.That(p.Prepare(Path.Combine(drive.RootDirectory.FullName, "axora-never-created.json"), FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Installed unsupported volume rejected");
            Console.WriteLine("ENVIRONMENT: network/removable/unsupported filesystem cases limited to installed drives; syntax and fixed-NTFS API gate covered.");
        }));
        await c.CaseAsync("hardlink-reparse", () => Sync(() =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "original"); string link = Path.Combine(f.Root, "hardlink.json");
            if (!CreateHardLink(link, f.Target, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var p = f.Publisher(); c.That(p.Prepare(link, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination && p.Prepare(f.Target, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Hardlink aliases both rejected"); File.Delete(link);
            string symlink = Path.Combine(f.Root, "link.json"), directoryLink = Path.Combine(f.Root, "directory-link");
            try
            {
                File.CreateSymbolicLink(symlink, f.Target); Directory.CreateSymbolicLink(directoryLink, f.Root);
                c.That(p.Prepare(symlink, FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Reparse file target rejected");
                c.That(p.Prepare(Path.Combine(directoryLink, "new.json"), FlashcardExportFormat.AxoraJson).Failure == ExportResultState.InvalidDestination, "Traversed reparse directory rejected");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException && (ex.HResult & 0xffff) == 1314)
            { Console.WriteLine("ENVIRONMENT-NOT-AVAILABLE: symbolic-link creation privilege. Hardlink cases ran; symbolic-link rejection remains unverified."); }
            finally { if (File.Exists(symlink)) File.Delete(symlink); if (Directory.Exists(directoryLink)) Directory.Delete(directoryLink); }
        }));
        await c.CaseAsync("publisher-idle", () => Sync(() =>
        {
            using var f = new Fixture(); int calls = 0; _ = f.Publisher((_, _) => calls++);
            c.That(calls == 0 && Directory.GetFileSystemEntries(f.Root).Length == 0 && !Directory.Exists(f.Paths.Root), "Publisher constructor has no file work or dormant fault callback");
        }));
    }
    private static async Task RunIntegrationAsync(Checks c)
    {
        await c.CaseAsync("export-performance", async () =>
        {
            using var f = new Fixture(); var snapshot = Snapshot(); var publisher = f.Publisher();
            foreach (var format in new[] { FlashcardExportFormat.Csv, FlashcardExportFormat.AxoraJson })
            {
                string path = Path.Combine(f.Root, "measured" + ExportFileNameSanitizer.Extension(format));
                var plan = f.Plan(publisher, destination: path, format: format);
                var timer = System.Diagnostics.Stopwatch.StartNew(); var result = await publisher.PublishAsync(plan, snapshot); timer.Stop();
                c.That(result.State == ExportResultState.Published, "Measured publication remains verified " + format);
                Console.WriteLine($"PERFORMANCE: format={format}; publicationMs={timer.Elapsed.TotalMilliseconds:0.00}; bytes={new FileInfo(path).Length}; peakWorkingSet={System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64}; dialogTimeExcluded=true");
            }
        });
        await c.CaseAsync("coordinator-lazy", async () =>
        {
            using var f = new Fixture(); int constructions = 0; var picker = new ProbePicker(f.Target);
            using var host = StudioBootstrap.BuildHost(f.Paths, services => services.AddSingleton<IStudioSavePicker>(_ => { constructions++; return picker; }));
            var factory = host.Services.GetRequiredService<Func<FlashcardExportCoordinator>>();
            var unused = new StudioExportSession(factory); int shutdowns = 0;
            _ = host.Services.GetRequiredService<FlashcardsViewModel>();
            c.That(!unused.IsCreated && constructions == 0 && !Directory.Exists(f.Paths.Root), "Host/factory/Flashcards entry performs no export construction or filesystem work");
            var firstStop = unused.ShutdownAsync(() => { shutdowns++; return Task.CompletedTask; });
            c.That(ReferenceEquals(firstStop, unused.ShutdownAsync(() => throw new InvalidOperationException())), "Unused shutdown has one owned task"); await firstStop;
            c.That(!unused.IsCreated && constructions == 0 && shutdowns == 1, "Unused shutdown never constructs coordinator");
            var session = new StudioExportSession(factory);
            var first = await session.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(Snapshot(), null, "Captured"));
            File.Delete(f.Target); await session.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(Snapshot(), null, "Captured"));
            c.That(first.State == ExportResultState.Published && session.IsCreated && constructions == 1, "Only first real export creates the session singleton once");
            await session.ShutdownAsync(() => Task.CompletedTask);
        });
        await c.CaseAsync("coordinator-busy", async () =>
        {
            using var f = new Fixture(); var selected = Signal<StudioPickerResult>(); var picker = new ProbePicker(f.Target) { Select = _ => selected.Task };
            var coordinator = new FlashcardExportCoordinator(picker, f.Publisher());
            var first = coordinator.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(Snapshot(), null, "Captured"));
            await picker.Entered.Task;
            var second = await coordinator.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => throw new InvalidOperationException("Must not capture second request"));
            c.That(second.ReasonCode == "Busy" && picker.Selections == 1 && !first.IsCompleted, "Busy second admission does not capture, queue or open another dialog");
            selected.SetResult(new(StudioPickerState.Selected, f.Target)); await first;
        });
        await c.CaseAsync("coordinator-picker-terminals", async () =>
        {
            foreach (var terminal in new[] { StudioPickerState.Canceled, StudioPickerState.Failed })
            {
                using var f = new Fixture(); var picker = new ProbePicker(f.Target) { Select = _ => Task.FromResult(new StudioPickerResult(terminal)) };
                var coordinator = new FlashcardExportCoordinator(picker, f.Publisher());
                var result = await coordinator.ExportAsync(1, FlashcardExportFormat.Csv, () => new(Snapshot(), null, "Captured"));
                c.That(result.State == (terminal == StudioPickerState.Canceled ? ExportResultState.Canceled : ExportResultState.Rejected)
                    && Directory.GetFileSystemEntries(f.Root).Length == 0 && !coordinator.IsActive, "Picker terminal settles without destination/artifact creation " + terminal);
            }
        });
        await c.CaseAsync("coordinator-consent", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved consent sentinel"); Guid firstId = Guid.Empty;
            var picker = new ProbePicker(f.Target) { Consent = (id, plan, _) =>
                { firstId = id; c.That(!plan.ReplacementApproved && plan.Existing is not null && File.ReadAllText(f.Target) == "approved consent sentinel", "Consent receives exact unapproved plan before any mutation"); return Task.FromResult(StudioPickerState.Canceled); } };
            var coordinator = new FlashcardExportCoordinator(picker, f.Publisher()); var s = Snapshot();
            var canceled = await coordinator.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(s, null, "Captured"));
            c.That(canceled.State == ExportResultState.Canceled && File.ReadAllText(f.Target) == "approved consent sentinel", "Cancel replacement preserves exact sentinel");
            picker.Consent = (id, plan, _) => { c.That(id != Guid.Empty && id != firstId && !plan.ReplacementApproved, "Second consent has fresh operation identity and approval"); return Task.FromResult(StudioPickerState.Selected); };
            var replaced = await coordinator.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(s, null, "Captured"));
            c.That(replaced.State == ExportResultState.Published && picker.Consents == 2, "Explicit per-operation Replace invokes production publisher"); ValidFile(c, f.Target, s, FlashcardExportFormat.AxoraJson);
        });
        await c.CaseAsync("coordinator-destination-change", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved");
            var picker = new ProbePicker(f.Target) { Consent = (_, _, _) => { File.WriteAllText(f.Target, "changed during consent"); return Task.FromResult(StudioPickerState.Selected); } };
            var result = await new FlashcardExportCoordinator(picker, f.Publisher()).ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(Snapshot(), null, "Captured"));
            c.That(result.State == ExportResultState.DestinationChanged && result.Publication is { CommitAttempted: false } && File.ReadAllText(f.Target) == "changed during consent", "Consent cannot authorize a changed fingerprint");
        });
        await c.CaseAsync("coordinator-status", () => Sync(() =>
        {
            c.That(FlashcardExportCoordinator.Status(ExportResultState.Canceled, 2).Contains("canceled") && FlashcardExportCoordinator.Status(ExportResultState.DestinationChanged, 2).Contains("nothing was overwritten"), "Cancellation and destination-change statuses are explicit");
            c.That(!FlashcardExportCoordinator.Status(ExportResultState.PublishedButVerificationFailed, 2).Contains("successfully")
                && FlashcardExportCoordinator.Status(ExportResultState.Indeterminate, 2).Contains("Indeterminate")
                && FlashcardExportCoordinator.Status(ExportResultState.Published, 2, true).Contains("Cleanup warning"), "Unverified/indeterminate/cleanup statuses preserve safety distinctions");
        }));
        await c.CaseAsync("coordinator-navigation", async () =>
        {
            using var f = new Fixture(); var selection = Signal<StudioPickerResult>(); var picker = new ProbePicker(f.Target) { Select = _ => selection.Task };
            var vm = Vm(); var before = vm.CaptureExportSnapshot().Snapshot!;
            var session = new StudioExportSession(() => new(picker, f.Publisher())); vm.ConfigureExport(session);
            var work = vm.ExportAsync(FlashcardExportFormat.AxoraJson, 1); await picker.Entered.Task;
            vm.NextCard(); vm.ActiveDeck = null; // A route/page can leave while session task remains owned.
            c.That(session.IsActive && vm.IsExportBusy && before.Deck.Cards.All(x => x.ReviewCount == 0), "Navigation does not orphan export or rate captured cards");
            selection.SetResult(new(StudioPickerState.Selected, f.Target)); await work;
            c.That(!session.IsActive && !vm.IsExportBusy && vm.ExportStatus.Contains("successfully"), "Shared ViewModel receives terminal result after navigation"); ValidFile(c, f.Target, before, FlashcardExportFormat.AxoraJson);
        });
        await c.CaseAsync("coordinator-stop-picker", async () =>
        {
            using var f = new Fixture(); var selection = Signal<StudioPickerResult>(); CancellationTokenRegistration registration = default;
            var picker = new ProbePicker(f.Target) { Select = token => { registration = token.Register(() => selection.TrySetResult(new(StudioPickerState.Canceled))); return selection.Task; } };
            var coordinator = new FlashcardExportCoordinator(picker, f.Publisher());
            var work = coordinator.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(Snapshot(), null, "Captured")); await picker.Entered.Task;
            var stop = coordinator.StopAsync(); c.That(ReferenceEquals(stop, coordinator.StopAsync()), "Repeated StopAsync returns same operation settlement task");
            await stop; registration.Dispose();
            var rejected = await coordinator.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => throw new InvalidOperationException());
            c.That((await work).State == ExportResultState.Canceled && picker.CancelRequests == 1 && rejected.ReasonCode == "AdmissionClosed" && !File.Exists(f.Target), "Picker shutdown cancels and permanently closes admission");
        });
        foreach (var pair in new[] { ("coordinator-stop-staging", ExportBoundary.AfterStageWrite), ("coordinator-stop-admitted", ExportBoundary.CommitAdmitted), ("coordinator-stop-verification", ExportBoundary.BeforeFinalVerification) })
        {
            await c.CaseAsync(pair.Item1, async () =>
            {
                using var f = new Fixture(); File.WriteAllText(f.Target, "approved shutdown sentinel");
                using var release = new ManualResetEventSlim(); var entered = Signal<bool>(); var picker = new ProbePicker(f.Target);
                var publisher = f.Publisher((b, _) => { if (b == pair.Item2) { entered.TrySetResult(true); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new IOException("Test barrier timed out"); } });
                var coordinator = new FlashcardExportCoordinator(picker, publisher); var session = new StudioExportSession(() => coordinator);
                var host = new ExportProbeHost(); var lifecycle = new StudioLifecycle(host, () => Task.CompletedTask);
                await lifecycle.StartAsync(_ => Task.CompletedTask, () => { });
                var work = session.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(Snapshot(), null, "Captured"));
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
                var shutdown = session.ShutdownAsync(lifecycle.ShutdownAsync);
                try { c.That(!shutdown.IsCompleted && host.Stops == 0 && host.Disposals == 0 && session.IsActive, "App production session gate retains host beneath held publisher " + pair.Item2); }
                finally { release.Set(); }
                await shutdown; var outcome = await work;
                c.That(outcome.State == (pair.Item2 == ExportBoundary.AfterStageWrite ? ExportResultState.Canceled : ExportResultState.Published)
                    && host.Stops == 1 && host.Disposals == 1 && !session.IsActive, "Export settlement precedes H0 stop/disposal " + pair.Item2);
                if (pair.Item2 == ExportBoundary.AfterStageWrite) c.That(File.ReadAllText(f.Target) == "approved shutdown sentinel" && Directory.GetFileSystemEntries(f.Root).Length == 1, "Precommit shutdown preserves destination and cleans owned artifacts");
                else c.That(outcome.Publication is { CommitAdmitted: true } && outcome.Publication.CleanupWarnings.Count == 0, "Admitted shutdown completes verification/cleanup despite cancellation");
            });
        }
        await c.CaseAsync("session-shutdown-context", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved context sentinel");
            using var release = new ManualResetEventSlim(); var entered = Signal<bool>();
            var publisher = f.Publisher((boundary, _) =>
            { if (boundary == ExportBoundary.CommitAdmitted) { entered.TrySetResult(true); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new IOException("Context barrier timed out"); } });
            var session = new StudioExportSession(() => new(new ProbePicker(f.Target), publisher));
            var host = new ExportProbeHost(); var lifecycle = new StudioLifecycle(host, () => Task.CompletedTask);
            await lifecycle.StartAsync(_ => Task.CompletedTask, () => { });
            var work = session.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(Snapshot(), null, "Captured"));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var unavailableUi = new CaptiveContext(); var previous = SynchronizationContext.Current; Task shutdown;
            try
            {
                SynchronizationContext.SetSynchronizationContext(unavailableUi);
                shutdown = session.ShutdownAsync(lifecycle.ShutdownAsync);
                c.That(ReferenceEquals(shutdown, session.ShutdownAsync(lifecycle.ShutdownAsync)) && !shutdown.IsCompleted
                    && host.Disposals == 0 && unavailableUi.Posts == 0, "Shutdown publishes one owned task without capturing UI context");
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); release.Set(); }
            unavailableUi.Drain(); // Allows a defective historical context-capturing implementation to settle after its failed assertion.
            await shutdown.WaitAsync(TimeSpan.FromSeconds(15));
            c.That((await work).State == ExportResultState.Published && host.Stops == 1 && host.Disposals == 1
                && unavailableUi.Posts == 0, "Admitted settlement and H0 disposal finish without an available UI continuation pump");
        });
        await c.CaseAsync("coordinator-recovery", async () =>
        {
            foreach (var boundary in new[] { ExportBoundary.Cleanup, ExportBoundary.CommitAdmitted, ExportBoundary.BeforeFinalVerification })
            {
                using var f = new Fixture(); File.WriteAllText(f.Target, "approved");
                var publisher = f.Publisher((b, x) => { if (b != boundary) return; if (b == ExportBoundary.Cleanup) throw new IOException(); else File.WriteAllText(x.Destination, "changed"); });
                var result = await new FlashcardExportCoordinator(new ProbePicker(f.Target), publisher).ExportAsync(1, FlashcardExportFormat.AxoraJson, () => new(Snapshot(), null, "Captured"));
                var expected = boundary == ExportBoundary.Cleanup ? ExportResultState.Published : boundary == ExportBoundary.CommitAdmitted ? ExportResultState.Indeterminate : ExportResultState.PublishedButVerificationFailed;
                c.That(result.State == expected && result.Publication is { RecoveryPaths.Count: > 0 } && (boundary != ExportBoundary.Cleanup || result.Message.Contains("Cleanup warning")), "Coordinator propagates production recovery result " + boundary);
            }
        });
        await c.CaseAsync("picker-adapter-contracts", async () =>
        {
            foreach (var format in Enum.GetValues<FlashcardExportFormat>())
            {
                var sta = new ProbeSta(); var dialog = new ProbeDialog(sta); StudioSavePicker.DialogSettings? settings = null;
                var picker = new StudioSavePicker(sta, s => { settings = s; return dialog; }, (_, _, _) => throw new InvalidOperationException(), owner => owner == 123);
                var task = picker.SelectAsync(123, format, "NUL", default); c.That(dialog.Shows == 0, "Native adapter is queued until owning STA executes"); sta.Pump(); var result = await task;
                c.That(settings is { Owner: 123 } && settings.Flags == StudioSavePicker.RequiredFlags && (settings.Flags & 2) == 0
                    && settings.Pattern == "*" + ExportFileNameSanitizer.Extension(format) && settings.Extension == ExportFileNameSanitizer.Extension(format)[1..]
                    && settings.SuggestedName.StartsWith("_NUL") && dialog.Owner == 123, "Picker flags/filter/extension/sanitized filename/HWND " + format);
                c.That(result.State == StudioPickerState.Selected && result.Path == "test-owned-path" && dialog.Paths == 1 && dialog.Disposals == 1, "Path extraction and exactly-once terminal release " + format);
            }
        });
        await c.CaseAsync("picker-adapter-cancel-release", async () =>
        {
            var sta = new ProbeSta(); using var cancel = new CancellationTokenSource(); var dialog = new ProbeDialog(sta) { HResult = StudioSavePicker.ErrorCanceled };
            var picker = new StudioSavePicker(sta, _ => dialog, (_, _, _) => dialog, _ => true);
            dialog.OnShow = () =>
            {
                int cancellationThread = 0;
                Task.Factory.StartNew(() => { cancellationThread = Environment.CurrentManagedThreadId; cancel.Cancel(); },
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).GetAwaiter().GetResult();
                c.That(cancellationThread != Environment.CurrentManagedThreadId && dialog.Closes == 0,
                    "Cancellation on a distinct thread only posts Close across apartments");
                sta.Pump();
            };
            var work = picker.SelectAsync(1, FlashcardExportFormat.Csv, "cards", cancel.Token); sta.Pump(); var result = await work;
            c.That(result.State == StudioPickerState.Canceled && dialog.Closes == 1 && dialog.Paths == 0 && dialog.Disposals == 1 && !sta.HasThreadAccess, "Owning STA closes dialog; Show returns; COM backend release precedes settlement");
            picker.RequestCancel(); sta.Pump(); c.That(dialog.Closes == 1, "Terminal picker unregisters active dialog; stale cancel cannot close another operation");
        });
        await c.CaseAsync("picker-adapter-ui-close", async () =>
        {
            foreach (bool tokenCancellation in new[] { false, true })
            {
                var sta = new ProbeSta(); using var cancel = new CancellationTokenSource();
                var dialog = new ProbeDialog(sta) { HResult = StudioSavePicker.ErrorCanceled };
                var picker = new StudioSavePicker(sta, _ => dialog, (_, _, _) => dialog, _ => true);
                dialog.OnShow = () =>
                {
                    if (tokenCancellation) cancel.Cancel(); else picker.RequestCancel();
                    // No recursive dispatcher pump: the real native modal loop does not drain the
                    // queue whose current callback is still synchronously executing Show.
                    c.That(dialog.Closes == 1, "UI STA cancellation closes active dialog without a second dispatcher callback " + tokenCancellation);
                };
                var task = picker.SelectAsync(1, FlashcardExportFormat.Csv, "cards", cancel.Token);
                sta.Pump();
                c.That((await task).State == StudioPickerState.Canceled && dialog.Closes == 1 && dialog.Disposals == 1,
                    "UI-close cancellation settles only after owning STA dialog release " + tokenCancellation);
            }
        });
        await c.CaseAsync("picker-adapter-failure", async () =>
        {
            foreach (bool releaseFails in new[] { false, true })
            {
                var sta = new ProbeSta(); var dialog = new ProbeDialog(sta) { PathFails = !releaseFails, ReleaseFails = releaseFails };
                var picker = new StudioSavePicker(sta, _ => dialog, (_, _, _) => dialog, _ => true);
                var task = picker.SelectAsync(1, FlashcardExportFormat.Csv, "cards", default); sta.Pump();
                c.That((await task).State == StudioPickerState.Failed && dialog.Disposals == 1, "Extraction/release failure cannot return selection success " + releaseFails);
            }
        });
        await c.CaseAsync("picker-owner-guard", async () =>
        {
            var sta = new ProbeSta(); int creations = 0;
            var picker = new StudioSavePicker(sta, _ => { creations++; return new ProbeDialog(sta); }, (_, _, _) => throw new InvalidOperationException(), _ => false);
            var task = picker.SelectAsync(0, FlashcardExportFormat.Csv, "cards", default); sta.Pump();
            c.That((await task).State == StudioPickerState.Failed && creations == 0, "Zero/invalid owner rejects before COM creation");
            using var f = new Fixture(); File.WriteAllText(f.Target, "approved"); var plan = f.Plan(f.Publisher(), approve: false);
            c.That(await picker.ConfirmReplacementAsync(1, Guid.Empty, plan, default) == StudioPickerState.Failed, "Consent rejects missing operation identity");
        });
        await c.CaseAsync("consent-partial-button-init", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "consent sentinel"); var plan = f.Plan(f.Publisher(), approve: false);
            foreach (string phase in new[] { "BeforeText", "AfterText" })
            {
                var events = new List<string>(); int invokes = 0;
                var sta = new ProbeSta();
                var picker = new StudioSavePicker(sta, _ => throw new InvalidOperationException(),
                    (owner, id, selected) => NativeConsent(owner, id, selected,
                        (index, point) => { if (index == 1 && point == phase) throw new IOException("Second button fault"); },
                        (_, _) => { invokes++; return (0, 100); }, events.Add), _ => true);
                var task = picker.ConfirmReplacementAsync(1, Guid.NewGuid(), plan, default); sta.Pump();
                c.That(await task == StudioPickerState.Failed && invokes == 0, "Partial native button setup cannot show TaskDialog or grant consent " + phase);
                string[] expected = phase == "BeforeText"
                    ? ["ArrayAllocated", "TextAllocated:0", "EntryReady:0", "TextFreed:0", "ArrayFreed"]
                    : ["ArrayAllocated", "TextAllocated:0", "EntryReady:0", "TextAllocated:1", "TextFreed:0", "TextFreed:1", "ArrayFreed"];
                c.That(events.SequenceEqual(expected), "Only allocated button texts and the array are freed once; no uninitialized entry cleanup " + phase);
            }
            c.That(File.ReadAllText(f.Target) == "consent sentinel" && Directory.GetFileSystemEntries(f.Root).Length == 1,
                "Partial consent setup leaves destination exact and creates no export artifact");
        });
        await c.CaseAsync("consent-first-button-fault", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "consent sentinel"); var plan = f.Plan(f.Publisher(), approve: false);
            foreach (string phase in new[] { "BeforeText", "AfterText" })
            {
                var events = new List<string>(); int invokes = 0; var sta = new ProbeSta();
                var picker = new StudioSavePicker(sta, _ => throw new InvalidOperationException(),
                    (owner, id, selected) => NativeConsent(owner, id, selected,
                        (index, point) => { if (index == 0 && point == phase) throw new IOException("First button fault"); },
                        (_, _) => { invokes++; return (0, 100); }, events.Add), _ => true);
                var task = picker.ConfirmReplacementAsync(1, Guid.NewGuid(), plan, default); sta.Pump();
                c.That(await task == StudioPickerState.Failed && invokes == 0, "First button setup fault does not show TaskDialog " + phase);
                string[] expected = phase == "BeforeText" ? ["ArrayAllocated", "ArrayFreed"]
                    : ["ArrayAllocated", "TextAllocated:0", "TextFreed:0", "ArrayFreed"];
                c.That(events.SequenceEqual(expected), "First entry frees exactly the resources actually allocated " + phase);
            }
            c.That(File.ReadAllText(f.Target) == "consent sentinel", "First-button faults preserve destination");
        });
        await c.CaseAsync("consent-success-control", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "consent sentinel"); var plan = f.Plan(f.Publisher(), approve: false);
            foreach (int selected in new[] { 100, 101 })
            {
                var events = new List<string>(); int invokes = 0; var sta = new ProbeSta();
                var picker = new StudioSavePicker(sta, _ => throw new InvalidOperationException(),
                    (owner, id, selectedPlan) => NativeConsent(owner, id, selectedPlan, null,
                        (buffer, defaultButton) =>
                        {
                            invokes++;
                            int stride = checked(4 + IntPtr.Size);
                            c.That(defaultButton == 101 && Marshal.ReadInt32(buffer) == 100 && Marshal.ReadInt32(buffer, stride) == 101,
                                "Cancel remains default; Replace and Cancel native IDs are stable");
                            c.That(Marshal.PtrToStringUni(Marshal.ReadIntPtr(buffer, 4)) == "Replace"
                                && Marshal.PtrToStringUni(Marshal.ReadIntPtr(buffer, stride + 4)) == "Cancel",
                                "Both packed native button entries hold the correct owned UTF-16 text");
                            return (0, selected);
                        }, events.Add), _ => true);
                var task = picker.ConfirmReplacementAsync(1, Guid.NewGuid(), plan, default); sta.Pump();
                c.That(await task == (selected == 100 ? StudioPickerState.Selected : StudioPickerState.Canceled) && invokes == 1,
                    "Normal native consent returns exact Replace/Cancel state " + selected);
                c.That(events.SequenceEqual(["ArrayAllocated", "TextAllocated:0", "EntryReady:0", "TextAllocated:1", "EntryReady:1",
                    "TextFreed:0", "TextFreed:1", "ArrayFreed"]), "Both texts and array freed exactly once after normal consent " + selected);
                c.That(File.ReadAllText(f.Target) == "consent sentinel", "Consent alone does not mutate destination " + selected);
            }
        });
        await c.CaseAsync("consent-taskdialog-failure", async () =>
        {
            using var f = new Fixture(); File.WriteAllText(f.Target, "consent sentinel"); var plan = f.Plan(f.Publisher(), approve: false);
            foreach (bool throws in new[] { false, true })
            {
                var events = new List<string>(); int invokes = 0; var sta = new ProbeSta();
                var picker = new StudioSavePicker(sta, _ => throw new InvalidOperationException(),
                    (owner, id, selected) => NativeConsent(owner, id, selected, null,
                        (_, _) => { invokes++; if (throws) throw new IOException("TaskDialog fault"); return (unchecked((int)0x80004005), 0); },
                        events.Add), _ => true);
                var task = picker.ConfirmReplacementAsync(1, Guid.NewGuid(), plan, default); sta.Pump();
                c.That(await task == StudioPickerState.Failed && invokes == 1, "Native TaskDialog failure cannot become consent " + throws);
                c.That(events.SequenceEqual(["ArrayAllocated", "TextAllocated:0", "EntryReady:0", "TextAllocated:1", "EntryReady:1",
                    "TextFreed:0", "TextFreed:1", "ArrayFreed"]), "TaskDialog failure settles all owned native allocations " + throws);
                c.That(File.ReadAllText(f.Target) == "consent sentinel", "TaskDialog failure preserves destination " + throws);
            }
        });
    }
    private static StudioSavePicker.IDialog NativeConsent(nint owner, Guid id, ExportDestinationPlan plan,
        Action<int, string>? boundary, Func<nint, int, (int HResult, int Selected)> invoke, Action<string> trace)
    {
        var type = typeof(StudioSavePicker).GetNestedType("NativeConfirmation", System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native consent implementation missing");
        return (StudioSavePicker.IDialog)(Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, [owner, id, plan, boundary, invoke, trace], null) ?? throw new InvalidOperationException("Native consent instance missing"));
    }
    private static TaskCompletionSource<T> Signal<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class CaptiveContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();
        public int Posts;
        public override void Post(SendOrPostCallback callback, object? state) { lock (_queue) { Posts++; _queue.Enqueue((callback, state)); } }
        public void Drain() { while (true) { (SendOrPostCallback Callback, object? State) item; lock (_queue) { if (_queue.Count == 0) return; item = _queue.Dequeue(); } item.Callback(item.State); } }
    }
    private sealed class ProbePicker(string path) : IStudioSavePicker
    {
        public Func<CancellationToken, Task<StudioPickerResult>>? Select;
        public Func<Guid, ExportDestinationPlan, CancellationToken, Task<StudioPickerState>>? Consent;
        public TaskCompletionSource<bool> Entered = Signal<bool>(); public int Selections, Consents, CancelRequests;
        public Task<StudioPickerResult> SelectAsync(nint owner, FlashcardExportFormat format, string suggested, CancellationToken token)
        { Selections++; Entered.TrySetResult(true); return Select?.Invoke(token) ?? Task.FromResult(new StudioPickerResult(StudioPickerState.Selected, path)); }
        public Task<StudioPickerState> ConfirmReplacementAsync(nint owner, Guid id, ExportDestinationPlan plan, CancellationToken token)
        { Consents++; return Consent?.Invoke(id, plan, token) ?? Task.FromResult(StudioPickerState.Selected); }
        public void RequestCancel() => CancelRequests++;
    }
    private sealed class ProbeSta : StudioSavePicker.IStaDispatcher
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _queue = new();
        private volatile int _owningThread;
        public bool HasThreadAccess => _owningThread == Environment.CurrentManagedThreadId;
        public bool Post(Action action) { _queue.Enqueue(action); return true; }
        public void Pump() { int previous = _owningThread; _owningThread = Environment.CurrentManagedThreadId; try { while (_queue.TryDequeue(out var action)) action(); } finally { _owningThread = previous; } }
    }
    private sealed class ProbeDialog(ProbeSta sta) : StudioSavePicker.IDialog
    {
        public int HResult, Shows, Paths, Closes, Disposals; public nint Owner; public Action? OnShow; public bool PathFails, ReleaseFails;
        public int Show(nint owner) { if (!sta.HasThreadAccess) throw new InvalidOperationException("Wrong STA"); Owner = owner; Shows++; OnShow?.Invoke(); return HResult; }
        public string GetPath() { Paths++; if (PathFails) throw new IOException(); return "test-owned-path"; }
        public void Close() { if (!sta.HasThreadAccess) throw new InvalidOperationException("Wrong close apartment"); Closes++; }
        public void Dispose() { if (!sta.HasThreadAccess) throw new InvalidOperationException("Wrong release apartment"); Disposals++; if (ReleaseFails) throw new IOException(); }
    }
    private sealed class ExportProbeHost : IHost
    {
        public IServiceProvider Services => throw new NotSupportedException(); public int Stops, Disposals;
        public Task StartAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) { Stops++; return Task.CompletedTask; }
        public void Dispose() => Disposals++;
    }
    private sealed class BadClock : TimeProvider
    { public bool Bad; public override DateTimeOffset GetUtcNow() => Bad ? default : Now; }
    // Huge whitespace is generated into caller buffers; test itself does not allocate a whole artifact.
    private sealed class PaddingStream(long length) : Stream
    {
        private long _position;
        public override int Read(byte[] buffer, int offset, int count) { int n = (int)Math.Min(count, length - _position); Array.Fill(buffer, (byte)' ', offset, n); _position += n; return n; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => length; public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    [StructLayout(LayoutKind.Sequential)] private struct TestFileInfo
    { public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    private static ExportFileIdentity TestIdentity(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandle(handle, out var info)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return new(info.Volume, ((ulong)info.IndexHigh << 32) | info.IndexLow);
    }
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle, out TestFileInfo info);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newName, string existing, IntPtr security);
    [DllImport("kernel32.dll", EntryPoint = "GetShortPathNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathName(string path, StringBuilder shortPath, int capacity);
}
