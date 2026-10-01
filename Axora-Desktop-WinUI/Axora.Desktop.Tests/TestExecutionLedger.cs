using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;
using Axora.Desktop.Services;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Tests;

public partial class Program
{
    private enum EvidenceKind { Deterministic, Integration, EnvironmentRuntime, StaticConformance }
    private enum Disposition { Pass, Fail, EnvironmentNotAvailable, SkippedByPolicy, Blocked }

    private sealed record TestGroup(
        string Id, string Name, Func<Task> Run, EvidenceKind Evidence,
        string Environment, TimeSpan Timeout);

    private sealed class GroupResult
    {
        public required TestGroup Group { get; init; }
        public Disposition Disposition { get; set; }
        public string Reason { get; set; } = "";
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Observations { get; set; }
        public int StaticGaps { get; set; }
        public TimeSpan Elapsed { get; set; }
    }

    private static GroupResult? _currentGroup;
    private static readonly List<GroupResult> _ledger = new();
    private static readonly TimeSpan DefaultGroupTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NativeGroupTimeout = TimeSpan.FromMinutes(7);

    private static TestGroup G(string id, string name, Func<Task> run, EvidenceKind kind = EvidenceKind.Deterministic,
        string environment = "none", TimeSpan? timeout = null) =>
        new(id, name, run, kind, environment, timeout ?? DefaultGroupTimeout);

    private static string CategoryFor(string id) =>
        id.StartsWith("W1.5", StringComparison.Ordinal) ? "W1.5" : id.Split('-')[0];

    // This is the only authoritative group registry. Each entry must receive one terminal result.
    private static IReadOnlyList<TestGroup> Manifest() => new[]
    {
        G("M3-PDF", "Resume PDF", RunM3PdfTests),
        G("M4-FLASH", "Flashcards", RunM4FlashcardsTests),
        G("M4-IMAGE", "Batch image", RunM4BatchImageTests),
        G("W1", "Foundation hardening", RunW1HardeningTests),
        G("W1.5", "Extension manager", RunW1_5ExtensionManagerTests, EvidenceKind.Integration,
            "test-owned managed-file fixtures; no native executable launch"),
        G("W1.5-NATIVE", "Real executable version-probe gap", RunW1_5NativeProbeGapTests,
            EvidenceKind.StaticConformance, "trusted bounded executable fixture not supplied"),
        G("W2-A", "Converter domain", RunW2_ACoreDomainTests),
        G("W2-B1", "WIC image engine", RunW2_B1WicImageEngineTests, EvidenceKind.Integration, "Windows WIC"),
        G("W2-B2", "Document engines", RunW2_B2DocumentEngineTests, EvidenceKind.Integration),
        G("W2-C", "PDF renderer POC", RunW2_CPdfRendererPocTests, EvidenceKind.EnvironmentRuntime, "Windows PDF runtime"),
        G("W2-D", "Conversion orchestrator", RunW2_DConversionOrchestratorTests, EvidenceKind.Integration),
        G("W2-E", "Converter view model", RunW2_EUniversalConverterViewModelTests, EvidenceKind.Integration),
        G("W2-E1", "Converter runtime", RunW2_E1RealRuntimeInteractionTests, EvidenceKind.EnvironmentRuntime, "Windows runtime"),
        G("W2-F1", "Optimization domain", RunW2_F1OptimizationDomainModelTests),
        G("W2-F2", "Image enhancement", RunW2_F2ImageEngineEnhancementsTests, EvidenceKind.Integration, "Windows WIC"),
        G("W2-F3", "Optimization UI model", RunW2_F3OptimizationUiIntegrationTests, EvidenceKind.Integration),
        G("W2-F4", "Queue telemetry", RunW2_F4QueueTelemetryTests, EvidenceKind.Integration),
        G("W2-F5", "Format capability", RunW2_F5FormatCapabilityTests, EvidenceKind.Integration, "installed codecs"),
        G("W3-B", "Scholar persistence", RunW3_BScholarPersistenceTests, EvidenceKind.Integration),
        G("W3-C1", "Extraction contracts", RunW3_C1ExtractionContractsTests),
        G("W3-C2", "Text extraction", RunW3_C2DeterministicExtractionTests),
        G("W3-C3", "PDF extraction", RunW3_C3PdfExtractionTests, EvidenceKind.Integration, "Windows PDF runtime"),
        G("W3-C4", "DOCX extraction", RunW3_C4DocxExtractionTests, EvidenceKind.Integration),
        G("W3-C5-OCR", "OCR", RunW3_C5OcrTests, EvidenceKind.EnvironmentRuntime, "Windows OCR language pack"),
        G("W3-C5-RASTER", "Raster extraction", RunW3_C5RasterImageExtractionTests, EvidenceKind.Integration),
        G("W3-C5-PDF", "Hybrid PDF OCR", RunW3_C5PdfHybridOcrTests, EvidenceKind.EnvironmentRuntime, "Windows OCR language pack"),
        G("W3-C5-TIFF", "TIFF extraction", RunW3_C5TiffExtractionTests, EvidenceKind.Integration),
        G("W3-C6-1", "Normalization foundation", RunW3_C6_1NormalizationFoundationTests),
        G("W3-C6-2", "Unicode sanitization", RunW3_C6_2UnicodeAndSanitizationTests),
        G("W3-C6-3", "Structural normalization", RunW3_C6_3StructuralNormalizationTests),
        G("W3-C6-4", "Normalization integration", RunW3_C6_4NormalizationIntegrationTests, EvidenceKind.Integration),
        G("W3-C7-2", "Passage chunking", RunW3_C7_2PassageChunkingIntegrationTests, EvidenceKind.Integration),
        G("W3-C7-3", "Context windows", RunW3_C7_3BoundedContextWindowBuilderTests),
        G("W3-D", "Index service", RunW3_DIndexServiceTests, EvidenceKind.Integration,
            "optional DirectML GPU and neural model; lexical fallback available"),
        G("W3-E", "Search service", RunW3_ESearchServiceTests, EvidenceKind.Integration),
        G("W3-F", "Study synthesis", RunW3_FStudySynthesisEngineTests, EvidenceKind.Integration),
        G("R0-VOICE", "Studio R0 voice and Flashcards remediation", RunStudioR0VoiceFlashcardsTests, EvidenceKind.Integration),
        G("W4-T1", "Voice deterministic", RunW4Tier1_DeterministicLogicTests),
        G("W4-T2", "Voice mocked resilience", RunW4Tier2_MockedCoordinatorResilienceTests, EvidenceKind.Integration),
        G("W4-T3", "Voice host probes", RunW4Tier3_EnvironmentDependentRuntimeTests, EvidenceKind.EnvironmentRuntime, "Windows voice and microphone", TimeSpan.FromMinutes(2)),
        G("W4-T4", "Physical voice diagnostics", RunW4Tier4_DiagnosticsAndPrivacyTests, EvidenceKind.EnvironmentRuntime, "speech pack and microphone; --physical-voice", NativeGroupTimeout),
        G("W4-INT", "Voice view-model integration", RunW4_IntegrationTests, EvidenceKind.Integration),
        G("W4-RULES", "Voice rule matrix gaps", RunW4_RuleMatrixConformanceAudit, EvidenceKind.StaticConformance),
        G("P3B-LIFECYCLE", "Host startup and disposal ownership", RunP3BLifecycleTests, EvidenceKind.Integration),
    };

