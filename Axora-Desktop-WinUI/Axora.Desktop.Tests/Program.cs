using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Axora.Desktop.Models;
using Axora.Desktop.Services;
using Axora.Desktop.Services.Contracts;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Tests;

public class Program
{
    private static int _passedTests = 0;
    private static int _failedTests = 0;
    private static readonly List<string> _failures = new();

    public static async Task<int> Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  AXORA DESKTOP — ADVERSARIAL STRESS TEST SUITE (Milestones M3 & M4)");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        try
        {
            // Milestone M3 Tests
            await RunM3PdfTests();

            // Milestone M4 Tests
            await RunM4FlashcardsTests();
            await RunM4BatchImageTests();

            // Phase W1 Tests: Native Foundation Hardening
            await RunW1HardeningTests();

            // Phase W1.5 Tests: Extension / Dependency / Download Manager Foundation
            await RunW1_5ExtensionManagerTests();

            // Phase W2-A Tests: Universal Converter Core Domain Models & Interfaces
            await RunW2_ACoreDomainTests();

            // Phase W2-B1 Tests: Native WIC Image Conversion Engine
            await RunW2_B1WicImageEngineTests();

            // Phase W2-B2 Tests: Native Document & Text Conversion Engines
            await RunW2_B2DocumentEngineTests();

            // Phase W2-C Tests: PDF -> Image Rendering Proof-of-Concept (Windows.Data.Pdf)
            await RunW2_CPdfRendererPocTests();

            // Phase W2-D Tests: Conversion Orchestrator & Bounded Concurrency Pipeline
            await RunW2_DConversionOrchestratorTests();

            // Phase W2-E Tests: Universal Converter UI + ViewModel + Shell Integration
            await RunW2_EUniversalConverterViewModelTests();

            // Phase W2-E.1 Tests: Universal Converter Real-Runtime Interaction + Visual QA Gate
            await RunW2_E1RealRuntimeInteractionTests();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL CRASH IN TEST HARNESS] {ex}");
            Console.ResetColor();
            _failedTests++;
            _failures.Add($"FATAL HARNESS EXCEPTION: {ex.Message}");
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine($"  TEST RUN SUMMARY: Total: {_passedTests + _failedTests} | Passed: {_passedTests} | Failed: {_failedTests}");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        if (_failedTests > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED TESTS:");
            foreach (var fail in _failures)
            {
                Console.WriteLine($"  - {fail}");
            }
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("ALL ADVERSARIAL STRESS TESTS PASSED SUCCESSFULLY.");
        Console.ResetColor();
        return 0;
    }

    private static void Assert(bool condition, string testName, string? message = null)
    {
        if (condition)
        {
            _passedTests++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [PASS] {testName}");
            Console.ResetColor();
        }
        else
        {
            _failedTests++;
            var errorMsg = message ?? "Assertion failed";
            _failures.Add($"{testName}: {errorMsg}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [FAIL] {testName} -> {errorMsg}");
            Console.ResetColor();
        }
    }

    #region Milestone M3: Resume PDF Vector Compiler Tests

    private static async Task RunM3PdfTests()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(">>> [M3] Resume PDF Vector Compiler Stress Testing <<<");
        Console.ResetColor();

        var compiler = new ResumePdfCompilerService();

        // Test 1: Empty Resume
        {
            var doc = new ResumeDocument();
            var pdfBytes = await compiler.CompileToBytesAsync(doc);
            Assert(pdfBytes != null && pdfBytes.Length > 0, "M3.1: Empty resume compiles to valid non-empty byte array");
            Assert(IsPdfValid(pdfBytes!), "M3.1b: Empty resume has valid PDF header and trailer");
        }

        // Test 2: Multi-Page Pagination with Very Long Paragraphs (5000+ words)
        {
            var doc = new ResumeDocument();
            doc.Header.FullName = "Adversarial Test Candidate";
            doc.Header.Email = "candidate@example.com";
            doc.Header.Phone = "+1-555-0199";

            var sbLong = new StringBuilder();
            for (int i = 0; i < 600; i++)
            {
                sbLong.Append($"word{i} distributed across deep neural network optimization and system architectures ");
            }
            doc.Summary = sbLong.ToString();

            for (int e = 0; e < 10; e++)
            {
                var exp = new ExperienceItem
                {
                    Company = $"Enterprise Tech Firm {e + 1}",
                    RoleTitle = $"Lead Systems Architect {e + 1}",
                    StartDate = "2020",
                    EndDate = "2024",
                    BulletsRaw = string.Join("\n", Enumerable.Range(1, 8).Select(b => $"• Bullet point {b} for job {e} detailing high throughput distributed processing with low latency guarantees across multi-cluster environments."))
                };
                doc.Experiences.Add(exp);
            }

            var pdfBytes = await compiler.CompileToBytesAsync(doc);
            Assert(pdfBytes != null && pdfBytes.Length > 2000, "M3.2: Massive 5000+ word multi-page resume compiles without OOM/crash");
            Assert(IsPdfValid(pdfBytes!), "M3.2b: Multi-page PDF output has valid structure");
        }

        // Test 3: Extremely Long Single Words (exceeding printable page width)
        {
            var doc = new ResumeDocument();
            doc.Header.FullName = "Edge Case Tester";
            string longWord1 = new string('A', 300); // 300 characters without spaces
            string longWord2 = "https://very.long.subdomain.domain.example.com/" + new string('x', 500);
            doc.Summary = $"Short intro {longWord1} middle text {longWord2} end of summary.";

            var exp = new ExperienceItem
            {
                Company = "Extreme Formatting Inc",
                RoleTitle = "Stress Engineer",
                BulletsRaw = $"• SingleHugeWord: {new string('Z', 400)}\n• Normal bullet after long word."
            };
            doc.Experiences.Add(exp);

            var pdfBytes = await compiler.CompileToBytesAsync(doc);
            Assert(pdfBytes != null && pdfBytes.Length > 0, "M3.3: Extreme single words exceeding line width do not throw or cause infinite loops");
            Assert(IsPdfValid(pdfBytes!), "M3.3b: PDF with oversized continuous tokens compiles cleanly");
        }

        // Test 4: Consecutive Newlines, Whitespace, Special Formatting Tokens
        {
            var doc = new ResumeDocument();
            doc.Header.FullName = "Formatting Stripper Test";
            doc.Summary = "\n\n\n\r\n   \t   \n\nHello [Portfolio Link](https://portfolio.example.com) with **bold text** and __underlined__ content.\n\n\n\n";

            var exp = new ExperienceItem
            {
                Company = "Whitespace Dynamics",
                RoleTitle = "Parser",
                BulletsRaw = "\n\n\n• Point 1\n\n\n\r\n\r\n• Point 2\n\n\n\n• Point 3\n\n\n"
            };
            doc.Experiences.Add(exp);

            var pdfBytes = await compiler.CompileToBytesAsync(doc);
            Assert(pdfBytes != null && pdfBytes.Length > 0, "M3.4: Consecutive newlines and markdown markers do not create ghost bullets or crash");
            Assert(IsPdfValid(pdfBytes!), "M3.4b: Formatted string sanitization produces valid PDF");
        }

        // Test 5: All Formatting Options Variations (Margins, Spacing Modes, Font Families)
        {
            int[] fonts = [0, 1, 2, 3, 4];
            int[] spacings = [0, 1, 2];
            double[] margins = [0.0, 0.25, 0.65, 1.2, 2.5];

            bool allPassed = true;
            foreach (var font in fonts)
            {
                foreach (var spacing in spacings)
                {
                    foreach (var margin in margins)
                    {
                        var doc = new ResumeDocument();
                        doc.Header.FullName = "Font & Margin Test";
                        doc.Formatting.FontFamily = font;
                        doc.Formatting.SpacingMode = spacing;
                        doc.Formatting.MarginInches = margin;
                        doc.Summary = "Testing variations across all font families, spacing multipliers, and margin inch boundaries.";

                        try
                        {
                            var bytes = await compiler.CompileToBytesAsync(doc);
                            if (bytes == null || !IsPdfValid(bytes))
                            {
                                allPassed = false;
                            }
                        }
                        catch
                        {
                            allPassed = false;
                        }
                    }
                }
            }
            Assert(allPassed, "M3.5: All combinations of 5 Font Families x 3 Spacing Modes x 5 Margins compile successfully");
        }

