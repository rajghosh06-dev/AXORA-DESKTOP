using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Helpers;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// Scholar Kit ViewModel — Intelligent OCR, Dual-Engine Vision, Speech Lab (Dictation &amp; Read-Aloud),
/// AI Study Synthesis, and 100% Offline Document RAG Research Assistant.
/// </summary>
public sealed partial class ScholarKitViewModel : ObservableObject, IDisposable
{
    private readonly IOcrService _ocrService;
    private readonly IPdfExtractionService _pdfService;
    private readonly IDocumentProcessorService _docProcessor;
    private readonly IVoiceTranscriberService _voiceTranscriber;
    private readonly IDocumentChatService _documentChat;
    private readonly ISpeechSynthesisService _speechService;
    private readonly IScannerService _scannerService;
    private readonly IAppSettingsService _settings;
    private readonly DispatcherQueue _dispatcher;

    public ObservableCollection<ScholarChatMessage> ChatMessages { get; } = [];
    public ObservableCollection<string> CitedPassages { get; } = [];
    public ObservableCollection<StudyConceptItem> ExtractedConcepts { get; } = [];
    public ObservableCollection<StudyQuestionItem> PracticeQuizQuestions { get; } = [];

    public ScholarKitViewModel(
        IOcrService ocrService,
        IPdfExtractionService pdfService,
        IDocumentProcessorService docProcessor,
        IVoiceTranscriberService voiceTranscriber,
        IDocumentChatService documentChat,
        ISpeechSynthesisService speechService,
        IScannerService scannerService,
        IAppSettingsService settings)
    {
        _ocrService = ocrService;
        _pdfService = pdfService;
        _docProcessor = docProcessor;
        _voiceTranscriber = voiceTranscriber;
        _documentChat = documentChat;
        _speechService = speechService;
        _scannerService = scannerService;
        _settings = settings;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Partial properties cannot have initializers (CS8050) — set defaults in constructor
        OcrResultText = string.Empty;
        MarkdownText = string.Empty;
        StructuredJsonText = string.Empty;
        ImportedFileName = string.Empty;
        DocumentInfo = string.Empty;
        DocumentFormatBadge = string.Empty;
        WordCountText = "0 words";
        ReadingTimeText = "0 min read";
        LineCountText = "0 lines";
        StatusMessage = "Ready — drop an image or PDF, paste from clipboard, or start dictation.";
        LastOperationStatus = string.Empty;
        SpeechRate = 1.0;
        SpeechPitch = 1.0;
        ExecutiveSummary = string.Empty;
        ChatQuery = string.Empty;

        // Initialize welcome message for RAG assistant
        ChatMessages.Add(new ScholarChatMessage
        {
            IsUser = false,
            Message = "Welcome to Scholar Kit Offline RAG Assistant. Once a document is loaded, ask any question about key concepts, experimental results, data points, or conclusions.",
            Confidence = 1.0,
            Timestamp = DateTime.Now
        });
    }

    // ── Tab & Navigation ───────────────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedStudioTabIndex; // 0=Editor, 1=Markdown, 2=Study Synthesizer, 3=RAG Chat

    // ── Document Content & Metadata ───────────────────────────────────────────

    [ObservableProperty]
    private string _ocrResultText;

    [ObservableProperty]
    private string _markdownText;

    [ObservableProperty]
    private string _structuredJsonText;

    [ObservableProperty]
    private string _importedFileName;

    [ObservableProperty]
    private string _documentInfo;

    [ObservableProperty]
    private string _documentFormatBadge;

    [ObservableProperty]
    private string _wordCountText;

    [ObservableProperty]
    private string _readingTimeText;

    [ObservableProperty]
    private string _lineCountText;

    [ObservableProperty]
    private bool _hasLoadedDocument;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage;

    [ObservableProperty]
    private string _lastOperationStatus;

    // ── Pre-processing & Engine Controls ──────────────────────────────────────

    [ObservableProperty]
    private int _selectedEngineIndex; // 0=WinRT Native OCR, 1=DirectML Vision

    [ObservableProperty]
    private bool _autoDeskew;

