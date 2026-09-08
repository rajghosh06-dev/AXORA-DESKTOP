# AXORA Desktop WinUI — Phase W3-C Extraction Architecture
## Document Extraction Pipeline, Service Hierarchy & Capability Decoupling

**Document Version**: 1.1.0 (Phase W3-C Architecture Revision)  
**Target Platform**: Windows 11 (`net9.0-windows10.0.26100.0`) · C# 13 · Windows App SDK 1.6  
**Status**: **PLANNING ARCHITECTURE ONLY — ZERO IMPLEMENTATION COMMENCED**  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. End-to-End Extraction Pipeline Topology

The Phase W3-C document extraction pipeline is designed as a strict, unidirectional, stage-isolated pipeline. It decouples format sniffing, parser engine selection, PDF stream handling, OCR capability routing, two-tier text representation, and passage chunking from user interface state and downstream storage.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        USER DOCUMENT INGESTION SOURCE                       │
│  (Native FilePicker / Drag-and-Drop / Clipboard Stream / WIA Scanner)       │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Stream / FilePath (Read-Only)
┌──────────────────────────────────────▼──────────────────────────────────────┐
│                    STAGE 1: FORMAT DETECTION & SNIFFING                     │
│  IDocumentFormatDetector (Magic Byte Inspection, BOM, ZIP Structure, MIME)  │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ DetectedFormat + Encoding
┌──────────────────────────────────────▼──────────────────────────────────────┐
│               STAGE 2: EXTRACTION ORCHESTRATION & DISPATCH                  │
│  IScholarExtractionOrchestrator                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │ Selects Matching IDocumentExtractorEngine:                            │  │
│  │ ├─ IPdfDocumentExtractorEngine   (Digital Text + Decoupled OCR Engine)│  │
│  │ ├─ PlainTextExtractorEngine      (UTF-8, UTF-16, ASCII, Windows-1252) │  │
│  │ ├─ DocxDocumentExtractorEngine   (OpenXML WordProcessingML)           │  │
│  │ ├─ MarkdownExtractorEngine       (CommonMark / GFM Structure)         │  │
│  │ ├─ DelimitedTextExtractorEngine  (CSV / TSV Bounded Tabular Parser)   │  │
│  │ ├─ LocalHtmlExtractorEngine      (Local-Only DOM Stripper, Zero Net)  │  │
│  │ └─ ImageOcrExtractorEngine       (Single Image / Multi-Page TIFF)     │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Raw Page Collection (RawText Ground Truth)
┌──────────────────────────────────────▼──────────────────────────────────────┐
│                  STAGE 3: PAGE-AWARE STRUCTURAL MAPPING                     │
│  IDocumentPageBuilder (PageSemanticsType: PhysicalPage / LogicalSection /   │
│                        VirtualPage; Zero Fabricated Physical Numbers)       │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Sequence of DocumentPage
┌──────────────────────────────────────▼──────────────────────────────────────┐
│                 STAGE 4: TWO-TIER TEXT REPRESENTATION                       │
│  - RawText: Preserved Exact Source Codepoints (Immutable Ground Truth)      │
│  - NormalizedText: ITextNormalizer (Configurable TextNormalizationOptions)  │
│    (Lossless CRLF/Control Cleanup vs. Optional NFKC/Ligatures/Hyphens)      │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Normalized Page Text
┌──────────────────────────────────────▼──────────────────────────────────────┐
│                 STAGE 5: PAGE-BOUNDED PASSAGE CHUNKING                      │
│  IPassageChunker (Configurable ChunkingOptions: Target 350, Stride 60,      │
│                   Snap 40; Strict Non-Crossing Page Borders; Char Offsets)  │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Fully Materialized ScholarDocument
┌──────────────────────────────────────▼──────────────────────────────────────┐
│                    STAGE 6: LOCAL LIBRARY PERSISTENCE                       │
│  IScholarLibraryService (W3-B Staged Atomic JSON Storage under %APPDATA%)   │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. PDF Engine Boundary & Architecture Decoupling

