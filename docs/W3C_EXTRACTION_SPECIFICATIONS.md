# AXORA Desktop WinUI — Phase W3-C Extraction Specifications
## Deep-Dive Specifications: OCR Boundaries, Page Semantics, Normalization, Chunking, Security & Failure Models

**Document Version**: 1.1.0 (Phase W3-C Technical Specifications Revision)  
**Target Platform**: Windows 11 (`net9.0-windows10.0.26100.0`) · C# 13 · Windows App SDK 1.6  
**Status**: **TECHNICAL PLANNING SPECIFICATION ONLY — ZERO IMPLEMENTATION COMMENCED**  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Specification 1: OCR Capability Boundary & State Model

OCR is classified strictly as an on-demand, modular capability. It must never be assumed to be unconditionally available or required for all documents.

### 1.1. OCR Capability State Machine
```
┌─────────────────┐
│ OcrNotRequired  │  (Default state for digital PDF, TXT, DOCX, CSV, MD)
└────────┬────────┘
         │ Document requires OCR (Scanned PDF or Image)
         ▼
┌─────────────────┐
│  OcrAvailable   │  (Matching OCR Engine & Language Pack detected)
└────────┬────────┘
         │
         ├── Missing OS Pack ────────> ┌────────────────────────┐
         │                             │ OcrLanguageUnavailable │ (Displays language pack prompt)
         │                             └────────────────────────┘
         ├── Missing Engine ─────────> ┌────────────────────────┐
         │                             │     OcrUnavailable     │ (Displays OCR engine prompt)
         │                             └────────────────────────┘
         │ Engine starts processing
         ▼
┌─────────────────┐
│  OcrProcessing  │  (Live progress reported via IProgress<double>)
└────────┬────────┘
         │
         ├── User clicks Cancel ─────> ┌──────────────────┐
         │                             │   OcrCancelled   │ (Clean teardown; no resource leaks)
         │                             └──────────────────┘
         ├── All pages succeed ──────> ┌──────────────────┐
         │                             │   OcrSucceeded   │ (Reproducible extracted text)
         │                             └──────────────────┘
         ├── Some pages fail ────────> ┌───────────────────────┐
         │                             │ OcrPartiallyCompleted │ (Per-page diagnostic notices)
         │                             └───────────────────────┘
         └── Fatal runtime error ────> ┌──────────────────┐
                                       │    OcrFailed     │ (Non-crashing error state)
                                       └──────────────────┘
```

### 1.2. Decoupled Interface Contract
```csharp
public interface IOcrEngine
{
    string EngineId { get; }
    Task<OcrResult> RecognizeImageAsync(Stream imageStream, string? languageTag = null, CancellationToken ct = default);
}

public interface IOcrCapabilityStateProvider
{
    OcrCapabilityState State { get; }
    string? ActiveLanguageTag { get; }
    IReadOnlyList<string> InstalledLanguages { get; }
    Task<OcrCapabilityState> RefreshStateAsync(CancellationToken ct = default);
}
```

### 1.3. Mixed-Content Detection Heuristic
For multi-page PDF documents, each page is evaluated independently:
1. **Digital Character Density**: The page content stream is decoded via `IPdfDocumentExtractorEngine`. If `characterCount >= 50` printable non-whitespace characters, the page is classified as **Digital**.
2. **Raster Scan Detection**: If `characterCount < 50`, the page resources are inspected for image XObjects (`/Subtype /Image`). If one or more raster images exist, the page is classified as **Scanned**.
3. **Selective Dispatch**: In a 100-page document where pages 1–90 are digital and pages 91–100 are scanned:
   - Pages 1–90 bypass OCR completely and parse at microsecond speeds.
   - Pages 91–100 dispatch sequentially or in throttled parallel batches to `IOcrService`.
   - Resulting `ScholarDocument` contains all 100 pages unified in true source order.

---

## 2. Specification 2: Page Semantics & Boundary Invariants

Scholar Kit relies on strict page awareness to maintain academic citations and navigation. Fabricating physical page numbers where none exist is strictly prohibited.

### 2.1. Explicit Page Semantics Enum
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