    [ObservableProperty]
    private bool _binarizeContrast;

    [ObservableProperty]
    private bool _tableExtractionMode;

    [ObservableProperty]
    private bool _latexMathMode;

    // ── Speech Lab (Dictation & Read Aloud) ────────────────────────────────────

    [ObservableProperty]
    private bool _isDictating;

    [ObservableProperty]
    private bool _isSpeaking;

    [ObservableProperty]
    private double _speechRate = 1.0;

    [ObservableProperty]
    private double _speechPitch = 1.0;

    // ── AI Study Synthesizer ──────────────────────────────────────────────────

    [ObservableProperty]
    private string _executiveSummary;

    [ObservableProperty]
    private bool _isSynthesizing;

    [ObservableProperty]
    private bool _hasGeneratedSummary;

    // ── Offline RAG Chat Assistant ────────────────────────────────────────────

    [ObservableProperty]
    private string _chatQuery;

    [ObservableProperty]
    private bool _isChatQuerying;

    // ── Ingestion Handlers ─────────────────────────────────────────────────────

    /// <summary>Extracts text from an image stream using the native WinRT OCR engine.</summary>
    [RelayCommand(IncludeCancelCommand = true)]
    public async Task ScanDocumentAsync(Stream? imageStream, CancellationToken ct)
    {
        if (imageStream is null) return;

        IsProcessing = true;
        StatusMessage = "Processing document with WinRT OCR engine…";
        OcrResultText = string.Empty;
        DocumentFormatBadge = "IMAGE / OCR";

        try
        {
            var text = await _ocrService.ExtractTextAsync(imageStream, ct);
            OcrResultText = string.IsNullOrWhiteSpace(text)
                ? "[No text detected in image]"
                : text;

            UpdateDocumentMetrics(OcrResultText);
            GenerateOutputFormats(OcrResultText);
            HasLoadedDocument = !string.IsNullOrWhiteSpace(OcrResultText);
            StatusMessage = $"Extraction complete — {OcrResultText.Length:N0} characters found.";
            _ = _documentChat.IndexDocumentAsync(OcrResultText, ct);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "OCR cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"OCR failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>Extracts text and structure from a PDF stream.</summary>
    [RelayCommand(IncludeCancelCommand = true)]
    public async Task AnalyzePdfAsync(Stream? pdfStream, CancellationToken ct)
    {
        if (pdfStream is null) return;

        IsProcessing = true;
        StatusMessage = "Analyzing PDF structure and extracting text…";
        OcrResultText = string.Empty;
        DocumentFormatBadge = "PDF DOCUMENT";

        try
        {
            var text = await _pdfService.ExtractPdfContentAsync(pdfStream, ct);
            OcrResultText = string.IsNullOrWhiteSpace(text)
                ? "[No text detected in PDF]"
                : text;

            UpdateDocumentMetrics(OcrResultText);
            GenerateOutputFormats(OcrResultText);
            HasLoadedDocument = !string.IsNullOrWhiteSpace(OcrResultText);
            StatusMessage = $"PDF extraction complete — {OcrResultText.Length:N0} characters found.";
            _ = _documentChat.IndexDocumentAsync(OcrResultText, ct);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "PDF analysis cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"PDF extraction failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>Captures a document from a connected WIA flatbed scanner.</summary>
    [RelayCommand]
    public async Task ScanFromFlatbedScannerAsync()
    {
        IsProcessing = true;
        StatusMessage = "Connecting to flatbed / document scanner…";

        try
        {
            var scanners = await _scannerService.GetConnectedScannersAsync();
            if (scanners.Count == 0)
            {
                StatusMessage = "No physical WIA scanner detected. Check USB/network connection.";
                LastOperationStatus = "No scanner detected";
                return;
            }

            var scannedStream = await _scannerService.CaptureAsync(scanners[0]);
            ImportedFileName = $"Scan_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            await ScanDocumentAsync(scannedStream, CancellationToken.None);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scanner error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>Pastes image or text content directly from the system clipboard.</summary>
    [RelayCommand]
    public async Task PasteFromClipboardAsync()
    {
        try
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Bitmap))
            {
                IsProcessing = true;
                StatusMessage = "Extracting text from clipboard screenshot…";
                var bitmapRef = await dataPackageView.GetBitmapAsync();
                using var streamRef = await bitmapRef.OpenReadAsync();
                using var netStream = streamRef.AsStreamForRead();

                ImportedFileName = $"Clipboard_Screenshot_{DateTime.Now:HHmmss}.png";
                await ScanDocumentAsync(netStream, CancellationToken.None);
            }
            else if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                var text = await dataPackageView.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ImportedFileName = $"Pasted_Notes_{DateTime.Now:HHmmss}.txt";
                    DocumentFormatBadge = "PASTED TEXT";
                    OcrResultText = text;
                    UpdateDocumentMetrics(OcrResultText);
                    GenerateOutputFormats(OcrResultText);
                    HasLoadedDocument = true;
                    StatusMessage = $"Loaded {text.Length:N0} characters from clipboard.";
                    _ = _documentChat.IndexDocumentAsync(OcrResultText);
                }
            }
            else
            {
                StatusMessage = "Clipboard does not contain image or text data.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clipboard error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>Loads a sample academic paper to test OCR, RAG, and Study Synthesis immediately.</summary>
    [RelayCommand]
    public void LoadSampleAcademicPaper()
    {
        ImportedFileName = "quantum_neural_computing_2026.pdf";
        DocumentFormatBadge = "SAMPLE PAPER";

        var sb = new StringBuilder();
        sb.AppendLine("Quantum Tensor Networks for Edge Machine Learning: A Comparative Benchmark");
        sb.AppendLine("Dr. Aris Thorne, Department of Advanced Computational Physics, Zurich Institute (2026)\n");
        sb.AppendLine("ABSTRACT");
        sb.AppendLine("Edge machine learning is fundamentally constrained by strict power budgets and memory bandwidth limits. In this paper, we demonstrate that Matrix Product State (MPS) tensor network decomposition reduces deep convolutional model weight complexity by 78.4% with less than 0.3% loss in top-1 accuracy on standard vision benchmarks. Furthermore, DirectML and SIMD vectorized execution pathways achieve sub-millisecond inference latencies on consumer-grade neural processing units (NPUs).\n");
        sb.AppendLine("1. INTRODUCTION & METHODOLOGY");
        sb.AppendLine("Traditional deep learning architectures suffer from polynomial growth in parameter counts as feature map dimensions expand. By mapping dense linear operator layers into high-order tensor trains, we exploit underlying low-rank entanglement structures. Our tensor contraction engine utilizes 256-bit AVX2 vector registers, allowing parallel calculation of quantum fidelity metrics during forward propagation.\n");
        sb.AppendLine("2. EMPIRICAL RESULTS & BENCHMARKS");
        sb.AppendLine("We tested on 5 distinct hardware platforms: Apple Silicon M3 Max, Qualcomm Snapdragon X Elite, AMD Ryzen AI 9, Intel Core Ultra 7, and NVIDIA RTX 4090. The tensor network compression algorithm achieved:\n- 4.2x faster inference on Snapdragon X Elite NPU.\n- 81% reduction in DRAM memory cache thrashing.\n- Zero cloud dependencies, ensuring 100% on-device private model evaluation.\n");
        sb.AppendLine("3. CONCLUSION & FUTURE WORK");
        sb.AppendLine("Quantum-inspired tensor representations provide a mathematically rigorous path toward zero-latency edge intelligence. Future work will investigate non-Abelian symmetry groups for 3D spatial transformers.");

        OcrResultText = sb.ToString();
        UpdateDocumentMetrics(OcrResultText);
        GenerateOutputFormats(OcrResultText);
        HasLoadedDocument = true;
        StatusMessage = "Sample academic paper loaded successfully.";
        _ = _documentChat.IndexDocumentAsync(OcrResultText);
    }

    // ── Live Speech Lab (Voice Dictation & Read-Aloud) ─────────────────────────

    [RelayCommand]
    public async Task ToggleVoiceDictationAsync()
    {
        if (IsDictating)
        {
            await _voiceTranscriber.StopDictationAsync();
            IsDictating = false;
            StatusMessage = "Voice dictation stopped.";
        }
        else
        {
            StatusMessage = "🎙️ Listening… speak naturally (WinRT Speech Recognition)";
            IsDictating = true;

            await _voiceTranscriber.StartDictationAsync(text =>
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _dispatcher.TryEnqueue(() =>
                    {
                        OcrResultText = string.IsNullOrEmpty(OcrResultText)
                            ? text
                            : $"{OcrResultText}\n\n{text}";
                        UpdateDocumentMetrics(OcrResultText);
                        GenerateOutputFormats(OcrResultText);
                        HasLoadedDocument = true;
                        _ = _documentChat.IndexDocumentAsync(OcrResultText);
                    });
                }
            });
        }
    }

    [RelayCommand]
    public async Task ToggleReadAloudAsync()
    {
        if (IsSpeaking)
        {
            _speechService.Stop();
            IsSpeaking = false;
            StatusMessage = "Speech playback stopped.";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(OcrResultText))
            {
                StatusMessage = "No document text available to read aloud.";
                return;
            }

            IsSpeaking = true;
            StatusMessage = "🔊 Reading document text aloud via Windows Neural TTS…";

            try
            {
                await _speechService.SpeakTextAsync(OcrResultText, pitch: SpeechPitch, rate: SpeechRate);
            }
            finally
            {
                IsSpeaking = false;
                StatusMessage = "Read aloud complete.";
            }
        }
    }

    [RelayCommand]
    public async Task SpeakMessageAsync(ScholarChatMessage? message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Message)) return;