### 2.1. Current Baseline Implementation
The existing WinUI codebase contains:
- `PdfExtractionService.cs`: Coordinates high-level extraction workflows.
- `PdfTextExtractor.cs`: Content stream parser wrapping `PdfSharpCore`.

### 2.2. Current Runtime & Package Limitations of PdfSharpCore
While `PdfSharpCore` provides a lightweight, pure-.NET mechanism for decoding basic PDF text objects (`Tj`, `TJ`), it exhibits notable architectural limitations:
1. **Complex Font CMaps**: Limited handling of custom `/ToUnicode` mapping tables, CID-keyed composite fonts (`Type0`), and non-standard font encodings, which can produce empty or scrambled character strings.
2. **Page Rasterization**: `PdfSharpCore` does **not** include a PDF page rasterizer/renderer. To dispatch scanned PDF pages to OCR, AXORA requires a mechanism to render PDF pages into raster bitmaps (`SoftwareBitmap` / `InMemoryRandomAccessStream`).
3. **Multi-Column Reading Order**: Stream order in PDF does not necessarily match visual reading order, requiring layout heuristics.

### 2.3. Architectural Decoupling Strategy
To prevent hard-coupling AXORA's long-term architecture to `PdfSharpCore`, Phase W3-C establishes an engine abstraction:

```csharp
public interface IPdfDocumentExtractorEngine : IDocumentExtractorEngine
{
    Task<PdfExtractionCapabilities> GetEngineCapabilitiesAsync(CancellationToken ct = default);
}

public sealed record PdfExtractionCapabilities
{
    public required string EngineName { get; init; }
    public required string EngineVersion { get; init; }
    public required bool SupportsDirectRasterization { get; init; }
    public required bool SupportsCustomFontCmaps { get; init; }
    public required bool SupportsRightToLeftScripts { get; init; }
}

public interface IPdfPageRasterizer
{
    bool CanRasterize { get; }
    Task<Stream> RasterizePageToPngStreamAsync(Stream pdfStream, int pageIndex, double targetDpi = 300.0, CancellationToken ct = default);
}
```

- **Baseline Engine**: `PdfSharpCore` serves as the initial baseline for digital text extraction.
- **Pluggable Evaluation**: The architecture permits swapping or augmenting with alternative PDF extraction and rasterization engines (e.g., `Windows.Data.Pdf.PdfDocument` via WinRT, PDFium native wrapper, or a dedicated local capability) if empirical validation demonstrates CMap or raster rendering deficiencies.

---

## 3. Two-Tier Text Representation: RawText vs. NormalizedText

To balance exact academic fidelity against searchable consistency, W3-C introduces an explicit two-tier text representation:

### 3.1. Representation Invariants
1. **`RawText` (Ground Truth)**:
   - Stores the exact characters, codepoints, formatting, and line structures emitted by the extraction engine.
   - Mathematical equations, chemical formulas, LaTeX sequences, code snippets, and exact punctuation are preserved without mutation.
   - Never modified by downstream normalizers or chunkers.
2. **`NormalizedText` (Derived Analysis Text)**:
   - Generated on demand or cached for search, passage chunking, and downstream index generation.
   - Governed by an explicit, configurable contract: `TextNormalizationOptions`.

### 3.2. Normalization Options Contract
```csharp
public sealed record TextNormalizationOptions
{
    // Tier A: Safe, Lossless Cleanup (Preserves all semantic meaning)
    public bool NormalizeLineEndings { get; init; } = true;         // CRLF / CR -> LF
    public bool StripNonPrintableControlChars { get; init; } = true; // ASCII 0x00-0x08, 0x0B-0x0C, 0x0E-0x1F (keeps 	, 
)
    public bool StripBOMAndZeroWidthChars { get; init; } = true;     // ﻿, ​-‍

    // Tier B: Formatting Adjustments (Configurable, may affect visual layout)
    public bool CollapseConsecutiveSpaces { get; init; } = true;     // Multiple spaces/tabs -> single space
    public bool PreserveParagraphBreaks { get; init; } = true;       // Standardize double-newlines for paragraphs

    // Tier C: Typography & Script Adjustments (Optional, may alter exact glyphs)
    public bool ApplyUnicodeNfkc { get; init; } = false;             // Canonical compatibility decomposition
    public bool UnfoldTypesettingLigatures { get; init; } = true;    // 'ﬁ' -> 'fi', 'ﬂ' -> 'fl'
    public bool RepairLinebreakHyphenation { get; init; } = false;   // Hyphen at newline repair (disabled for code/chemistry)
}

public interface ITextNormalizer
{
    string Normalize(string rawText, TextNormalizationOptions? options = null);
}
```