### 2.2. Invariants by Format
| Format | Page Semantics Type | Boundary Determinant | Empty Page Policy |
| :--- | :--- | :--- | :--- |
| **PDF** | `PhysicalPage` (1:1) | Each `PdfPage` in `PdfDocument.Pages` maps strictly to one `DocumentPage` (`PageNumber = 1..N`). | Empty pages are preserved with `RawText = string.Empty` and `Chunks = []`. Never deleted, preventing page number drift. |
| **DOCX** | `LogicalSection` | Explicit page breaks (`<w:br w:type="page"/>` / `<w:lastRenderedPageBreak/>`) and section breaks (`<w:sectPr>`). Physical pages are NOT promised. | Empty sections preserved if explicitly broken. |
| **Single Image** | `PhysicalPage` (Single) | Exactly one `DocumentPage` (`PageNumber = 1`). Dimensions set to pixel width/height. | N/A |
| **Multi-Frame TIFF** | `PhysicalPage` (1:1) | Each directory frame in TIFF container maps to one `DocumentPage` (`PageNumber = 1..FrameCount`). | Blank frames are preserved. |
| **Plain Text / MD / HTML** | `LogicalSection` / `VirtualPage` | Segmented at form-feed characters (`\f`), Markdown top headings (`#`), or virtual chunks (~3,000 chars). | Empty virtual pages omitted. |

### 2.3. Zero False Page Numbers Invariant
Under NO circumstance may the extraction engine fabricate arbitrary physical page numbers:
- If a PDF has 14 physical pages, `ScholarDocument.PageCount` MUST equal 14 with `PageSemantics = PhysicalPage`.
- If physical page 4 is completely blank, `Pages[3].PageNumber` MUST equal 4 with `Pages[3].RawText == string.Empty`.
- For DOCX, pages are explicitly presented in the UI as "Section N" or "Logical Page N", never disguised as confirmed printer sheets.

---

## 3. Specification 3: Two-Tier Text Normalization Engine

### 3.1. Two-Tier Model
1. **`RawText`**: Unmutated ground truth. Preserves exact codepoints, mathematical/chemical formulas, and whitespace.
2. **`NormalizedText`**: Derived text for search and passage chunking. Governed by `TextNormalizationOptions`.

### 3.2. Normalization Options & Separation
```csharp
public sealed record TextNormalizationOptions
{
    // Tier 1: Lossless Cleanup (Preserves all scholarly meaning)
    public bool NormalizeLineEndings { get; init; } = true;          // CRLF -> LF
    public bool StripNonPrintableControlChars { get; init; } = true;  // ASCII 0x00-0x08, 0x0B-0x0C, 0x0E-0x1F (keeps 	, 
)
    public bool StripBOMAndZeroWidthChars { get; init; } = true;      // ﻿, ​-‍

    // Tier 2: Formatting Standardizations
    public bool CollapseConsecutiveSpaces { get; init; } = true;      // Multiple spaces -> single space
    public bool PreserveParagraphBreaks { get; init; } = true;        // Standardize double newlines

    // Tier 3: Optional Glyphic Transformations (Configurable)
    public bool ApplyUnicodeNfkc { get; init; } = false;              // Unicode NFKC normalization
    public bool UnfoldTypesettingLigatures { get; init; } = true;     // 'ﬁ' -> 'fi', 'ﬂ' -> 'fl'
    public bool RepairLinebreakHyphenation { get; init; } = false;    // Disabled by default for formulas/code
}
```

### 3.3. Scientific & Mathematical Notation Protection
- Superscripts (`x²`, `10⁻³`), subscripts (`H₂O`), Greek symbols (`α`, `β`, `γ`), and mathematical operators (`∫`, `∑`, `√`, `≤`, `≥`, `≠`) are strictly preserved.
- Chemical equations and LaTeX math blocks (`$...$`, `$$...$$`) remain uncorrupted.

---

## 4. Specification 4: Configurable Passage Chunking Engine

### 4.1. Configurable Parameters & Validation Defaults
Passage chunk sizes are configurable defaults, subject to empirical QA validation on academic documents:
```csharp
public sealed record ChunkingOptions
{
    public int TargetChunkSizeChars { get; init; } = 350;        // Proposed default
    public int StrideOverlapChars { get; init; } = 60;           // Proposed default
    public int SentenceSnapBoundaryDelta { get; init; } = 40;    // Proposed default
    public bool SnapToSentenceBoundaries { get; init; } = true;
}
```