    // The previous W1.5 fixture wrote text with an .exe suffix, then asked production
    // VersionDetector to execute it. That is not a valid positive executable fixture.
    // Keep manager/validator integration deterministic without pretending to test the
    // native executable process probe, which has its own explicit manifest gap.
    private sealed class W1_5ManagedFixtureDetector : IVersionDetector
    {
        private readonly IExtensionCacheService _cache;
        private readonly VersionDetector _versionSemantics;

        public W1_5ManagedFixtureDetector(IExtensionCacheService cache)
        {
            _cache = cache;
            _versionSemantics = new VersionDetector(cache);
        }

        public Task<(bool isDetected, string? version, string? executablePath)> DetectInstalledVersionAsync(
            ExtensionModel extension, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            string path = Path.Combine(_cache.GetExtensionInstallDirectory(extension.Id), extension.ExecutableName);
            bool exists = File.Exists(path);
            return Task.FromResult<(bool isDetected, string? version, string? executablePath)>(
                exists ? (true, "1.0.0", path) : (false, null, null));
        }

        public Task<string?> FetchLatestVersionAsync(ExtensionModel extension, CancellationToken ct = default) =>
            _versionSemantics.FetchLatestVersionAsync(extension, ct);

        public int CompareVersions(string? versionA, string? versionB) =>
            _versionSemantics.CompareVersions(versionA, versionB);
    }

    private static Task RunW1_5NativeProbeGapTests()
    {
        RecordStaticGap("W1.5 positive VersionDetector process/version probe requires a trusted, bounded executable fixture; text/partial-PE files cannot prove it");
        return Task.CompletedTask;
    }