---

## 4. Page Semantics Contract: Physical, Logical & Virtual

Scholar Kit strictly differentiates between physical media pages and flow-based document structures. Fabricating physical page numbers where none exist is strictly prohibited.

### 4.1. Page Semantics Classification
```csharp
public enum PageSemanticsType
{
    /// <summary>
    /// Exact 1:1 physical page mapping from fixed-layout source (PDF, scanned paper, multi-frame TIFF).
    /// </summary>
    PhysicalPage,

    /// <summary>
    /// Semantic document division based on explicit author breaks (Word section breaks, explicit page breaks, Markdown headers, HTML article elements).
    /// </summary>
    LogicalSection,

    /// <summary>
    /// Virtual pagination computed by character/paragraph thresholds for unsegmented continuous streams (plain text without form-feeds).
    /// </summary>
    VirtualPage
}
```

### 4.2. Page Mapping Rules by Format
| Format | Page Semantics Type | Page Count Meaning | Empty Page Policy |
| :--- | :--- | :--- | :--- |
| **PDF** | `PhysicalPage` | Exact physical pages in PDF catalog (`1..N`). | Preserved with `RawText = string.Empty` to maintain strict 1:1 indexing. |
| **Scanned Image** | `PhysicalPage` | Exactly 1 page (`PageNumber = 1`). | Preserved. |
| **Multi-Frame TIFF** | `PhysicalPage` | 1:1 mapping with image directory frames (`1..FrameCount`). | Preserved. |
| **Word DOCX** | `LogicalSection` | Sections delimited by explicit page breaks (`<w:br w:type="page"/>` / `<w:lastRenderedPageBreak/>`) or section breaks (`<w:sectPr>`). Physical page numbers are NOT promised. | Empty sections preserved if explicitly broken. |
| **Markdown** | `LogicalSection` | Top-level structural headings (`# Heading 1`) or thematic breaks (`---`). | Empty sections omitted. |
| **Delimited (CSV/TSV)** | `LogicalSection` | Single table logical document (`PageNumber = 1`), with row/column bounds. | Empty tables preserved. |
| **Local HTML** | `LogicalSection` | `<article>`, `<section>`, or `<h1>` boundaries. | Empty sections omitted. |
| **Plain Text** | `VirtualPage` | Form-feed characters (`\f`) if present; otherwise virtual blocks of ~3,000 characters. | Omitted if whitespace only. |

---

## 5. Configurable Passage Chunking Contract

Passage chunking provides granular text windows for study sessions and future search. Chunk parameters are **not** hard-coded dogma; they are configurable defaults subject to empirical QA validation.

### 5.1. Chunking Contract
```csharp
public sealed record ChunkingOptions
{
    /// <summary>
    /// Target chunk size in characters. Proposed baseline default: 350.
    /// Subject to empirical QA validation across academic texts.
    /// </summary>
    public int TargetChunkSizeChars { get; init; } = 350;

    /// <summary>
    /// Overlap stride in characters between consecutive chunks. Proposed baseline default: 60.
    /// </summary>
    public int StrideOverlapChars { get; init; } = 60;

    /// <summary>
    /// Search window delta around target boundary to snap to nearest sentence end. Proposed default: 40.
    /// </summary>
    public int SentenceSnapBoundaryDelta { get; init; } = 40;

    /// <summary>
    /// Whether to attempt sentence boundary snapping (periods, question marks, exclamation marks).
    /// </summary>
    public bool SnapToSentenceBoundaries { get; init; } = true;
}

public interface IPassageChunker
{
    IReadOnlyList<DocumentPassageChunk> ChunkPage(
        string documentId,
        int pageNumber,
        string pageText,
        ChunkingOptions? options = null);
}
```