### 4.2. Chunking Algorithm & Invariants
```
For each DocumentPage in ScholarDocument:
    rawPageText = Page.RawText
    If string.IsNullOrWhiteSpace(rawPageText): continue to next page
    
    offset = 0
    chunkIndex = 0
    pageLength = rawPageText.Length
    
    While offset < pageLength:
        targetEnd = min(offset + options.TargetChunkSizeChars, pageLength)
        
        If targetEnd < pageLength and options.SnapToSentenceBoundaries:
            snapPoint = FindNearestSentenceEnd(rawPageText, targetEnd, options.SentenceSnapBoundaryDelta)
            If snapPoint != -1:
                targetEnd = snapPoint
        
        chunkText = rawPageText[offset..targetEnd].Trim()
        
        If chunkText.Length > 0:
            Emit DocumentPassageChunk:
                ChunkId = chunkIndex++
                DocumentId = Document.DocumentId
                PageNumber = Page.PageNumber
                ChunkIndex = chunkIndex
                StartCharOffset = offset
                EndCharOffset = targetEnd
                Text = chunkText
                EmbeddingStatus = PassageEmbeddingStatus.NoEmbedding
                Embedding = null
        
        offset = max(offset + 1, targetEnd - options.StrideOverlapChars)
```

- **Strict Non-Crossing Invariant**: Chunks NEVER span across page boundaries.
- **Exact Offset Provenance**: `StartCharOffset` and `EndCharOffset` map 1:1 to indices in `DocumentPage.RawText`.
- **No Silent Text Loss**: Every character in the source page is accounted for.
- **Deterministic**: 100% reproducible across test runs.

---

## 5. Specification 5: Configurable Security Limits & Resource Bounds

All thresholds are configurable options based on realistic academic workloads, subject to validation:

| Resource Boundary | Default Threshold | Rationale & Protection |
| :--- | :--- | :--- |
| **Maximum File Size** | 250 MB | Protects local memory while permitting large academic books and theses. |
| **Maximum Page Count** | 1,000 pages | Prevents pathological runaway extraction on giant archives. |
| **Maximum Image Resolution** | 16,384 x 16,384 px | Prevents uncompressed bitmap out-of-memory crashes during OCR. |
| **Maximum DOCX Uncompressed Size** | 500 MB | Defends against ZIP-bomb decompression attacks. |
| **Maximum ZIP Compression Ratio** | 100:1 | Aborts decompression if compression ratio exceeds safe limit. |
| **XML DTD Processing** | Prohibited (`XmlResolver = null`) | Completely neutralizes XXE and Billion Laughs XML entity expansion. |
| **Extraction Timeout** | 5 minutes | Prevents hung background worker threads. |

---

## 6. Specification 6: Objective Acceptance Gates

Phase W3-C replaces generic "100% accuracy" or "zero warnings globally" claims with objective, fixture-based criteria:

1. **Compilation & Warning Gate**:
   - Zero build errors across the solution.
   - **Zero new compiler or analyzer warnings attributable to W3-C code.**
   - Pre-existing baseline warnings remain cataloged and untouched.
2. **Fixture-Based Corpus Validation**:
   - Digital PDF fixture (`quantum_neural_computing_2026.pdf`): 100% of pages extracted with correct page count and non-empty text.
   - Plain text fixtures (UTF-8, UTF-16, ASCII, ANSI): Zero decoding exceptions; encoding accurately identified.
   - Delimited text fixture: CSV/TSV parsed into validated rows and Markdown table representation.
   - Markdown fixture: Headers, code fences, and lists structured into logical sections.
   - DOCX fixture: OpenXML headings, paragraphs, and tables parsed without Microsoft Office.
3. **Reproducibility Gate**:
   - Extraction output for identical inputs produces identical results. OCR text is reproducible within a fixed engine/runtime configuration.
4. **Boundary & Immutability Gate**:
   - Zero bytes written to source document. Chunks never cross page boundaries. Offsets match exact source character positions.