    private static void RecordAssertion(bool passed)
    {
        if (_currentGroup is null) throw new InvalidOperationException("Assertion outside registered group");
        if (passed) _currentGroup.Passed++; else _currentGroup.Failed++;
    }

    private static void RecordEnvironmentObservation(string name, string value)
    {
        if (_currentGroup is null) throw new InvalidOperationException("Observation outside registered group");
        _currentGroup.Observations++;
        Console.WriteLine($"  [ENV-OBSERVATION] {name}: {value}");
    }

    private static void RecordEnvironmentUnavailable(string reason)
    {
        if (_currentGroup is null) throw new InvalidOperationException("Unavailable outside registered group");
        _currentGroup.Disposition = Disposition.EnvironmentNotAvailable;
        _currentGroup.Reason = reason;
        Console.WriteLine($"  [ENVIRONMENT-NOT-AVAILABLE] {reason}");
    }

    private static void RecordStaticGap(string description)
    {
        if (_currentGroup is null) throw new InvalidOperationException("Gap outside registered group");
        _currentGroup.StaticGaps++;
        Console.WriteLine($"  [STATIC-GAP] {description}");
    }

    private static async Task<int> RunManifestAsync(string[] args)
    {
        var sw = Stopwatch.StartNew();
        var manifest = Manifest();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var duplicateIds = manifest.Where(g => !ids.Add(g.Id)).Select(g => g.Id).ToArray();
        bool manifestOnly = args.Length == 1 && args[0] == "--manifest-only";
        var groupArgs = args.Where(a => a.StartsWith("--group=", StringComparison.Ordinal)).ToArray();
        var badArgs = args.Where(a => a != "--physical-voice" && a != "--manifest-only" && !a.StartsWith("--group=", StringComparison.Ordinal)).ToArray();
        string? requestedGroup = groupArgs.Length == 1 ? groupArgs[0]["--group=".Length..] : null;
        IReadOnlyList<TestGroup> selected = requestedGroup is null ? manifest : manifest.Where(g => g.Id == requestedGroup).ToArray();
        if (manifestOnly)
        {
            foreach (var group in manifest)
                Console.WriteLine($"[MANIFEST-GROUP] id={group.Id} timeout-seconds={(int)group.Timeout.TotalSeconds} category={CategoryFor(group.Id)} evidence={group.Evidence} environment={group.Environment}");
            Console.WriteLine($"[MANIFEST-SUMMARY] expected={manifest.Count} duplicates={duplicateIds.Length}");
            return duplicateIds.Length == 0 ? 0 : 2;
        }
        string? isolated = Environment.GetEnvironmentVariable("AXORA_TEST_APPDATA_ROOT");
        bool isolatedAppData = !string.IsNullOrWhiteSpace(isolated) &&
            string.Equals(Path.GetFullPath(isolated), Path.GetFullPath(Environment.GetEnvironmentVariable("APPDATA") ?? ""), StringComparison.OrdinalIgnoreCase) &&
            Path.GetFullPath(isolated).StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"[MANIFEST] expected={selected.Count} ids={string.Join(',', selected.Select(g => g.Id))}");
        Console.WriteLine($"[ISOLATION] APPDATA={isolated ?? "missing"} verified={isolatedAppData}");
        if (duplicateIds.Length > 0 || groupArgs.Length > 1 || (requestedGroup is not null && selected.Count != 1) ||
            (args.Contains("--manifest-only") && !manifestOnly) || badArgs.Length > 0 || !isolatedAppData)
        {
            Console.WriteLine($"[HARNESS-BLOCKED] duplicate={string.Join(',', duplicateIds)} unknown-group={requestedGroup ?? "none"} unknown-args={string.Join(',', badArgs)} isolated-appdata={isolatedAppData}");
            PrintLedger(selected, sw, duplicateIds.Length, "BLOCKED");
            return 2;
        }