### 5.2. Hard Invariants for Passage Chunking
1. **Strict Page Boundary Isolation**: Chunks **MUST NEVER** cross page borders. A chunk starting on Page 1 must terminate on Page 1.
2. **Exact Offset Provenance**: Every chunk must store exact `StartCharOffset` and `EndCharOffset` mapping directly into the page's text stream, enabling reliable UI text highlighting.
3. **No Silent Text Loss**: The union of chunks across a page must cover all non-whitespace content.
4. **Deterministic & Reproducible**: Given identical text and `ChunkingOptions`, the chunker produces bit-for-bit identical chunk boundaries across runs.

---

## 6. Decoupled OCR Engine & Capability State Architecture

To satisfy AXORA's modular capability architecture, OCR is fully abstracted from Windows runtime APIs:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         OCR CAPABILITY ABSTRACTION                          │
├─────────────────────────────────────────────────────────────────────────────┤
│  IOcrCapabilityStateProvider                                                │
│  ├─ OcrCapabilityState CurrentState { get; }                                │
│  ├─ string? ActiveLanguageTag { get; }                                      │
│  ├─ IReadOnlyList<string> InstalledLanguages { get; }                       │
│  └─ Task<OcrCapabilityState> RefreshStateAsync(CancellationToken ct)        │
├─────────────────────────────────────────────────────────────────────────────┤
│  IOcrEngineProvider                                                         │
│  └─ Task<IOcrService?> ResolveBestEngineAsync(string? languageTag)          │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                ┌──────────────────────┴──────────────────────┐
                ▼                                             ▼
┌───────────────────────────────┐             ┌───────────────────────────────┐
│ SYSTEM-PROVIDED (Tier 1)      │             │ OPTIONAL LOCAL (Tier 2)       │
│ WinRtOcrEngineProvider        │             │ (Future Capability Manager)   │
│ - Windows.Media.Ocr.OcrEngine │             │ - Local ONNX / Tesseract      │
│ - Zero downloads              │             │ - Custom Language Packs       │
│ - OS Installed Language Packs │             │ - %APPDATA%\Axora\            │
│                               │             │   Capabilities\               │
└───────────────────────────────┘             └───────────────────────────────┘
```

### 6.1. Capability State Model
```csharp
public enum OcrCapabilityState
{
    /// <summary>
    /// OCR engine is installed, operational, and has matching language packs.
    /// </summary>
    OcrAvailable,

    /// <summary>
    /// No local OCR engine is installed or functional on the operating system.
    /// </summary>
    OcrUnavailable,

    /// <summary>
    /// OCR engine is present, but requested language pack (e.g. German, Japanese) is not installed.
    /// </summary>
    OcrLanguageUnavailable,

    /// <summary>
    /// OCR engine encountered an unrecoverable runtime or COM initialization error.
    /// </summary>
    OcrFailed,

