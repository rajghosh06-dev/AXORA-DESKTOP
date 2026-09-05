using System;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Axora.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        void Log(string msg) => System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        Log("Program.Main started.");

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Log($"[AppDomain] UnhandledException (IsTerminating={e.IsTerminating}): {e.ExceptionObject}");
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log($"[TaskScheduler] UnobservedTaskException: {e.Exception.Flatten()}");
            e.SetObserved();
        };

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Log("ComWrappersSupport initialized.");

            if (args != null && args.Any(a => a.Equals("--poc-pdf-render", StringComparison.OrdinalIgnoreCase)))
            {
                Log("Executing PDF Renderer POC diagnostic suite...");
                Console.WriteLine("================================================================================");
                Console.WriteLine("  AXORA WINUI 3 — NATIVE PDF RENDERING POC (Windows.Data.Pdf)");
                Console.WriteLine("================================================================================");

                try
                {
                    var report = System.Threading.Tasks.Task.Run(async () =>
                        await Services.PdfRendererPocService.RunPocDiagnosticSuiteAsync()).GetAwaiter().GetResult();

                    foreach (var finding in report.Findings)
                    {
                        Console.WriteLine($"  {finding}");
                        Log($"[POC] {finding}");
                    }

                    Console.WriteLine("================================================================================");
                    Console.WriteLine($"  POC RESULT: {report.PassedChecks}/{report.TotalChecks} PASSED (Failed: {report.FailedChecks})");
                    Console.WriteLine($"  Duration: {report.TotalDuration.TotalMilliseconds:F1}ms | Memory Delta: {report.MemoryDeltaBytes / 1024.0:F1} KB");
                    Console.WriteLine($"  Decision: {(report.IsApprovedForW2Core ? "APPROVED FOR W2 CORE" : "DEFERRED / FAILED")}");
                    Console.WriteLine("================================================================================");

                    Log($"PDF Renderer POC finished. Passed={report.PassedChecks}, Failed={report.FailedChecks}, Approved={report.IsApprovedForW2Core}");
                    Environment.ExitCode = report.FailedChecks == 0 ? 0 : 1;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FATAL POC ERROR] {ex}");
                    Log($"[FATAL POC ERROR] {ex}");
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args != null && args.Any(a => a.Equals("--run-orchestrator-e2e", StringComparison.OrdinalIgnoreCase)))
            {
                Log("Executing Conversion Orchestrator E2E diagnostic suite...");
                try
                {
                    bool pass = System.Threading.Tasks.Task.Run(async () =>
                        await RunOrchestratorE2EDiagnosticSuiteAsync()).GetAwaiter().GetResult();

                    Log($"Conversion Orchestrator E2E finished. Pass={pass}");
                    Environment.ExitCode = pass ? 0 : 1;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FATAL ORCHESTRATOR ERROR] {ex}");
                    Log($"[FATAL ORCHESTRATOR ERROR] {ex}");
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args != null && args.Any(a => a.Equals("--qa-converter-real-gate", StringComparison.OrdinalIgnoreCase)))
            {
                Log("Executing Universal Converter Real-Runtime QA Gate...");
                try
                {
                    bool pass = System.Threading.Tasks.Task.Run(async () =>
                        await RunUniversalConverterRealGateAsync()).GetAwaiter().GetResult();

                    Log($"Universal Converter Real-Runtime QA Gate finished. Pass={pass}");
                    Environment.ExitCode = pass ? 0 : 1;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FATAL CONVERTER QA ERROR] {ex}");
                    Log($"[FATAL CONVERTER QA ERROR] {ex}");
                    Environment.ExitCode = 1;
                }
                return;
            }

            Application.Start((p) =>
            {
                try
                {
                    Log("Application.Start callback invoked.");
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                    Log("App instance created successfully.");
                }
                catch (Exception appEx)
                {
                    Log($"Exception creating App: {appEx}");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            Log($"Application.Start exception: {ex}");
            throw;
        }
        finally
        {
            try
            {
                App.ShutdownAsync().GetAwaiter().GetResult();
                Log("Program.Main shutdown completed.");
            }
            catch (Exception ex)
            {
                Log($"Shutdown exception in Program.Main: {ex}");
            }
        }
    }

    private static async Task<bool> RunOrchestratorE2EDiagnosticSuiteAsync()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  AXORA WINUI 3 — UNIVERSAL CONVERTER ORCHESTRATOR RUNTIME E2E");
        Console.WriteLine("================================================================================");

        var tempDir = Path.Combine(Path.GetTempPath(), "Axora_Orch_E2E_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var engines = new Services.Contracts.IConversionEngine[]
            {
                new Services.WicImageConversionEngine(),
                new Services.PdfDocumentConversionEngine(),
                new Services.TextMarkdownConversionEngine(),
                new Services.WindowsPdfRendererConversionEngine()
            };

            await using var orchestrator = new Services.ConversionOrchestrator(engines);

            // 1. Prepare image file (100x100 PNG)
            var srcPng = Path.Combine(tempDir, "sample.png");
            using (var bmp = new SkiaSharp.SKBitmap(100, 100))
            {
                using (var canvas = new SkiaSharp.SKCanvas(bmp))
                {
                    canvas.Clear(SkiaSharp.SKColors.CornflowerBlue);
                }
                using var fs = File.OpenWrite(srcPng);
                bmp.Encode(fs, SkiaSharp.SKEncodedImageFormat.Png, 100);
            }

            // 2. Prepare text file
            var srcTxt = Path.Combine(tempDir, "sample.txt");
            await File.WriteAllTextAsync(srcTxt, "Hello from Axora Universal Converter Orchestrator.\nLine two of document.\nLine three.");

            // 3. Prepare PDF file
            var srcPdf = Path.Combine(tempDir, "sample_doc.pdf");
            using (var pdf = new PdfSharpCore.Pdf.PdfDocument())
            {
                var page = pdf.AddPage();
                using var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
                var font = new PdfSharpCore.Drawing.XFont("Arial", 14);
                gfx.DrawString("Orchestrator Live PDF Test", font, PdfSharpCore.Drawing.XBrushes.Black, 50, 50);
                pdf.Save(srcPdf);
            }

            // Hash check before
            var pngHashBefore = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcPng);
            var txtHashBefore = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcTxt);
            var pdfHashBefore = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcPdf);

            // Setup 4 real jobs:
            // Scenario 1: image -> image (PNG -> JPG)
            var job1 = new Models.ConversionJob
            {
                SourceFilePath = srcPng,
                TargetExtension = ".jpg",
                DestinationDirectory = tempDir
            };

            // Scenario 2: TXT -> PDF
            var job2 = new Models.ConversionJob
            {
                SourceFilePath = srcTxt,
                TargetExtension = ".pdf",
                DestinationDirectory = tempDir
            };

            // Scenario 3: PDF -> TXT
            var job3 = new Models.ConversionJob
            {
                SourceFilePath = srcPdf,
                TargetExtension = ".txt",
                DestinationDirectory = tempDir
            };

            // Scenario 4: PDF -> image (PDF -> PNG via W2-C approved native renderer)
            var job4 = new Models.ConversionJob
            {
                SourceFilePath = srcPdf,
                TargetExtension = ".png",
                DestinationDirectory = tempDir
            };

            var jobs = new[] { job1, job2, job3, job4 };

            Console.WriteLine("[1] Enqueueing 4 mixed conversion jobs to ConversionOrchestrator...");
            await orchestrator.ExecuteQueueAsync(jobs);

            // Validate results
            bool allSucceeded = true;
            for (int i = 0; i < jobs.Length; i++)
            {
                var j = jobs[i];
                bool ok = j.State == Models.ConversionJobState.Succeeded && File.Exists(j.OutputFilePath) && j.OutputSizeBytes > 0;
                Console.WriteLine($"  [Scenario {i + 1}] {Path.GetFileName(j.SourceFilePath)} -> {j.TargetExtension} : State={j.State}, Size={j.OutputSizeBytes}B, Output={Path.GetFileName(j.OutputFilePath)} [{(ok ? "PASS" : "FAIL")}]");
                if (!ok) allSucceeded = false;
            }

            // Immutability checks
            var pngHashAfter = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcPng);
            var txtHashAfter = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcTxt);
            var pdfHashAfter = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcPdf);

            bool immutabilityPreserved = (pngHashBefore == pngHashAfter) && (txtHashBefore == txtHashAfter) && (pdfHashBefore == pdfHashAfter);
            Console.WriteLine($"  [Immutability] Source files SHA-256 unchanged: {(immutabilityPreserved ? "PASS" : "FAIL")}");

            bool overallPass = allSucceeded && immutabilityPreserved;
            Console.WriteLine("================================================================================");
            Console.WriteLine($"  ORCHESTRATOR E2E RESULT: {(overallPass ? "ALL 4 SCENARIOS PASSED (100%)" : "FAILED")}");
            Console.WriteLine("================================================================================");

            return overallPass;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch { }
        }
    }

    private static async Task<bool> RunUniversalConverterRealGateAsync()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  AXORA WINUI 3 — UNIVERSAL CONVERTER REAL-RUNTIME INTERACTION QA GATE");
        Console.WriteLine("================================================================================");

        var tempDir = Path.Combine(Path.GetTempPath(), "Axora_Converter_Gate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var engines = new Services.Contracts.IConversionEngine[]
            {
                new Services.WicImageConversionEngine(),
                new Services.PdfDocumentConversionEngine(),
                new Services.TextMarkdownConversionEngine(),
                new Services.WindowsPdfRendererConversionEngine()
            };

            await using var orchestrator = new Services.ConversionOrchestrator(engines);
            var notif = new Services.NotificationService();
            var settings = new Services.AppSettingsService(customDirectory: tempDir);
            using var vm = new ViewModels.UniversalConverterViewModel(orchestrator, notif, settings);

            int passed = 0;
            int total = 0;
            void Check(string title, bool condition, string detail = "")
            {
                total++;
                if (condition)
                {
                    passed++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"  [PASS] {title}");
                    Console.ResetColor();
                    if (!string.IsNullOrEmpty(detail)) Console.WriteLine($" - {detail}"); else Console.WriteLine();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"  [FAIL] {title}");
                    Console.ResetColor();
                    if (!string.IsNullOrEmpty(detail)) Console.WriteLine($" - {detail}"); else Console.WriteLine();
                }
                Console.Out.Flush();
            }

            // 1. Prepare Fixtures
            var srcPng = Path.Combine(tempDir, "source_photo.png");
            using (var bmp = new SkiaSharp.SKBitmap(200, 200))
            {
                using (var canvas = new SkiaSharp.SKCanvas(bmp))
                {
                    canvas.Clear(SkiaSharp.SKColors.CornflowerBlue);
                }
                using var fs = File.OpenWrite(srcPng);
                bmp.Encode(fs, SkiaSharp.SKEncodedImageFormat.Png, 100);
            }

            var srcTxt = Path.Combine(tempDir, "notes.txt");
            await File.WriteAllTextAsync(srcTxt, "Axora Universal Converter real-runtime text test.\nSecond line.\nThird line.");

            var srcMd = Path.Combine(tempDir, "document.md");
            await File.WriteAllTextAsync(srcMd, "# Header\n\n**Bold text**\n\n- Item 1\n- Item 2\n\n```csharp\nint x = 42;\n```\n<script>alert('xss')</script>");

            var pngShaBefore = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcPng);
            var txtShaBefore = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcTxt);
            var mdShaBefore = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcMd);

            // ── SCENARIO 1: Real Image Conversion (PNG -> JPG) ──
            Console.WriteLine("\n[Scenario 1] Real Image Conversion (PNG -> JPG)...");
            vm.AddFiles([srcPng]);
            vm.SelectedTargetFormat = "JPG";
            vm.JpegQuality = 85;
            await vm.StartConversionCommand.ExecuteAsync(null);

            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var imgItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == srcPng);
            bool imgSucceeded = imgItem != null && imgItem.Job.State == Models.ConversionJobState.Succeeded && imgItem.HumanStatus == "Completed";
            Check("PNG -> JPG Job State and HumanStatus", imgSucceeded, $"State={imgItem?.Job.State}, Status={imgItem?.HumanStatus}");

            string imgOut = imgItem?.OutputFilePath ?? "";
            bool imgFileExists = File.Exists(imgOut) && new FileInfo(imgOut).Length > 0;
            Check("JPG Output File Created & Non-Empty", imgFileExists, $"Path={Path.GetFileName(imgOut)}, Size={imgItem?.FormattedOutputSize}");

            byte[] imgBytes = imgFileExists ? await File.ReadAllBytesAsync(imgOut) : [];
            bool validJpgHeader = imgBytes.Length >= 2 && imgBytes[0] == 0xFF && imgBytes[1] == 0xD8;
            Check("JPG Magic Bytes SOI (0xFF, 0xD8)", validJpgHeader);

            using (var decodedBmp = imgFileExists ? SkiaSharp.SKBitmap.Decode(imgOut) : null)
            {
                bool decodable = decodedBmp != null && decodedBmp.Width == 200 && decodedBmp.Height == 200;
                Check("JPG Independently Decodable via SkiaSharp (200x200)", decodable);
            }

            var pngShaAfter = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcPng);
            Check("Source PNG Immutability (SHA-256 Identical)", pngShaBefore == pngShaAfter);

            var leftoverTmp1 = Directory.GetFiles(tempDir, "*.tmp_axora_*");
            Check("Staging Cleanliness (0 Orphaned Temp Files)", leftoverTmp1.Length == 0, $"Found {leftoverTmp1.Length} leftovers");

            // ── SCENARIO 2: Real Document Conversion (TXT -> PDF) ──
            Console.WriteLine("\n[Scenario 2] Real Document Conversion (TXT -> PDF)...");
            vm.ClearQueueCommand.Execute(null);
            vm.AddFiles([srcTxt]);
            vm.SelectedTargetFormat = "PDF";
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var txtItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == srcTxt);
            bool txtSucceeded = txtItem != null && txtItem.Job.State == Models.ConversionJobState.Succeeded;
            string txtOut = txtItem?.OutputFilePath ?? "";
            bool pdfExists = File.Exists(txtOut) && new FileInfo(txtOut).Length > 0;
            byte[] pdfBytes = pdfExists ? await File.ReadAllBytesAsync(txtOut) : [];
            bool validPdfHeader = pdfBytes.Length >= 5 && Encoding.ASCII.GetString(pdfBytes, 0, 5) == "%PDF-";
            Check("TXT -> PDF Converted with Valid %PDF- Header", txtSucceeded && validPdfHeader, $"Path={Path.GetFileName(txtOut)}");

            var txtShaAfter = await Services.ConversionOutputValidator.ComputeFileSha256Async(srcTxt);
            Check("Source TXT Immutability (SHA-256 Identical)", txtShaBefore == txtShaAfter);

            // ── SCENARIO 3: Real Document Conversion (MD -> HTML) ──
            Console.WriteLine("\n[Scenario 3] Real Document Conversion (MD -> HTML)...");
            vm.ClearQueueCommand.Execute(null);
            vm.AddFiles([srcMd]);
            vm.SelectedTargetFormat = "HTML";
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var mdItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == srcMd);
            bool mdSucceeded = mdItem != null && mdItem.Job.State == Models.ConversionJobState.Succeeded;
            string mdOut = mdItem?.OutputFilePath ?? "";
            string htmlText = File.Exists(mdOut) ? await File.ReadAllTextAsync(mdOut) : "";
            bool validHtml = htmlText.Contains("<h1") && htmlText.Contains("<strong>") && !htmlText.Contains("<script");
            Check("MD -> HTML Converted & Sanitized (No Script Injection)", mdSucceeded && validHtml);

            // ── SCENARIO 4: Collision Policy AutoRename ──
            Console.WriteLine("\n[Scenario 4] Collision Policy: AutoRename...");
            vm.ClearQueueCommand.Execute(null);
            var colSrc = Path.Combine(tempDir, "collision_sample.png");
            File.Copy(srcPng, colSrc, true);
            var existingDest = Path.Combine(tempDir, "collision_sample.jpg");
            await File.WriteAllTextAsync(existingDest, "ORIGINAL_DATA_PRESERVED");

            vm.AddFiles([colSrc]);
            vm.SelectedTargetFormat = "JPG";
            vm.SelectedCollisionPolicyIndex = 0; // AutoRename
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            string autoRenamedTarget = Path.Combine(tempDir, "collision_sample (1).jpg");
            bool autoRenameOk = File.Exists(autoRenamedTarget) && (await File.ReadAllTextAsync(existingDest) == "ORIGINAL_DATA_PRESERVED");
            Check("AutoRename Created (1) Suffix & Kept Target Untouched", autoRenameOk, $"Renamed: {Path.GetFileName(autoRenamedTarget)}");

            // ── SCENARIO 5: Collision Policy Skip ──
            Console.WriteLine("\n[Scenario 5] Collision Policy: Skip...");
            vm.ClearQueueCommand.Execute(null);
            var skipSrc = Path.Combine(tempDir, "skip_sample.png");
            File.Copy(srcPng, skipSrc, true);
            var skipDest = Path.Combine(tempDir, "skip_sample.jpg");
            await File.WriteAllTextAsync(skipDest, "UNTOUCHED_TARGET");

            vm.AddFiles([skipSrc]);
            vm.SelectedTargetFormat = "JPG";
            vm.SelectedCollisionPolicyIndex = 2; // Skip
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var skipItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == skipSrc);
            bool skipOk = skipItem != null && skipItem.Job.State == Models.ConversionJobState.Skipped &&
                          skipItem.HumanStatus == "Skipped" && (await File.ReadAllTextAsync(skipDest) == "UNTOUCHED_TARGET");
            Check("Skip Policy Left Destination Untouched & Set Skipped State", skipOk);

            // ── SCENARIO 6: Collision Policy Overwrite ──
            Console.WriteLine("\n[Scenario 6] Collision Policy: Overwrite...");
            vm.ClearQueueCommand.Execute(null);
            var overSrc = Path.Combine(tempDir, "overwrite_sample.png");
            File.Copy(srcPng, overSrc, true);
            var overDest = Path.Combine(tempDir, "overwrite_sample.jpg");
            await File.WriteAllTextAsync(overDest, "STALE_DATA_TO_REPLACE");

            vm.AddFiles([overSrc]);
            vm.SelectedTargetFormat = "JPG";
            vm.SelectedCollisionPolicyIndex = 1; // Overwrite
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var overItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == overSrc);
            byte[] overBytes = File.Exists(overDest) ? await File.ReadAllBytesAsync(overDest) : [];
            bool overOk = overItem != null && overItem.Job.State == Models.ConversionJobState.Succeeded &&
                          overBytes.Length > 2 && overBytes[0] == 0xFF && overBytes[1] == 0xD8;
            Check("Overwrite Policy Replaced Target with Valid JPEG Output", overOk);

            // ── SCENARIO 7: Real Controlled Retry ──
            Console.WriteLine("\n[Scenario 7] Real Controlled Error & Retry...");
            vm.ClearQueueCommand.Execute(null);
            var lockSrc = Path.Combine(tempDir, "locked_image.png");
            File.Copy(srcPng, lockSrc, true);

            // Hold exclusive write/read lock
            var lockStream = new FileStream(lockSrc, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            vm.AddFiles([lockSrc]);
            vm.SelectedTargetFormat = "JPG";
            await vm.StartConversionCommand.ExecuteAsync(null);
            for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);

            var failedItem = vm.Queue.FirstOrDefault(j => j.SourceFilePath == lockSrc);
            bool failCaptured = failedItem != null && failedItem.Job.State == Models.ConversionJobState.Failed && failedItem.CanRetry;
            Check("Exclusive Lock Handled as Graceful Failed Job with CanRetry", failCaptured);

            // Unlock and retry
            lockStream.Dispose();
            if (failedItem != null)
            {
                await vm.RetryJobCommand.ExecuteAsync(failedItem);
                for (int i = 0; i < 40 && vm.IsProcessing; i++) await Task.Delay(100);
            }

            bool retrySucceeded = failedItem != null && failedItem.Job.State == Models.ConversionJobState.Succeeded && failedItem.HumanStatus == "Completed";
            Check("Retry Command Succeeded Once Lock Released", retrySucceeded);

            // ── SCENARIO 8: Unsupported File Rejection & Drag/Drop ──
            Console.WriteLine("\n[Scenario 8] File Intake & Drag/Drop Rejection...");
            var invalidFile = Path.Combine(tempDir, "unknown.xyz");
            await File.WriteAllTextAsync(invalidFile, "not a supported media or document");
            vm.AddFiles([invalidFile]);
            bool rejectedOk = vm.HasRejectedFiles && !string.IsNullOrEmpty(vm.RejectedFilesMessage) && !vm.Queue.Any(j => j.SourceFilePath == invalidFile);
            Check("Unsupported File (.xyz) Rejected with InfoBar Notification", rejectedOk);

            // ── SCENARIO 9: Final Staging Cleanliness ──
            var allLeftovers = Directory.GetFiles(tempDir, "*.tmp_axora_*", SearchOption.AllDirectories);
            Check("Final Staging Cleanliness: 0 Leftover .tmp_axora_* Files", allLeftovers.Length == 0);

            Console.WriteLine("================================================================================");
            Console.WriteLine($"  REAL-RUNTIME QA GATE SUMMARY: {passed}/{total} PASSED (Failed: {total - passed})");
            Console.WriteLine($"  DECISION: {(passed == total ? "GATE APPROVED (PASS)" : "GATE REJECTED (FAIL)")}");
            Console.WriteLine("================================================================================");
            Console.Out.Flush();

            return passed == total;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch { }
        }
    }
}