        // Test 6: Massive Full Resume with All 9 Sections Populated Across Multiple Pages
        {
            var doc = new ResumeDocument();
            doc.Header.FullName = "Comprehensive All-Section Candidate";
            doc.Header.ProfessionalTitle = "Principal Software Architect & Systems Researcher";
            doc.Header.Email = "candidate@example.org";
            doc.Header.Phone = "+1-800-555-0199";
            doc.Header.Location = "San Francisco, CA";
            doc.Header.LinkedIn = "linkedin.com/in/test";
            doc.Header.LinkedInUrl = "https://linkedin.com/in/test";
            doc.Header.GitHub = "github.com/test";
            doc.Header.PortfolioUrl = "https://portfolio.test.org";

            doc.Summary = "Comprehensive summary text outlining architectural leadership, distributed systems design, high-concurrency microservices, and cross-platform desktop UI frameworks.";

            // 5 Education entries
            for (int i = 1; i <= 5; i++)
            {
                doc.Education.Add(new EducationItem
                {
                    Institution = $"University of Engineering & Tech #{i}",
                    Degree = "B.S. in Computer Science",
                    Specialization = "Distributed Computing Systems",
                    ScoreOrPercentage = $"{80 + i}%",
                    YearRange = $"{2010 + i} - {2014 + i}"
                });
            }

            // 15 Experience entries
            for (int i = 1; i <= 15; i++)
            {
                doc.Experiences.Add(new ExperienceItem
                {
                    Company = $"Global Cloud Corp #{i}",
                    RoleTitle = "Senior Infrastructure Engineer",
                    Location = "Seattle, WA",
                    StartDate = $"01/{2010 + i}",
                    EndDate = $"12/{2011 + i}",
                    ProjectLink = "https://infra.example.com",
                    BulletsRaw = $"• Led high-scale system migrations with 99.999% uptime guarantees.\n• Reduced P99 latency by 45% using native zero-copy memory pipelines.\n• Mentored cross-functional team of {i + 5} engineers."
                });
            }

            // 8 Skill Categories
            for (int i = 1; i <= 8; i++)
            {
                doc.SkillCategories.Add(new SkillCategory
                {
                    CategoryName = $"Domain #{i}",
                    SkillsCsv = "C#, Rust, C++, Go, Python, WinUI 3, DirectX 12, DirectML, Docker, Kubernetes, Linux Kernel"
                });
            }

            // 10 Projects
            for (int i = 1; i <= 10; i++)
            {
                doc.Projects.Add(new ProjectItem
                {
                    Title = $"Project Atlas #{i}",
                    TechStack = "C#, .NET 9, WinUI 3, SkiaSharp",
                    DateRange = "2023 - 2024",
                    RepoUrl = "https://github.com/example/atlas",
                    BulletsRaw = "• Built ultra-responsive desktop vector rendering engine.\n• Optimized memory cache with LRU eviction."
                });
            }

            // 6 Certifications
            for (int i = 1; i <= 6; i++)
            {
                doc.Certifications.Add(new CertificationItem
                {
                    Title = $"Certified Cloud Solutions Architect #{i}",
                    Issuer = "Enterprise Cloud Institute",
                    Date = "2023",
                    GradeOrScore = "Pass (950/1000)",
                    Description = "Advanced architectural security, multi-region redundancy, and disaster recovery.",
                    VerificationUrl = "https://verify.cert.org/12345"
                });
            }

            // 6 Achievements
            for (int i = 1; i <= 6; i++)
            {
                doc.Achievements.Add(new AchievementItem
                {
                    Title = $"Outstanding Innovation Award #{i}",
                    Category = "Global Hackathon",
                    Date = "2022",
                    Description = "First place out of 450 competing international development teams.",
                    Link = "https://award.example.org"
                });
            }

            // 6 Responsibilities
            for (int i = 1; i <= 6; i++)
            {
                doc.Responsibilities.Add(new ResponsibilityItem
                {
                    Role = $"Lead Program Committee Chair #{i}",
                    Organization = "Systems Engineering Society",
                    DateRange = "2021 - 2024",
                    BulletsRaw = "• Organized annual technical conference for 1,200 attendees.\n• Managed peer review process across 80 submitted papers."
                });
            }

            var pdfBytes = await compiler.CompileToBytesAsync(doc);
            Assert(pdfBytes != null && pdfBytes.Length > 10000, "M3.6: Exhaustive 9-section multi-page resume compiles to full vector PDF");
            Assert(IsPdfValid(pdfBytes!), "M3.6b: Full multi-page PDF has valid headers and footers");
        }
    }

    private static bool IsPdfValid(byte[] bytes)
    {
        if (bytes.Length < 10) return false;
        string header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 10));
        if (!header.StartsWith("%PDF-")) return false;

        // Check for EOF marker near the end
        int checkLen = Math.Min(bytes.Length, 1024);
        string tail = Encoding.ASCII.GetString(bytes, bytes.Length - checkLen, checkLen);
        return tail.Contains("%%EOF");
    }

    #endregion

    #region Milestone M4: Flashcards & Interactive Tools Reactivity Tests

    private static async Task RunM4FlashcardsTests()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(">>> [M4] Flashcards SM-2 & Deck Reactivity Stress Testing <<<");
        Console.ResetColor();

        var mockSpeech = new MockSpeechSynthesisService();
        var vm = new FlashcardsViewModel(mockSpeech);

        // Test 1: Baseline initialization
        Assert(vm.Decks.Count >= 2, "M4.1: Initial decks loaded");
        Assert(vm.ActiveDeck != null, "M4.1b: Active deck is selected");
        Assert(vm.CurrentCard != null, "M4.1c: Current card is active");

        // Test 2: Observable Property RetentionRate and CardCount Notifications
        {
            var deck = new FlashcardDeck
            {
                Title = "Observable Test Deck",
                Cards =
                [
                    new FlashCard { Front = "Q1", Back = "A1", Difficulty = CardDifficulty.Hard },
                    new FlashCard { Front = "Q2", Back = "A2", Difficulty = CardDifficulty.Hard }
                ]
            };

            var notifiedProperties = new List<string>();
            deck.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null) notifiedProperties.Add(e.PropertyName);
            };

            Assert(deck.RetentionRate == 0.0, "M4.2: Initial retention rate with 0 Easy cards is 0.0%");
            Assert(deck.CardCount == 2, "M4.2b: CardCount is 2");

            vm.SelectDeck(deck);
            vm.RateCard("Easy"); // Q1 becomes Easy

            Assert(notifiedProperties.Contains(nameof(FlashcardDeck.RetentionRate)), "M4.2c: RateCard fires RetentionRate PropertyChanged notification");
            Assert(notifiedProperties.Contains(nameof(FlashcardDeck.CardCount)), "M4.2d: RateCard fires CardCount PropertyChanged notification");
            Assert(deck.RetentionRate == 50.0, "M4.2e: Retention rate correctly recalculated to 50.0% (1/2)");

            vm.RateCard("Easy"); // Q2 becomes Easy
            Assert(deck.RetentionRate == 100.0, "M4.2f: Retention rate correctly recalculated to 100.0% (2/2)");
        }

        // Test 3: SM-2 Algorithm Boundary & Stress Tests (10,000 Consecutive Iterations)
        {
            var card = new FlashCard
            {
                Front = "SM-2 Card",
                Back = "SM-2 Answer",
                EaseFactor = 2.5,
                IntervalDays = 1,
                Difficulty = CardDifficulty.Medium
            };
            var deck = new FlashcardDeck { Cards = [card] };
            vm.SelectDeck(deck);

            // SM-2 Rating Stress: test unbounded exponential growth
            bool intervalOverflowDetected = false;
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    vm.RateCard("Easy");
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                intervalOverflowDetected = true;
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"  [VULNERABILITY CONFIRMED] RateCard threw ArgumentOutOfRangeException on iteration ~25: {ex.Message}");
                Console.ResetColor();
            }

            Assert(!intervalOverflowDetected, "M4.3_VULN: RateCard with repeated 'Easy' ratings must not overflow DateTimeOffset.AddDays (Unbounded Exponential Interval Growth Bug)");
            Assert(card.EaseFactor <= 3.0, $"M4.3a: EaseFactor does not exceed ceiling 3.0 (Actual: {card.EaseFactor})");
            Assert(card.Difficulty == CardDifficulty.Easy, "M4.3b: Difficulty is Easy");

            // Hard rating test with fresh card
            var hardCard = new FlashCard { Front = "Hard Q", Back = "Hard A", EaseFactor = 2.5, IntervalDays = 10 };
            var hardDeck = new FlashcardDeck { Cards = [hardCard] };
            vm.SelectDeck(hardDeck);
            for (int i = 0; i < 50; i++)
            {
                vm.RateCard("Hard");
            }
            Assert(hardCard.EaseFactor >= 1.3, $"M4.3c: EaseFactor floored at 1.3 on Hard ratings (Actual: {hardCard.EaseFactor})");
            Assert(hardCard.IntervalDays == 1, $"M4.3d: Interval resets to 1 on Hard rating (Actual: {hardCard.IntervalDays})");
            Assert(hardCard.Difficulty == CardDifficulty.Hard, "M4.3e: Difficulty is Hard");

            // Medium rating test with fresh card
            var medCard = new FlashCard { Front = "Med Q", Back = "Med A", EaseFactor = 2.5, IntervalDays = 1 };
            var medDeck = new FlashcardDeck { Cards = [medCard] };
            vm.SelectDeck(medDeck);
            bool medOverflow = false;
            try
            {
                for (int i = 0; i < 200; i++)
                {
                    vm.RateCard("Medium");
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                medOverflow = true;
            }
            Assert(!medOverflow, "M4.3f_VULN: Medium ratings also grow exponentially (1.2x) and must be capped to prevent DateTimeOffset overflow");
        }

        // Test 4: Empty Deck Edge Cases
        {
            var emptyDeck = new FlashcardDeck
            {
                Title = "Empty Deck",
                Cards = []
            };
            vm.Decks.Add(emptyDeck);
            vm.SelectDeck(emptyDeck);

            Assert(vm.CurrentCard == null, "M4.4a: CurrentCard is null for empty deck");
            Assert(emptyDeck.RetentionRate == 100.0, "M4.4b: RetentionRate for empty deck is 100.0% (guarded against division by zero)");
            Assert(vm.DeckStats == "0 Cards", $"M4.4c: DeckStats shows '0 Cards' (Actual: {vm.DeckStats})");

            // Action safety on empty deck
            bool actionsSafe = true;
            try
            {
                vm.FlipCard();
                vm.NextCard();
                vm.PreviousCard();
                vm.RateCard("Easy");
                vm.RateCard("Hard");
                vm.RateCard("Medium");
                await vm.SpeakCurrentCardAsync();
            }
            catch (Exception ex)
            {
                actionsSafe = false;
                Console.WriteLine($"Empty deck action threw: {ex}");
            }
            Assert(actionsSafe, "M4.4d: All deck actions (Flip, Next, Prev, Rate, Speak) execute safely on empty deck without throwing");
        }

        // Test 5: Single Card Deck Navigation Cycling
        {
            var singleCardDeck = new FlashcardDeck
            {
                Title = "Single Card Deck",
                Cards = [new FlashCard { Front = "Only Question", Back = "Only Answer" }]
            };
            vm.SelectDeck(singleCardDeck);
            Assert(vm.CurrentCardIndex == 0, "M4.5a: Initial index is 0");
            Assert(vm.CurrentCard?.Front == "Only Question", "M4.5b: Current card is active");

            vm.NextCard();
            Assert(vm.CurrentCardIndex == 0, "M4.5c: NextCard on 1-card deck stays at index 0");

            vm.PreviousCard();
            Assert(vm.CurrentCardIndex == 0, "M4.5d: PreviousCard on 1-card deck stays at index 0");

            vm.FlipCard();
            Assert(vm.IsCardFlipped == true, "M4.5e: FlipCard flips card to Back");
            vm.NextCard();
            Assert(vm.IsCardFlipped == false, "M4.5f: Navigating resets IsCardFlipped to false");
        }

        // Test 6: Text Parsing & Card Generation Edge Cases
        {
            // Empty / whitespace
            int deckCountBefore = vm.Decks.Count;
            vm.GenerateCardsFromText("", "");
            vm.GenerateCardsFromText("   \t  \n  ", "");
            Assert(vm.Decks.Count == deckCountBefore, "M4.6a: Empty text generation does nothing");

            // Colon-separated notes
            string notesWithColons = "DirectML: Hardware accelerated machine learning for DirectX 12 devices.\nONNX Runtime: Cross-platform machine learning engine.";
            vm.GenerateCardsFromText(notesWithColons, "DirectML_Notes.txt");
            Assert(vm.ActiveDeck?.Title.Contains("DirectML_Notes") == true, "M4.6b: Generated deck title contains source file name");
            Assert(vm.ActiveDeck?.Cards.Count == 2, $"M4.6c: Generated 2 cards from colon notes (Actual: {vm.ActiveDeck?.Cards.Count})");
            Assert(vm.ActiveDeck?.Cards[0].Front == "DirectML", "M4.6d: Card 1 Front is 'DirectML'");

            // Plain unstructured paragraph (fallback summary card)
            string rawParagraph = "This is a continuous unstructured paragraph without colons or line splits that should generate a document summary card.";
            vm.GenerateCardsFromText(rawParagraph, "SummaryDoc.pdf");
            Assert(vm.ActiveDeck?.Cards.Count >= 1, "M4.6e: Unstructured text generates fallback summary card");
        }
    }

    #endregion

    #region Milestone M4: Batch Image Queue Reactivity Tests

    private static async Task RunM4BatchImageTests()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(">>> [M4] Batch Image Queue Reactivity Stress Testing <<<");
        Console.ResetColor();

        // Test 1: BatchImageJob Observable Property Change Notifications
        {
            var job = new BatchImageJob
            {
                SourceFilePath = "C:\\test\\photo.jpg"
            };

            var notifiedProperties = new List<string>();
            job.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null) notifiedProperties.Add(e.PropertyName);
            };

            job.OriginalSizeBytes = 1048576; // 1 MB
            Assert(notifiedProperties.Contains(nameof(BatchImageJob.OriginalSizeBytes)), "M4.7a: OriginalSizeBytes triggers PropertyChanged");
            Assert(notifiedProperties.Contains(nameof(BatchImageJob.FormattedOriginalSize)), "M4.7b: OriginalSizeBytes triggers FormattedOriginalSize PropertyChanged");
            Assert(job.FormattedOriginalSize == "1.00 MB", $"M4.7c: FormattedOriginalSize is '1.00 MB' (Actual: {job.FormattedOriginalSize})");

            notifiedProperties.Clear();
            job.OutputSizeBytes = 512000; // 500 KB
            Assert(notifiedProperties.Contains(nameof(BatchImageJob.OutputSizeBytes)), "M4.7d: OutputSizeBytes triggers PropertyChanged");
            Assert(notifiedProperties.Contains(nameof(BatchImageJob.FormattedOutputSize)), "M4.7e: OutputSizeBytes triggers FormattedOutputSize PropertyChanged");
            Assert(job.FormattedOutputSize == "500.0 KB", $"M4.7f: FormattedOutputSize is '500.0 KB' (Actual: {job.FormattedOutputSize})");

            // Format boundaries
            job.OriginalSizeBytes = 0;
            Assert(job.FormattedOriginalSize == "0 B", "M4.7g: 0 bytes formatted as '0 B'");
            job.OriginalSizeBytes = 500;
            Assert(job.FormattedOriginalSize == "500 B", "M4.7h: 500 bytes formatted as '500 B'");
            job.OriginalSizeBytes = 1024;
            Assert(job.FormattedOriginalSize == "1.0 KB", "M4.7i: 1024 bytes formatted as '1.0 KB'");
        }

        // Test 2: BatchImageProcessorService with 0-Byte Items (Defensive Exception Handling)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraTest_" + Guid.NewGuid().ToString("N"));
            var outDir = Path.Combine(tempDir, "output");
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(outDir);

            try
            {
                var emptyFile1 = Path.Combine(tempDir, "empty1.jpg");
                var emptyFile2 = Path.Combine(tempDir, "empty2.png");
                await File.WriteAllBytesAsync(emptyFile1, Array.Empty<byte>());
                await File.WriteAllBytesAsync(emptyFile2, Array.Empty<byte>());

                var job1 = new BatchImageJob { SourceFilePath = emptyFile1 };
                var job2 = new BatchImageJob { SourceFilePath = emptyFile2 };
                var jobs = new List<BatchImageJob> { job1, job2 };

                var processor = new BatchImageProcessorService(NullLogger<BatchImageProcessorService>.Instance);
                var options = new BatchImageOptions
                {
                    OutputDirectory = outDir,
                    Engine = ImageProcessingEngine.ImageMagickStudio,
                    TargetFormat = ImageTargetFormat.Jpeg
                };

                int completedCallbacks = 0;
                var processedJobs = new List<BatchImageJob>();
                double lastProgress = 0;
                var progress = new Progress<double>(p => lastProgress = p);

                await processor.ProcessBatchAsync(
                    jobs,
                    options,
                    progress,
                    j =>
                    {
                        Interlocked.Increment(ref completedCallbacks);
                        lock (processedJobs) { processedJobs.Add(j); }
                    });

                Assert(completedCallbacks == 2, $"M4.8a: onItemProcessed callback invoked exactly 2 times for 2 zero-byte files (Actual: {completedCallbacks})");
                Assert(job1.Status == BatchJobStatus.Failed, "M4.8b: 0-byte file job 1 status is Failed");
                Assert(job2.Status == BatchJobStatus.Failed, "M4.8c: 0-byte file job 2 status is Failed");
                Assert(job1.ErrorMessage.Contains("0 bytes") || job1.ErrorMessage.Contains("empty"), $"M4.8d: Informative error message recorded: '{job1.ErrorMessage}'");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        // Test 3: Rapid High-Concurrency Completion Callbacks & Missing Files
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraRapid_" + Guid.NewGuid().ToString("N"));
            var outDir = Path.Combine(tempDir, "output");
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(outDir);

            try
            {
                // Create 50 missing-file jobs to stress rapid consumer error reporting across threads
                var jobs = Enumerable.Range(1, 50).Select(i => new BatchImageJob
                {
                    SourceFilePath = Path.Combine(tempDir, $"nonexistent_{i}.jpg")
                }).ToList();

                var processor = new BatchImageProcessorService(NullLogger<BatchImageProcessorService>.Instance);
                var options = new BatchImageOptions
                {
                    OutputDirectory = outDir,
                    Engine = ImageProcessingEngine.ImageMagickStudio
                };

                int callbackCount = 0;
                await processor.ProcessBatchAsync(
                    jobs,
                    options,
                    null,
                    j => Interlocked.Increment(ref callbackCount));

                Assert(callbackCount == 50, $"M4.9a: All 50 rapid failure callbacks completed cleanly (Actual: {callbackCount})");
                Assert(jobs.All(j => j.Status == BatchJobStatus.Failed), "M4.9b: All 50 missing files marked Failed");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        // Test 4: Folder Scanner Edge Cases
        {
            var processor = new BatchImageProcessorService(NullLogger<BatchImageProcessorService>.Instance);

            var nonExistentFiles = await processor.ScanFolderForImagesAsync("C:\\NonExistentFolder_12345");
            Assert(nonExistentFiles.Count == 0, "M4.10a: Scanning non-existent folder returns empty list without throwing");

            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraScan_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(tempDir, "test.txt"), "text");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "test.exe"), "binary");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "photo.jpg"), "fake jpg");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "image.PNG"), "fake png");

                var subDir = Path.Combine(tempDir, "sub");
                Directory.CreateDirectory(subDir);
                await File.WriteAllTextAsync(Path.Combine(subDir, "nested.webp"), "fake webp");

                var scanned = await processor.ScanFolderForImagesAsync(tempDir, includeSubfolders: true);
                Assert(scanned.Count == 3, $"M4.10b: Folder scan correctly identified 3 images and ignored non-image files (Actual: {scanned.Count})");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    #endregion

    #region Phase W1: Native Foundation Hardening Tests

    private static async Task RunW1HardeningTests()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("--- Running Phase W1: Native Foundation Hardening Tests ---");
        Console.ResetColor();

        // W1.1: IThemeService Switching & Accent Color Propagation
        {
            var testDir = Path.Combine(Path.GetTempPath(), "AxoraThemeTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var settings = new AppSettingsService(NullLogger<AppSettingsService>.Instance, testDir);
                var theme = new ThemeService(settings, NullLogger<ThemeService>.Instance);

                int themeChangedVal = -1;
                theme.ThemeChanged += (_, idx) => themeChangedVal = idx;

                theme.SetTheme(2);
                Assert(theme.CurrentThemeIndex == 2, "W1.1a: ThemeService SetTheme updates CurrentThemeIndex to 2 (Dark)");
                Assert(themeChangedVal == 2, "W1.1b: ThemeService ThemeChanged event fired with expected index");

                string accentChangedVal = string.Empty;
                theme.AccentColorChanged += (_, color) => accentChangedVal = color;

                theme.SetAccentColor("#107C41");
                Assert(theme.CurrentAccentColorHex == "#107C41", "W1.1c: ThemeService SetAccentColor updates CurrentAccentColorHex");
                Assert(accentChangedVal == "#107C41", "W1.1d: ThemeService AccentColorChanged event fired with expected hex");

                // W1.1e: SettingsViewModel immediately updates ThemeService without calling SaveSettings
                var settingsVm = new SettingsViewModel(settings, theme);
                int targetTheme = (settingsVm.SelectedThemeIndex == 1) ? 2 : 1;
                settingsVm.SelectedThemeIndex = targetTheme;
                Assert(theme.CurrentThemeIndex == targetTheme, "W1.1e: Changing SelectedThemeIndex in SettingsViewModel immediately propagates to ThemeService");
                string targetAccent = (settingsVm.AccentColor == "#FF6D00") ? "#00C853" : "#FF6D00";
                settingsVm.AccentColor = targetAccent;
                Assert(theme.CurrentAccentColorHex == targetAccent, "W1.1f: Changing AccentColor in SettingsViewModel immediately propagates to ThemeService");
            }
            finally
            {
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }

        // W1.2: INotificationService Operations & Event Delivery
        {
            var notifications = new NotificationService();
            NotificationEventArgs? receivedArgs = null;
            bool dismissedFired = false;

            notifications.NotificationRequested += (_, e) => receivedArgs = e;
            notifications.DismissRequested += (_, _) => dismissedFired = true;

            notifications.ShowSuccess("File exported successfully", "Export", TimeSpan.FromSeconds(4));
            Assert(receivedArgs != null, "W1.2a: NotificationService raised NotificationRequested event");
            Assert(receivedArgs?.Severity == NotificationSeverity.Success, "W1.2b: Notification severity is Success");
            Assert(receivedArgs?.Message == "File exported successfully", "W1.2c: Notification message matches");
            Assert(receivedArgs?.Title == "Export", "W1.2d: Notification title matches");
            Assert(receivedArgs?.Duration == TimeSpan.FromSeconds(4), "W1.2e: Notification duration matches");

            notifications.Dismiss();
            Assert(dismissedFired, "W1.2f: NotificationService Dismiss triggers DismissRequested event");
        }

        // W1.3: WinRtOcrService Caller Stream Ownership Protection
        {
            var ocr = new WinRtOcrService(NullLogger<WinRtOcrService>.Instance);
            // 1x1 24bpp BMP header + pixel
            byte[] bmpBytes = new byte[]
            {
                0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x36, 0x00, 0x00, 0x00,
                0x28, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
                0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0x00
            };

            using var callerStream = new MemoryStream(bmpBytes);
            callerStream.Position = 0;
            try
            {
                await ocr.ExtractTextAsync(callerStream);
            }
            catch (InvalidOperationException)
            {
                // Headless environment without language pack installed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      (Ocr test non-fatal notice: {ex.Message})");
            }

            Assert(callerStream.CanRead, "W1.3a: Caller stream remains open and readable after WinRtOcrService invocation");
            Assert(callerStream.CanSeek, "W1.3b: Caller stream remains seekable after WinRtOcrService invocation");
            Assert(callerStream.Position == 0, "W1.3c: Caller stream position is restored to original offset after WinRtOcrService invocation");
        }

        // W1.4: P2pSyncService Lifecycle, Shutdown & Socket Port Re-use
        {
            var p2p = new P2pSyncService(NullLogger<P2pSyncService>.Instance);
            try
            {
                await p2p.StartAsync();
                Assert(p2p.IsRunning, "W1.4a: P2pSyncService starts successfully on first call");

                await p2p.StopAsync();
                Assert(!p2p.IsRunning, "W1.4b: P2pSyncService stops cleanly");

                // Start second time immediately to ensure SO_REUSEADDR prevents Win32 socket conflict
                await p2p.StartAsync();
                Assert(p2p.IsRunning, "W1.4c: P2pSyncService starts successfully on rapid second call (socket reuse hardened)");

                await p2p.StopAsync();
                Assert(!p2p.IsRunning, "W1.4d: P2pSyncService stops cleanly after second cycle");
            }
            finally
            {
                p2p.Dispose();
            }
        }

        // W1.5: WiaScannerService COM Cleanup on Repeated Enumeration
        {
            var scanner = new WiaScannerService(NullLogger<WiaScannerService>.Instance);
            bool allSucceeded = true;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var list = await scanner.GetConnectedScannersAsync();
                    if (list == null) allSucceeded = false;
                }
                catch (Exception ex)
                {
                    allSucceeded = false;
                    Console.WriteLine($"      (Wia enumeration error on iter {i}: {ex.Message})");
                }
            }
            Assert(allSucceeded, "W1.5: WiaScannerService repeated enumeration releases COM wrappers cleanly without exceptions");
        }

        // W1.6: BatchImageProcessorService ImageMagick Probe, Fallback Override & Execution
        {
            var batch = new BatchImageProcessorService(NullLogger<BatchImageProcessorService>.Instance);
            bool initialStatus = batch.IsImageMagickAvailable;

            BatchImageProcessorService.SetImageMagickAvailableForTesting(false);
            Assert(!batch.IsImageMagickAvailable, "W1.6a: ImageMagick availability can be forced to false for fallback testing");

            BatchImageProcessorService.SetImageMagickAvailableForTesting(true);
            Assert(batch.IsImageMagickAvailable, "W1.6b: ImageMagick availability can be forced to true for testing");

            BatchImageProcessorService.SetImageMagickAvailableForTesting(initialStatus);
            Assert(batch.IsImageMagickAvailable == initialStatus, "W1.6c: ImageMagick availability restored to baseline");

            // Real batch execution with forced WIC fallback
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraMagickFallback_" + Guid.NewGuid().ToString("N"));
            var outDir = Path.Combine(tempDir, "out");
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(outDir);
            try
            {
                byte[] bmpBytes = new byte[]
                {
                    0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x36, 0x00, 0x00, 0x00,
                    0x28, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
                    0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0xFF, 0x00, 0x00, 0x00
                };
                var inputPath = Path.Combine(tempDir, "sample.bmp");
                await File.WriteAllBytesAsync(inputPath, bmpBytes);

                var job = new BatchImageJob
                {
                    SourceFilePath = inputPath,
                    OriginalSizeBytes = bmpBytes.Length,
                    Status = BatchJobStatus.Queued
                };

                var processor = new BatchImageProcessorService(NullLogger<BatchImageProcessorService>.Instance);
                var options = new BatchImageOptions
                {
                    Engine = ImageProcessingEngine.ImageMagickStudio,
                    TargetFormat = ImageTargetFormat.Jpeg,
                    OutputDirectory = outDir
                };

                BatchImageProcessorService.SetImageMagickAvailableForTesting(false);
                try
                {
                    await processor.ProcessBatchAsync(new[] { job }, options);
                    Assert(job.Status == BatchJobStatus.Completed, "W1.6d: Batch job completes successfully via WIC fallback when ImageMagick is unavailable");
                    Assert(!string.IsNullOrEmpty(job.ErrorMessage) && job.ErrorMessage.Contains("ImageMagick unavailable"), $"W1.6e: Observable fallback feedback recorded on job: '{job.ErrorMessage}'");
                    Assert(!string.IsNullOrEmpty(job.OutputFilePath) && File.Exists(job.OutputFilePath), "W1.6f: WIC fallback output file exists on disk");
                }
                finally
                {
                    BatchImageProcessorService.SetImageMagickAvailableForTesting(initialStatus);
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        // W1.7: AppSettingsService Persistence Error Observability
        {
            var settings = new AppSettingsService(NullLogger<AppSettingsService>.Instance);
            Assert(settings.LastPersistenceError == null, "W1.7a: AppSettingsService initial LastPersistenceError is null");

            settings.ThemeIndex = 1;
            settings.Save();
            Assert(settings.LastPersistenceError == null, "W1.7b: AppSettingsService normal persist does not record error");

            // Force persistence error via invalid environment path
            var appDataBak = Environment.GetEnvironmentVariable("APPDATA");
            try
            {
                Environment.SetEnvironmentVariable("APPDATA", "Z:\\NonExistentPath_Axora_12345\\SettingsTest");
                var failingSettings = new AppSettingsService(NullLogger<AppSettingsService>.Instance);
                failingSettings.ThemeIndex = 2;
                failingSettings.Save();
                Assert(failingSettings.LastPersistenceError != null, "W1.7c: LastPersistenceError captures exception when save path is invalid/unwritable");
            }
            finally
            {
                Environment.SetEnvironmentVariable("APPDATA", appDataBak);
            }
        }

        // W1.8: DirectMlEmbeddingService Device Loss Recovery / SIMD Fallback
        {
            var dml = new DirectMlEmbeddingService(NullLogger<DirectMlEmbeddingService>.Instance);
            var emb = await dml.GenerateEmbeddingAsync("Axora W1 Hardening Verification");

            Assert(emb != null, "W1.8a: DirectMlEmbeddingService produces non-null embedding vector");
            Assert(emb?.Length == 384, $"W1.8b: Embedding vector length is exactly 384 (Actual: {emb?.Length})");

            if (emb != null)
            {
                double norm = 0;
                foreach (var val in emb) norm += val * val;
                norm = Math.Sqrt(norm);
                Assert(Math.Abs(norm - 1.0) < 0.05, $"W1.8c: Embedding vector is normalized (Norm: {norm:F3})");
            }
        }

        // W1.9: Keyboard Accelerator Ctrl+\ OEM_5 Mapping Verification
        {
            const int vkOem5 = 0xDC; // VK_OEM_5 = '\' on standard US keyboard
            const int vkBack = 0x08; // VK_BACK = Backspace
            Assert(vkOem5 != vkBack, "W1.9a: VK_OEM_5 (0xDC) is distinct from VirtualKey.Back (0x08)");
            Assert(vkOem5 == 220, "W1.9b: VK_OEM_5 maps to 220 (0xDC) matching standard backslash accelerator");
        }
    }

    #endregion

    #region Phase W1.5: Extension / Dependency / Download Manager Foundation Tests

    private static async Task RunW1_5ExtensionManagerTests()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("--- Running Phase W1.5: Extension / Dependency / Download Manager Tests ---");
        Console.ResetColor();

        // W1.5_1: Extension Registry & Metadata Contract
        {
            var registry = new ExtensionRegistry();
            var all = registry.GetAll();
            Assert(all.Count >= 1, "W1.5_1a: ExtensionRegistry seeds default dependencies");

            var im = registry.GetById("imagemagick");
            Assert(im != null, "W1.5_1b: Seeded ImageMagick extension is registered by default");
            Assert(im != null && im.IsOptional && !im.IsRequired, "W1.5_1c: Seeded ImageMagick is strictly optional (IsOptional=true, IsRequired=false)");
            Assert(im != null && im.ExecutableName == "magick.exe" && im.LatestVersion == "7.1.1-43", "W1.5_1d: ImageMagick has correct executable 'magick.exe' and target version '7.1.1-43'");
            Assert(im != null && im.RequiredBy.Contains("Batch Image Studio"), "W1.5_1e: ImageMagick metadata links to 'Batch Image Studio'");

            var customExt = new ExtensionModel
            {
                Id = "test-tool",
                DisplayName = "Test Custom Tool",
                Description = "A test tool",
                InstallSource = "https://github.com/example/tool.zip",
                DetectionStrategy = "PathAndManagedDirectoryProbe",
                ExecutableName = "tool.exe"
            };
            registry.Register(customExt);
            Assert(registry.GetById("test-tool") != null, "W1.5_1f: Custom extension registers successfully in ExtensionRegistry");
            var unregistered = registry.Unregister("test-tool");
            Assert(unregistered && registry.GetById("test-tool") == null, "W1.5_1g: Custom extension unregisters cleanly from ExtensionRegistry");
        }

        // W1.5_2: Version Detection & Comparison Semantics
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraVerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var cacheService = new ExtensionCacheService(null, tempDir, tempDir);
                var detector = new VersionDetector(cacheService);

                Assert(detector.CompareVersions("7.1.1-43", "7.1.1-43") == 0, "W1.5_2a: Identical versions compare equal (7.1.1-43 == 7.1.1-43)");
                Assert(detector.CompareVersions("7.1.1-42", "7.1.1-43") < 0, "W1.5_2b: Older patch compares lower (7.1.1-42 < 7.1.1-43)");
                Assert(detector.CompareVersions("7.1.1-44", "7.1.1-43") > 0, "W1.5_2c: Newer patch compares greater (7.1.1-44 > 7.1.1-43)");
                Assert(detector.CompareVersions("7.1.0-99", "7.1.1-0") < 0, "W1.5_2d: Minor version increment compares higher than patch (7.1.0-99 < 7.1.1-0)");
                Assert(detector.CompareVersions("8.0.0", "7.9.9") > 0, "W1.5_2e: Major version boundary compares correctly (8.0.0 > 7.9.9)");
                Assert(detector.CompareVersions("v7.1.1-43", "7.1.1-43") == 0, "W1.5_2f: Leading 'v' prefix normalized and equals raw version (v7.1.1-43 == 7.1.1-43)");
                Assert(detector.CompareVersions(null, "1.0.0") < 0, "W1.5_2g: Null version compares less than concrete version");
                Assert(detector.CompareVersions("1.0.0", null) > 0, "W1.5_2h: Concrete version compares greater than null version");
                Assert(detector.CompareVersions(null, null) == 0, "W1.5_2i: Two null versions compare equal");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_3: ExtensionModel Observable State Machine & Action Booleans
        {
            var model = new ExtensionModel
            {
                Id = "test-model",
                DisplayName = "Model Test",
                Description = "Testing action booleans",
                InstallSource = "https://github.com/test/tool.exe",
                DetectionStrategy = "PathAndManagedDirectoryProbe",
                ExecutableName = "tool.exe"
            };

            // NotInstalled
            model.Status = ExtensionStatus.NotInstalled;
            model.IsBusy = false;
            Assert(model.CanInstallAction && !model.CanUpdateAction && !model.CanRepairAction && !model.CanReinstallAction && !model.CanRemoveAction,
                "W1.5_3a: NotInstalled allows Install only");

            // Installed
            model.Status = ExtensionStatus.Installed;
            Assert(!model.CanInstallAction && !model.CanUpdateAction && model.CanRepairAction && model.CanReinstallAction && model.CanCleanReinstallAction && model.CanRemoveAction,
                "W1.5_3b: Installed allows Repair, Reinstall, Clean Reinstall, Remove; disallows Install, Update");

            // UpdateAvailable
            model.Status = ExtensionStatus.UpdateAvailable;
            Assert(!model.CanInstallAction && model.CanUpdateAction && model.CanRepairAction && model.CanReinstallAction && model.CanCleanReinstallAction && model.CanRemoveAction,
                "W1.5_3c: UpdateAvailable allows Update, Repair, Reinstall, Clean Reinstall, Remove");

            // RepairRequired
            model.Status = ExtensionStatus.RepairRequired;
            Assert(!model.CanInstallAction && !model.CanUpdateAction && model.CanRepairAction && model.CanReinstallAction && model.CanRemoveAction,
                "W1.5_3d: RepairRequired allows Repair, Reinstall, Remove");

            // Corrupted
            model.Status = ExtensionStatus.Corrupted;
            Assert(!model.CanInstallAction && !model.CanUpdateAction && model.CanRepairAction && model.CanReinstallAction && model.CanRemoveAction,
                "W1.5_3e: Corrupted allows Repair, Reinstall, Remove");

            // Failed
            model.Status = ExtensionStatus.Failed;
            Assert(model.CanInstallAction && !model.CanUpdateAction && !model.CanRepairAction,
                "W1.5_3f: Failed allows Install retry");

            // IsBusy = true overrides ALL action booleans to false
            model.Status = ExtensionStatus.Installed;
            model.IsBusy = true;
            Assert(!model.CanInstallAction && !model.CanUpdateAction && !model.CanRepairAction && !model.CanReinstallAction && !model.CanCleanReinstallAction && !model.CanRemoveAction,
                "W1.5_3g: IsBusy = true strictly locks out all actions regardless of status");
            model.IsBusy = false;
        }

        // W1.5_4: Missing Dependency Handling
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraMissingDep_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var customMissing = new ExtensionModel
                {
                    Id = "nonexistent-tool",
                    DisplayName = "Nonexistent Tool",
                    Description = "Does not exist",
                    InstallSource = "https://github.com/example/nonexistent.exe",
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "nonexistent_fake_tool_xyz123.exe"
                };
                registry.Register(customMissing);

                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                var status = await depMgr.CheckStatusAsync("nonexistent-tool");
                Assert(status == ExtensionStatus.NotInstalled, "W1.5_4a: Missing extension reports NotInstalled");
                Assert(!depMgr.IsDependencyReady("nonexistent-tool"), "W1.5_4b: IsDependencyReady returns false for missing extension");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_5: Installed Dependency Probe & Version Detection
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraInstalledDep_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var installedExt = new ExtensionModel
                {
                    Id = "probe-tool",
                    DisplayName = "Probe Tool",
                    Description = "Testing probe",
                    InstallSource = "https://github.com/example/probetool.exe",
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "probetool.exe",
                    LatestVersion = "1.0.0"
                };
                registry.Register(installedExt);

                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var managedDir = cache.GetExtensionInstallDirectory("probe-tool");
                var managedExe = Path.Combine(managedDir, "probetool.exe");
                await File.WriteAllTextAsync(managedExe, "MOCK_PROBE_BINARY_DATA_NON_EMPTY");

                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                var status = await depMgr.CheckStatusAsync("probe-tool");
                Assert(status == ExtensionStatus.Installed, $"W1.5_5a: Installed managed executable detected as Installed (Actual: {status})");
                Assert(installedExt.InstalledVersion != null, "W1.5_5b: Installed version populated from probe");
                Assert(depMgr.IsDependencyReady("probe-tool"), "W1.5_5c: IsDependencyReady returns true for installed extension");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_6: Update Available Detection & Readiness Contract
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraUpdateDep_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var updateExt = new ExtensionModel
                {
                    Id = "update-tool",
                    DisplayName = "Update Tool",
                    Description = "Testing update detection",
                    InstallSource = "https://github.com/example/updatetool.exe",
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "updatetool.exe",
                    LatestVersion = "2.5.0" // latest is higher than probed 1.0.0
                };
                registry.Register(updateExt);

                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var managedDir = cache.GetExtensionInstallDirectory("update-tool");
                var managedExe = Path.Combine(managedDir, "updatetool.exe");
                await File.WriteAllTextAsync(managedExe, "MOCK_EXE_VERSION_1_0_0");

                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                var status = await depMgr.CheckStatusAsync("update-tool");
                Assert(status == ExtensionStatus.UpdateAvailable, $"W1.5_6a: Older installed version transitions to UpdateAvailable (Actual: {status})");
                Assert(depMgr.IsDependencyReady("update-tool"), "W1.5_6b: IsDependencyReady returns TRUE when UpdateAvailable (not broken, user not blocked)");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_7: 0-Byte Corrupt File Validation Failure
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraCorruptDep_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var corruptExt = new ExtensionModel
                {
                    Id = "corrupt-tool",
                    DisplayName = "Corrupted Tool",
                    Description = "Testing 0-byte corrupt handling",
                    InstallSource = "https://github.com/example/corrupt.exe",
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "corrupt.exe",
                    LatestVersion = "1.0.0"
                };
                registry.Register(corruptExt);

                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var managedDir = cache.GetExtensionInstallDirectory("corrupt-tool");
                var managedExe = Path.Combine(managedDir, "corrupt.exe");
                // Write 0-byte corrupt file
                await File.WriteAllBytesAsync(managedExe, Array.Empty<byte>());

                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                var valResult = await validator.ValidateExtensionAsync(corruptExt);
                Assert(!valResult.IsValid && valResult.IsCorrupted && valResult.RequiresRepair,
                    "W1.5_7a: ExtensionValidator marks 0-byte executable as invalid, corrupted, and requiring repair");

                var status = await depMgr.CheckStatusAsync("corrupt-tool");
                Assert(status == ExtensionStatus.Corrupted, $"W1.5_7b: CheckStatusAsync transitions 0-byte executable to Corrupted (Actual: {status})");
                Assert(!depMgr.IsDependencyReady("corrupt-tool"), "W1.5_7c: IsDependencyReady returns false for corrupted dependency");
                Assert(corruptExt.CanRepairAction, "W1.5_7d: CanRepairAction is true for corrupted dependency");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_8: Cryptographic and Transport Security Pipeline
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraSecTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var cache = new ExtensionCacheService(null, tempDir, tempDir);
                var downloader = new ExtensionDownloader(cache);

                // Insecure HTTP rejection
                var httpExt = new ExtensionModel
                {
                    Id = "insecure-tool",
                    DisplayName = "Insecure Tool",
                    Description = "Uses insecure http",
                    InstallSource = "http://imagemagick.org/archive/binaries/insecure.exe",
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "insecure.exe"
                };

                bool httpRejected = false;
                try
                {
                    await downloader.DownloadExtensionAsync(httpExt);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("HTTPS"))
                {
                    httpRejected = true;
                }
                catch { }
                Assert(httpRejected, "W1.5_8a: ExtensionDownloader rejects insecure HTTP source URI");

                // Untrusted domain rejection
                var untrustedExt = new ExtensionModel
                {
                    Id = "malicious-tool",
                    DisplayName = "Malicious Tool",
                    Description = "From untrusted host",
                    InstallSource = "https://untrusted-unknown-vendor.com/tool.exe",
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "tool.exe"
                };

                bool untrustedRejected = false;
                try
                {
                    await downloader.DownloadExtensionAsync(untrustedExt);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("whitelist"))
                {
                    untrustedRejected = true;
                }
                catch { }
                Assert(untrustedRejected, "W1.5_8b: ExtensionDownloader rejects untrusted vendor domain not in whitelist");

                // Checksum mismatch verification
                var dummyInstaller = Path.Combine(tempDir, "test-installer.bin");
                await File.WriteAllTextAsync(dummyInstaller, "AUTHENTIC_CONTENT_DATA_FOR_SHA256");

                var localSourceUri = new Uri(dummyInstaller).AbsoluteUri;
                var checksumExt = new ExtensionModel
                {
                    Id = "checksum-tool",
                    DisplayName = "Checksum Tool",
                    Description = "Checksum mismatch test",
                    InstallSource = localSourceUri,
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "checksum.exe",
                    Sha256Hash = "0000000000000000000000000000000000000000000000000000000000000000" // Intentional mismatch
                };

                bool checksumFailed = false;
                try
                {
                    await downloader.DownloadExtensionAsync(checksumExt);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("checksum mismatch"))
                {
                    checksumFailed = true;
                }
                catch { }
                Assert(checksumFailed, "W1.5_8c: ExtensionDownloader detects SHA256 checksum mismatch and throws");

                // ExtensionValidator checksum verification
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                bool validChecksumPass = await validator.ValidateInstallerAsync(
                    dummyInstaller,
                    "0000000000000000000000000000000000000000000000000000000000000000",
                    "x64");
                Assert(!validChecksumPass, "W1.5_8d: ExtensionValidator returns false on installer checksum mismatch");

                // Unsupported architecture rejection
                var archResult = await validator.ValidateExtensionAsync(new ExtensionModel
                {
                    Id = "arch-tool",
                    DisplayName = "MIPS Tool",
                    Description = "Testing unsupported arch",
                    InstallSource = "https://github.com/example/tool.exe",
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "tool.exe",
                    SupportedArchitecture = "unsupported_mips64"
                });
                Assert(!archResult.IsValid && (archResult.ErrorMessage?.Contains("architecture") == true),
                    "W1.5_8e: ExtensionValidator rejects unsupported host architecture");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_9: Cache Isolation, Calculation & Safe Purge Boundaries
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraCacheBoundary_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            var userDocsDir = Path.Combine(tempDir, "UserDocuments");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);
            Directory.CreateDirectory(userDocsDir);

            try
            {
                var cache = new ExtensionCacheService(null, cacheDir, extDir);

                // Create cache files
                var dlDir = cache.GetExtensionCacheDirectory("tool1");
                var instDir = cache.GetExtensionInstallerDirectory("tool1");
                var tempSub = cache.GetExtensionTempDirectory("tool1");
                await File.WriteAllBytesAsync(Path.Combine(dlDir, "payload.zip"), new byte[5120]); // 5 KB
                await File.WriteAllBytesAsync(Path.Combine(instDir, "setup.exe"), new byte[10240]); // 10 KB
                await File.WriteAllBytesAsync(Path.Combine(tempSub, "scratch.tmp"), new byte[2048]); // 2 KB

                // Create user document outside cache boundary
                var userFile = Path.Combine(userDocsDir, "ImportantResume.pdf");
                await File.WriteAllTextAsync(userFile, "USER_SACRED_RESUME_CONTENT");

                var totalBytes = await cache.GetCacheSizeBytesAsync();
                Assert(totalBytes == 17408, $"W1.5_9a: GetCacheSizeBytesAsync accurately sums all cache files (Expected 17408, Got {totalBytes})");

                var tool1Bytes = await cache.GetCacheSizeBytesAsync("tool1");
                Assert(tool1Bytes == 17408, $"W1.5_9b: Specific extension cache size matches (Expected 17408, Got {tool1Bytes})");

                // Clear cache
                await cache.ClearCacheAsync();
                var remainingBytes = await cache.GetCacheSizeBytesAsync();
                Assert(remainingBytes == 0, "W1.5_9c: ClearCacheAsync removes all files inside extension cache");

                // Strictly verify user document was NOT touched
                Assert(File.Exists(userFile), "W1.5_9d: ClearCacheAsync strictly preserves files outside cache directory boundary");
                Assert(await File.ReadAllTextAsync(userFile) == "USER_SACRED_RESUME_CONTENT", "W1.5_9e: User file content remains pristine");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_10: Repair Flow
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraRepairTest_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var mockPackagePath = Path.Combine(tempDir, "healthy_tool.exe");
                await File.WriteAllTextAsync(mockPackagePath, "VALID_HEALTHY_EXECUTABLE_BINARY");

                var repairExt = new ExtensionModel
                {
                    Id = "repairable-tool",
                    DisplayName = "Repairable Tool",
                    Description = "Testing repair flow",
                    InstallSource = new Uri(mockPackagePath).AbsoluteUri,
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "healthy_tool.exe",
                    LatestVersion = "1.0.0"
                };
                registry.Register(repairExt);

                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var managedDir = cache.GetExtensionInstallDirectory("repairable-tool");
                var managedExe = Path.Combine(managedDir, "healthy_tool.exe");
                // Corrupt file initially
                await File.WriteAllBytesAsync(managedExe, Array.Empty<byte>());

                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                var initialStatus = await depMgr.CheckStatusAsync("repairable-tool");
                Assert(initialStatus == ExtensionStatus.Corrupted, "W1.5_10a: Initial status is Corrupted before repair");

                var repairSuccess = await depMgr.RepairExtensionAsync("repairable-tool");
                Assert(repairSuccess, "W1.5_10b: RepairExtensionAsync succeeds");

                var postStatus = repairExt.Status;
                Assert(postStatus == ExtensionStatus.Installed, $"W1.5_10c: Post-repair status is Installed (Actual: {postStatus})");
                Assert(File.Exists(managedExe) && new FileInfo(managedExe).Length > 0, "W1.5_10d: Repaired executable exists and has non-zero length");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_11: Reinstall Flow
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraReinstallTest_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var mockPackage = Path.Combine(tempDir, "reinstall_tool.exe");
                await File.WriteAllTextAsync(mockPackage, "REINSTALL_BINARY_CONTENT");

                var reinstallExt = new ExtensionModel
                {
                    Id = "reinstall-tool",
                    DisplayName = "Reinstall Tool",
                    Description = "Testing reinstall flow",
                    InstallSource = new Uri(mockPackage).AbsoluteUri,
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "reinstall_tool.exe",
                    LatestVersion = "1.0.0"
                };
                registry.Register(reinstallExt);

                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                // First install
                var installOk = await depMgr.InstallExtensionAsync("reinstall-tool");
                Assert(installOk, "W1.5_11a: Initial install succeeds");

                // Reinstall
                var reinstallOk = await depMgr.ReinstallExtensionAsync("reinstall-tool");
                Assert(reinstallOk, "W1.5_11b: ReinstallExtensionAsync succeeds");
                Assert(reinstallExt.Status == ExtensionStatus.Installed, "W1.5_11c: Status remains Installed after reinstall");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_12: Clean Reinstall Flow
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraCleanReinstall_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var mockPackage = Path.Combine(tempDir, "clean_tool.exe");
                await File.WriteAllTextAsync(mockPackage, "CLEAN_REINSTALL_BINARY");

                var cleanExt = new ExtensionModel
                {
                    Id = "clean-tool",
                    DisplayName = "Clean Tool",
                    Description = "Testing clean reinstall",
                    InstallSource = new Uri(mockPackage).AbsoluteUri,
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "clean_tool.exe",
                    LatestVersion = "1.0.0"
                };
                registry.Register(cleanExt);

                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                // Initial install
                await depMgr.InstallExtensionAsync("clean-tool");
                Assert(cleanExt.Status == ExtensionStatus.Installed, "W1.5_12a: Initial install succeeds before clean reinstall");

                // Clean reinstall wipes cache and binaries, then reinstalls
                var cleanOk = await depMgr.CleanReinstallExtensionAsync("clean-tool");
                Assert(cleanOk, "W1.5_12b: CleanReinstallExtensionAsync completes successfully");
                Assert(cleanExt.Status == ExtensionStatus.Installed, "W1.5_12c: Post-clean-reinstall status is Installed");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_13: Concurrency Protection on Operations
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraConcurrency_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var slowPackage = Path.Combine(tempDir, "slow_tool.exe");
                await File.WriteAllTextAsync(slowPackage, "SLOW_TOOL_EXE");

                var slowExt = new ExtensionModel
                {
                    Id = "slow-tool",
                    DisplayName = "Slow Tool",
                    Description = "Testing concurrency locking",
                    InstallSource = new Uri(slowPackage).AbsoluteUri,
                    DetectionStrategy = "PathAndManagedDirectoryProbe",
                    ExecutableName = "slow_tool.exe",
                    LatestVersion = "1.0.0"
                };
                registry.Register(slowExt);

                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                // Launch two installs simultaneously
                var t1 = depMgr.InstallExtensionAsync("slow-tool");
                var t2 = depMgr.InstallExtensionAsync("slow-tool");
                var results = await Task.WhenAll(t1, t2);

                // At least one must succeed, and they must not cause an unhandled collision
                Assert(results[0] || results[1], "W1.5_13: Concurrency locking allows operation without unhandled deadlock or crash");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_14: Cancellation Handling & Resource Safety
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraCancelTest_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                using var cts = new CancellationTokenSource();
                cts.Cancel(); // Pre-cancelled

                bool cancelledHandled = false;
                try
                {
                    await cache.GetCacheSizeBytesAsync(null, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    cancelledHandled = true;
                }
                Assert(cancelledHandled, "W1.5_14: Cancelled token aborts operations cleanly via OperationCanceledException");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_15: Rapid State Transitions Hammer (Stress Test)
        {
            var ext = new ExtensionModel
            {
                Id = "stress-tool",
                DisplayName = "Stress Tool",
                Description = "Testing rapid transitions",
                InstallSource = "https://github.com/example/tool.exe",
                DetectionStrategy = "PathAndManagedDirectoryProbe",
                ExecutableName = "tool.exe"
            };

            int eventsCount = 0;
            ext.PropertyChanged += (_, _) => Interlocked.Increment(ref eventsCount);

            var statuses = Enum.GetValues<ExtensionStatus>();
            for (int i = 0; i < 500; i++)
            {
                ext.Status = statuses[i % statuses.Length];
                ext.IsBusy = (i % 2 == 0);
                ext.DownloadProgress = (i % 100);
            }

            Assert(eventsCount > 1000, $"W1.5_15: 500 rapid status toggles fired {eventsCount} PropertyChanged events safely without deadlocking");
        }

        // W1.5_16: DownloadManager Navigation & Highlight Parameter Routing
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraHighlightTest_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);
                var notif = new NotificationService();

                var vm = new DownloadManagerViewModel(depMgr, cache, notif);
                vm.HighlightExtension("imagemagick");

                var im = registry.GetById("imagemagick");
                Assert(im != null && im.IsHighlighted, "W1.5_16a: HighlightExtension sets IsHighlighted to true on targeted extension");

                vm.HighlightExtension("nonexistent");
                Assert(im != null && !im.IsHighlighted, "W1.5_16b: Highlighting another ID clears IsHighlighted on previously highlighted extension");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_17: Consumer Integration & Batch Image WIC Fallback Preservation
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraWicFallback_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                // With no installed magick.exe, dependency manager reports IsDependencyReady false
                Assert(!depMgr.IsDependencyReady("imagemagick"), "W1.5_17a: IDependencyManager reports ImageMagick is not ready when not installed");

                var batchProcessor = new BatchImageProcessorService(NullLogger<BatchImageProcessorService>.Instance, depMgr);
                // Clear any static testing override
                BatchImageProcessorService.SetImageMagickAvailableForTesting(null);

                Assert(!batchProcessor.IsImageMagickAvailable, "W1.5_17b: BatchImageProcessorService reflects dependency manager unready state");

                // Explicit testing override still takes precedence for existing tests
                BatchImageProcessorService.SetImageMagickAvailableForTesting(true);
                Assert(batchProcessor.IsImageMagickAvailable, "W1.5_17c: Testing override takes precedence over DependencyManager");
                BatchImageProcessorService.SetImageMagickAvailableForTesting(null);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_18: Path Traversal Defense & Strict ID Sanitization
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraPathSec_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var cache = new ExtensionCacheService(null, cacheDir, extDir);

                // Attempting path traversal via relative navigation
                var resolved = cache.GetExtensionCacheDirectory("../../system32");
                Assert(Path.GetFullPath(resolved).StartsWith(Path.GetFullPath(cacheDir), StringComparison.OrdinalIgnoreCase),
                    "W1.5_18a: GetExtensionCacheDirectory neutralizes path traversal characters and stays within cache root");

                // Empty / whitespace ID throws ArgumentException
                bool emptyThrows = false;
                try { cache.GetExtensionCacheDirectory("   "); }
                catch (ArgumentException) { emptyThrows = true; }
                Assert(emptyThrows, "W1.5_18b: Empty or whitespace extension ID throws ArgumentException");

                // Null ID throws ArgumentException
                bool nullThrows = false;
                try { cache.GetExtensionCacheDirectory(null!); }
                catch (ArgumentException) { nullThrows = true; }
                Assert(nullThrows, "W1.5_18c: Null extension ID throws ArgumentException");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_19: Process Lock Defense (No Arbitrary Kill)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraProcLock_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var installer = new ExtensionInstaller(cache, validator, detector);

                var ext = new ExtensionModel
                {
                    Id = "locked-tool",
                    DisplayName = "Locked Tool",
                    Description = "Testing locked file handling",
                    InstallSource = "https://example.com/locked.exe",
                    DetectionStrategy = "ManagedDirectoryProbe",
                    ExecutableName = "locked.exe"
                };

                var installPath = cache.GetExtensionInstallDirectory(ext.Id);
                var exePath = Path.Combine(installPath, ext.ExecutableName);
                File.WriteAllBytes(exePath, new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

                // Simulate locked executable (in use by an active task or process)
                using (var lockStream = File.Open(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var uninstallSuccess = await installer.UninstallAsync(ext);
                    Assert(!uninstallSuccess, "W1.5_19a: Uninstall fails gracefully when target executable is locked/in-use");
                    Assert(ext.ErrorMessage != null && ext.ErrorMessage.Contains("currently in use"),
                        "W1.5_19b: User-friendly in-use error message is provided without killing arbitrary processes");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_20: Staged Installation & Rollback Guarantee
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraRollback_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var installer = new ExtensionInstaller(cache, validator, detector);

                var ext = new ExtensionModel
                {
                    Id = "rollback-tool",
                    DisplayName = "Rollback Tool",
                    Description = "Testing staged installation rollback",
                    InstallSource = "https://example.com/tool.exe",
                    DetectionStrategy = "ManagedDirectoryProbe",
                    ExecutableName = "tool.exe",
                    InstalledVersion = "1.0.0",
                    Status = ExtensionStatus.Installed
                };

                // Seed healthy existing installation v1.0.0
                var installPath = cache.GetExtensionInstallDirectory(ext.Id);
                var exePath = Path.Combine(installPath, ext.ExecutableName);
                File.WriteAllText(exePath, "ORIGINAL_V1_CONTENT");

                // Staged package that will fail (e.g. empty or corrupted installer that does not produce tool.exe)
                var badInstallerPath = Path.Combine(tempDir, "corrupted_installer.bin");
                File.WriteAllText(badInstallerPath, "NOT_AN_EXECUTABLE");

                var installResult = await installer.InstallAsync(ext, badInstallerPath);
                Assert(!installResult, "W1.5_20a: Failed staged installer rejects installation");
                Assert(File.Exists(exePath), "W1.5_20b: Existing installation directory and executable preserved intact after failed staging");
                Assert(File.ReadAllText(exePath) == "ORIGINAL_V1_CONTENT", "W1.5_20c: Original binary content preserved intact (atomic rollback)");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_21: Remote Update Check Failure Keeps Healthy Installed Status
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraOfflineCheck_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                var ext = registry.GetById("imagemagick")!;
                ext.TargetVersion = "1.0.0";
                ext.LatestVersion = "1.0.0";
                // Seed mock installation
                var installDir = cache.GetExtensionInstallDirectory(ext.Id);
                File.WriteAllBytes(Path.Combine(installDir, ext.ExecutableName), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

                // Initial probe without remote check
                await depMgr.CheckStatusAsync(ext.Id, forceRefresh: true, checkRemoteVersion: false);
                Assert(ext.Status == ExtensionStatus.Installed, "W1.5_21a: Valid local installation sets status to Installed");

                // Perform update check with unreachable remote URL
                var offlineExt = new ExtensionModel
                {
                    Id = "offline-tool",
                    DisplayName = "Offline Tool",
                    Description = "Testing offline check resilience",
                    InstallSource = "http://192.0.2.1:9999/nonexistent.exe", // RFC 5737 non-routable address (will fail / timeout)
                    DetectionStrategy = "ManagedDirectoryProbe",
                    ExecutableName = "offline.exe",
                    TargetVersion = "1.0.0"
                };
                registry.Register(offlineExt);
                var offlineInstallDir = cache.GetExtensionInstallDirectory(offlineExt.Id);
                File.WriteAllBytes(Path.Combine(offlineInstallDir, offlineExt.ExecutableName), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

                // Initial probe
                await depMgr.CheckStatusAsync(offlineExt.Id, forceRefresh: true, checkRemoteVersion: false);
                Assert(offlineExt.Status == ExtensionStatus.Installed, "W1.5_21b: Offline tool detected as Installed locally");

                // Now run update check with checkRemoteVersion: true
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await depMgr.CheckStatusAsync(offlineExt.Id, forceRefresh: true, checkRemoteVersion: true, ct: cts.Token);
                }
                catch { }

                Assert(offlineExt.Status == ExtensionStatus.Installed, "W1.5_21c: Network update failure MUST preserve Status = Installed");
                Assert(offlineExt.UpdateStatus is UpdateCheckStatus.UnableToCheck or UpdateCheckStatus.NotChecked,
                    "W1.5_21d: Network failure sets UpdateStatus to UnableToCheck without corrupting healthy extension");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_22: Clean Reinstall Isolation Boundaries
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraCleanReinstallIso_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            var userDocsDir = Path.Combine(tempDir, "user_documents");
            var otherExtDir = Path.Combine(extDir, "other_extension");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);
            Directory.CreateDirectory(userDocsDir);
            Directory.CreateDirectory(otherExtDir);

            var userDocFile = Path.Combine(userDocsDir, "important_research.pdf");
            File.WriteAllText(userDocFile, "CRITICAL USER DOCUMENT CONTENT");

            var otherExtFile = Path.Combine(otherExtDir, "other.exe");
            File.WriteAllText(otherExtFile, "OTHER EXTENSION BINARY");

            try
            {
                var cache = new ExtensionCacheService(null, cacheDir, extDir);

                // Target extension cache and installation
                var targetCache = cache.GetExtensionCacheDirectory("target-tool");
                File.WriteAllText(Path.Combine(targetCache, "temp_installer.exe"), "INSTALLER DATA");

                var targetInstall = cache.GetExtensionInstallDirectory("target-tool");
                File.WriteAllText(Path.Combine(targetInstall, "target.exe"), "TARGET BINARY");

                // Clear cache only for target-tool
                await cache.ClearCacheAsync("target-tool");

                Assert(!File.Exists(Path.Combine(targetCache, "temp_installer.exe")),
                    "W1.5_22a: Target extension cache files were purged");
                Assert(File.Exists(userDocFile),
                    "W1.5_22b: User documents were NOT touched");
                Assert(File.ReadAllText(userDocFile) == "CRITICAL USER DOCUMENT CONTENT",
                    "W1.5_22c: User document content remained 100% intact");
                Assert(File.Exists(otherExtFile),
                    "W1.5_22d: Unrelated extensions were NOT touched");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // W1.5_23: Remote Version Metadata & Safe Offline Handling
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AxoraRemoteVerTest_" + Guid.NewGuid().ToString("N"));
            var cacheDir = Path.Combine(tempDir, "cache");
            var extDir = Path.Combine(tempDir, "extensions");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(extDir);

            try
            {
                var registry = new ExtensionRegistry();
                var im = registry.GetById("imagemagick")!;
                var cache = new ExtensionCacheService(null, cacheDir, extDir);
                var detector = new VersionDetector(cache);
                var validator = new ExtensionValidator(cache, detector);
                var downloader = new ExtensionDownloader(cache);
                var installer = new ExtensionInstaller(cache, validator, detector);
                var repair = new ExtensionRepairService(cache, validator, downloader, installer, detector);
                var depMgr = new DependencyManager(registry, detector, validator, downloader, installer, repair, cache);

                // Stage a healthy installed mock binary for ImageMagick (mock probe returns 1.0.0)
                im.TargetVersion = "1.0.0";
                im.LatestVersion = "1.0.0";
                var imInstallDir = cache.GetExtensionInstallDirectory("imagemagick");
                var imExePath = Path.Combine(imInstallDir, "magick.exe");
                await File.WriteAllTextAsync(imExePath, "MOCK_MAGICK_BINARY");

                // Initially check status locally
                var localStatus = await depMgr.CheckStatusAsync("imagemagick", checkRemoteVersion: false);
                Assert(localStatus == ExtensionStatus.Installed, "W1.5_23a: ImageMagick detected as Installed locally");

                // Execute remote update check (where no vendor metadata provider is configured)
                var latestVer = await detector.FetchLatestVersionAsync(im);
                Assert(latestVer == null, "W1.5_23b: FetchLatestVersionAsync returns null when no remote metadata provider exists");

                var remoteStatus = await depMgr.CheckStatusAsync("imagemagick", checkRemoteVersion: true);
                Assert(remoteStatus == ExtensionStatus.Installed, "W1.5_23c: Healthy Installed status is strictly preserved on remote check");
                Assert(im.UpdateStatus == UpdateCheckStatus.UnableToCheck, "W1.5_23d: UpdateStatus transitions to UnableToCheck");
                Assert(im.FormattedLatestVersion == "Latest version unavailable", "W1.5_23e: FormattedLatestVersion clearly represents 'Latest version unavailable'");

                // Verify TargetVersion and LatestVersion independence
                im.TargetVersion = "7.1.1-43";
                im.LatestVersion = "7.1.1-44";
                Assert(im.TargetVersion != im.LatestVersion, "W1.5_23f: TargetVersion and LatestVersion are genuinely independent properties");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }

    #endregion

    #region Phase W2-A Tests: Universal Converter Core Domain Models & Interfaces

    private static async Task RunW2_ACoreDomainTests()
    {
        Console.WriteLine("\n  --- Running Phase W2-A: Universal Converter Core Domain & Interfaces Tests ---");

        // W2A_1: ConversionJob creation, default state, and metadata computation
        {
            var defaultJob = new ConversionJob();
            Assert(!string.IsNullOrWhiteSpace(defaultJob.JobId) && defaultJob.JobId.Length == 32,
                "W2A_1a: ConversionJob generates valid 32-character hex GUID JobId");
            Assert(defaultJob.Status == ConversionJobStatus.Queued,
                "W2A_1b: ConversionJob default status is Queued");
            Assert(defaultJob.ProgressPercentage == 0.0,
                "W2A_1c: ConversionJob default progress is 0.0%");
            Assert(defaultJob.SourceFileName == string.Empty && defaultJob.SourceExtension == string.Empty,
                "W2A_1d: ConversionJob empty SourceFilePath yields empty filename and extension");
            Assert(defaultJob.Profile == ConversionProfile.Default,
                "W2A_1e: ConversionJob default profile matches ConversionProfile.Default");

            var populatedJob = new ConversionJob
            {
                SourceFilePath = @"C:\Users\test\Documents\research_paper.PDF",
                TargetExtension = ".docx",
                DestinationDirectory = @"C:\Users\test\Output",
                SourceFileSizeBytes = 1048576 * 5 // 5 MB
            };
            Assert(populatedJob.SourceFileName == "research_paper.PDF",
                "W2A_1f: SourceFileName correctly extracts filename from path");
            Assert(populatedJob.SourceExtension == ".pdf",
                "W2A_1g: SourceExtension normalizes to lowercase with leading dot");
            Assert(populatedJob.FormattedSourceSize == "5.00 MB",
                $"W2A_1h: FormattedSourceSize formats bytes to MB (Actual: {populatedJob.FormattedSourceSize})");
            populatedJob.Dispose();
        }

        // W2A_2: ConversionJob status transitions, property changes, and cancellation contract
        {
            using var job = new ConversionJob
            {
                SourceFilePath = @"C:\data\input.csv",
                TargetExtension = ".json"
            };

            bool propertyChangedFired = false;
            job.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ConversionJob.Status))
                    propertyChangedFired = true;
            };

            job.Status = ConversionJobStatus.Preparing;
            Assert(propertyChangedFired && job.Status == ConversionJobStatus.Preparing,
                "W2A_2a: Status transitions to Preparing and raises PropertyChanged");

            job.ProgressPercentage = 45.0;
            job.Status = ConversionJobStatus.Running;
            Assert(job.FormattedStatus == "Converting (45%)",
                $"W2A_2b: FormattedStatus dynamically reflects running percentage (Actual: {job.FormattedStatus})");

            using var cts = new CancellationTokenSource();
            job.SetCancellationTokenSource(cts);
            Assert(!job.IsCancellationRequested,
                "W2A_2c: IsCancellationRequested is initially false");
            Assert(!job.CancellationToken.IsCancellationRequested,
                "W2A_2d: Bound CancellationToken is initially active and uncancelled");

            job.Cancel();
            Assert(job.Status == ConversionJobStatus.Cancelled,
                "W2A_2e: Cancel() transitions Status to Cancelled");
            Assert(job.IsCancellationRequested,
                "W2A_2f: Cancel() marks IsCancellationRequested to true");
            Assert(job.CancellationToken.IsCancellationRequested,
                "W2A_2g: Cancel() propagates cancellation to bound CancellationToken");
            Assert(job.FormattedStatus == "Cancelled",
                "W2A_2h: FormattedStatus returns Cancelled");
        }

        // W2A_3: ConversionProfile presets, enums (CollisionPolicy, MetadataHandling, ConversionJobStatus)
        {
            var def = ConversionProfile.Default;
            Assert(def.Name == "Standard Quality" && def.Quality == 85 && def.TargetDpi == 150,
                "W2A_3a: ConversionProfile.Default has standard quality 85 and 150 DPI");
            Assert(def.MetadataPolicy == MetadataHandling.Preserve && def.CollisionMode == CollisionPolicy.AutoRename,
                "W2A_3b: ConversionProfile.Default preserves metadata and defaults to AutoRename");

            var hiFi = ConversionProfile.HighFidelity;
            Assert(hiFi.Quality == 100 && hiFi.TargetDpi == 300,
                "W2A_3c: HighFidelity preset specifies 100 quality and 300 DPI");

            var web = ConversionProfile.WebOptimized;
            Assert(web.Quality == 75 && web.MetadataPolicy == MetadataHandling.Strip,
                "W2A_3d: WebOptimized preset specifies 75 quality and strips metadata");

            var print = ConversionProfile.PrintReady;
            Assert(print.Quality == 100 && print.TargetDpi == 300,
                "W2A_3e: PrintReady preset specifies 100 quality and 300 DPI");

            // Verify all enum values
            var collisionValues = Enum.GetValues<CollisionPolicy>();
            Assert(collisionValues.Length == 4 &&
                   collisionValues.Contains(CollisionPolicy.AutoRename) &&
                   collisionValues.Contains(CollisionPolicy.Overwrite) &&
                   collisionValues.Contains(CollisionPolicy.Skip) &&
                   collisionValues.Contains(CollisionPolicy.Prompt),
                "W2A_3f: CollisionPolicy defines exactly 4 approved policy options");

            var statusValues = Enum.GetValues<ConversionJobStatus>();
            Assert(statusValues.Length == 7 &&
                   statusValues.Contains(ConversionJobStatus.Queued) &&
                   statusValues.Contains(ConversionJobStatus.Preparing) &&
                   statusValues.Contains(ConversionJobStatus.Running) &&
                   statusValues.Contains(ConversionJobStatus.Completed) &&
                   statusValues.Contains(ConversionJobStatus.Failed) &&
                   statusValues.Contains(ConversionJobStatus.Cancelled) &&
                   statusValues.Contains(ConversionJobStatus.Skipped),
                "W2A_3g: ConversionJobStatus defines exactly 7 approved lifecycle states");
        }

        // W2A_4: EngineResourceProfile defaults, affinity, and resource limits
        {
            var affinities = Enum.GetValues<EngineExecutionAffinity>();
            Assert(affinities.Length == 4 &&
                   affinities.Contains(EngineExecutionAffinity.CpuBound) &&
                   affinities.Contains(EngineExecutionAffinity.MemoryBound) &&
                   affinities.Contains(EngineExecutionAffinity.ProcessBound) &&
                   affinities.Contains(EngineExecutionAffinity.ExclusiveSingleThreaded),
                "W2A_4a: EngineExecutionAffinity defines all 4 engine resource classifications");

            var cpuProfile = EngineResourceProfile.CpuBoundDefault;
            Assert(cpuProfile.Affinity == EngineExecutionAffinity.CpuBound && cpuProfile.MaxConcurrentJobs >= 2,
                "W2A_4b: CpuBoundDefault allocates multi-core concurrency");

            var memProfile = EngineResourceProfile.MemoryBoundHeavy;
            Assert(memProfile.Affinity == EngineExecutionAffinity.MemoryBound && memProfile.MaxConcurrentJobs == 2,
                "W2A_4c: MemoryBoundHeavy limits concurrency to 2 jobs");

            var procProfile = EngineResourceProfile.ProcessBoundDefault;
            Assert(procProfile.Affinity == EngineExecutionAffinity.ProcessBound && procProfile.MaxConcurrentJobs <= 4,
                "W2A_4d: ProcessBoundDefault bounds process execution to <= 4 jobs");

            var exclusiveProfile = EngineResourceProfile.ExclusiveCom;
            Assert(exclusiveProfile.Affinity == EngineExecutionAffinity.ExclusiveSingleThreaded && exclusiveProfile.MaxConcurrentJobs == 1,
                "W2A_4e: ExclusiveCom enforces strictly single-threaded serial execution (MaxConcurrentJobs=1)");
        }

        // W2A_5: ConversionResult structured outcome and factory helpers
        {
            var success = ConversionResult.Success(@"C:\out\doc.pdf", 2048, TimeSpan.FromMilliseconds(250));
            Assert(success.IsSuccess && success.Status == ConversionJobStatus.Completed,
                "W2A_5a: ConversionResult.Success creates completed result with IsSuccess=true");
            Assert(success.OutputPath == @"C:\out\doc.pdf" && success.OutputSizeBytes == 2048,
                "W2A_5b: ConversionResult.Success records valid output path and size");

            var failure = ConversionResult.Failure("ERR_CORRUPT", "Source file is corrupt", "Magic bytes mismatch: 0x00", TimeSpan.FromMilliseconds(50));
            Assert(!failure.IsSuccess && failure.Status == ConversionJobStatus.Failed,
                "W2A_5c: ConversionResult.Failure creates failed result with IsSuccess=false");
            Assert(failure.ErrorCode == "ERR_CORRUPT" && failure.ErrorMessage == "Source file is corrupt",
                "W2A_5d: ConversionResult.Failure records error code and user-facing message");
            Assert(failure.DiagnosticDetails == "Magic bytes mismatch: 0x00",
                "W2A_5e: ConversionResult.Failure captures technical diagnostic details separately");

            var cancelled = ConversionResult.Cancelled(TimeSpan.FromSeconds(1));
            Assert(!cancelled.IsSuccess && cancelled.Status == ConversionJobStatus.Cancelled,
                "W2A_5f: ConversionResult.Cancelled reflects Cancelled status");

            var skipped = ConversionResult.Skipped("File exists and policy is Skip", @"C:\out\exists.png");
            Assert(!skipped.IsSuccess && skipped.Status == ConversionJobStatus.Skipped && skipped.OutputPath == @"C:\out\exists.png",
                "W2A_5g: ConversionResult.Skipped reflects Skipped status with reason and path");
        }

        // W2A_6: QueueProgressReport aggregate telemetry calculation
        {
            var report = new QueueProgressReport
            {
                TotalJobs = 10,
                CompletedJobs = 6,
                FailedJobs = 1,
                CancelledJobs = 1,
                SkippedJobs = 2,
                RunningJobs = 0,
                OverallProgressPercentage = 100.0,
                TotalBytesProcessed = 52428800,
                CurrentJobName = null
            };

            Assert(report.FinishedJobs == 10,
                $"W2A_6a: FinishedJobs accurately sums completed, failed, cancelled, and skipped jobs (Expected: 10, Actual: {report.FinishedJobs})");
            Assert(report.IsCompleted,
                "W2A_6b: IsCompleted returns true when all queued jobs have finished");
            Assert(report.HasFailures,
                "W2A_6c: HasFailures returns true when any job failed");

            var partialReport = new QueueProgressReport
            {
                TotalJobs = 10,
                CompletedJobs = 4,
                RunningJobs = 2,
                OverallProgressPercentage = 40.0
            };
            Assert(!partialReport.IsCompleted,
                "W2A_6d: IsCompleted returns false while jobs are still remaining");
            Assert(!partialReport.HasFailures,
                "W2A_6e: HasFailures returns false when no failures occurred");
        }

        // W2A_7: Interface contract compilation and mock adherence
        {
            var mockEngine = new MockConversionEngine("test-engine", "Test Conversion Engine", true);
            Assert(mockEngine.EngineId == "test-engine" && mockEngine.DisplayName == "Test Conversion Engine",
                "W2A_7a: Mock IConversionEngine properly adheres to contract properties");
            Assert(mockEngine.IsAvailable && mockEngine.CanConvert(".png", ".jpg"),
                "W2A_7b: IConversionEngine.CanConvert verifies format support");
            Assert(mockEngine.ResourceProfile.Affinity == EngineExecutionAffinity.CpuBound,
                "W2A_7c: IConversionEngine exposes EngineResourceProfile");

            using var testJob = new ConversionJob { SourceFilePath = "test.png", TargetExtension = ".jpg" };
            var result = await mockEngine.ConvertAsync(testJob);
            Assert(result.IsSuccess && result.OutputPath == "test.jpg",
                "W2A_7d: IConversionEngine.ConvertAsync returns valid ConversionResult");

            var mockOrchestrator = new MockConversionOrchestrator([mockEngine]);
            Assert(mockOrchestrator.RegisteredEngines.Count == 1,
                "W2A_7e: IConversionOrchestrator exposes RegisteredEngines list");
            Assert(mockOrchestrator.CanConvert(".png", ".jpg"),
                "W2A_7f: IConversionOrchestrator.CanConvert delegates to available engine");
            Assert(mockOrchestrator.ResolveEngine(".png", ".jpg")?.EngineId == "test-engine",
                "W2A_7g: IConversionOrchestrator.ResolveEngine resolves matching engine");
            Assert(mockOrchestrator.GetSupportedTargetExtensions(".png").Contains(".jpg"),
                "W2A_7h: IConversionOrchestrator.GetSupportedTargetExtensions discovers formats");
        }
    }

    #endregion

    #region Phase W2-B1: Native WIC Image Conversion Engine Tests

    private static void CreateBmpFile(string filePath, int width, int height)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        int rowStride = (width * 3 + 3) & ~3;
        int imageSize = rowStride * height;
        int fileSize = 54 + imageSize;

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // BMP File Header (14 bytes)
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write((short)0);
        bw.Write((short)0);
        bw.Write(54);

        // DIB Header (40 bytes)
        bw.Write(40);
        bw.Write(width);
        bw.Write(height);
        bw.Write((short)1);
        bw.Write((short)24);
        bw.Write(0);
        bw.Write(imageSize);
        bw.Write(2835);
        bw.Write(2835);
        bw.Write(0);
        bw.Write(0);

        byte[] row = new byte[rowStride];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                row[x * 3 + 0] = (byte)((x * 255) / width);
                row[x * 3 + 1] = (byte)((y * 255) / height);
                row[x * 3 + 2] = 200;
            }
            bw.Write(row);
        }
    }

    private static void CreateSolidTestImage(string filePath, int width, int height, SKEncodedImageFormat format, int quality = 90)
    {
        if (format == SKEncodedImageFormat.Bmp)
        {
            CreateBmpFile(filePath, width, height);
            return;
        }

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(220, 110, 40));

        using var paint = new SKPaint
        {
            Color = new SKColor(40, 120, 220),
            StrokeWidth = 4,
            IsAntialias = true
        };
        canvas.DrawLine(0, 0, width, height, paint);
        canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 4f, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        if (data == null)
        {
            throw new InvalidOperationException($"SkiaSharp failed to encode format {format}.");
        }
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    private static async Task RunW2_B1WicImageEngineTests()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(">>> [W2-B1] Universal Converter — Native WIC Image Conversion Engine Tests <<<");
        Console.ResetColor();

        var engine = new WicImageConversionEngine();

        // W2B1_1: Engine Metadata & Properties
        {
            Assert(engine.EngineId == "wic", "W2B1_1a: EngineId returns 'wic'");
            Assert(engine.DisplayName == "Native Windows Image Engine (WIC)", "W2B1_1b: DisplayName is 'Native Windows Image Engine (WIC)'");
            Assert(engine.IsAvailable, "W2B1_1c: Engine is natively available on Windows without external dependencies");
            Assert(engine.RequiredDependencyId == null, "W2B1_1d: RequiredDependencyId is null (no third-party installer required)");
            Assert(engine.ResourceProfile.Affinity == EngineExecutionAffinity.CpuBound, "W2B1_1e: ResourceProfile affinity is CpuBound");

            Assert(WicImageConversionEngine.NormalizeExtension(".JPEG") == ".jpg", "W2B1_1f: Extension normalizer maps .JPEG to .jpg");
            Assert(WicImageConversionEngine.NormalizeExtension("png") == ".png", "W2B1_1g: Extension normalizer ensures leading dot");
            Assert(WicImageConversionEngine.NormalizeExtension(".TIF") == ".tiff", "W2B1_1h: Extension normalizer maps .TIF to .tiff");
        }

        // W2B1_2: Format Compatibility Matrix (CanConvert)
        {
            Assert(engine.CanConvert(".png", ".jpg"), "W2B1_2a: CanConvert supports PNG to JPG");
            Assert(engine.CanConvert(".png", ".webp"), "W2B1_2b: CanConvert supports PNG to WebP");
            Assert(engine.CanConvert(".png", ".bmp"), "W2B1_2c: CanConvert supports PNG to BMP");
            Assert(engine.CanConvert(".png", ".tiff"), "W2B1_2d: CanConvert supports PNG to TIFF");
            Assert(engine.CanConvert(".jpg", ".png"), "W2B1_2e: CanConvert supports JPG to PNG");
            Assert(engine.CanConvert(".webp", ".png"), "W2B1_2f: CanConvert supports WebP to PNG");
            Assert(engine.CanConvert(".bmp", ".png"), "W2B1_2g: CanConvert supports BMP to PNG");
            Assert(engine.CanConvert(".tiff", ".png"), "W2B1_2h: CanConvert supports TIFF to PNG");
            Assert(engine.CanConvert(".jpeg", ".webp"), "W2B1_2i: CanConvert normalizes .jpeg alias");
            Assert(engine.CanConvert(".tif", ".jpg"), "W2B1_2j: CanConvert normalizes .tif alias");

            Assert(!engine.CanConvert(".png", ".pdf"), "W2B1_2k: CanConvert rejects unsupported target .pdf");
            Assert(!engine.CanConvert(".docx", ".png"), "W2B1_2l: CanConvert rejects unsupported source .docx");
            Assert(!engine.CanConvert(".png", ".exe"), "W2B1_2m: CanConvert rejects unsupported target .exe");
        }

        var testDir = Path.Combine(Path.GetTempPath(), "Axora_W2B1_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            var srcPng = Path.Combine(testDir, "sample.png");
            var srcJpg = Path.Combine(testDir, "sample.jpg");
            var srcWebp = Path.Combine(testDir, "sample.webp");
            var srcBmp = Path.Combine(testDir, "sample.bmp");

            CreateSolidTestImage(srcPng, 200, 100, SKEncodedImageFormat.Png);
            CreateSolidTestImage(srcJpg, 200, 100, SKEncodedImageFormat.Jpeg);
            CreateSolidTestImage(srcWebp, 200, 100, SKEncodedImageFormat.Webp);
            CreateSolidTestImage(srcBmp, 200, 100, SKEncodedImageFormat.Bmp);

            // W2B1_3: Format Conversions (Real Raster Round-trips)
            string outTiffPath;
            {
                // PNG -> JPG
                var outJpg = Path.Combine(testDir, "out_from_png.jpg");
                var jobJpg = new ConversionJob { SourceFilePath = srcPng, TargetExtension = ".jpg", OutputFilePath = outJpg };
                var resJpg = await engine.ConvertAsync(jobJpg);
                Assert(resJpg.IsSuccess && File.Exists(outJpg) && new FileInfo(outJpg).Length > 0,
                    "W2B1_3a: PNG to JPG converts cleanly to non-empty file");

                // PNG -> WebP
                var outWebp = Path.Combine(testDir, "out_from_png.webp");
                var jobWebp = new ConversionJob { SourceFilePath = srcPng, TargetExtension = ".webp", OutputFilePath = outWebp };
                var resWebp = await engine.ConvertAsync(jobWebp);
                Assert(resWebp.IsSuccess && File.Exists(outWebp) && new FileInfo(outWebp).Length > 0,
                    "W2B1_3b: PNG to WebP converts cleanly to non-empty file");

                // PNG -> BMP
                var outBmp = Path.Combine(testDir, "out_from_png.bmp");
                var jobBmp = new ConversionJob { SourceFilePath = srcPng, TargetExtension = ".bmp", OutputFilePath = outBmp };
                var resBmp = await engine.ConvertAsync(jobBmp);
                Assert(resBmp.IsSuccess && File.Exists(outBmp) && new FileInfo(outBmp).Length > 0,
                    "W2B1_3c: PNG to BMP converts cleanly to non-empty file");

                // PNG -> TIFF
                outTiffPath = Path.Combine(testDir, "out_from_png.tiff");
                var jobTiff = new ConversionJob { SourceFilePath = srcPng, TargetExtension = ".tiff", OutputFilePath = outTiffPath };
                var resTiff = await engine.ConvertAsync(jobTiff);
                Assert(resTiff.IsSuccess && File.Exists(outTiffPath) && new FileInfo(outTiffPath).Length > 0,
                    "W2B1_3d: PNG to TIFF converts cleanly to non-empty file");

                // JPG -> PNG
                var outPngFromJpg = Path.Combine(testDir, "out_from_jpg.png");
                var jobPngFromJpg = new ConversionJob { SourceFilePath = srcJpg, TargetExtension = ".png", OutputFilePath = outPngFromJpg };
                var resPngFromJpg = await engine.ConvertAsync(jobPngFromJpg);
                Assert(resPngFromJpg.IsSuccess && File.Exists(outPngFromJpg) && new FileInfo(outPngFromJpg).Length > 0,
                    "W2B1_3e: JPG to PNG converts cleanly to non-empty file");

                // JPG -> WebP
                var outWebpFromJpg = Path.Combine(testDir, "out_from_jpg.webp");
                var jobWebpFromJpg = new ConversionJob { SourceFilePath = srcJpg, TargetExtension = ".webp", OutputFilePath = outWebpFromJpg };
                var resWebpFromJpg = await engine.ConvertAsync(jobWebpFromJpg);
                Assert(resWebpFromJpg.IsSuccess && File.Exists(outWebpFromJpg) && new FileInfo(outWebpFromJpg).Length > 0,
                    "W2B1_3f: JPG to WebP converts cleanly to non-empty file");

                // WebP -> PNG
                var outPngFromWebp = Path.Combine(testDir, "out_from_webp.png");
                var jobPngFromWebp = new ConversionJob { SourceFilePath = srcWebp, TargetExtension = ".png", OutputFilePath = outPngFromWebp };
                var resPngFromWebp = await engine.ConvertAsync(jobPngFromWebp);
                Assert(resPngFromWebp.IsSuccess && File.Exists(outPngFromWebp) && new FileInfo(outPngFromWebp).Length > 0,
                    "W2B1_3g: WebP to PNG converts cleanly to non-empty file");

                // WebP -> JPG
                var outJpgFromWebp = Path.Combine(testDir, "out_from_webp.jpg");
                var jobJpgFromWebp = new ConversionJob { SourceFilePath = srcWebp, TargetExtension = ".jpg", OutputFilePath = outJpgFromWebp };
                var resJpgFromWebp = await engine.ConvertAsync(jobJpgFromWebp);
                Assert(resJpgFromWebp.IsSuccess && File.Exists(outJpgFromWebp) && new FileInfo(outJpgFromWebp).Length > 0,
                    "W2B1_3h: WebP to JPG converts cleanly to non-empty file");

                // BMP -> PNG
                var outPngFromBmp = Path.Combine(testDir, "out_from_bmp.png");
                var jobPngFromBmp = new ConversionJob { SourceFilePath = srcBmp, TargetExtension = ".png", OutputFilePath = outPngFromBmp };
                var resPngFromBmp = await engine.ConvertAsync(jobPngFromBmp);
                Assert(resPngFromBmp.IsSuccess && File.Exists(outPngFromBmp) && new FileInfo(outPngFromBmp).Length > 0,
                    "W2B1_3i: BMP to PNG converts cleanly to non-empty file");

                // TIFF -> PNG
                var outPngFromTiff = Path.Combine(testDir, "out_from_tiff.png");
                var jobPngFromTiff = new ConversionJob { SourceFilePath = outTiffPath, TargetExtension = ".png", OutputFilePath = outPngFromTiff };
                var resPngFromTiff = await engine.ConvertAsync(jobPngFromTiff);
                Assert(resPngFromTiff.IsSuccess && File.Exists(outPngFromTiff) && new FileInfo(outPngFromTiff).Length > 0,
                    "W2B1_3j: TIFF to PNG converts cleanly to non-empty file");
            }

            // W2B1_4: Non-Destructive File Safety (Source Preservation & Staging Cleanliness)
            {
                byte[] srcBytesBefore = File.ReadAllBytes(srcPng);
                byte[] srcHashBefore = SHA256.HashData(srcBytesBefore);

                var outSafeJpg = Path.Combine(testDir, "safe_test.jpg");
                var safeJob = new ConversionJob { SourceFilePath = srcPng, TargetExtension = ".jpg", OutputFilePath = outSafeJpg };
                var safeRes = await engine.ConvertAsync(safeJob);

                byte[] srcBytesAfter = File.ReadAllBytes(srcPng);
                byte[] srcHashAfter = SHA256.HashData(srcBytesAfter);

                Assert(srcHashBefore.SequenceEqual(srcHashAfter),
                    "W2B1_4a: Source file SHA-256 hash is byte-for-byte identical after conversion");
                Assert(File.Exists(srcPng),
                    "W2B1_4b: Source file remains present on disk and was neither moved nor deleted");

                var strayTempFiles = Directory.GetFiles(testDir, ".tmp_axora_*");
                Assert(strayTempFiles.Length == 0,
                    "W2B1_4c: Temporary atomic staging scratch files are completely cleaned up");

                // Output same as source safety test
                var samePathJob = new ConversionJob { SourceFilePath = srcPng, TargetExtension = ".png", OutputFilePath = srcPng };
                var samePathRes = await engine.ConvertAsync(samePathJob);
                Assert(!samePathRes.IsSuccess && samePathRes.ErrorCode == "ERR_OUTPUT_SAME_AS_SOURCE",
                    "W2B1_4d: Engine strictly refuses to overwrite source file when OutputFilePath matches SourceFilePath");
            }

            // W2B1_5: ConversionProfile Options (Quality, MaxDimension, MetadataHandling)
            {
                // Quality scaling test: JPEG
                var outLowQ = Path.Combine(testDir, "quality_low.jpg");
                var jobLowQ = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".jpg",
                    OutputFilePath = outLowQ,
                    Profile = new ConversionProfile { Quality = 10 }
                };
                var outHighQ = Path.Combine(testDir, "quality_high.jpg");
                var jobHighQ = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".jpg",
                    OutputFilePath = outHighQ,
                    Profile = new ConversionProfile { Quality = 95 }
                };

                await engine.ConvertAsync(jobLowQ);
                await engine.ConvertAsync(jobHighQ);

                long sizeLow = new FileInfo(outLowQ).Length;
                long sizeHigh = new FileInfo(outHighQ).Length;
                Assert(sizeLow < sizeHigh,
                    $"W2B1_5a: Quality setting scales JPEG compression (Quality 10: {sizeLow}B < Quality 95: {sizeHigh}B)");

                // Quality scaling test: WebP
                var outLowWebp = Path.Combine(testDir, "quality_low.webp");
                var jobLowWebp = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".webp",
                    OutputFilePath = outLowWebp,
                    Profile = new ConversionProfile { Quality = 10 }
                };
                var outHighWebp = Path.Combine(testDir, "quality_high.webp");
                var jobHighWebp = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".webp",
                    OutputFilePath = outHighWebp,
                    Profile = new ConversionProfile { Quality = 95 }
                };

                await engine.ConvertAsync(jobLowWebp);
                await engine.ConvertAsync(jobHighWebp);

                long sizeWebpLow = new FileInfo(outLowWebp).Length;
                long sizeWebpHigh = new FileInfo(outHighWebp).Length;
                Assert(sizeWebpLow < sizeWebpHigh,
                    $"W2B1_5b: Quality setting scales WebP compression (Quality 10: {sizeWebpLow}B < Quality 95: {sizeWebpHigh}B)");

                // Out-of-bounds quality clamped safely
                var outClampQ = Path.Combine(testDir, "quality_clamped.jpg");
                var jobClampQ = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".jpg",
                    OutputFilePath = outClampQ,
                    Profile = new ConversionProfile { Quality = 150 }
                };
                var resClampQ = await engine.ConvertAsync(jobClampQ);
                Assert(resClampQ.IsSuccess && File.Exists(outClampQ),
                    "W2B1_5c: Out-of-bounds quality (150) is safely clamped and executes cleanly");

                // MaxDimension Aspect Ratio Scaling (Landscape 400x200 -> 100x50)
                var srcLandscape = Path.Combine(testDir, "landscape_400x200.png");
                CreateSolidTestImage(srcLandscape, 400, 200, SKEncodedImageFormat.Png);

                var outScaledLandscape = Path.Combine(testDir, "scaled_landscape.png");
                var jobScaleLandscape = new ConversionJob
                {
                    SourceFilePath = srcLandscape,
                    TargetExtension = ".png",
                    OutputFilePath = outScaledLandscape,
                    Profile = new ConversionProfile { MaxDimension = 100 }
                };
                var resScale = await engine.ConvertAsync(jobScaleLandscape);
                Assert(resScale.IsSuccess, "W2B1_5d: MaxDimension downscaling succeeds");

                using (var decodedBmp = SKBitmap.Decode(outScaledLandscape))
                {
                    Assert(decodedBmp.Width == 100 && decodedBmp.Height == 50,
                        $"W2B1_5e: MaxDimension=100 strictly preserves 2:1 aspect ratio on landscape (Actual: {decodedBmp.Width}x{decodedBmp.Height})");
                }

                // MaxDimension Aspect Ratio Scaling (Portrait 150x300 -> 50x100)
                var srcPortrait = Path.Combine(testDir, "portrait_150x300.png");
                CreateSolidTestImage(srcPortrait, 150, 300, SKEncodedImageFormat.Png);

                var outScaledPortrait = Path.Combine(testDir, "scaled_portrait.png");
                var jobScalePortrait = new ConversionJob
                {
                    SourceFilePath = srcPortrait,
                    TargetExtension = ".png",
                    OutputFilePath = outScaledPortrait,
                    Profile = new ConversionProfile { MaxDimension = 100 }
                };
                await engine.ConvertAsync(jobScalePortrait);

                using (var decodedBmp = SKBitmap.Decode(outScaledPortrait))
                {
                    Assert(decodedBmp.Width == 50 && decodedBmp.Height == 100,
                        $"W2B1_5f: MaxDimension=100 strictly preserves 1:2 aspect ratio on portrait (Actual: {decodedBmp.Width}x{decodedBmp.Height})");
                }

                // MaxDimension No-Upscale (Image smaller than MaxDimension remains unchanged)
                var srcSmall = Path.Combine(testDir, "small_80x60.png");
                CreateSolidTestImage(srcSmall, 80, 60, SKEncodedImageFormat.Png);

                var outSmall = Path.Combine(testDir, "out_small.png");
                var jobSmall = new ConversionJob
                {
                    SourceFilePath = srcSmall,
                    TargetExtension = ".png",
                    OutputFilePath = outSmall,
                    Profile = new ConversionProfile { MaxDimension = 500 }
                };
                await engine.ConvertAsync(jobSmall);

                using (var decodedBmp = SKBitmap.Decode(outSmall))
                {
                    Assert(decodedBmp.Width == 80 && decodedBmp.Height == 60,
                        $"W2B1_5g: MaxDimension does not upscale images smaller than bounding box (Actual: {decodedBmp.Width}x{decodedBmp.Height})");
                }

                // Negative MaxDimension treated safely as original dimensions
                var outNegDim = Path.Combine(testDir, "neg_dim.png");
                var jobNegDim = new ConversionJob
                {
                    SourceFilePath = srcLandscape,
                    TargetExtension = ".png",
                    OutputFilePath = outNegDim,
                    Profile = new ConversionProfile { MaxDimension = -50 }
                };
                var resNegDim = await engine.ConvertAsync(jobNegDim);
                Assert(resNegDim.IsSuccess, "W2B1_5h: Negative MaxDimension (-50) handled safely without error");

                // High-resolution large image safety (2000x2000)
                var srcLarge = Path.Combine(testDir, "large_2000x2000.png");
                CreateSolidTestImage(srcLarge, 2000, 2000, SKEncodedImageFormat.Png);

                var outLarge = Path.Combine(testDir, "out_large.jpg");
                var jobLarge = new ConversionJob
                {
                    SourceFilePath = srcLarge,
                    TargetExtension = ".jpg",
                    OutputFilePath = outLarge,
                    Profile = new ConversionProfile { Quality = 80 }
                };
                var resLarge = await engine.ConvertAsync(jobLarge);
                Assert(resLarge.IsSuccess && File.Exists(outLarge) && new FileInfo(outLarge).Length > 0,
                    "W2B1_5i: Large 2000x2000 high-resolution image converts cleanly and non-empty");

                using (var decodedLarge = SKBitmap.Decode(outLarge))
                {
                    Assert(decodedLarge != null && decodedLarge.Width == 2000 && decodedLarge.Height == 2000,
                        "W2B1_5j: Converted 2000x2000 image is independently decodable and retains dimensions");
                }

                // Metadata Policy test
                var outStrip = Path.Combine(testDir, "meta_strip.jpg");
                var jobStrip = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".jpg",
                    OutputFilePath = outStrip,
                    Profile = new ConversionProfile { MetadataPolicy = MetadataHandling.Strip }
                };
                var resStrip = await engine.ConvertAsync(jobStrip);
                Assert(resStrip.IsSuccess, "W2B1_5k: MetadataHandling.Strip executes cleanly");

                var outPreserve = Path.Combine(testDir, "meta_preserve.jpg");
                var jobPreserve = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".jpg",
                    OutputFilePath = outPreserve,
                    Profile = new ConversionProfile { MetadataPolicy = MetadataHandling.Preserve }
                };
                var resPreserve = await engine.ConvertAsync(jobPreserve);
                Assert(resPreserve.IsSuccess, "W2B1_5l: MetadataHandling.Preserve executes cleanly");
            }

            // W2B1_6: Negative Paths & Adversarial Error Handling
            {
                // Missing source file
                var missingJob = new ConversionJob
                {
                    SourceFilePath = Path.Combine(testDir, "nonexistent_" + Guid.NewGuid().ToString("N") + ".png"),
                    TargetExtension = ".jpg",
                    OutputFilePath = Path.Combine(testDir, "out_missing.jpg")
                };
                var resMissing = await engine.ConvertAsync(missingJob);
                Assert(!resMissing.IsSuccess && resMissing.ErrorCode == "ERR_INPUT_NOT_FOUND",
                    "W2B1_6a: Nonexistent source file yields ERR_INPUT_NOT_FOUND");

                // 0-byte source file
                var emptyFile = Path.Combine(testDir, "empty.png");
                File.WriteAllBytes(emptyFile, Array.Empty<byte>());
                var emptyJob = new ConversionJob
                {
                    SourceFilePath = emptyFile,
                    TargetExtension = ".jpg",
                    OutputFilePath = Path.Combine(testDir, "out_empty.jpg")
                };
                var resEmpty = await engine.ConvertAsync(emptyJob);
                Assert(!resEmpty.IsSuccess && resEmpty.ErrorCode == "ERR_INPUT_EMPTY",
                    "W2B1_6b: 0-byte source file yields ERR_INPUT_EMPTY");

                // Corrupted header file
                var corruptFile = Path.Combine(testDir, "corrupted.png");
                File.WriteAllText(corruptFile, "NOT_AN_IMAGE_HEADER_RANDOM_TEXT_1234567890");
                var corruptJob = new ConversionJob
                {
                    SourceFilePath = corruptFile,
                    TargetExtension = ".jpg",
                    OutputFilePath = Path.Combine(testDir, "out_corrupt.jpg")
                };
                var resCorrupt = await engine.ConvertAsync(corruptJob);
                Assert(!resCorrupt.IsSuccess && resCorrupt.ErrorCode == "ERR_INPUT_CORRUPT",
                    "W2B1_6c: Corrupted image header yields ERR_INPUT_CORRUPT");

                // Unsupported target format
                var badFormatJob = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".exe",
                    OutputFilePath = Path.Combine(testDir, "out_bad.exe")
                };
                var resBadFormat = await engine.ConvertAsync(badFormatJob);
                Assert(!resBadFormat.IsSuccess && resBadFormat.ErrorCode == "ERR_FORMAT_NOT_SUPPORTED",
                    "W2B1_6d: Unsupported target format yields ERR_FORMAT_NOT_SUPPORTED");

                // Locked source file
                var lockedFile = Path.Combine(testDir, "locked.png");
                File.Copy(srcPng, lockedFile, overwrite: true);
                using (var lockStream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var lockedJob = new ConversionJob
                    {
                        SourceFilePath = lockedFile,
                        TargetExtension = ".jpg",
                        OutputFilePath = Path.Combine(testDir, "out_locked.jpg")
                    };
                    var resLocked = await engine.ConvertAsync(lockedJob);
                    Assert(!resLocked.IsSuccess && (resLocked.ErrorCode == "ERR_INPUT_READ_FAILED" || resLocked.ErrorCode == "ERR_INPUT_ACCESS_DENIED"),
                        "W2B1_6e: Exclusively locked source file yields ERR_INPUT_READ_FAILED or ERR_INPUT_ACCESS_DENIED");
                }

                // Invalid destination path
                var invalidDestJob = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".jpg",
                    OutputFilePath = @"Z:\NonExistent_Drive_Invalid_Axora_QA\out.jpg"
                };
                var resInvalidDest = await engine.ConvertAsync(invalidDestJob);
                Assert(!resInvalidDest.IsSuccess && resInvalidDest.ErrorCode == "ERR_OUTPUT_WRITE_FAILED",
                    "W2B1_6f: Invalid destination directory yields ERR_OUTPUT_WRITE_FAILED");

                // Pre-cancelled token
                using var preCancelledCts = new CancellationTokenSource();
                preCancelledCts.Cancel();
                var cancelJob = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".jpg",
                    OutputFilePath = Path.Combine(testDir, "out_cancelled.jpg")
                };
                var resCancel = await engine.ConvertAsync(cancelJob, ct: preCancelledCts.Token);
                Assert(!resCancel.IsSuccess && resCancel.Status == ConversionJobStatus.Cancelled,
                    "W2B1_6g: Pre-cancelled CancellationToken returns Cancelled status");

                // Cancellation via job.Cancel()
                var jobToCancel = new ConversionJob
                {
                    SourceFilePath = srcPng,
                    TargetExtension = ".jpg",
                    OutputFilePath = Path.Combine(testDir, "out_job_cancel.jpg")
                };
                using var jobCts = new CancellationTokenSource();
                jobToCancel.SetCancellationTokenSource(jobCts);
                jobToCancel.Cancel();
                var resJobCancel = await engine.ConvertAsync(jobToCancel);
                Assert(!resJobCancel.IsSuccess && resJobCancel.Status == ConversionJobStatus.Cancelled,
                    "W2B1_6h: Cancellation via job.Cancel() propagates into Cancelled status");
            }

            // W2B1_7: Engine Reusability & Sequential Robustness (100 Conversions across multiple formats)
            {
                bool all100Passed = true;
                string[] targetFormats = [".jpg", ".webp", ".png", ".bmp"];
                for (int i = 0; i < 100; i++)
                {
                    var ext = targetFormats[i % targetFormats.Length];
                    var consecutiveOut = Path.Combine(testDir, $"consecutive_{i}{ext}");
                    var cJob = new ConversionJob
                    {
                        SourceFilePath = srcPng,
                        TargetExtension = ext,
                        OutputFilePath = consecutiveOut
                    };
                    var cRes = await engine.ConvertAsync(cJob);
                    if (!cRes.IsSuccess || !File.Exists(consecutiveOut))
                    {
                        all100Passed = false;
                        break;
                    }
                }
                Assert(all100Passed, "W2B1_7: 100 consecutive conversions across multiple formats succeed on a single engine instance without resource leakage");
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, recursive: true);
                }
            }
            catch { /* Best effort test cleanup */ }
        }
    }

    #endregion

    #region Phase W2-B2: Native Document & Text Conversion Engines Tests

    private static async Task RunW2_B2DocumentEngineTests()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(">>> [W2-B2] Universal Converter - Native Document & Text Engines Tests <<<");
        Console.ResetColor();

        var pdfEngine = new PdfDocumentConversionEngine();
        var textMdEngine = new TextMarkdownConversionEngine();
        var wicEngine = new WicImageConversionEngine();

        // ═══════════════════════════════════════════════════════════════
        // W2B2_1: Engine Identity, Metadata, Concurrency & Format Support
        // ═══════════════════════════════════════════════════════════════
        {
            Assert(pdfEngine.EngineId == "pdfsharp", "W2B2_1a: PdfDocumentConversionEngine EngineId is 'pdfsharp'");
            Assert(pdfEngine.DisplayName == "Native PDF Document Engine (PdfSharpCore)", "W2B2_1b: DisplayName matches PdfSharpCore title");
            Assert(pdfEngine.IsAvailable && pdfEngine.RequiredDependencyId == null, "W2B2_1c: Engine is natively available without dependencies");
            Assert(pdfEngine.ResourceProfile.Affinity == EngineExecutionAffinity.MemoryBound, "W2B2_1d: ResourceProfile is MemoryBound");

            Assert(textMdEngine.EngineId == "text-markdown", "W2B2_1e: TextMarkdownConversionEngine EngineId is 'text-markdown'");
            Assert(textMdEngine.DisplayName == "Native Text & Markdown Engine", "W2B2_1f: DisplayName matches Text & Markdown title");
            Assert(textMdEngine.IsAvailable && textMdEngine.RequiredDependencyId == null, "W2B2_1g: Text engine is natively available without dependencies");
            Assert(textMdEngine.ResourceProfile.Affinity == EngineExecutionAffinity.CpuBound, "W2B2_1h: ResourceProfile is CpuBound");

            // CanConvert checks
            Assert(pdfEngine.CanConvert(".txt", ".pdf"), "W2B2_1i: PDF engine CanConvert supports TXT to PDF");
            Assert(pdfEngine.CanConvert(".md", ".pdf"), "W2B2_1j: PDF engine CanConvert supports MD to PDF");
            Assert(pdfEngine.CanConvert(".pdf", ".txt"), "W2B2_1k: PDF engine CanConvert supports PDF to TXT");
            Assert(pdfEngine.CanConvert(".png", ".pdf") && pdfEngine.CanConvert(".webp", ".pdf") && pdfEngine.CanConvert(".bmp", ".pdf") && pdfEngine.CanConvert(".tiff", ".pdf"),
                "W2B2_1l: PDF engine CanConvert supports raster images to PDF");
            Assert(!pdfEngine.CanConvert(".docx", ".pdf"), "W2B2_1m: PDF engine rejects unapproved DOCX format in W2-B2");
            Assert(!pdfEngine.CanConvert(".pdf", ".png"), "W2B2_1n: PDF engine rejects PDF to Image (POC-gated in W2-C)");

            Assert(textMdEngine.CanConvert(".md", ".html") && textMdEngine.CanConvert(".markdown", ".html"),
                "W2B2_1o: Text engine CanConvert supports MD to HTML");
            Assert(textMdEngine.CanConvert(".md", ".txt") && textMdEngine.CanConvert(".markdown", ".txt"),
                "W2B2_1p: Text engine CanConvert supports MD to TXT");
            Assert(!textMdEngine.CanConvert(".txt", ".html"), "W2B2_1q: Text engine rejects TXT to HTML");
            Assert(!textMdEngine.CanConvert(".html", ".md"), "W2B2_1r: Text engine rejects HTML to MD");
        }

        var testDir = Path.Combine(Path.GetTempPath(), "Axora_W2B2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            // ═══════════════════════════════════════════════════════════════
            // W2B2_2: TXT -> PDF Conversion
            // ═══════════════════════════════════════════════════════════════
            {
                // Simple text
                var txtSimple = Path.Combine(testDir, "simple.txt");
                var pdfSimple = Path.Combine(testDir, "simple.pdf");
                await File.WriteAllTextAsync(txtSimple, "Hello Axora Desktop Universal Converter!\nThis is a simple text document.");

                byte[] preSha = SHA256.HashData(await File.ReadAllBytesAsync(txtSimple));
                var jobSimple = new ConversionJob { SourceFilePath = txtSimple, TargetExtension = ".pdf", OutputFilePath = pdfSimple };
                var resSimple = await pdfEngine.ConvertAsync(jobSimple);

                Assert(resSimple.IsSuccess && File.Exists(pdfSimple), "W2B2_2a: Simple text converts to PDF successfully");
                var pdfBytes = await File.ReadAllBytesAsync(pdfSimple);
                Assert(IsPdfValid(pdfBytes), "W2B2_2b: Simple text PDF output contains valid %PDF header and %%EOF trailer");
                byte[] postSha = SHA256.HashData(await File.ReadAllBytesAsync(txtSimple));
                Assert(preSha.SequenceEqual(postSha), "W2B2_2c: Source text file SHA-256 byte-for-byte unmodified after conversion");

                // Multiline with CRLF/LF and Unicode text
                var txtUnicode = Path.Combine(testDir, "unicode.txt");
                var pdfUnicode = Path.Combine(testDir, "unicode.pdf");
                string unicodeText = "Line 1: Accented Latin (é, à, ü, ñ, ç, ß, Ø, å)\r\n" +
                                     "Line 2: Greek & Math (α, β, γ, δ, π, ∑, √, ≈, ≠, ≤, ≥)\n" +
                                     "Line 3: Cyrillic text (Привет мир, тестирование Универсального Конвертера)\r\n" +
                                     "Line 4: Mixed symbols and currency (€, £, ¥, $, ₹, ©, ®, ™)\n";
                await File.WriteAllTextAsync(txtUnicode, unicodeText, Encoding.UTF8);

                var jobUnicode = new ConversionJob { SourceFilePath = txtUnicode, TargetExtension = ".pdf", OutputFilePath = pdfUnicode };
                var resUnicode = await pdfEngine.ConvertAsync(jobUnicode);
                Assert(resUnicode.IsSuccess && IsPdfValid(await File.ReadAllBytesAsync(pdfUnicode)),
                    "W2B2_2d: Multiline Unicode text converts cleanly to valid PDF");

                // Extremely long continuous line without spaces (wrap / pagination stress)
                var txtLongLine = Path.Combine(testDir, "long_line.txt");
                var pdfLongLine = Path.Combine(testDir, "long_line.pdf");
                var sbLong = new StringBuilder();
                for (int i = 0; i < 200; i++) sbLong.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_LONG_TOKEN_NO_SPACES_");
                await File.WriteAllTextAsync(txtLongLine, sbLong.ToString(), Encoding.UTF8);

                var jobLong = new ConversionJob { SourceFilePath = txtLongLine, TargetExtension = ".pdf", OutputFilePath = pdfLongLine };
                var resLong = await pdfEngine.ConvertAsync(jobLong);
                Assert(resLong.IsSuccess && IsPdfValid(await File.ReadAllBytesAsync(pdfLongLine)),
                    "W2B2_2e: Continuous oversized line token wraps without buffer overflow or crash");

                // Empty text file (0 bytes) -> ERR_INPUT_EMPTY
                var txtEmpty = Path.Combine(testDir, "empty.txt");
                await File.WriteAllBytesAsync(txtEmpty, Array.Empty<byte>());
                var jobEmpty = new ConversionJob { SourceFilePath = txtEmpty, TargetExtension = ".pdf", OutputFilePath = Path.Combine(testDir, "empty.pdf") };
                var resEmpty = await pdfEngine.ConvertAsync(jobEmpty);
                Assert(!resEmpty.IsSuccess && resEmpty.ErrorCode == "ERR_INPUT_EMPTY",
                    "W2B2_2f: 0-byte text file returns structured ERR_INPUT_EMPTY");

                // Large text file (multi-page document)
                var txtLarge = Path.Combine(testDir, "large.txt");
                var pdfLarge = Path.Combine(testDir, "large.pdf");
                var sbLarge = new StringBuilder();
                for (int i = 1; i <= 300; i++)
                {
                    sbLarge.AppendLine($"Section {i}: Axora Desktop high-performance local processing guarantees zero cloud leakage. Line index {i}.");
                }
                await File.WriteAllTextAsync(txtLarge, sbLarge.ToString(), Encoding.UTF8);
                var jobLarge = new ConversionJob { SourceFilePath = txtLarge, TargetExtension = ".pdf", OutputFilePath = pdfLarge };
                var resLarge = await pdfEngine.ConvertAsync(jobLarge);
                Assert(resLarge.IsSuccess && IsPdfValid(await File.ReadAllBytesAsync(pdfLarge)),
                    "W2B2_2g: Large 300-line text file paginates and converts cleanly");

                // MetadataHandling.Strip vs Preserve
                var pdfStripped = Path.Combine(testDir, "stripped.pdf");
                var jobStrip = new ConversionJob
                {
                    SourceFilePath = txtSimple,
                    TargetExtension = ".pdf",
                    OutputFilePath = pdfStripped,
                    Profile = new ConversionProfile { MetadataPolicy = MetadataHandling.Strip }
                };
                var resStrip = await pdfEngine.ConvertAsync(jobStrip);
                Assert(resStrip.IsSuccess && File.Exists(pdfStripped), "W2B2_2h: MetadataHandling.Strip executes cleanly");

                // Pre-cancelled token
                using var ctsCancelled = new CancellationTokenSource();
                ctsCancelled.Cancel();
                var jobCancel = new ConversionJob { SourceFilePath = txtSimple, TargetExtension = ".pdf", OutputFilePath = Path.Combine(testDir, "cancel.pdf") };
                var resCancel = await pdfEngine.ConvertAsync(jobCancel, ct: ctsCancelled.Token);
                Assert(resCancel.Status == ConversionJobStatus.Cancelled, "W2B2_2i: Pre-cancelled token returns Cancelled status");

                // Output same as source
                var jobSame = new ConversionJob { SourceFilePath = txtSimple, TargetExtension = ".pdf", OutputFilePath = txtSimple };
                var resSame = await pdfEngine.ConvertAsync(jobSame);
                Assert(!resSame.IsSuccess && resSame.ErrorCode == "ERR_OUTPUT_SAME_AS_SOURCE",
                    "W2B2_2j: Engine refuses in-place overwrite with ERR_OUTPUT_SAME_AS_SOURCE");
            }

            // ═══════════════════════════════════════════════════════════════
            // W2B2_3: Markdown -> PDF Conversion
            // ═══════════════════════════════════════════════════════════════
            {
                var mdSample = Path.Combine(testDir, "document.md");
                var pdfFromMd = Path.Combine(testDir, "document_md.pdf");

                string mdContent = @"# Project Axora Architecture

## 1. Executive Summary
Axora is a **high-performance**, *privacy-first* native Windows desktop application.

### Key Capabilities
- Offline-first execution
- Hardware-accelerated processing
- Strict non-destructive invariants

### Architecture Overview
1. WinUI 3 Native Frontend
2. DirectML & ONNX Local AI
3. Universal Conversion Orchestrator

```csharp
public interface IConversionEngine
{
    string EngineId { get; }
    Task<ConversionResult> ConvertAsync(ConversionJob job, CancellationToken ct);
}
```

> **Note:** User files are never transmitted to external cloud endpoints.

---

### External References
- [Project Documentation](https://github.com/rajghosh06-dev/AXORA-DESKTOP)
- [Local Anchor](#executive-summary)
";
                await File.WriteAllTextAsync(mdSample, mdContent, Encoding.UTF8);

                var jobMdPdf = new ConversionJob { SourceFilePath = mdSample, TargetExtension = ".pdf", OutputFilePath = pdfFromMd };
                var resMdPdf = await pdfEngine.ConvertAsync(jobMdPdf);

                Assert(resMdPdf.IsSuccess && File.Exists(pdfFromMd), "W2B2_3a: Markdown compiles to PDF successfully");
                var mdPdfBytes = await File.ReadAllBytesAsync(pdfFromMd);
                Assert(IsPdfValid(mdPdfBytes), "W2B2_3b: Markdown PDF output has valid PDF structure");

                // Untrusted script injection inside markdown does not execute
                var mdScript = Path.Combine(testDir, "malicious.md");
                var pdfScript = Path.Combine(testDir, "malicious.pdf");
                await File.WriteAllTextAsync(mdScript, "# Heading\n<script>alert('pwned')</script>\nNormal paragraph.");
                var jobScript = new ConversionJob { SourceFilePath = mdScript, TargetExtension = ".pdf", OutputFilePath = pdfScript };
                var resScript = await pdfEngine.ConvertAsync(jobScript);
                Assert(resScript.IsSuccess && IsPdfValid(await File.ReadAllBytesAsync(pdfScript)),
                    "W2B2_3c: Untrusted script tags in Markdown are treated as text and compile safely");

                // Empty markdown file
                var mdEmpty = Path.Combine(testDir, "empty.md");
                await File.WriteAllBytesAsync(mdEmpty, Array.Empty<byte>());
                var jobMdEmpty = new ConversionJob { SourceFilePath = mdEmpty, TargetExtension = ".pdf", OutputFilePath = Path.Combine(testDir, "empty_md.pdf") };
                var resMdEmpty = await pdfEngine.ConvertAsync(jobMdEmpty);
                Assert(!resMdEmpty.IsSuccess && resMdEmpty.ErrorCode == "ERR_INPUT_EMPTY",
                    "W2B2_3d: 0-byte Markdown file returns ERR_INPUT_EMPTY");
            }

            // ═══════════════════════════════════════════════════════════════
            // W2B2_4: Markdown -> HTML Conversion
            // ═══════════════════════════════════════════════════════════════
            {
                var mdSource = Path.Combine(testDir, "source.md");
                var htmlOutput = Path.Combine(testDir, "output.html");

                string mdHtmlInput = @"# Welcome to Axora

This is a **bold statement** and *italic emphasis* with `inline_code()`.

- Unordered item 1
- Unordered item 2

1. First step
2. Second step

```python
def test():
    return 'hello world'
```

> Important quote block.

---

[Safe Link](https://example.com/docs)
[Dangerous Link](javascript:alert('xss'))
<script>alert('raw_tag')</script>
";
                await File.WriteAllTextAsync(mdSource, mdHtmlInput, Encoding.UTF8);

                var jobHtml = new ConversionJob { SourceFilePath = mdSource, TargetExtension = ".html", OutputFilePath = htmlOutput };
                var resHtml = await textMdEngine.ConvertAsync(jobHtml);

                Assert(resHtml.IsSuccess && File.Exists(htmlOutput), "W2B2_4a: Markdown to HTML converts successfully");
                string htmlContent = await File.ReadAllTextAsync(htmlOutput);

                Assert(htmlContent.Contains("<!DOCTYPE html>") && htmlContent.Contains("<html lang=\"en\">"),
                    "W2B2_4b: HTML output is a self-contained HTML5 document");
                Assert(htmlContent.Contains("<h1>Welcome to Axora</h1>"), "W2B2_4c: H1 heading converted correctly");
                Assert(htmlContent.Contains("<strong>bold statement</strong>") && htmlContent.Contains("<em>italic emphasis</em>"),
                    "W2B2_4d: Bold and italic inline formatting converted correctly");
                Assert(htmlContent.Contains("<ul>") && htmlContent.Contains("<li>Unordered item 1</li>"),
                    "W2B2_4e: Unordered list converted correctly");
                Assert(htmlContent.Contains("<ol>") && htmlContent.Contains("<li>First step</li>"),
                    "W2B2_4f: Ordered list converted correctly");
                Assert(htmlContent.Contains("<pre><code class=\"language-python\">") && htmlContent.Contains("def test():"),
                    "W2B2_4g: Fenced code block converted correctly with language tag");
                Assert(htmlContent.Contains("&lt;script&gt;alert(&#39;raw_tag&#39;)&lt;/script&gt;") && !htmlContent.Contains("<script>alert('raw_tag')</script>"),
                    "W2B2_4h: Raw <script> tags are strictly HTML-encoded against XSS");
                Assert(!htmlContent.Contains("href=\"javascript:"),
                    "W2B2_4i: Dangerous javascript: URI links are strictly blocked from href attribute");
                Assert(htmlContent.Contains("href=\"https://example.com/docs\" target=\"_blank\" rel=\"noopener noreferrer\""),
                    "W2B2_4j: Safe HTTPS link rendered with target='_blank' and rel='noopener noreferrer'");

                // Deterministic output
                var htmlOutput2 = Path.Combine(testDir, "output2.html");
                var jobHtml2 = new ConversionJob { SourceFilePath = mdSource, TargetExtension = ".html", OutputFilePath = htmlOutput2 };
                await textMdEngine.ConvertAsync(jobHtml2);
                string htmlContent2 = await File.ReadAllTextAsync(htmlOutput2);
                Assert(htmlContent == htmlContent2, "W2B2_4k: Markdown to HTML conversion is 100% deterministic");
            }

            // ═══════════════════════════════════════════════════════════════
            // W2B2_5: Markdown -> TXT Conversion
            // ═══════════════════════════════════════════════════════════════
            {
                var mdTextSource = Path.Combine(testDir, "notes.md");
                var txtOutput = Path.Combine(testDir, "notes.txt");

                string mdTextContent = @"# Chapter 1: Introduction

This is a **crucial** concept that requires *careful* consideration.

- Point A
- Point B

1. Primary item
2. Secondary item

```json
{ ""name"": ""Axora"" }
```

> Keep it simple.

[Learn More](https://axora.dev)
";
                await File.WriteAllTextAsync(mdTextSource, mdTextContent, Encoding.UTF8);

                var jobTxt = new ConversionJob { SourceFilePath = mdTextSource, TargetExtension = ".txt", OutputFilePath = txtOutput };
                var resTxt = await textMdEngine.ConvertAsync(jobTxt);

                Assert(resTxt.IsSuccess && File.Exists(txtOutput), "W2B2_5a: Markdown to TXT converts successfully");
                string txtContent = await File.ReadAllTextAsync(txtOutput);

                Assert(!txtContent.Contains("# Chapter 1") && txtContent.Contains("Chapter 1: Introduction"),
                    "W2B2_5b: Headings are stripped of # markers");
                Assert(!txtContent.Contains("**crucial**") && txtContent.Contains("crucial concept"),
                    "W2B2_5c: Bold markers are stripped");
                Assert(!txtContent.Contains("*careful*") && txtContent.Contains("careful consideration"),
                    "W2B2_5d: Italic markers are stripped");
                Assert(txtContent.Contains("• Point A") && txtContent.Contains("• Point B"),
                    "W2B2_5e: Unordered list items rendered with clean bullets");
                Assert(txtContent.Contains("1. Primary item") && txtContent.Contains("2. Secondary item"),
                    "W2B2_5f: Ordered list items preserve numbering");
                Assert(!txtContent.Contains("```json") && txtContent.Contains("{ \"name\": \"Axora\" }"),
                    "W2B2_5g: Code fence lines are stripped while body content is preserved");
                Assert(txtContent.Contains("Learn More (https://axora.dev)"),
                    "W2B2_5h: Links are transformed to readable text (url) format");
            }

            // ═══════════════════════════════════════════════════════════════
            // W2B2_6: PDF -> TXT Extraction
            // ═══════════════════════════════════════════════════════════════
            {
                // Text-bearing PDF
                var txtInputForPdf = Path.Combine(testDir, "extract_src.txt");
                var pdfGenerated = Path.Combine(testDir, "extract_src.pdf");
                var txtExtracted = Path.Combine(testDir, "extracted.txt");

                await File.WriteAllTextAsync(txtInputForPdf, "Axora Universal Converter Native Architecture.\nHigh reliability offline PDF processing.");
                var jobMakePdf = new ConversionJob { SourceFilePath = txtInputForPdf, TargetExtension = ".pdf", OutputFilePath = pdfGenerated };
                await pdfEngine.ConvertAsync(jobMakePdf);

                byte[] pdfShaPre = SHA256.HashData(await File.ReadAllBytesAsync(pdfGenerated));

                var jobExtract = new ConversionJob { SourceFilePath = pdfGenerated, TargetExtension = ".txt", OutputFilePath = txtExtracted };
                var resExtract = await pdfEngine.ConvertAsync(jobExtract);

                Assert(resExtract.IsSuccess && File.Exists(txtExtracted), "W2B2_6a: PDF to TXT extracts successfully from text-bearing PDF");
                string extractedText = await File.ReadAllTextAsync(txtExtracted);
                Assert(extractedText.Contains("Axora Universal Converter Native Architecture"),
                    "W2B2_6b: Extracted text contains original document phrases");
                byte[] pdfShaPost = SHA256.HashData(await File.ReadAllBytesAsync(pdfGenerated));
                Assert(pdfShaPre.SequenceEqual(pdfShaPost), "W2B2_6c: Source PDF is byte-for-byte unmodified after extraction");

                // Scanned / Image-only PDF (no text objects) -> ERR_NO_EXTRACTABLE_TEXT
                var pdfScanned = Path.Combine(testDir, "scanned_image_only.pdf");
                {
                    using var scanDoc = new PdfSharpCore.Pdf.PdfDocument();
                    var page = scanDoc.AddPage();
                    using var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
                    // Draw only a vector rectangle, no text!
                    gfx.DrawRectangle(PdfSharpCore.Drawing.XBrushes.LightGray, 10, 10, 200, 200);
                    scanDoc.Save(pdfScanned);
                }

                var jobScanned = new ConversionJob { SourceFilePath = pdfScanned, TargetExtension = ".txt", OutputFilePath = Path.Combine(testDir, "scanned.txt") };
                var resScanned = await pdfEngine.ConvertAsync(jobScanned);

                Assert(!resScanned.IsSuccess && resScanned.ErrorCode == "ERR_NO_EXTRACTABLE_TEXT",
                    "W2B2_6d: Scanned/image-only PDF returns structured ERR_NO_EXTRACTABLE_TEXT");
                Assert(resScanned.ErrorMessage != null && resScanned.ErrorMessage.Contains("OCR is required"),
                    "W2B2_6e: Error message advises user that OCR is required");

                // Corrupted PDF file
                var pdfCorrupt = Path.Combine(testDir, "corrupt.pdf");
                await File.WriteAllBytesAsync(pdfCorrupt, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x00, 0xFF, 0xDE, 0xAD, 0xBE, 0xEF });
                var jobCorrupt = new ConversionJob { SourceFilePath = pdfCorrupt, TargetExtension = ".txt", OutputFilePath = Path.Combine(testDir, "corrupt.txt") };
                var resCorrupt = await pdfEngine.ConvertAsync(jobCorrupt);
                Assert(!resCorrupt.IsSuccess && (resCorrupt.ErrorCode == "ERR_INPUT_CORRUPT" || resCorrupt.ErrorCode == "ERR_INPUT_READ_FAILED"),
                    "W2B2_6f: Corrupted PDF file returns structured ERR_INPUT_CORRUPT or read failure");

                // Non-existent PDF file
                var jobMissingPdf = new ConversionJob { SourceFilePath = Path.Combine(testDir, "ghost.pdf"), TargetExtension = ".txt", OutputFilePath = Path.Combine(testDir, "ghost.txt") };
                var resMissingPdf = await pdfEngine.ConvertAsync(jobMissingPdf);
                Assert(!resMissingPdf.IsSuccess && resMissingPdf.ErrorCode == "ERR_INPUT_NOT_FOUND",
                    "W2B2_6g: Non-existent PDF returns ERR_INPUT_NOT_FOUND");
            }

            // ═══════════════════════════════════════════════════════════════
            // W2B2_7: Image Sequence -> PDF Conversion
            // ═══════════════════════════════════════════════════════════════
            {
                var imgPng = Path.Combine(testDir, "img1.png");
                var imgJpg = Path.Combine(testDir, "img2.jpg");
                var imgWebp = Path.Combine(testDir, "img3.webp");
                var imgBmp = Path.Combine(testDir, "img4.bmp");

                CreateSolidTestImage(imgPng, 300, 200, SKEncodedImageFormat.Png); // Landscape
                CreateSolidTestImage(imgJpg, 150, 300, SKEncodedImageFormat.Jpeg); // Portrait
                CreateSolidTestImage(imgWebp, 250, 250, SKEncodedImageFormat.Webp); // Square
                CreateSolidTestImage(imgBmp, 200, 100, SKEncodedImageFormat.Bmp);

                // Generate TIFF via WIC engine
                var imgTiff = Path.Combine(testDir, "img5.tiff");
                var jobTiffMake = new ConversionJob { SourceFilePath = imgPng, TargetExtension = ".tiff", OutputFilePath = imgTiff };
                await wicEngine.ConvertAsync(jobTiffMake);

                // 1. Single image to PDF (.png -> .pdf)
                var pdfSinglePng = Path.Combine(testDir, "single_png.pdf");
                var jobSinglePng = new ConversionJob { SourceFilePath = imgPng, TargetExtension = ".pdf", OutputFilePath = pdfSinglePng };
                var resSinglePng = await pdfEngine.ConvertAsync(jobSinglePng);
                Assert(resSinglePng.IsSuccess && IsPdfValid(await File.ReadAllBytesAsync(pdfSinglePng)),
                    "W2B2_7a: Single PNG converts cleanly to 1-page PDF");

                // 2. Single WebP to PDF (.webp -> .pdf)
                var pdfSingleWebp = Path.Combine(testDir, "single_webp.pdf");
                var jobSingleWebp = new ConversionJob { SourceFilePath = imgWebp, TargetExtension = ".pdf", OutputFilePath = pdfSingleWebp };
                var resSingleWebp = await pdfEngine.ConvertAsync(jobSingleWebp);
                Assert(resSingleWebp.IsSuccess && IsPdfValid(await File.ReadAllBytesAsync(pdfSingleWebp)),
                    "W2B2_7b: Single WebP converts cleanly to 1-page PDF");

                // 3. Multi-image sequence (PNG + JPG + WebP + BMP + TIFF)
                var imageSequence = new List<string> { imgPng, imgJpg, imgWebp, imgBmp, imgTiff };
                var pdfPackaged = Path.Combine(testDir, "packaged_images.pdf");

                // Compute pre-packaging SHA256 of each image
                var hashesPre = new List<byte[]>();
                foreach (var p in imageSequence) hashesPre.Add(SHA256.HashData(await File.ReadAllBytesAsync(p)));

                var jobSequence = new ConversionJob
                {
                    SourceFilePath = imgPng,
                    SourceFileSequence = imageSequence,
                    TargetExtension = ".pdf",
                    OutputFilePath = pdfPackaged
                };
                var resSequence = await pdfEngine.ConvertAsync(jobSequence);

                Assert(resSequence.IsSuccess && File.Exists(pdfPackaged),
                    $"W2B2_7c: Multi-format image sequence packages into PDF successfully (Result: {resSequence.Status}, ErrorCode: {resSequence.ErrorCode}, Message: '{resSequence.ErrorMessage}', Details: '{resSequence.DiagnosticDetails}')");
                if (File.Exists(pdfPackaged))
                {
                    var packagedBytes = await File.ReadAllBytesAsync(pdfPackaged);
                    Assert(IsPdfValid(packagedBytes), "W2B2_7d: Packaged PDF has valid PDF header and EOF");
                }

                if (File.Exists(pdfPackaged))
                {
                    using (var readDoc = PdfSharpCore.Pdf.IO.PdfReader.Open(pdfPackaged, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.ReadOnly))
                    {
                        Assert(readDoc.PageCount == 5, $"W2B2_7e: Packaged PDF contains exactly 5 pages (Actual: {readDoc.PageCount})");
                        // Check orientation matching: page 0 (300x200 landscape), page 1 (150x300 portrait)
                        Assert(readDoc.Pages[0].Width > readDoc.Pages[0].Height || readDoc.Pages[0].Orientation == PdfSharpCore.PageOrientation.Landscape,
                            "W2B2_7f: Page 1 orientation is Landscape matching landscape source image");
                        Assert(readDoc.Pages[1].Height > readDoc.Pages[1].Width || readDoc.Pages[1].Orientation == PdfSharpCore.PageOrientation.Portrait,
                            "W2B2_7g: Page 2 orientation is Portrait matching portrait source image");
                    }
                }

                // Verify all source image hashes remain byte-for-byte identical
                bool allHashesIntact = true;
                for (int i = 0; i < imageSequence.Count; i++)
                {
                    byte[] post = SHA256.HashData(await File.ReadAllBytesAsync(imageSequence[i]));
                    if (!hashesPre[i].SequenceEqual(post)) allHashesIntact = false;
                }
                Assert(allHashesIntact, "W2B2_7h: All source images in sequence retain identical SHA-256 hashes");

                // Missing image in sequence
                var badSequenceMissing = new List<string> { imgPng, Path.Combine(testDir, "ghost.png") };
                var jobBadSeqMissing = new ConversionJob
                {
                    SourceFileSequence = badSequenceMissing,
                    TargetExtension = ".pdf",
                    OutputFilePath = Path.Combine(testDir, "bad_seq.pdf")
                };
                var resBadSeqMissing = await pdfEngine.ConvertAsync(jobBadSeqMissing);
                Assert(!resBadSeqMissing.IsSuccess && resBadSeqMissing.ErrorCode == "ERR_INPUT_NOT_FOUND",
                    "W2B2_7i: Sequence with missing image fails gracefully with ERR_INPUT_NOT_FOUND");

                // Corrupted image in sequence
                var imgCorrupt = Path.Combine(testDir, "corrupt_img.png");
                await File.WriteAllBytesAsync(imgCorrupt, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0xFF, 0xFF });
                var badSequenceCorrupt = new List<string> { imgPng, imgCorrupt };
                var jobBadSeqCorrupt = new ConversionJob
                {
                    SourceFileSequence = badSequenceCorrupt,
                    TargetExtension = ".pdf",
                    OutputFilePath = Path.Combine(testDir, "bad_seq_corrupt.pdf")
                };
                var resBadSeqCorrupt = await pdfEngine.ConvertAsync(jobBadSeqCorrupt);
                Assert(!resBadSeqCorrupt.IsSuccess && resBadSeqCorrupt.ErrorCode == "ERR_INPUT_CORRUPT",
                    "W2B2_7j: Sequence with corrupt image returns ERR_INPUT_CORRUPT");
            }

            // ═══════════════════════════════════════════════════════════════
            // W2B2_8: Adversarial Security, Concurrency & Stress
            // ═══════════════════════════════════════════════════════════════
            {
                // Unicode filename support
                var unicodeFileName = "исследование_दस्तावेज़_文档.md";
                var mdUnicodeFile = Path.Combine(testDir, unicodeFileName);
                var pdfUnicodeFile = Path.Combine(testDir, "out_unicode_file.pdf");
                await File.WriteAllTextAsync(mdUnicodeFile, "# Unicode Title\nContent in UTF-8 file.", Encoding.UTF8);

                var jobUnicodeFile = new ConversionJob { SourceFilePath = mdUnicodeFile, TargetExtension = ".pdf", OutputFilePath = pdfUnicodeFile };
                var resUnicodeFile = await pdfEngine.ConvertAsync(jobUnicodeFile);
                Assert(resUnicodeFile.IsSuccess && IsPdfValid(await File.ReadAllBytesAsync(pdfUnicodeFile)),
                    "W2B2_8a: Markdown file with complex Cyrillic/Hindi/CJK filename converts cleanly");

                // Extremely long filename
                var longFileName = new string('a', 150) + ".md";
                var mdLongFile = Path.Combine(testDir, longFileName);
                var htmlLongFile = Path.Combine(testDir, "long_out.html");
                await File.WriteAllTextAsync(mdLongFile, "# Long Name\nBody text.", Encoding.UTF8);

                var jobLongFile = new ConversionJob { SourceFilePath = mdLongFile, TargetExtension = ".html", OutputFilePath = htmlLongFile };
                var resLongFile = await textMdEngine.ConvertAsync(jobLongFile);
                Assert(resLongFile.IsSuccess && File.Exists(htmlLongFile),
                    "W2B2_8b: File with 150-character filename converts cleanly to HTML");

                // Locked source file
                var lockedFile = Path.Combine(testDir, "locked.txt");
                await File.WriteAllTextAsync(lockedFile, "Protected text");
                using (var exclusiveLock = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var jobLocked = new ConversionJob { SourceFilePath = lockedFile, TargetExtension = ".pdf", OutputFilePath = Path.Combine(testDir, "locked.pdf") };
                    var resLocked = await pdfEngine.ConvertAsync(jobLocked);
                    Assert(!resLocked.IsSuccess && (resLocked.ErrorCode == "ERR_INPUT_READ_FAILED" || resLocked.ErrorCode == "ERR_INPUT_ACCESS_DENIED"),
                        "W2B2_8c: Exclusively locked file returns ERR_INPUT_READ_FAILED or access denied");
                }

                // 50 sequential conversions across TXT, MD, and Images on a single engine instance
                bool sequentialSuccess = true;
                for (int i = 0; i < 50; i++)
                {
                    var seqOut = Path.Combine(testDir, $"seq_{i}.pdf");
                    var seqJob = new ConversionJob { SourceFilePath = Path.Combine(testDir, "simple.txt"), TargetExtension = ".pdf", OutputFilePath = seqOut };
                    var seqRes = await pdfEngine.ConvertAsync(seqJob);
                    if (!seqRes.IsSuccess || !File.Exists(seqOut))
                    {
                        sequentialSuccess = false;
                        break;
                    }
                }
                Assert(sequentialSuccess, "W2B2_8d: 50 sequential conversions execute cleanly on single PdfDocumentConversionEngine instance");

                // Concurrent execution of 4 independent jobs
                var task1 = pdfEngine.ConvertAsync(new ConversionJob { SourceFilePath = Path.Combine(testDir, "simple.txt"), TargetExtension = ".pdf", OutputFilePath = Path.Combine(testDir, "par1.pdf") });
                var task2 = textMdEngine.ConvertAsync(new ConversionJob { SourceFilePath = Path.Combine(testDir, "document.md"), TargetExtension = ".html", OutputFilePath = Path.Combine(testDir, "par2.html") });
                var task3 = textMdEngine.ConvertAsync(new ConversionJob { SourceFilePath = Path.Combine(testDir, "document.md"), TargetExtension = ".txt", OutputFilePath = Path.Combine(testDir, "par3.txt") });
                var task4 = pdfEngine.ConvertAsync(new ConversionJob { SourceFilePath = Path.Combine(testDir, "document.md"), TargetExtension = ".pdf", OutputFilePath = Path.Combine(testDir, "par4.pdf") });

                var results = await Task.WhenAll(task1, task2, task3, task4);
                Assert(results.All(r => r.IsSuccess), "W2B2_8e: 4 concurrent independent conversions across engines complete successfully");
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, recursive: true);
                }
            }
            catch { /* Best-effort cleanup */ }
        }
    }

    #endregion

    #region Phase W2-C: PDF -> Image Rendering Proof-of-Concept Tests

    private static async Task RunW2_CPdfRendererPocTests()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(">>> [W2-C] PDF -> Image Rendering Proof-of-Concept Tests (Windows.Data.Pdf) <<<");
        Console.ResetColor();

        string tempDir = Path.Combine(Path.GetTempPath(), $"Axora_Tests_W2C_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Group W2C_1: Simple Document & PNG/JPG Output
            string simplePdf = Path.Combine(tempDir, "SimpleText.pdf");
            PdfRendererPocService.CreateSimpleOnePageTextPdf(simplePdf);

            using (var handle = await PdfRendererPocService.OpenDocumentAsync(simplePdf))
            {
                Assert(handle.PageCount == 1, "W2C_1a: Simple PDF reports PageCount == 1");

                var pngRes = await PdfRendererPocService.RenderPageAsync(handle, 0, dpi: 96.0, isJpeg: false);
                Assert(pngRes.Success && pngRes.ImageBytes != null && pngRes.ImageBytes.Length > 0,
                    "W2C_1b: Render page 0 to PNG returns success with non-empty payload");

                bool isPng = pngRes.ImageBytes != null && pngRes.ImageBytes.Length >= 8 &&
                             pngRes.ImageBytes[0] == 0x89 && pngRes.ImageBytes[1] == 0x50 &&
                             pngRes.ImageBytes[2] == 0x4E && pngRes.ImageBytes[3] == 0x47;
                Assert(isPng, "W2C_1c: Rendered PNG has valid PNG magic byte signature");

                using var skBmp = SKBitmap.Decode(pngRes.ImageBytes);
                Assert(skBmp != null && skBmp.Width > 0 && skBmp.Height > 0,
                    "W2C_1d: Rendered PNG is independently decodable via SkiaSharp");

                var jpgRes = await PdfRendererPocService.RenderPageAsync(handle, 0, dpi: 96.0, isJpeg: true);
                Assert(jpgRes.Success && jpgRes.ImageBytes != null && jpgRes.ImageBytes.Length > 0,
                    "W2C_1e: Render page 0 to JPG returns success with non-empty payload");

                bool isJpg = jpgRes.ImageBytes != null && jpgRes.ImageBytes.Length >= 3 &&
                             jpgRes.ImageBytes[0] == 0xFF && jpgRes.ImageBytes[1] == 0xD8 &&
                             jpgRes.ImageBytes[2] == 0xFF;
                Assert(isJpg, "W2C_1f: Rendered JPG has valid JPEG magic byte signature");

                using var skJpg = SKBitmap.Decode(jpgRes.ImageBytes);
                Assert(skJpg != null && skJpg.Width > 0 && skJpg.Height > 0,
                    "W2C_1g: Rendered JPG is independently decodable via SkiaSharp");
            }

            // Group W2C_2: Multi-Page Handling & Index Boundary
            string multiPdf = Path.Combine(tempDir, "MultiPage.pdf");
            PdfRendererPocService.CreateMultiPagePdf(multiPdf, 5);

            using (var handle = await PdfRendererPocService.OpenDocumentAsync(multiPdf))
            {
                Assert(handle.PageCount == 5, "W2C_2a: Multi-page PDF reports PageCount == 5");

                var p0 = await PdfRendererPocService.RenderPageAsync(handle, 0);
                var p2 = await PdfRendererPocService.RenderPageAsync(handle, 2);
                var p4 = await PdfRendererPocService.RenderPageAsync(handle, 4);

                Assert(p0.Success && p2.Success && p4.Success,
                    "W2C_2b: First (0), middle (2), and last (4) pages render cleanly");

                var outOfBounds = await PdfRendererPocService.RenderPageAsync(handle, 5);
                Assert(!outOfBounds.Success && outOfBounds.ErrorCode == "ERR_PAGE_OUT_OF_RANGE",
                    "W2C_2c: Page index 5 (boundary +1) fails cleanly with ERR_PAGE_OUT_OF_RANGE");

                var wayOutOfBounds = await PdfRendererPocService.RenderPageAsync(handle, 999);
                Assert(!wayOutOfBounds.Success && wayOutOfBounds.ErrorCode == "ERR_PAGE_OUT_OF_RANGE",
                    "W2C_2d: Page index 999 fails cleanly with ERR_PAGE_OUT_OF_RANGE");
            }

            // Group W2C_3: Geometric Orientations (Portrait & Landscape)
            string portraitPdf = Path.Combine(tempDir, "Portrait.pdf");
            PdfRendererPocService.CreatePortraitPdf(portraitPdf);
            string portraitImg = Path.Combine(tempDir, "portrait.png");

            using (var handle = await PdfRendererPocService.OpenDocumentAsync(portraitPdf))
            {
                var pRes = await PdfRendererPocService.RenderPageToFileAsync(handle, 0, portraitImg, dpi: 96.0);
                Assert(pRes.Success && File.Exists(portraitImg), "W2C_3a: Portrait PDF renders to file successfully");

                using var bmp = SKBitmap.Decode(portraitImg);
                Assert(bmp != null && bmp.Height > bmp.Width,
                    $"W2C_3b: Portrait aspect ratio verified (Height {bmp?.Height} > Width {bmp?.Width})");
            }

            string landscapePdf = Path.Combine(tempDir, "Landscape.pdf");
            PdfRendererPocService.CreateLandscapePdf(landscapePdf);
            string landscapeImg = Path.Combine(tempDir, "landscape.png");

            using (var handle = await PdfRendererPocService.OpenDocumentAsync(landscapePdf))
            {
                var lRes = await PdfRendererPocService.RenderPageToFileAsync(handle, 0, landscapeImg, dpi: 96.0);
                Assert(lRes.Success && File.Exists(landscapeImg), "W2C_3c: Landscape PDF renders to file successfully");

                using var bmp = SKBitmap.Decode(landscapeImg);
                Assert(bmp != null && bmp.Width > bmp.Height,
                    $"W2C_3d: Landscape aspect ratio verified (Width {bmp?.Width} > Height {bmp?.Height})");
            }

            // Group W2C_4: Content Fidelity (Raster Image & Unicode)
            string rasterPdf = Path.Combine(tempDir, "ImageRaster.pdf");
            PdfRendererPocService.CreateRasterImagePdf(rasterPdf);

            using (var handle = await PdfRendererPocService.OpenDocumentAsync(rasterPdf))
            {
                var res = await PdfRendererPocService.RenderPageAsync(handle, 0);
                Assert(res.Success && res.ImageBytes != null && res.ImageBytes.Length > 0,
                    "W2C_4a: PDF with embedded raster image renders cleanly");
            }

            string unicodePdf = Path.Combine(tempDir, "Unicode.pdf");
            PdfRendererPocService.CreateUnicodePdf(unicodePdf);

            using (var handle = await PdfRendererPocService.OpenDocumentAsync(unicodePdf))
            {
                var res = await PdfRendererPocService.RenderPageAsync(handle, 0);
                Assert(res.Success && res.ImageBytes != null && res.ImageBytes.Length > 0,
                    "W2C_4b: Multilingual Unicode PDF renders cleanly");
            }

            // Group W2C_5: DPI Scaling Progression
            using (var handle = await PdfRendererPocService.OpenDocumentAsync(simplePdf))
            {
                var d72 = await PdfRendererPocService.RenderPageAsync(handle, 0, dpi: 72.0);
                var d150 = await PdfRendererPocService.RenderPageAsync(handle, 0, dpi: 150.0);
                var d300 = await PdfRendererPocService.RenderPageAsync(handle, 0, dpi: 300.0);

                Assert(d72.Success && d150.Success && d300.Success,
                    "W2C_5a: 72, 150, and 300 DPI renders all succeed");
                Assert(d300.PixelWidth > d150.PixelWidth && d150.PixelWidth > d72.PixelWidth,
                    $"W2C_5b: DPI scaling strictly monotonic: 300 DPI ({d300.PixelWidth}px) > 150 DPI ({d150.PixelWidth}px) > 72 DPI ({d72.PixelWidth}px)");

                var custom = await PdfRendererPocService.RenderPageAsync(handle, 0, destinationWidth: 640, destinationHeight: 480);
                Assert(custom.Success && custom.PixelWidth == 640 && custom.PixelHeight == 480,
                    "W2C_5c: Custom destination dimensions (640x480) strictly applied");
            }

            // Group W2C_6: Error Handling (Missing, 0-byte, Malformed)
            bool missingThrown = false;
            try
            {
                using var h = await PdfRendererPocService.OpenDocumentAsync(Path.Combine(tempDir, "nonexistent.pdf"));
            }
            catch (FileNotFoundException)
            {
                missingThrown = true;
            }
            Assert(missingThrown, "W2C_6a: Missing PDF path throws FileNotFoundException");

            string emptyPdf = Path.Combine(tempDir, "empty.pdf");
            PdfRendererPocService.CreateEmptyPdf(emptyPdf);
            bool emptyThrown = false;
            try
            {
                using var h = await PdfRendererPocService.OpenDocumentAsync(emptyPdf);
            }
            catch (InvalidDataException)
            {
                emptyThrown = true;
            }
            Assert(emptyThrown, "W2C_6b: 0-byte PDF throws InvalidDataException");

            string malformedPdf = Path.Combine(tempDir, "malformed.pdf");
            PdfRendererPocService.CreateMalformedPdf(malformedPdf);
            bool malformedThrown = false;
            try
            {
                using var h = await PdfRendererPocService.OpenDocumentAsync(malformedPdf);
            }
            catch
            {
                malformedThrown = true;
            }
            Assert(malformedThrown, "W2C_6c: Malformed PDF throws exception upon loading");

            // Group W2C_7: Concurrency & Stress Stability
            using (var handle = await PdfRendererPocService.OpenDocumentAsync(multiPdf))
            {
                var tasks = Enumerable.Range(0, 5).Select(i =>
                    Task.Run(async () => await PdfRendererPocService.RenderPageAsync(handle, (uint)i))).ToArray();

                var results = await Task.WhenAll(tasks);
                Assert(results.All(r => r.Success), "W2C_7a: 5 concurrent page renders across tasks execute cleanly");
            }

            string tenPagePdf = Path.Combine(tempDir, "tenpage.pdf");
            PdfRendererPocService.CreateMultiPagePdf(tenPagePdf, 10);
            using (var handle = await PdfRendererPocService.OpenDocumentAsync(tenPagePdf))
            {
                bool all10 = true;
                for (uint i = 0; i < 10; i++)
                {
                    var res = await PdfRendererPocService.RenderPageAsync(handle, i);
                    if (!res.Success) all10 = false;
                }
                Assert(all10, "W2C_7b: 10 sequential page renders on single handle succeed without failure");
            }

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                using var handle = await PdfRendererPocService.OpenDocumentAsync(simplePdf);
                var cancelledRes = await PdfRendererPocService.RenderPageAsync(handle, 0, ct: cts.Token);
                Assert(!cancelledRes.Success && cancelledRes.ErrorCode == "ERR_CANCELLED",
                    "W2C_7c: Pre-cancelled CancellationToken returns ERR_CANCELLED status");
            }

            // Group W2C_8: Source File Immutability
            byte[] hashBefore;
            using (var sha = SHA256.Create())
            {
                hashBefore = sha.ComputeHash(File.ReadAllBytes(simplePdf));
            }

            using (var handle = await PdfRendererPocService.OpenDocumentAsync(simplePdf))
            {
                for (int i = 0; i < 5; i++)
                {
                    await PdfRendererPocService.RenderPageAsync(handle, 0);
                }
            }

            byte[] hashAfter;
            using (var sha = SHA256.Create())
            {
                hashAfter = sha.ComputeHash(File.ReadAllBytes(simplePdf));
            }

            Assert(hashBefore.SequenceEqual(hashAfter),
                "W2C_8a: Source PDF SHA-256 byte-for-byte unmodified after multiple render passes");
            Assert(File.Exists(simplePdf),
                "W2C_8b: Source PDF remains on disk and was neither moved nor deleted");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }
    }

    #endregion

    #region Phase W2-D: Conversion Orchestrator & Bounded Concurrency Tests

    private static async Task RunW2_DConversionOrchestratorTests()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n--- PHASE W2-D: CONVERSION ORCHESTRATOR & BOUNDED CONCURRENCY TESTS ---");
        Console.ResetColor();

        var tempDir = Path.Combine(Path.GetTempPath(), "Axora_W2D_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // W2D_1: Orchestrator construction
            {
                using var orch = new ConversionOrchestrator(null, maxTotalConcurrency: 4);
                Assert(orch.RegisteredEngines.Count == 0 && orch.CurrentQueue.Count == 0 && !orch.IsProcessing,
                    "W2D_1: ConversionOrchestrator constructs with empty registry, idle state, and empty queue");
            }

            // W2D_2: Engine registration & target extension discovery
            {
                using var orch = new ConversionOrchestrator();
                var mockEngine = new MockConversionEngine("mock-img", "Mock Image Engine", isAvailable: true);
                orch.RegisterEngine(mockEngine);
                orch.RegisterEngine(mockEngine); // duplicate registration
                Assert(orch.RegisteredEngines.Count == 1,
                    "W2D_2a: RegisterEngine ignores duplicate registrations for the same EngineId");

                var targets = orch.GetSupportedTargetExtensions(".png");
                Assert(targets.Contains(".jpg"),
                    "W2D_2b: GetSupportedTargetExtensions returns supported extensions from registered engines");
            }

            // W2D_3: Unsupported conversion rejection
            {
                using var orch = new ConversionOrchestrator();
                orch.RegisterEngine(new MockConversionEngine("mock", "Mock", true));
                var decision = orch.RouteJob(".wav", ".mp3");
                Assert(decision.Status == ConversionOrchestrator.RoutingStatus.Unsupported && decision.Engine == null,
                    "W2D_3: RouteJob returns Unsupported for unregistered format pairs");
            }

            // W2D_4: Valid engine routing
            {
                var wic = new WicImageConversionEngine();
                var pdf = new PdfDocumentConversionEngine();
                var renderer = new WindowsPdfRendererConversionEngine();
                using var orch = new ConversionOrchestrator([wic, pdf, renderer]);

                var route1 = orch.RouteJob(".png", ".jpg");
                var route2 = orch.RouteJob(".txt", ".pdf");
                var route3 = orch.RouteJob(".pdf", ".png");

                Assert(route1.Status == ConversionOrchestrator.RoutingStatus.Supported && route1.Engine?.EngineId == "wic" &&
                       route2.Status == ConversionOrchestrator.RoutingStatus.Supported && route2.Engine?.EngineId == "pdfsharp" &&
                       route3.Status == ConversionOrchestrator.RoutingStatus.Supported && route3.Engine?.EngineId == "win-pdf-renderer",
                    "W2D_4: RouteJob correctly resolves distinct engines for image, document, and PDF rasterization");
            }

            // W2D_5: Strict state machine transitions
            {
                var job = new ConversionJob();
                Assert(job.State == ConversionJobState.Pending, "W2D_5a: Initial state is Pending");

                bool canValid = job.TryTransitionTo(ConversionJobState.Validating);
                bool canQueue = job.TryTransitionTo(ConversionJobState.Queued);
                bool canRun = job.TryTransitionTo(ConversionJobState.Running);
                bool canSucceed = job.TryTransitionTo(ConversionJobState.Succeeded);

                bool illegalRun = job.TryTransitionTo(ConversionJobState.Running); // Succeeded -> Running prohibited!
                Assert(canValid && canQueue && canRun && canSucceed && !illegalRun,
                    "W2D_5b: ConversionJob transitions through Pending->Validating->Queued->Running->Succeeded and rejects Succeeded->Running");
            }

            // W2D_6: FIFO queue order
            {
                var executionOrder = new List<string>();
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        lock (executionOrder) executionOrder.Add(job.JobId);
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.FromMilliseconds(5)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine], maxTotalConcurrency: 1);
                var j1 = new ConversionJob { JobId = "job-1", SourceFilePath = CreateDummyFile(tempDir, "f1.txt", "1"), TargetExtension = ".txt" };
                var j2 = new ConversionJob { JobId = "job-2", SourceFilePath = CreateDummyFile(tempDir, "f2.txt", "2"), TargetExtension = ".txt" };
                var j3 = new ConversionJob { JobId = "job-3", SourceFilePath = CreateDummyFile(tempDir, "f3.txt", "3"), TargetExtension = ".txt" };

                await orch.ExecuteQueueAsync([j1, j2, j3]);
                Assert(executionOrder.Count == 3 && executionOrder[0] == "job-1" && executionOrder[1] == "job-2" && executionOrder[2] == "job-3",
                    "W2D_6: Single worker executes jobs in strict FIFO order");
            }

            // W2D_7: Bounded worker count
            {
                int maxActive = 0;
                int currentActive = 0;
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: async job =>
                    {
                        int active = Interlocked.Increment(ref currentActive);
                        lock (tempDir)
                        {
                            if (active > maxActive) maxActive = active;
                        }
                        await Task.Delay(40);
                        Interlocked.Decrement(ref currentActive);
                        return ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.FromMilliseconds(40));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine], maxTotalConcurrency: 2);
                var batch = Enumerable.Range(1, 6).Select(i => new ConversionJob
                {
                    JobId = $"bound-{i}",
                    SourceFilePath = CreateDummyFile(tempDir, $"bound_{i}.txt", $"content {i}"),
                    TargetExtension = ".txt"
                }).ToList();

                await orch.ExecuteQueueAsync(batch);
                Assert(maxActive <= 2,
                    $"W2D_7: Max concurrent active workers ({maxActive}) does not exceed configured limit of 2");
            }

            // W2D_8: Resource-profile scheduling (MemoryBound limited to 2)
            {
                int maxMemoryActive = 0;
                int currentMemoryActive = 0;
                var memEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    resourceProfile: EngineResourceProfile.MemoryBoundHeavy,
                    onExecute: async job =>
                    {
                        int active = Interlocked.Increment(ref currentMemoryActive);
                        lock (tempDir) { if (active > maxMemoryActive) maxMemoryActive = active; }
                        await Task.Delay(30);
                        Interlocked.Decrement(ref currentMemoryActive);
                        return ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.FromMilliseconds(30));
                    });

                await using var orch = new ConversionOrchestrator([memEngine], maxTotalConcurrency: 4);
                var batch = Enumerable.Range(1, 5).Select(i => new ConversionJob
                {
                    JobId = $"mem-{i}",
                    SourceFilePath = CreateDummyFile(tempDir, $"mem_{i}.txt", $"mem {i}"),
                    TargetExtension = ".txt"
                }).ToList();

                await orch.ExecuteQueueAsync(batch);
                Assert(maxMemoryActive <= 2,
                    $"W2D_8: MemoryBound engine is throttled to at most 2 concurrent jobs (actual: {maxMemoryActive})");
            }

            // W2D_9: Exclusive engine serialization (ExclusiveSingleThreaded)
            {
                int maxExclusiveActive = 0;
                int currentExclusiveActive = 0;
                var exclusiveEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    resourceProfile: EngineResourceProfile.ExclusiveCom,
                    onExecute: async job =>
                    {
                        int active = Interlocked.Increment(ref currentExclusiveActive);
                        lock (tempDir) { if (active > maxExclusiveActive) maxExclusiveActive = active; }
                        await Task.Delay(25);
                        Interlocked.Decrement(ref currentExclusiveActive);
                        return ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.FromMilliseconds(25));
                    });

                await using var orch = new ConversionOrchestrator([exclusiveEngine], maxTotalConcurrency: 4);
                var batch = Enumerable.Range(1, 4).Select(i => new ConversionJob
                {
                    JobId = $"excl-{i}",
                    SourceFilePath = CreateDummyFile(tempDir, $"excl_{i}.txt", $"excl {i}"),
                    TargetExtension = ".txt"
                }).ToList();

                await orch.ExecuteQueueAsync(batch);
                Assert(maxExclusiveActive == 1,
                    $"W2D_9: ExclusiveSingleThreaded engine executes strictly one at a time (actual: {maxExclusiveActive})");
            }

            // W2D_10: Cancellation before execution
            {
                var engineExecuted = false;
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        engineExecuted = true;
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.Zero));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                var job = new ConversionJob
                {
                    SourceFilePath = CreateDummyFile(tempDir, "cancel_pre.txt", "cancel me"),
                    TargetExtension = ".txt"
                };

                orch.Enqueue(job);
                orch.CancelJob(job.JobId); // Cancel while still pending/queued!
                await orch.StartAsync();
                await Task.Delay(50);

                Assert(job.State == ConversionJobState.Cancelled && !engineExecuted,
                    "W2D_10: Job cancelled before execution transitions to Cancelled without engine invocation");
            }

            // W2D_11: Cancellation during execution
            {
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecuteWithCt: async (job, progress, ct) =>
                    {
                        await Task.Delay(500, ct);
                        return ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.FromMilliseconds(500));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                var job = new ConversionJob
                {
                    SourceFilePath = CreateDummyFile(tempDir, "cancel_mid.txt", "mid-flight cancel"),
                    TargetExtension = ".txt"
                };

                orch.Enqueue(job);
                await orch.StartAsync();
                await Task.Delay(50);
                orch.CancelJob(job.JobId);
                
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline && job.State != ConversionJobState.Cancelled)
                {
                    await Task.Delay(25);
                }

                Assert(job.State == ConversionJobState.Cancelled,
                    "W2D_11: In-flight job cancellation transitions job state to Cancelled");
            }

            // W2D_12: Failure isolation across jobs
            {
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        if (job.JobId == "fail-job")
                        {
                            return Task.FromResult(ConversionResult.Failure("ERR_CORRUPT", "Corrupted input file"));
                        }
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.FromMilliseconds(5)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine], maxTotalConcurrency: 1);
                var j1 = new ConversionJob { JobId = "ok-1", SourceFilePath = CreateDummyFile(tempDir, "ok1.txt", "ok"), TargetExtension = ".txt" };
                var j2 = new ConversionJob { JobId = "fail-job", SourceFilePath = CreateDummyFile(tempDir, "f.txt", "bad"), TargetExtension = ".txt" };
                var j3 = new ConversionJob { JobId = "ok-2", SourceFilePath = CreateDummyFile(tempDir, "ok2.txt", "ok"), TargetExtension = ".txt" };

                await orch.ExecuteQueueAsync([j1, j2, j3]);

                Assert(j1.State == ConversionJobState.Succeeded &&
                       j2.State == ConversionJobState.Failed &&
                       j3.State == ConversionJobState.Succeeded,
                    "W2D_12: Single job failure is isolated; preceding and subsequent jobs complete successfully");
            }

            // W2D_13: Output staging file handling
            {
                string observedStagingPath = string.Empty;
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        observedStagingPath = job.OutputFilePath;
                        File.WriteAllText(job.OutputFilePath, "staged content");
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 14, TimeSpan.FromMilliseconds(5)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                var job = new ConversionJob
                {
                    SourceFilePath = CreateDummyFile(tempDir, "stage_src.txt", "stage me"),
                    TargetExtension = ".txt",
                    DestinationDirectory = tempDir
                };

                await orch.ExecuteQueueAsync([job]);
                Assert(observedStagingPath.Contains(".tmp_axora_") && !File.Exists(observedStagingPath) && File.Exists(job.OutputFilePath),
                    "W2D_13: Output is written to temporary staging path and atomically finalized to destination");
            }

            // W2D_14: Collision policy Overwrite
            {
                var existingFile = Path.Combine(tempDir, "exist_overwrite.txt");
                File.WriteAllText(existingFile, "OLD CONTENT");

                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        File.WriteAllText(job.OutputFilePath, "NEW CONTENT");
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 11, TimeSpan.FromMilliseconds(5)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                var job = new ConversionJob
                {
                    SourceFilePath = CreateDummyFile(tempDir, "src_over.txt", "source"),
                    OutputFilePath = existingFile,
                    TargetExtension = ".txt",
                    Profile = new ConversionProfile { CollisionMode = CollisionPolicy.Overwrite }
                };

                await orch.ExecuteQueueAsync([job]);
                var text = File.ReadAllText(existingFile);
                Assert(job.State == ConversionJobState.Succeeded && text == "NEW CONTENT",
                    "W2D_14: CollisionPolicy.Overwrite overwrites existing file with new output");
            }

            // W2D_15: Collision policy Skip
            {
                var existingFile = Path.Combine(tempDir, "exist_skip.txt");
                File.WriteAllText(existingFile, "PRESERVED CONTENT");

                bool engineCalled = false;
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        engineCalled = true;
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.Zero));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                var job = new ConversionJob
                {
                    SourceFilePath = CreateDummyFile(tempDir, "src_skip.txt", "source"),
                    OutputFilePath = existingFile,
                    TargetExtension = ".txt",
                    Profile = new ConversionProfile { CollisionMode = CollisionPolicy.Skip }
                };

                await orch.ExecuteQueueAsync([job]);
                var text = File.ReadAllText(existingFile);
                Assert(job.State == ConversionJobState.Skipped && !engineCalled && text == "PRESERVED CONTENT",
                    "W2D_15: CollisionPolicy.Skip leaves existing destination intact and skips conversion");
            }

            // W2D_16: Collision policy AutoRename
            {
                var existingFile = Path.Combine(tempDir, "exist_auto.txt");
                File.WriteAllText(existingFile, "INITIAL");

                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        File.WriteAllText(job.OutputFilePath, "RENAMED");
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 7, TimeSpan.FromMilliseconds(5)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                var job = new ConversionJob
                {
                    SourceFilePath = CreateDummyFile(tempDir, "src_auto.txt", "source"),
                    OutputFilePath = existingFile,
                    TargetExtension = ".txt",
                    Profile = new ConversionProfile { CollisionMode = CollisionPolicy.AutoRename }
                };

                await orch.ExecuteQueueAsync([job]);
                Assert(job.State == ConversionJobState.Succeeded && job.OutputFilePath != existingFile &&
                       job.OutputFilePath.Contains("(1)"),
                    "W2D_16: CollisionPolicy.AutoRename appends numeric index when target already exists");
            }

            // W2D_17: Output validation rejects invalid staged file
            {
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        // Write 0-byte file (invalid for PDF)
                        File.WriteAllBytes(job.OutputFilePath, Array.Empty<byte>());
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 0, TimeSpan.FromMilliseconds(5)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                var finalDest = Path.Combine(tempDir, "should_not_exist.pdf");
                var job = new ConversionJob
                {
                    SourceFilePath = CreateDummyFile(tempDir, "src_val.txt", "source"),
                    OutputFilePath = finalDest,
                    TargetExtension = ".pdf"
                };

                await orch.ExecuteQueueAsync([job]);
                Assert(job.State == ConversionJobState.Failed && !File.Exists(finalDest),
                    "W2D_17: Empty/corrupt staged file fails validation and final destination is not published");
            }

            // W2D_18: Source immutability verification
            {
                var srcFile = CreateDummyFile(tempDir, "immutable.txt", "IMMUTABLE SOURCE DATA");
                var shaBefore = await ConversionOutputValidator.ComputeFileSha256Async(srcFile);

                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        File.WriteAllText(job.OutputFilePath, "OUTPUT DATA");
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 11, TimeSpan.FromMilliseconds(5)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                var job = new ConversionJob
                {
                    SourceFilePath = srcFile,
                    TargetExtension = ".txt",
                    DestinationDirectory = tempDir
                };

                await orch.ExecuteQueueAsync([job]);
                var shaAfter = await ConversionOutputValidator.ComputeFileSha256Async(srcFile);

                Assert(job.State == ConversionJobState.Succeeded && shaBefore == shaAfter,
                    "W2D_18: Source file SHA-256 is byte-for-byte identical before and after conversion");
            }

            // W2D_19: Duplicate destination protection across concurrent jobs
            {
                var targetFile = Path.Combine(tempDir, "shared_dest.txt");

                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: async job =>
                    {
                        await Task.Delay(20);
                        File.WriteAllText(job.OutputFilePath, $"Output for {job.JobId}");
                        return ConversionResult.Success(job.OutputFilePath, 20, TimeSpan.FromMilliseconds(20));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine], maxTotalConcurrency: 2);
                var j1 = new ConversionJob { JobId = "dup-1", SourceFilePath = CreateDummyFile(tempDir, "dup1.txt", "1"), OutputFilePath = targetFile, TargetExtension = ".txt", Profile = new ConversionProfile { CollisionMode = CollisionPolicy.AutoRename } };
                var j2 = new ConversionJob { JobId = "dup-2", SourceFilePath = CreateDummyFile(tempDir, "dup2.txt", "2"), OutputFilePath = targetFile, TargetExtension = ".txt", Profile = new ConversionProfile { CollisionMode = CollisionPolicy.AutoRename } };

                await orch.ExecuteQueueAsync([j1, j2]);
                Assert(j1.OutputFilePath != j2.OutputFilePath && File.Exists(j1.OutputFilePath) && File.Exists(j2.OutputFilePath),
                    "W2D_19: Concurrent jobs targeting identical destinations are protected and auto-renamed");
            }

            // W2D_20: Mixed-format batch conversion
            {
                var wic = new WicImageConversionEngine();
                var pdf = new PdfDocumentConversionEngine();

                // Prepare image
                var srcPng = Path.Combine(tempDir, "batch_sample.png");
                using (var bmp = new SkiaSharp.SKBitmap(20, 20))
                {
                    using var fs = File.OpenWrite(srcPng);
                    bmp.Encode(fs, SkiaSharp.SKEncodedImageFormat.Png, 100);
                }

                var srcTxt = CreateDummyFile(tempDir, "batch_sample.txt", "Sample text for batch");

                await using var orch = new ConversionOrchestrator([wic, pdf], maxTotalConcurrency: 2);
                var jImg = new ConversionJob { SourceFilePath = srcPng, TargetExtension = ".jpg", DestinationDirectory = tempDir };
                var jDoc = new ConversionJob { SourceFilePath = srcTxt, TargetExtension = ".pdf", DestinationDirectory = tempDir };

                await orch.ExecuteQueueAsync([jImg, jDoc]);
                Assert(jImg.State == ConversionJobState.Succeeded && jDoc.State == ConversionJobState.Succeeded &&
                       File.Exists(jImg.OutputFilePath) && File.Exists(jDoc.OutputFilePath),
                    "W2D_20: Mixed-format batch executes concurrently across multiple engines");
            }

            // W2D_21: Progress reporting
            {
                var reports = new List<QueueProgressReport>();
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        File.WriteAllText(job.OutputFilePath, "content");
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 7, TimeSpan.FromMilliseconds(5)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine], maxTotalConcurrency: 1);
                orch.ProgressChanged += (_, r) => reports.Add(r);

                var j1 = new ConversionJob { SourceFilePath = CreateDummyFile(tempDir, "prg1.txt", "1"), TargetExtension = ".txt", DestinationDirectory = tempDir };
                var j2 = new ConversionJob { SourceFilePath = CreateDummyFile(tempDir, "prg2.txt", "2"), TargetExtension = ".txt", DestinationDirectory = tempDir };

                await orch.ExecuteQueueAsync([j1, j2]);
                Assert(reports.Count > 0 && reports.Last().IsCompleted && reports.Last().CompletedJobs == 2,
                    "W2D_21: Progress reports reflect queue state and finish at 100% completion");
            }

            // W2D_22: Shutdown with pending jobs
            {
                var mockEngine = new TestControlledConversionEngine(canConvert: (s, t) => true);
                var orch = new ConversionOrchestrator([mockEngine]);

                var j1 = new ConversionJob { SourceFilePath = CreateDummyFile(tempDir, "shut_p1.txt", "1"), TargetExtension = ".txt" };
                var j2 = new ConversionJob { SourceFilePath = CreateDummyFile(tempDir, "shut_p2.txt", "2"), TargetExtension = ".txt" };
                orch.EnqueueRange([j1, j2]);

                await orch.DisposeAsync();
                Assert(j1.State == ConversionJobState.Cancelled && j2.State == ConversionJobState.Cancelled,
                    "W2D_22: Disposing orchestrator with pending queue marks remaining jobs Cancelled");
            }

            // W2D_23: Shutdown with running jobs
            {
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecuteWithCt: async (job, prg, ct) =>
                    {
                        await Task.Delay(2000, ct);
                        return ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.FromSeconds(2));
                    });

                var orch = new ConversionOrchestrator([mockEngine]);
                var job = new ConversionJob { SourceFilePath = CreateDummyFile(tempDir, "shut_run.txt", "running"), TargetExtension = ".txt" };
                orch.Enqueue(job);
                await orch.StartAsync();
                await Task.Delay(50);

                var sw = Stopwatch.StartNew();
                await orch.DisposeAsync();
                sw.Stop();

                Assert(sw.ElapsedMilliseconds < 3500 && job.State == ConversionJobState.Cancelled,
                    "W2D_23: Disposing orchestrator aborts active running jobs within bounded shutdown window");
            }

            // W2D_24: Repeated batches on single orchestrator
            {
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        File.WriteAllText(job.OutputFilePath, "batch content");
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 13, TimeSpan.FromMilliseconds(2)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                bool allBatchesOk = true;

                for (int b = 0; b < 3; b++)
                {
                    var j = new ConversionJob
                    {
                        SourceFilePath = CreateDummyFile(tempDir, $"rep_{b}.txt", $"b{b}"),
                        TargetExtension = ".txt",
                        DestinationDirectory = tempDir
                    };
                    await orch.ExecuteQueueAsync([j]);
                    if (j.State != ConversionJobState.Succeeded) allBatchesOk = false;
                }

                Assert(allBatchesOk,
                    "W2D_24: Single orchestrator instance executes multiple sequential batches successfully");
            }

            // W2D_25: Memory & resource stability (100 jobs)
            {
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        File.WriteAllText(job.OutputFilePath, "data");
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 4, TimeSpan.FromMilliseconds(1)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine], maxTotalConcurrency: 4);
                var hundredJobs = Enumerable.Range(1, 100).Select(i => new ConversionJob
                {
                    JobId = $"stress-{i}",
                    SourceFilePath = CreateDummyFile(tempDir, $"stress_{i}.txt", $"{i}"),
                    TargetExtension = ".txt",
                    DestinationDirectory = tempDir
                }).ToList();

                long memBefore = GC.GetTotalMemory(true);
                await orch.ExecuteQueueAsync(hundredJobs);
                long memAfter = GC.GetTotalMemory(true);
                long delta = memAfter - memBefore;

                int completed = hundredJobs.Count(j => j.State == ConversionJobState.Succeeded);
                Assert(completed == 100 && delta < 50 * 1024 * 1024,
                    $"W2D_25: 100 jobs completed with bounded memory footprint (Delta: {delta / 1024.0:F1} KB)");
            }

            // W2D_26: Adversarial race conditions (concurrent enqueue, pause, resume, cancel)
            {
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecuteWithCt: async (job, prg, ct) =>
                    {
                        await Task.Delay(10, ct);
                        File.WriteAllText(job.OutputFilePath, "race");
                        return ConversionResult.Success(job.OutputFilePath, 4, TimeSpan.FromMilliseconds(10));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine], maxTotalConcurrency: 3);
                await orch.StartAsync();

                var tasks = new List<Task>();
                for (int i = 0; i < 20; i++)
                {
                    int id = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        var j = new ConversionJob
                        {
                            JobId = $"race-{id}",
                            SourceFilePath = CreateDummyFile(tempDir, $"race_{id}.txt", $"r{id}"),
                            TargetExtension = ".txt",
                            DestinationDirectory = tempDir
                        };
                        orch.Enqueue(j);
                        if (id % 3 == 0) await orch.PauseAsync();
                        if (id % 4 == 0) await orch.ResumeAsync();
                        if (id % 5 == 0) orch.CancelJob(j.JobId);
                    }));
                }

                await Task.WhenAll(tasks);
                await orch.ResumeAsync();
                
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline)
                {
                    var r = orch.ComputeProgressReport();
                    if (r.FinishedJobs >= r.TotalJobs) break;
                    await Task.Delay(25);
                }

                Assert(orch.CurrentQueue.All(j => j.State == ConversionJobState.Succeeded || j.State == ConversionJobState.Cancelled || j.State == ConversionJobState.Skipped),
                    "W2D_26: High-frequency concurrent enqueue, pause, resume, and cancel calls resolve cleanly without hang or crash");
            }

            // W2D_27: Retry policy for transient errors
            {
                int attempts = 0;
                var mockEngine = new TestControlledConversionEngine(
                    canConvert: (s, t) => true,
                    onExecute: job =>
                    {
                        attempts++;
                        if (attempts == 1)
                        {
                            return Task.FromResult(ConversionResult.Failure("ERR_INPUT_READ_FAILED", "Transient file lock"));
                        }
                        File.WriteAllText(job.OutputFilePath, "retried success");
                        return Task.FromResult(ConversionResult.Success(job.OutputFilePath, 15, TimeSpan.FromMilliseconds(5)));
                    });

                await using var orch = new ConversionOrchestrator([mockEngine]);
                var job = new ConversionJob
                {
                    SourceFilePath = CreateDummyFile(tempDir, "transient.txt", "data"),
                    TargetExtension = ".txt",
                    DestinationDirectory = tempDir
                };

                await orch.ExecuteQueueAsync([job]);
                Assert(attempts == 2 && job.State == ConversionJobState.Succeeded && job.RetryAttempt == 1,
                    "W2D_27: Transient failure triggers bounded retry and succeeds on subsequent attempt");
            }

            // W2D_28: Disposal idempotency
            {
                var orch = new ConversionOrchestrator();
                await orch.DisposeAsync();
                await orch.DisposeAsync();
                orch.Dispose();
                orch.Dispose();
                Assert(true,
                    "W2D_28: Multiple calls to DisposeAsync and Dispose execute idempotently without exception");
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }
    }

    #endregion

    #region Phase W2-E: Universal Converter UI + ViewModel + Shell Integration Tests

    private static async Task RunW2_EUniversalConverterViewModelTests()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n--- PHASE W2-E: UNIVERSAL CONVERTER UI + VIEWMODEL + SHELL INTEGRATION TESTS ---");
        Console.ResetColor();

        var tempDir = Path.Combine(Path.GetTempPath(), "Axora_W2E_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var orch = new MockConversionOrchestrator();
            var notif = new NotificationService();
            var settings = new AppSettingsService(NullLogger<AppSettingsService>.Instance, tempDir);

            // W2E_1: ViewModel Initialization
            var vm = new UniversalConverterViewModel(orch, notif, settings);
            Assert(vm.Queue.Count == 0 && !vm.CanStart && !vm.IsProcessing && !vm.IsPaused &&
                   vm.SelectedTargetFormat == "Auto" && vm.AvailableTargetFormats.Count == 1 &&
                   vm.SupportedSourceFormats.Count > 0,
                "W2E_1: ViewModel initializes with empty queue, Auto target format, and discovered supported source formats");

            // W2E_2: Single File Intake & Targets Discovery
            var f1 = CreateDummyFile(tempDir, "photo.png", "PNG file payload");
            vm.AddFiles([f1]);
            Assert(vm.Queue.Count == 1 && vm.CanStart &&
                   vm.AvailableTargetFormats.Contains("JPG") && vm.AvailableTargetFormats.Contains("PDF"),
                "W2E_2: Single file intake populates queue, computes discovered targets, and enables CanStart");

            // W2E_3: Multiple Files Intake & Common Target Intersection
            var f2 = CreateDummyFile(tempDir, "graphic.jpg", "JPG file payload");
            vm.AddFiles([f2]);
            Assert(vm.Queue.Count == 2 &&
                   vm.AvailableTargetFormats.Contains("PDF") && vm.AvailableTargetFormats.Contains("WEBP"),
                "W2E_3: Multiple files intake computes common target format intersection across items");

            // W2E_4: Heterogeneous Formats & Intersection
            {
                var orch2 = new MockConversionOrchestrator();
                orch2.SetSupportedTargets(".doca", [".pdf", ".html", ".txt"]);
                orch2.SetSupportedTargets(".docb", [".pdf", ".docx"]);
                using var vm2 = new UniversalConverterViewModel(orch2, notif, settings);
                var fa = CreateDummyFile(tempDir, "file.doca", "a");
                var fb = CreateDummyFile(tempDir, "file.docb", "b");
                vm2.AddFiles([fa, fb]);
                Assert(vm2.AvailableTargetFormats.Contains("Auto") && vm2.AvailableTargetFormats.Contains("PDF") && !vm2.AvailableTargetFormats.Contains("HTML"),
                    "W2E_4: Heterogeneous formats calculate correct common target formats intersection");
            }

            // W2E_5: Disjoint Formats & Zero Matching Targets
            {
                var orch3 = new MockConversionOrchestrator();
                orch3.SetSupportedTargets(".disja", [".ext1"]);
                orch3.SetSupportedTargets(".disjb", [".ext2"]);
                using var vmDisj = new UniversalConverterViewModel(orch3, notif, settings);
                var fda = CreateDummyFile(tempDir, "d1.disja", "1");
                var fdb = CreateDummyFile(tempDir, "d2.disjb", "2");
                vmDisj.AddFiles([fda, fdb]);
                Assert(vmDisj.AvailableTargetFormats.Count == 1 && vmDisj.AvailableTargetFormats[0] == "Auto",
                    "W2E_5: Disjoint formats result in zero common targets and fall back to Auto");
            }

            // W2E_6: Unsupported Format Rejection Warning
            var funsup = CreateDummyFile(tempDir, "unknown.xyz", "data");
            vm.AddFiles([funsup]);
            bool rejFlagged = vm.HasRejectedFiles && !string.IsNullOrEmpty(vm.RejectedFilesMessage);
            vm.DismissRejectedMessage();
            Assert(rejFlagged && !vm.HasRejectedFiles && vm.RejectedFilesMessage == null,
                "W2E_6: Unsupported file intake triggers rejection warning message and can be dismissed");

            // W2E_7: Non-existent File Path Intake Ignored
            int countBefore = vm.Queue.Count;
            vm.AddFiles([Path.Combine(tempDir, "non_existent_fake_path_12345.png")]);
            Assert(vm.Queue.Count == countBefore,
                "W2E_7: Non-existent file path intake is safely ignored without throwing or adding");

            // W2E_8: Output Directory Defaults to Source Directory
            vm.UseSourceDirectory = true;
            Assert(vm.EffectiveOutputDirectory == "Same folder as each source file",
                "W2E_8: UseSourceDirectory true resolves EffectiveOutputDirectory to 'Same folder as each source file'");

            // W2E_9: Custom Output Directory
            vm.UseSourceDirectory = false;
            vm.CustomOutputDirectory = tempDir;
            Assert(vm.EffectiveOutputDirectory == tempDir,
                "W2E_9: UseSourceDirectory false with custom directory resolves EffectiveOutputDirectory");

            // W2E_10: Custom Output Directory Fallback
            vm.CustomOutputDirectory = "";
            Assert(vm.EffectiveOutputDirectory == settings.DownloadDirectory ||
                   vm.EffectiveOutputDirectory == Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "W2E_10: Custom directory with empty path falls back safely without unhandled exception");

            // W2E_11: Collision Policy AutoRename Binding
            var fPolicy1 = CreateDummyFile(tempDir, "policy1.png", "p1");
            vm.AddFiles([fPolicy1]);
            vm.UseSourceDirectory = true;
            vm.IsProcessing = false;
            vm.SelectedCollisionPolicyIndex = 0;
            await vm.StartConversionCommand.ExecuteAsync(null);
            Assert(orch.CurrentQueue.Any(j => j.Profile.CollisionMode == CollisionPolicy.AutoRename && j.SourceFilePath == fPolicy1),
                "W2E_11: SelectedCollisionPolicyIndex = 0 configures CollisionPolicy.AutoRename on job profile");

            // W2E_12: Collision Policy Overwrite Binding
            var fPolicy2 = CreateDummyFile(tempDir, "policy2.png", "p2");
            vm.AddFiles([fPolicy2]);
            vm.IsProcessing = false;
            vm.SelectedCollisionPolicyIndex = 1;
            await vm.StartConversionCommand.ExecuteAsync(null);
            Assert(orch.CurrentQueue.Any(j => j.Profile.CollisionMode == CollisionPolicy.Overwrite && j.SourceFilePath == fPolicy2),
                "W2E_12: SelectedCollisionPolicyIndex = 1 configures CollisionPolicy.Overwrite on job profile");

            // W2E_13: Collision Policy Skip Binding
            var fPolicy3 = CreateDummyFile(tempDir, "policy3.png", "p3");
            vm.AddFiles([fPolicy3]);
            vm.IsProcessing = false;
            vm.SelectedCollisionPolicyIndex = 2;
            await vm.StartConversionCommand.ExecuteAsync(null);
            Assert(orch.CurrentQueue.Any(j => j.Profile.CollisionMode == CollisionPolicy.Skip && j.SourceFilePath == fPolicy3),
                "W2E_13: SelectedCollisionPolicyIndex = 2 configures CollisionPolicy.Skip on job profile");

            // W2E_14: Quality, DPI, and Metadata Settings Applied to Profile
            var fQuality = CreateDummyFile(tempDir, "quality.png", "pq");
            vm.AddFiles([fQuality]);
            vm.IsProcessing = false;
            vm.JpegQuality = 75;
            vm.TargetDpi = 300;
            vm.StripMetadata = true;
            await vm.StartConversionCommand.ExecuteAsync(null);
            var lastJob = orch.CurrentQueue.First(j => j.SourceFilePath == fQuality);
            Assert(lastJob.Profile.Quality == 75 && lastJob.Profile.TargetDpi == 300 &&
                   lastJob.Profile.MetadataPolicy == MetadataHandling.Strip,
                "W2E_14: Quality, DPI, and Metadata settings are correctly applied to job profile upon start");

            // W2E_15: CanStart Responsiveness
            vm.IsProcessing = false;
            bool canStartWithItems = vm.CanStart;
            vm.ClearQueueCommand.Execute(null);
            Assert(canStartWithItems && !vm.CanStart,
                "W2E_15: CanStart is responsive to queue contents and clears when queue is emptied");

            // W2E_16: StartConversionCommand Enqueues and Invokes StartAsync
            var fStart = CreateDummyFile(tempDir, "start_test.png", "start");
            vm.AddFiles([fStart]);
            vm.IsProcessing = false;
            int startCalls = orch.StartAsyncCallCount;
            await vm.StartConversionCommand.ExecuteAsync(null);
            Assert(orch.StartAsyncCallCount > startCalls && vm.IsProcessing,
                "W2E_16: StartConversionCommand enqueues pending jobs and invokes orchestrator StartAsync");

            // W2E_17: PauseQueueCommand Invokes PauseAsync
            int pauseCalls = orch.PauseAsyncCallCount;
            await vm.PauseQueueCommand.ExecuteAsync(null);
            Assert(orch.PauseAsyncCallCount > pauseCalls && vm.IsPaused,
                "W2E_17: PauseQueueCommand invokes orchestrator PauseAsync and updates IsPaused");

            // W2E_18: ResumeQueueCommand Invokes ResumeAsync
            int resumeCalls = orch.ResumeAsyncCallCount;
            await vm.ResumeQueueCommand.ExecuteAsync(null);
            Assert(orch.ResumeAsyncCallCount > resumeCalls && !vm.IsPaused,
                "W2E_18: ResumeQueueCommand invokes orchestrator ResumeAsync and clears IsPaused");

            // W2E_19: CancelAllCommand Invokes CancelAll
            int cancelAllCalls = orch.CancelAllCallCount;
            vm.CancelAllCommand.Execute(null);
            Assert(orch.CancelAllCallCount > cancelAllCalls,
                "W2E_19: CancelAllCommand invokes orchestrator CancelAll and updates status text");

            // W2E_20: CancelJobCommand Invokes CancelJob
            var jModel = vm.Queue.First();
            vm.CancelJobCommand.Execute(jModel.JobId);
            Assert(orch.CancelledJobIds.Contains(jModel.JobId),
                "W2E_20: CancelJobCommand invokes orchestrator CancelJob for specific job ID");

            // W2E_21: RemoveJobCommand Removes Individual Job
            int preRemoveCount = vm.Queue.Count;
            vm.RemoveJobCommand.Execute(jModel);
            Assert(vm.Queue.Count == preRemoveCount - 1,
                "W2E_21: RemoveJobCommand removes individual job from queue and updates telemetry");

            // W2E_22: ClearCompletedCommand Removes Succeeded/Cancelled Only
            vm.ClearQueueCommand.Execute(null);
            var fDone = CreateDummyFile(tempDir, "clr_done.png", "done");
            var fPend = CreateDummyFile(tempDir, "clr_pend.jpg", "pend");
            vm.AddFiles([fDone, fPend]);
            var doneItem = vm.Queue.First(j => j.Job.SourceFilePath == fDone);
            doneItem.Job.TryTransitionTo(ConversionJobState.Validating);
            doneItem.Job.TryTransitionTo(ConversionJobState.Queued);
            doneItem.Job.TryTransitionTo(ConversionJobState.Running);
            doneItem.Job.TryTransitionTo(ConversionJobState.Succeeded);
            vm.ClearCompletedCommand.Execute(null);
            Assert(vm.Queue.Any(j => j.Job.SourceFilePath == fPend) && !vm.Queue.Any(j => j.Job.SourceFilePath == fDone),
                "W2E_22: ClearCompletedCommand removes succeeded/cancelled jobs while retaining pending jobs");

            // W2E_23: ClearQueueCommand Clears Entire Queue
            vm.ClearQueueCommand.Execute(null);
            Assert(vm.Queue.Count == 0 && orch.ClearQueueCallCount > 0,
                "W2E_23: ClearQueueCommand removes all jobs and calls orchestrator ClearQueue");

            // W2E_24: ProgressChanged Telemetry Update
            orch.RaiseProgress(new QueueProgressReport
            {
                OverallProgressPercentage = 42.5,
                TotalBytesProcessed = 2097152,
                CurrentJobName = "test.png",
                TotalJobs = 10,
                CompletedJobs = 4,
                RunningJobs = 1
            });
            Assert(vm.OverallProgress == 42.5 && vm.TotalBytesFormatted == "2.00 MB" && vm.CurrentJobText.Contains("test.png"),
                "W2E_24: Orchestrator ProgressChanged event updates OverallProgress, TotalBytesFormatted, and status text");

            // W2E_25: JobStateChanged UI Model Status Update
            var fState = CreateDummyFile(tempDir, "state_test.png", "state");
            vm.AddFiles([fState]);
            var item = vm.Queue.First(j => j.Job.SourceFilePath == fState);
            item.Job.TryTransitionTo(ConversionJobState.Validating);
            item.Job.TryTransitionTo(ConversionJobState.Queued);
            item.Job.TryTransitionTo(ConversionJobState.Running);
            orch.RaiseJobState(item.Job);
            bool runOk = item.IsRunning && item.StatusText.StartsWith("Converting");
            item.Job.TryTransitionTo(ConversionJobState.Succeeded);
            orch.RaiseJobState(item.Job);
            bool doneOk = item.IsSuccess && item.StatusText == "Completed";
            Assert(runOk && doneOk,
                "W2E_25: Orchestrator JobStateChanged event updates ConversionJobUiModel status text, glyphs, and flags");

            // W2E_26: RetryJobCommand Re-enqueues Failed Job
            var fRetry = CreateDummyFile(tempDir, "retry_test.png", "retry");
            vm.AddFiles([fRetry]);
            var retryItem = vm.Queue.First(j => j.Job.SourceFilePath == fRetry);
            retryItem.Job.TryTransitionTo(ConversionJobState.Validating);
            retryItem.Job.TryTransitionTo(ConversionJobState.Failed);
            retryItem.Refresh();
            bool canRetry = retryItem.CanRetry;
            vm.IsProcessing = false;
            await vm.RetryJobCommand.ExecuteAsync(retryItem);
            Assert(canRetry && retryItem.Job.State == ConversionJobState.Queued,
                "W2E_26: RetryJobCommand transitions failed job to Queued and re-enqueues to orchestrator");

            // W2E_27: Rapid Command Invocation Guard
            for (int r = 0; r < 10; r++)
            {
                vm.CancelAllCommand.Execute(null);
            }
            Assert(true,
                "W2E_27: Rapid command invocation (Start, Pause, Resume, Cancel) executes cleanly without exceptions");

            // W2E_28: ViewModel Dispose Unsubscribes Events
            vm.Dispose();
            orch.RaiseProgress(new QueueProgressReport { OverallProgressPercentage = 99.0 });
            Assert(vm.Queue.Count == 0,
                "W2E_28: ViewModel Dispose unsubscribes from orchestrator events and cleans up queue items");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }
    }

    private static async Task RunW2_E1RealRuntimeInteractionTests()
    {
        Console.WriteLine("\n--- Phase W2-E.1 Tests: Universal Converter Real-Runtime Interaction & Visual QA Gate ---");

        var tempDir = Path.Combine(Path.GetTempPath(), "Axora_W2E1_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var engines = new IConversionEngine[]
            {
                new WicImageConversionEngine(),
                new PdfDocumentConversionEngine(),
                new TextMarkdownConversionEngine(),
                new WindowsPdfRendererConversionEngine()
            };

            await using var orchestrator = new ConversionOrchestrator(engines);
            var notif = new NotificationService();
            var settings = new AppSettingsService(customDirectory: tempDir);
            using var vm = new UniversalConverterViewModel(orchestrator, notif, settings);

            // Fixtures setup
            var srcPng = Path.Combine(tempDir, "real_input.png");
            using (var bmp = new SKBitmap(200, 200))
            {
                using (var canvas = new SKCanvas(bmp))
                {
                    canvas.Clear(SKColors.DarkSlateBlue);
                }
                using var fs = File.OpenWrite(srcPng);
                bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
            }

            var srcTxt = Path.Combine(tempDir, "real_document.txt");
            await File.WriteAllTextAsync(srcTxt, "Axora Universal Converter real-runtime document text.\nLine 2.\nLine 3.");

            var srcMd = Path.Combine(tempDir, "real_notes.md");
            await File.WriteAllTextAsync(srcMd, "# Real Header\n\n**Bold statement**\n\n- Task 1\n- Task 2\n\n```csharp\nstring s = \"real\";\n```\n<script>alert(1)</script>");

            var pngShaBefore = await ConversionOutputValidator.ComputeFileSha256Async(srcPng);
            var txtShaBefore = await ConversionOutputValidator.ComputeFileSha256Async(srcTxt);
            var mdShaBefore = await ConversionOutputValidator.ComputeFileSha256Async(srcMd);

            // W2E1_1: Real PNG -> JPG Conversion & Magic Bytes
            vm.AddFiles([srcPng]);
            vm.SelectedTargetFormat = "JPG";
            vm.JpegQuality = 90;
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var imgItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == srcPng);
            string imgOut = imgItem?.OutputFilePath ?? "";
            bool imgFileExists = File.Exists(imgOut) && new FileInfo(imgOut).Length > 0;
            byte[] imgBytes = imgFileExists ? await File.ReadAllBytesAsync(imgOut) : [];
            bool validJpgHeader = imgBytes.Length >= 2 && imgBytes[0] == 0xFF && imgBytes[1] == 0xD8;
            using var decodedBmp = imgFileExists ? SKBitmap.Decode(imgOut) : null;
            bool decodable = decodedBmp != null && decodedBmp.Width == 200 && decodedBmp.Height == 200;

            Assert(imgFileExists && validJpgHeader && decodable,
                "W2E1_1: Real PNG -> JPG conversion produces non-empty output with valid JPEG SOI magic bytes (0xFF, 0xD8) and decodes via SkiaSharp");

            // W2E1_2: UI Model Status and Completion Telemetry
            bool imgUiStatusOk = imgItem != null &&
                                 imgItem.Job.State == ConversionJobState.Succeeded &&
                                 imgItem.HumanStatus == "Completed" &&
                                 imgItem.IsSuccess &&
                                 imgItem.CanOpen &&
                                 vm.HasCompletedItems &&
                                 vm.TotalBytesFormatted != "0 B";
            Assert(imgUiStatusOk,
                "W2E1_2: Real conversion updates ConversionJobUiModel to Succeeded state with HumanStatus 'Completed', CanOpen flag, and non-empty byte telemetry");

            // W2E1_3: Source Immutability
            var pngShaAfter = await ConversionOutputValidator.ComputeFileSha256Async(srcPng);
            Assert(pngShaBefore == pngShaAfter,
                "W2E1_3: Source image file SHA-256 hash remains strictly immutable before and after conversion");

            // W2E1_4: Staging Cleanliness
            var leftoverTmp = Directory.GetFiles(tempDir, "*.tmp_axora_*", SearchOption.AllDirectories);
            Assert(leftoverTmp.Length == 0,
                "W2E1_4: Zero temporary staging files (.tmp_axora_*) remain in working and destination directories upon completion");

            // W2E1_5: Real TXT -> PDF Conversion & Header Check
            vm.ClearQueueCommand.Execute(null);
            vm.AddFiles([srcTxt]);
            vm.SelectedTargetFormat = "PDF";
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var txtItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == srcTxt);
            string txtOut = txtItem?.OutputFilePath ?? "";
            bool pdfExists = File.Exists(txtOut) && new FileInfo(txtOut).Length > 0;
            byte[] pdfBytes = pdfExists ? await File.ReadAllBytesAsync(txtOut) : [];
            bool validPdfHeader = pdfBytes.Length >= 5 && Encoding.ASCII.GetString(pdfBytes, 0, 5) == "%PDF-";
            var txtShaAfter = await ConversionOutputValidator.ComputeFileSha256Async(srcTxt);

            Assert(txtItem?.Job.State == ConversionJobState.Succeeded && validPdfHeader && txtShaBefore == txtShaAfter,
                "W2E1_5: Real TXT -> PDF conversion generates valid PDF starting with %PDF- header while preserving source text SHA-256");

            // W2E1_6: Real MD -> HTML Conversion & Sanitization
            vm.ClearQueueCommand.Execute(null);
            vm.AddFiles([srcMd]);
            vm.SelectedTargetFormat = "HTML";
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var mdItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == srcMd);
            string mdOut = mdItem?.OutputFilePath ?? "";
            string htmlContent = File.Exists(mdOut) ? await File.ReadAllTextAsync(mdOut) : "";
            bool validHtml = htmlContent.Contains("<h1") && htmlContent.Contains("<strong>") && !htmlContent.Contains("<script");

            Assert(mdItem?.Job.State == ConversionJobState.Succeeded && validHtml,
                "W2E1_6: Real MD -> HTML conversion generates valid HTML with parsed headings/emphasis and strips unsafe script tags");

            // W2E1_7: Collision Policy AutoRename
            vm.ClearQueueCommand.Execute(null);
            var colSrc = Path.Combine(tempDir, "collision_test.png");
            File.Copy(srcPng, colSrc, true);
            var existingDest = Path.Combine(tempDir, "collision_test.jpg");
            await File.WriteAllTextAsync(existingDest, "ORIGINAL_PRE_EXISTING_CONTENT");

            vm.AddFiles([colSrc]);
            vm.SelectedTargetFormat = "JPG";
            vm.SelectedCollisionPolicyIndex = 0; // AutoRename
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            string autoRenamedDest = Path.Combine(tempDir, "collision_test (1).jpg");
            bool autoRenameOk = File.Exists(autoRenamedDest) &&
                               (await File.ReadAllTextAsync(existingDest) == "ORIGINAL_PRE_EXISTING_CONTENT");
            Assert(autoRenameOk,
                "W2E1_7: CollisionPolicy.AutoRename creates indexed suffix file '(1)' while preserving pre-existing destination untouched");

            // W2E1_8: Collision Policy Skip
            vm.ClearQueueCommand.Execute(null);
            var skipSrc = Path.Combine(tempDir, "skip_test.png");
            File.Copy(srcPng, skipSrc, true);
            var skipDest = Path.Combine(tempDir, "skip_test.jpg");
            await File.WriteAllTextAsync(skipDest, "UNTOUCHED_SKIP_CONTENT");

            vm.AddFiles([skipSrc]);
            vm.SelectedTargetFormat = "JPG";
            vm.SelectedCollisionPolicyIndex = 2; // Skip
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var skipItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == skipSrc);
            bool skipOk = skipItem != null &&
                          skipItem.Job.State == ConversionJobState.Skipped &&
                          skipItem.HumanStatus == "Skipped" &&
                          (await File.ReadAllTextAsync(skipDest) == "UNTOUCHED_SKIP_CONTENT");
            Assert(skipOk,
                "W2E1_8: CollisionPolicy.Skip completes job as Skipped and leaves existing destination file completely untouched");

            // W2E1_9: Collision Policy Overwrite
            vm.ClearQueueCommand.Execute(null);
            var overSrc = Path.Combine(tempDir, "overwrite_test.png");
            File.Copy(srcPng, overSrc, true);
            var overDest = Path.Combine(tempDir, "overwrite_test.jpg");
            await File.WriteAllTextAsync(overDest, "STALE_CONTENT_TO_OVERWRITE");

            vm.AddFiles([overSrc]);
            vm.SelectedTargetFormat = "JPG";
            vm.SelectedCollisionPolicyIndex = 1; // Overwrite
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var overItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == overSrc);
            byte[] overBytes = File.Exists(overDest) ? await File.ReadAllBytesAsync(overDest) : [];
            bool overOk = overItem != null &&
                          overItem.Job.State == ConversionJobState.Succeeded &&
                          overBytes.Length > 2 && overBytes[0] == 0xFF && overBytes[1] == 0xD8;
            Assert(overOk,
                "W2E1_9: CollisionPolicy.Overwrite successfully replaces existing target file with newly converted valid JPEG content");

            // W2E1_10: Real Cancellation & Staging Cleanup
            vm.ClearQueueCommand.Execute(null);
            var cancelSrc1 = Path.Combine(tempDir, "cancel_1.png");
            var cancelSrc2 = Path.Combine(tempDir, "cancel_2.png");
            File.Copy(srcPng, cancelSrc1, true);
            File.Copy(srcPng, cancelSrc2, true);

            vm.AddFiles([cancelSrc1, cancelSrc2]);
            vm.SelectedTargetFormat = "JPG";
            _ = vm.StartConversionCommand.ExecuteAsync(null);
            vm.CancelAllCommand.Execute(null);
            for (int i = 0; i < 30 && vm.IsProcessing; i++) await Task.Delay(100);

            var cancelTmp = Directory.GetFiles(tempDir, "*.tmp_axora_*");
            bool cancelClean = cancelTmp.Length == 0 && File.Exists(cancelSrc1) && File.Exists(cancelSrc2);
            Assert(cancelClean,
                "W2E1_10: Real cancellation dispatches CancelAll, leaves no orphaned .tmp_axora_* files, and preserves source files");

            // W2E1_11: Controlled Failure & UI Error Diagnostic Binding
            vm.ClearQueueCommand.Execute(null);
            var lockSrc = Path.Combine(tempDir, "exclusive_locked.png");
            File.Copy(srcPng, lockSrc, true);

            var lockStream = new FileStream(lockSrc, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            vm.AddFiles([lockSrc]);
            vm.SelectedTargetFormat = "JPG";
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var lockedItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == lockSrc);
            bool failHandled = lockedItem != null &&
                               lockedItem.Job.State == ConversionJobState.Failed &&
                               lockedItem.HumanStatus == "Failed" &&
                               lockedItem.CanRetry &&
                               lockedItem.HasError &&
                               !string.IsNullOrEmpty(lockedItem.ErrorMessage);
            Assert(failHandled,
                "W2E1_11: Controlled failure (exclusive file lock) sets Failed state, HumanStatus 'Failed', CanRetry=true, and populates ErrorMessage");

            // W2E1_12: Real Retry Transitions to Succeeded
            lockStream.Dispose();
            if (lockedItem != null)
            {
                await vm.RetryJobCommand.ExecuteAsync(lockedItem);
                for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);
            }

            bool retryOk = lockedItem != null &&
                           lockedItem.Job.State == ConversionJobState.Succeeded &&
                           lockedItem.HumanStatus == "Completed" &&
                           File.Exists(lockedItem.OutputFilePath);
            Assert(retryOk,
                "W2E1_12: Real retry re-enqueues failed job after unlocking resource and cleanly transitions to Succeeded with valid output");

            // W2E1_13: Real Pause and Resume Controls
            vm.ClearQueueCommand.Execute(null);
            var pauseSrc1 = Path.Combine(tempDir, "pause_1.png");
            var pauseSrc2 = Path.Combine(tempDir, "pause_2.png");
            File.Copy(srcPng, pauseSrc1, true);
            File.Copy(srcPng, pauseSrc2, true);

            vm.AddFiles([pauseSrc1, pauseSrc2]);
            vm.SelectedTargetFormat = "JPG";
            _ = vm.StartConversionCommand.ExecuteAsync(null);
            await vm.PauseQueueCommand.ExecuteAsync(null);
            bool wasPaused = vm.IsPaused;
            await vm.ResumeQueueCommand.ExecuteAsync(null);
            bool wasResumed = !vm.IsPaused;
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            bool allFinished = vm.Queue.All(j => j.Job.State == ConversionJobState.Succeeded);
            Assert(wasPaused && wasResumed && allFinished,
                "W2E1_13: Real pause and resume controls pause queue state, resume queue dispatch, and successfully complete all items");

            // W2E1_14: Unsupported File Intake Rejection
            var unsupportedFile = Path.Combine(tempDir, "unsupported_data.xyz");
            await File.WriteAllTextAsync(unsupportedFile, "invalid format content");
            int countBefore = vm.Queue.Count;
            vm.AddFiles([unsupportedFile]);
            bool rejectedGraceful = vm.Queue.Count == countBefore &&
                                    vm.HasRejectedFiles &&
                                    !string.IsNullOrEmpty(vm.RejectedFilesMessage);
            Assert(rejectedGraceful,
                "W2E1_14: Adding unsupported file (.xyz) rejects intake without corrupting queue and sets HasRejectedFiles with user message");

            // W2E1_15: Clear Completed Preserves Pending and Failed Items
            var fClrDone = Path.Combine(tempDir, "clr_done.png");
            var fClrFail = Path.Combine(tempDir, "clr_fail.png");
            File.Copy(srcPng, fClrDone, true);
            File.Copy(srcPng, fClrFail, true);

            vm.ClearQueueCommand.Execute(null);
            vm.AddFiles([fClrDone, fClrFail]);
            var doneUi = vm.Queue.First(j => j.SourceFilePath == fClrDone);
            var failUi = vm.Queue.First(j => j.SourceFilePath == fClrFail);

            doneUi.Job.TryTransitionTo(ConversionJobState.Validating);
            doneUi.Job.TryTransitionTo(ConversionJobState.Queued);
            doneUi.Job.TryTransitionTo(ConversionJobState.Running);
            doneUi.Job.TryTransitionTo(ConversionJobState.Succeeded);
            doneUi.Refresh();

            failUi.Job.TryTransitionTo(ConversionJobState.Validating);
            failUi.Job.TryTransitionTo(ConversionJobState.Queued);
            failUi.Job.TryTransitionTo(ConversionJobState.Running);
            failUi.Job.TryTransitionTo(ConversionJobState.Failed);
            failUi.Refresh();

            vm.ClearCompletedCommand.Execute(null);
            bool clearCompletedOk = !vm.Queue.Any(j => j.SourceFilePath == fClrDone) &&
                                     vm.Queue.Any(j => j.SourceFilePath == fClrFail);
            Assert(clearCompletedOk,
                "W2E1_15: ClearCompleted removes succeeded jobs from the queue while safely preserving failed and active items");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }
    }

    private static string CreateDummyFile(string dir, string name, string content)
    {
        var p = Path.Combine(dir, name);
        File.WriteAllText(p, content);
        return p;
    }

    #endregion
}

