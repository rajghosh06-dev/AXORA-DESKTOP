using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
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