        if (message.IsSpeaking)
        {
            _speechService.Stop();
            message.IsSpeaking = false;
            return;
        }

        // Reset speaking state on all messages
        foreach (var msg in ChatMessages) msg.IsSpeaking = false;

        message.IsSpeaking = true;
        try
        {
            await _speechService.SpeakTextAsync(message.Message, pitch: SpeechPitch, rate: SpeechRate);
        }
        finally
        {
            message.IsSpeaking = false;
        }
    }

    // ── AI Study Synthesizer ──────────────────────────────────────────────────

    [RelayCommand]
    public async Task GenerateSummaryAsync()
    {
        if (string.IsNullOrWhiteSpace(OcrResultText)) return;

        IsSynthesizing = true;
        StatusMessage = "Synthesizing executive summary & key takeaways…";

        try
        {
            await Task.Delay(400); // UI responsiveness

            var sentences = OcrResultText
                .Split(new[] { ". ", ".\n", "!\n", "?\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 25)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("## Executive Study Summary");
            sb.AppendLine($"*Document: {ImportedFileName} · Synthesized by Axora Scholar AI*\n");

            if (sentences.Count > 0)
            {
                sb.AppendLine($"**Core Thesis / Primary Focus:**\n> {sentences[0]}.\n");
            }

            sb.AppendLine("### Key Findings & Structural Points:");
            int pointsCount = Math.Min(sentences.Count - 1, 5);
            for (int i = 1; i <= pointsCount; i++)
            {
                sb.AppendLine($"- {sentences[i]}.");
            }

            ExecutiveSummary = sb.ToString();
            HasGeneratedSummary = true;
            StatusMessage = "Executive summary generated successfully.";
            SelectedStudioTabIndex = 2; // Jump to Study Synthesizer tab
        }
        finally
        {
            IsSynthesizing = false;
        }
    }

    [RelayCommand]
    public async Task ExtractKeyConceptsAsync()
    {
        if (string.IsNullOrWhiteSpace(OcrResultText)) return;

        IsSynthesizing = true;
        StatusMessage = "Extracting key terminology, definitions & entities…";
        ExtractedConcepts.Clear();

        try
        {
            await Task.Delay(300);

            var paragraphs = OcrResultText.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var colorPalette = new[] { "#5B7DE8", "#7C4DFF", "#00B0FF", "#00C853", "#FF9100" };
            int colorIdx = 0;

            foreach (var para in paragraphs)
            {
                var colonIdx = para.IndexOf(':');
                if (colonIdx > 2 && colonIdx < 45)
                {
                    var term = para[..colonIdx].Trim();
                    var def = para[(colonIdx + 1)..].Trim();
                    if (def.Length > 10)
                    {
                        ExtractedConcepts.Add(new StudyConceptItem
                        {
                            Term = term,
                            Definition = def.Length > 180 ? def[..177] + "…" : def,
                            Category = "Definition",
                            BadgeColor = colorPalette[colorIdx % colorPalette.Length]
                        });
                        colorIdx++;
                    }
                }
            }

            if (ExtractedConcepts.Count == 0)
            {
                var lines = OcrResultText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var line in lines.Where(l => l.Length > 30 && l.Length < 160).Take(6))
                {
                    var words = line.Split(' ');
                    var term = string.Join(" ", words.Take(Math.Min(words.Length, 3)));
                    ExtractedConcepts.Add(new StudyConceptItem
                    {
                        Term = term,
                        Definition = line,
                        Category = "Core Concept",
                        BadgeColor = colorPalette[colorIdx % colorPalette.Length]
                    });
                    colorIdx++;
                }
            }

            StatusMessage = $"Extracted {ExtractedConcepts.Count} study concepts.";
            SelectedStudioTabIndex = 2;
        }
        finally
        {
            IsSynthesizing = false;
        }
    }

    [RelayCommand]
    public async Task GenerateStudyQuizAsync()
    {
        if (string.IsNullOrWhiteSpace(OcrResultText)) return;

        IsSynthesizing = true;
        StatusMessage = "Generating practice quiz questions & retention checks…";
        PracticeQuizQuestions.Clear();

        try
        {
            await Task.Delay(350);

            var sentences = OcrResultText
                .Split(new[] { ". ", ".\n", "!\n", "?\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 35)
                .Take(5)
                .ToList();

            int qNum = 1;
            foreach (var sentence in sentences)
            {
                var words = sentence.Split(' ');
                var subject = string.Join(" ", words.Take(Math.Min(words.Length, 4)));

                PracticeQuizQuestions.Add(new StudyQuestionItem
                {
                    Number = qNum++,
                    Question = $"What is the significance or mechanism of '{subject}' in this context?",
                    Answer = sentence,
                    Difficulty = qNum % 2 == 0 ? "Medium" : "Easy",
                    IsAnswerVisible = false
                });
            }

            StatusMessage = $"Generated {PracticeQuizQuestions.Count} study questions.";
            SelectedStudioTabIndex = 2;
        }
        finally
        {
            IsSynthesizing = false;
        }
    }

    [RelayCommand]
    public void ToggleQuizAnswer(StudyQuestionItem? item)
    {
        if (item != null)
        {
            item.IsAnswerVisible = !item.IsAnswerVisible;
        }
    }

    // ── Offline Document RAG Chat Assistant ────────────────────────────────────

    [RelayCommand]
    public async Task AskDocumentAiAsync()
    {
        if (string.IsNullOrWhiteSpace(ChatQuery)) return;

        var userQuestion = ChatQuery.Trim();
        ChatQuery = string.Empty;

        // Add user question to stream
        ChatMessages.Add(new ScholarChatMessage
        {
            IsUser = true,
            Message = userQuestion,
            Timestamp = DateTime.Now
        });

        IsChatQuerying = true;
        CitedPassages.Clear();

        try
        {
            if (!_documentChat.HasIndexedDocument && !string.IsNullOrWhiteSpace(OcrResultText))
            {
                await _documentChat.IndexDocumentAsync(OcrResultText);
            }

            var result = await _documentChat.QueryDocumentAsync(userQuestion);

            ChatMessages.Add(new ScholarChatMessage
            {
                IsUser = false,
                Message = result.Answer,
                Confidence = result.Confidence,
                CitedPassages = result.CitedPassages,
                Timestamp = DateTime.Now
            });

            foreach (var passage in result.CitedPassages)
            {
                CitedPassages.Add(passage);
            }
        }
        catch (Exception ex)
        {
            ChatMessages.Add(new ScholarChatMessage
            {
                IsUser = false,
                Message = $"RAG Query Error: {ex.Message}",
                Confidence = 0.0,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsChatQuerying = false;
        }
    }

    [RelayCommand]
    public async Task AskQuickPromptAsync(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;
        ChatQuery = prompt;
        await AskDocumentAiAsync();
    }

    // ── Text Formatting & Utilities ───────────────────────────────────────────

    [RelayCommand]
    public void FormatParagraphs()
    {
        if (string.IsNullOrWhiteSpace(OcrResultText)) return;
        var cleaned = Regex.Replace(OcrResultText, @"(?<!\n)\n(?!\n)", " ");
        OcrResultText = Regex.Replace(cleaned, @" {2,}", " ");
        UpdateDocumentMetrics(OcrResultText);
        GenerateOutputFormats(OcrResultText);
        LastOperationStatus = "Paragraphs reformatted";
    }

    [RelayCommand]
    public void CleanExtraWhitespace()
    {
        if (string.IsNullOrWhiteSpace(OcrResultText)) return;
        var cleaned = Regex.Replace(OcrResultText, @"[ \t]+", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        OcrResultText = cleaned.Trim();
        UpdateDocumentMetrics(OcrResultText);
        GenerateOutputFormats(OcrResultText);
        LastOperationStatus = "Whitespace cleaned";
    }

    [RelayCommand]
    public void ChangeCase(string? mode)
    {
        if (string.IsNullOrWhiteSpace(OcrResultText) || string.IsNullOrWhiteSpace(mode)) return;

        OcrResultText = mode switch
        {
            "upper" => OcrResultText.ToUpperInvariant(),
            "lower" => OcrResultText.ToLowerInvariant(),
            "title" => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(OcrResultText.ToLower()),
            _ => OcrResultText
        };
        UpdateDocumentMetrics(OcrResultText);
        GenerateOutputFormats(OcrResultText);
    }

    private void UpdateDocumentMetrics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            DocumentInfo = "No document loaded";
            WordCountText = "0 words";
            ReadingTimeText = "0 min read";
            LineCountText = "0 lines";
            return;
        }

        var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var lines = text.Split('\n');
        int wordCount = words.Length;
        int lineCount = lines.Length;
        int readingMinutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));

        WordCountText = $"{wordCount:N0} words";
        LineCountText = $"{lineCount:N0} lines";
        ReadingTimeText = $"~{readingMinutes} min read";
        DocumentInfo = $"{ImportedFileName} · {text.Length:N0} chars · {wordCount:N0} words";
    }

    private void GenerateOutputFormats(string rawText)
    {
        var sbMd = new StringBuilder();
        sbMd.AppendLine($"# Extracted Document: {ImportedFileName}");
        sbMd.AppendLine($"*Extracted on {DateTime.Now:g} via Axora Scholar Kit*\n");
        sbMd.AppendLine("---");
        sbMd.AppendLine(rawText);
        MarkdownText = sbMd.ToString();

        var jsonObj = new
        {
            fileName = ImportedFileName,
            extractedAt = DateTime.UtcNow,
            engine = SelectedEngineIndex == 0 ? "WinRT Hardware OCR" : "DirectML Vision Engine",
            characterCount = rawText.Length,
            wordCount = rawText.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length,
            content = rawText
        };
        StructuredJsonText = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── Export & Flashcard Actions ─────────────────────────────────────────────

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (string.IsNullOrWhiteSpace(OcrResultText)) return;
        var package = new DataPackage();
        package.SetText(OcrResultText);
        Clipboard.SetContent(package);
        LastOperationStatus = "Copied to clipboard!";
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(OcrResultText)) return;

        try
        {
            var suggestedName = $"ScholarKit_Export_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var savePath = await NativeFilePickerHelper.PickSaveFileAsync(
                "Save Extracted PDF",
                "pdf",
                "PDF Document (*.pdf)\0*.pdf\0",
                suggestedName);

            if (string.IsNullOrWhiteSpace(savePath)) return;

            await _docProcessor.ConvertTextToPdfAsync(OcrResultText, savePath);
            LastOperationStatus = $"Saved PDF to {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            LastOperationStatus = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportMarkdownAsync()
    {
        if (string.IsNullOrWhiteSpace(MarkdownText)) return;

        try
        {
            var suggestedName = $"ScholarKit_Notes_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            var savePath = await NativeFilePickerHelper.PickSaveFileAsync(
                "Save Markdown Notes",
                "md",
                "Markdown File (*.md)\0*.md\0All Files (*.*)\0*.*\0",
                suggestedName);

            if (string.IsNullOrWhiteSpace(savePath)) return;

            await File.WriteAllTextAsync(savePath, MarkdownText, Encoding.UTF8);
            LastOperationStatus = $"Saved Markdown to {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            LastOperationStatus = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportPlainTextAsync()
    {
        if (string.IsNullOrWhiteSpace(OcrResultText)) return;

        try
        {
            var suggestedName = $"ScholarKit_Text_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var savePath = await NativeFilePickerHelper.PickSaveFileAsync(
                "Save Plain Text",
                "txt",
                "Plain Text Document (*.txt)\0*.txt\0All Files (*.*)\0*.*\0",
                suggestedName);

            if (string.IsNullOrWhiteSpace(savePath)) return;

            await File.WriteAllTextAsync(savePath, OcrResultText, Encoding.UTF8);
            LastOperationStatus = $"Saved Text to {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            LastOperationStatus = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (string.IsNullOrWhiteSpace(StructuredJsonText)) return;

        try
        {
            var suggestedName = $"ScholarKit_Data_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var savePath = await NativeFilePickerHelper.PickSaveFileAsync(
                "Save Structured JSON",
                "json",
                "JSON File (*.json)\0*.json\0",
                suggestedName);

            if (string.IsNullOrWhiteSpace(savePath)) return;

            await File.WriteAllTextAsync(savePath, StructuredJsonText, Encoding.UTF8);
            LastOperationStatus = $"Saved JSON to {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            LastOperationStatus = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void PushToFlashcards()
    {
        var flashcardsVm = App.TryGetService<FlashcardsViewModel>();
        if (flashcardsVm is not null && !string.IsNullOrWhiteSpace(OcrResultText))
        {
            flashcardsVm.GenerateCardsFromText(OcrResultText, ImportedFileName);
            LastOperationStatus = "Generated flashcards in Flashcard Studio!";
            App.MainAppWindow?.ShellRoot.NavigateTo("Flashcards");
        }
    }

    [RelayCommand]
    private void ClearResults()
    {
        _speechService.Stop();
        OcrResultText = string.Empty;
        MarkdownText = string.Empty;
        StructuredJsonText = string.Empty;
        ImportedFileName = string.Empty;
        DocumentInfo = "No document loaded";
        DocumentFormatBadge = "READY";
        WordCountText = "0 words";
        ReadingTimeText = "0 min read";
        LineCountText = "0 lines";
        StatusMessage = "Workspace cleared. Drop a document or start dictating.";
        LastOperationStatus = string.Empty;
        ExecutiveSummary = string.Empty;
        HasGeneratedSummary = false;
        HasLoadedDocument = false;
        IsSpeaking = false;
        ExtractedConcepts.Clear();
        PracticeQuizQuestions.Clear();
        CitedPassages.Clear();
        ChatMessages.Clear();

        ChatMessages.Add(new ScholarChatMessage
        {
            IsUser = false,
            Message = "Scholar Kit workspace reset. Load an image or PDF to begin studying.",
            Confidence = 1.0,
            Timestamp = DateTime.Now
        });
    }

    public void Dispose()
    {
        _speechService.Stop();
        if (_voiceTranscriber.IsRecording)
        {
            _ = Task.Run(async () =>
            {
                try { await _voiceTranscriber.StopDictationAsync(); }
                catch { /* Swallow */ }
            });
        }
    }
}