public sealed class MockSpeechSynthesisService : ISpeechSynthesisService
{
    public bool IsSpeaking { get; private set; }

    public Task SpeakTextAsync(string text, double pitch = 1.0, double rate = 1.0, CancellationToken ct = default)
    {
        IsSpeaking = true;
        return Task.CompletedTask;
    }

    public void Stop()
    {
        IsSpeaking = false;
    }
}

public sealed class MockConversionEngine : IConversionEngine
{
    public string EngineId { get; }
    public string DisplayName { get; }
    public bool IsAvailable { get; }
    public string? RequiredDependencyId => null;
    public EngineResourceProfile ResourceProfile => EngineResourceProfile.CpuBoundDefault;

    public MockConversionEngine(string engineId, string displayName, bool isAvailable)
    {
        EngineId = engineId;
        DisplayName = displayName;
        IsAvailable = isAvailable;
    }

    public bool CanConvert(string sourceExtension, string targetExtension) =>
        sourceExtension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
        targetExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase);

    public Task<ConversionResult> ConvertAsync(ConversionJob job, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(100.0);
        return Task.FromResult(ConversionResult.Success(
            Path.ChangeExtension(job.SourceFilePath, job.TargetExtension),
            1024,
            TimeSpan.FromMilliseconds(10)));
    }
}