        bool stop = false;
        foreach (var group in selected)
        {
            var result = new GroupResult { Group = group, Disposition = Disposition.Pass };
            _ledger.Add(result);
            Console.WriteLine($"[GROUP-START] {group.Id} | {group.Name} | category={CategoryFor(group.Id)} | {group.Evidence} | prerequisite={group.Environment} | timeout={group.Timeout.TotalSeconds}s");
            var groupSw = Stopwatch.StartNew();
            _currentGroup = result;
            try
            {
                if (stop)
                {
                    result.Disposition = Disposition.Blocked;
                    result.Reason = "Prior group exceeded its timeout; execution stopped to prevent concurrent native work";
                }
                else if (group.Id == "W4-T4")
                {
                    using var probe = new VoiceTranscriberService(logger: null);
                    bool ready = await probe.CheckPrerequisitesAsync().WaitAsync(TimeSpan.FromSeconds(15));
                    if (!ready) RecordEnvironmentUnavailable("Windows speech recognition language/prerequisite unavailable; no microphone opened");
                    else if (args.Contains("--physical-voice"))
                    {
                        using var monitor = new AudioDeviceMonitor(logger: null);
                        await monitor.RefreshStatusAsync().WaitAsync(TimeSpan.FromSeconds(15));
                        if (!monitor.HasMicrophone) RecordEnvironmentUnavailable("No microphone capture endpoint; physical voice test not run");
                        else await group.Run().WaitAsync(group.Timeout);
                    }
                    else if (!args.Contains("--physical-voice"))
                    {
                        result.Disposition = Disposition.SkippedByPolicy;
                        result.Reason = "Physical microphone tests require explicit --physical-voice opt-in";
                    }
                }
                else await group.Run().WaitAsync(group.Timeout);

                if (result.Failed > 0) result.Disposition = Disposition.Fail;
                else if (result.StaticGaps > 0 && result.Disposition == Disposition.Pass) result.Disposition = Disposition.SkippedByPolicy;
            }
            catch (TimeoutException)
            {
                result.Disposition = Disposition.Blocked;
                result.Reason = $"Group timeout after {group.Timeout.TotalSeconds}s; native task may still be pending";
                stop = true;
            }
            catch (Exception ex)
            {
                result.Disposition = Disposition.Fail;
                result.Reason = ex.ToString();
            }
            finally
            {
                _currentGroup = null;
                groupSw.Stop();
                result.Elapsed = groupSw.Elapsed;
                Console.WriteLine($"[GROUP-END] {group.Id} | {result.Disposition} | pass={result.Passed} fail={result.Failed} observations={result.Observations} static-gaps={result.StaticGaps} elapsed={result.Elapsed.TotalSeconds:F1}s | {result.Reason}");
            }
        }

        int executedDuplicates = _ledger.GroupBy(r => r.Group.Id, StringComparer.Ordinal).Sum(g => Math.Max(0, g.Count() - 1));
        int missing = selected.Count(g => _ledger.All(r => r.Group.Id != g.Id));
        int unknown = _ledger.Count(r => selected.All(g => g.Id != r.Group.Id));
        bool invalid = missing > 0 || unknown > 0 || executedDuplicates > 0 || _ledger.Any(r => r.Disposition is Disposition.Fail or Disposition.Blocked) || duplicateIds.Length > 0;
        PrintLedger(selected, sw, duplicateIds.Length + executedDuplicates, invalid ? "FAIL" : "PASS-INCOMPLETE-PHYSICAL-COVERAGE");
        return invalid ? 1 : 0;
    }

    private static void PrintLedger(IReadOnlyList<TestGroup> manifest, Stopwatch sw, int duplicates, string exit)
    {
        int missing = manifest.Count(g => _ledger.All(r => r.Group.Id != g.Id));
        int unknown = _ledger.Count(r => manifest.All(g => g.Id != r.Group.Id));
        int count(Disposition disposition) => _ledger.Count(r => r.Disposition == disposition);
        Console.WriteLine($"[LEDGER-SUMMARY] expected={manifest.Count} executed={_ledger.Count} pass={count(Disposition.Pass)} fail={count(Disposition.Fail)} environment-not-available={count(Disposition.EnvironmentNotAvailable)} skipped-by-policy={count(Disposition.SkippedByPolicy)} blocked={count(Disposition.Blocked)} missing={missing} duplicates={duplicates} unknown={unknown} assertions-passed={_ledger.Sum(r => r.Passed)} assertions-failed={_ledger.Sum(r => r.Failed)} environment-observations={_ledger.Sum(r => r.Observations)} static-gaps={_ledger.Sum(r => r.StaticGaps)} elapsed-seconds={sw.Elapsed.TotalSeconds:F1} exit={exit}");
        Console.WriteLine($"[EVIDENCE] deterministic={_ledger.Where(r => r.Group.Evidence == EvidenceKind.Deterministic).Sum(r => r.Passed)} integration={_ledger.Where(r => r.Group.Evidence == EvidenceKind.Integration).Sum(r => r.Passed)} environment-runtime={_ledger.Where(r => r.Group.Evidence == EvidenceKind.EnvironmentRuntime).Sum(r => r.Passed)} static-gaps={_ledger.Sum(r => r.StaticGaps)}");
    }
}