    /// <summary>
    /// Multi-page batch OCR completed with some pages extracted and some pages degraded.
    /// </summary>
    OcrPartiallyCompleted
}
```

### 6.2. OCR Execution Governance
1. **Intelligent Skip**: If a PDF page contains >= 50 printable digital characters with valid font mappings, OCR is skipped entirely.
2. **Selective Page Dispatch**: In mixed PDFs, only pages lacking digital text streams trigger OCR.
3. **Reproducibility**: OCR output is certified as **reproducible within a fixed engine, language pack, and OS runtime configuration**.
4. **Isolated Memory Buffer**: Bitmap streams dispatched to OCR are wrapped in isolated memory streams. Caller-owned streams are never closed.
5. **Unmanaged Resource Governance**: Native `SoftwareBitmap` instances are explicitly wrapped in `using` declarations to prevent native heap leaks.

---

## 7. Bounded Format Extractors

All format extractors operate under strict local bounding:
1. **Plain Text (`PlainTextExtractorEngine`)**:
   - Supports UTF-8 (BOM and BOM-less), UTF-16LE, UTF-16BE, ASCII, and ANSI/Windows-1252.
   - Paginates virtually at form-feed characters (``) or configurable character limits (~3,000 characters).
2. **Delimited Text (`DelimitedTextExtractorEngine`)**:
   - Parses RFC 4180 CSV and TSV files with row and column bounds.
   - Formats tabular data into structured Markdown tables for clean rendering in Scholar Kit.
3. **Markdown (`MarkdownExtractorEngine`)**:
   - Parses CommonMark and GFM documents, preserving heading hierarchies, lists, and code blocks.
   - Strips or preserves formatting markers into logical document sections.
4. **Local HTML (`LocalHtmlExtractorEngine`)**:
   - **Strictly local-only**: Zero network access, zero remote stylesheet/image fetching, zero JavaScript execution.
   - Strips `<script>`, `<style>`, `<iframe>`, and event handlers.
   - Extracts clean document text from semantic HTML elements (`<article>`, `<main>`, `<p>`, headings).

---

## 8. Configurable Security & Resource Limits

To defend against pathological files and resource exhaustion while handling realistic academic workloads, all thresholds are defined as configurable options with safe defaults:

```csharp
public sealed record ExtractionSecurityOptions
{
    public long MaxFileSizeBytes { get; init; } = 250 * 1024 * 1024; // 250 MB
    public int MaxPagesToExtract { get; init; } = 1000;              // 1,000 pages
    public int MaxImageDimensionPx { get; init; } = 16384;           // 16K x 16K px
    public long MaxDocxUncompressedBytes { get; init; } = 500 * 1024 * 1024; // 500 MB
    public double MaxZipCompressionRatio { get; init; } = 100.0;     // 100:1 ratio limit
    public int MaxXmlDocumentChars { get; init; } = 50_000_000;      // 50M characters
    public TimeSpan ExtractionTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
```

---

## 9. Dependency Injection Registration Strategy

In `App.xaml.cs`, W3-C registers services into `Microsoft.Extensions.DependencyInjection`:

```csharp
// Format Sniffer, Normalizer & Chunker
services.AddSingleton<IDocumentFormatDetector, DocumentFormatDetector>();
services.AddSingleton<ITextNormalizer, TextNormalizer>();
services.AddSingleton<IPassageChunker, PassageChunker>();

// Extractor Engines (Decoupled Engine Strategy)
services.AddSingleton<IPdfDocumentExtractorEngine, PdfDocumentExtractorEngine>();
services.AddSingleton<IDocumentExtractorEngine>(sp => sp.GetRequiredService<IPdfDocumentExtractorEngine>());
services.AddSingleton<IDocumentExtractorEngine, PlainTextExtractorEngine>();
services.AddSingleton<IDocumentExtractorEngine, DocxDocumentExtractorEngine>();
services.AddSingleton<IDocumentExtractorEngine, MarkdownExtractorEngine>();
services.AddSingleton<IDocumentExtractorEngine, DelimitedTextExtractorEngine>();
services.AddSingleton<IDocumentExtractorEngine, LocalHtmlExtractorEngine>();
services.AddSingleton<IDocumentExtractorEngine, ImageOcrExtractorEngine>();

// OCR Abstraction Provider
services.AddSingleton<IOcrCapabilityStateProvider, WinRtOcrCapabilityStateProvider>();
services.AddSingleton<IOcrEngineProvider, OcrEngineProvider>();

// Primary Orchestration Facade
services.AddSingleton<IScholarExtractionOrchestrator, ScholarExtractionOrchestrator>();
```

---

## 10. Warning Gate & Build Integrity Invariant

- **Zero Build Errors**: Entire solution must compile cleanly.
- **Zero New Warnings Attributable to W3-C**: All new W3-C code, interfaces, models, and tests must produce zero new compiler (`CS*`) or analyzer warnings.
- **Isolated Baseline Warnings**: Existing baseline warnings (e.g. MVVMTK0045, CS0618) remain separately identified and cataloged; W3-C does not attempt a global warning cleanup.