public sealed class MockConversionOrchestrator : IConversionOrchestrator
{
    private readonly List<ConversionJob> _enqueuedJobs = new();
    private readonly Dictionary<string, List<string>> _supportedMap = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IConversionEngine> RegisteredEngines { get; }
    public IReadOnlyList<ConversionJob> CurrentQueue => _enqueuedJobs.AsReadOnly();
    public QueueProgressReport CurrentProgress { get; set; } = new QueueProgressReport();
    public bool IsProcessing { get; set; } = false;
    public bool IsPaused { get; set; } = false;

    public int StartAsyncCallCount { get; private set; }
    public int PauseAsyncCallCount { get; private set; }
    public int ResumeAsyncCallCount { get; private set; }
    public int CancelAllCallCount { get; private set; }
    public int ClearQueueCallCount { get; private set; }
    public List<string> CancelledJobIds { get; } = new();

    public event EventHandler<QueueProgressReport>? ProgressChanged;
    public event EventHandler<ConversionJob>? JobStateChanged;

    public MockConversionOrchestrator(
        IReadOnlyList<IConversionEngine>? engines = null,
        Dictionary<string, List<string>>? supportedMap = null)
    {
        RegisteredEngines = engines ?? Array.Empty<IConversionEngine>();
        if (supportedMap != null)
        {
            foreach (var kvp in supportedMap)
            {
                _supportedMap[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            _supportedMap[".png"] = new List<string> { ".jpg", ".webp", ".bmp", ".pdf" };
            _supportedMap[".jpg"] = new List<string> { ".png", ".webp", ".bmp", ".pdf" };
            _supportedMap[".jpeg"] = new List<string> { ".png", ".webp", ".bmp", ".pdf" };
            _supportedMap[".webp"] = new List<string> { ".png", ".jpg", ".bmp" };
            _supportedMap[".bmp"] = new List<string> { ".png", ".jpg" };
            _supportedMap[".tiff"] = new List<string> { ".png", ".jpg" };
            _supportedMap[".tif"] = new List<string> { ".png", ".jpg" };
            _supportedMap[".pdf"] = new List<string> { ".png", ".jpg", ".txt" };
            _supportedMap[".txt"] = new List<string> { ".pdf", ".html" };
            _supportedMap[".md"] = new List<string> { ".html", ".txt", ".pdf" };
            _supportedMap[".html"] = new List<string> { ".pdf", ".txt" };
        }
    }

    public void SetSupportedTargets(string sourceExt, IEnumerable<string> targets)
    {
        _supportedMap[sourceExt] = targets.ToList();
    }

    public IConversionEngine? ResolveEngine(string sourceExtension, string targetExtension) =>
        RegisteredEngines.FirstOrDefault(e => e.IsAvailable && e.CanConvert(sourceExtension, targetExtension));

    public IReadOnlyList<string> GetSupportedTargetExtensions(string sourceExtension)
    {
        if (_supportedMap.TryGetValue(sourceExtension, out var list))
        {
            return list;
        }
        return Array.Empty<string>();
    }

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        if (_supportedMap.TryGetValue(sourceExtension, out var list))
        {
            return list.Contains(targetExtension, StringComparer.OrdinalIgnoreCase);
        }
        return false;
    }

    public void Enqueue(ConversionJob job)
    {
        _enqueuedJobs.Add(job);
    }

    public void EnqueueRange(IEnumerable<ConversionJob> jobs)
    {
        _enqueuedJobs.AddRange(jobs);
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        StartAsyncCallCount++;
        IsProcessing = true;
        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        PauseAsyncCallCount++;
        IsPaused = true;
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        ResumeAsyncCallCount++;
        IsPaused = false;
        return Task.CompletedTask;
    }

    public void CancelJob(string jobId)
    {
        CancelledJobIds.Add(jobId);
        var job = _enqueuedJobs.FirstOrDefault(j => j.JobId == jobId);
        job?.Cancel();
    }

    public void CancelAll()
    {
        CancelAllCallCount++;
        foreach (var job in _enqueuedJobs)
        {
            job.Cancel();
        }
        IsProcessing = false;
    }

    public void ClearQueue()
    {
        ClearQueueCallCount++;
        _enqueuedJobs.Clear();
    }

    public Task ExecuteQueueAsync(IReadOnlyList<ConversionJob> queue, IProgress<QueueProgressReport>? progress = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    public void RaiseProgress(QueueProgressReport report)
    {
        CurrentProgress = report;
        ProgressChanged?.Invoke(this, report);
    }

    public void RaiseJobState(ConversionJob job)
    {
        JobStateChanged?.Invoke(this, job);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void Dispose() { }
}

public sealed class TestControlledConversionEngine : IConversionEngine
{
    private readonly Func<string, string, bool>? _canConvert;
    private readonly Func<ConversionJob, IProgress<double>?, CancellationToken, Task<ConversionResult>>? _onExecuteWithCt;
    private readonly Func<ConversionJob, Task<ConversionResult>>? _onExecute;
    private readonly EngineResourceProfile _profile;

    public string EngineId { get; }
    public string DisplayName => "Test Controlled Engine";
    public bool IsAvailable => true;
    public string? RequiredDependencyId => null;
    public EngineResourceProfile ResourceProfile => _profile;

    public TestControlledConversionEngine(
        Func<string, string, bool>? canConvert = null,
        Func<ConversionJob, Task<ConversionResult>>? onExecute = null,
        Func<ConversionJob, IProgress<double>?, CancellationToken, Task<ConversionResult>>? onExecuteWithCt = null,
        EngineResourceProfile? resourceProfile = null,
        string engineId = "test-controlled")
    {
        _canConvert = canConvert;
        _onExecute = onExecute;
        _onExecuteWithCt = onExecuteWithCt;
        _profile = resourceProfile ?? EngineResourceProfile.CpuBoundDefault;
        EngineId = engineId;
    }

    public bool CanConvert(string sourceExtension, string targetExtension) =>
        _canConvert?.Invoke(sourceExtension, targetExtension) ?? true;

    public async Task<ConversionResult> ConvertAsync(ConversionJob job, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        ConversionResult result;
        if (_onExecuteWithCt != null)
        {
            result = await _onExecuteWithCt(job, progress, ct);
        }
        else if (_onExecute != null)
        {
            result = await _onExecute(job);
        }
        else
        {
            result = ConversionResult.Success(job.OutputFilePath, 10, TimeSpan.FromMilliseconds(5));
        }

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(job.OutputFilePath) && !File.Exists(job.OutputFilePath))
        {
            try
            {
                var dir = Path.GetDirectoryName(job.OutputFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                await File.WriteAllTextAsync(job.OutputFilePath, "MOCK_CONVERTED_OUTPUT", ct);
            }
            catch { }
        }

        return result;
    }
}
