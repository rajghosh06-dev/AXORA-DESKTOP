# Phase W3-C.6 Architecture Specification: Text Normalization & Paragraph Assembly

**Phase**: `W3-C.6 — Text Normalization & Paragraph Assembly`  
**Status**: `PLANNING ONLY / APPROVED FOR REVIEW`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. System Context & Architectural Position

The `ITextNormalizer` service sits downstream of format-specific extraction engines and upstream of passage chunking (`IPassageChunker`, Phase W3-C.7) and library persistence (`IScholarLibraryService`, Phase W3-B):

```
┌────────────────────────────────────────────────────────────────────────┐
│                   PHASE W3-C EXTRACTION PIPELINE                       │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│   [ Input Document Stream / File ]                                     │
│                  │                                                     │
│                  ▼                                                     │
│   [ IDocumentFormatDetector ]  (Content sniffing & MIME)               │
│                  │                                                     │
│                  ▼                                                     │
│   [ IDocumentExtractorEngine ] (Format-specific: PDF, DOCX, TIFF, etc.) │
│                  │                                                     │
│                  ▼                                                     │
│   [ RawExtractionResult ]                                              │
│         Pages: [ ExtractedPageRaw (PageNumber, RawText, NormalizedText=null) ]
│                  │                                                     │
│                  ▼                                                     │
│ ══════════════════════════════════════════════════════════════════════ │
│   [ PHASE W3-C.6: ITextNormalizer ]                                    │
│         Input:  ExtractedPageRaw.RawText                               │
│         Output: ExtractedPageRaw.NormalizedText                        │
│                                                                        │
│         ├── 1. Fast Sanitization (BOM, Zero-Width, Control Chars)     │
│         ├── 2. Line Ending Canonicalization (CRLF/CR -> LF)            │
│         ├── 3. Unicode & Ligature Processing (Ligatures, NBSP)         │
│         ├── 4. Source-Format-Aware Structural Scanner                  │
│         │      • Code block detection (freeze formatting)             │
│         │      • DOCX Markdown token reconciliation                    │
│         │      • Table & List preservation                             │
│         │      • PDF / OCR line wrapping & fragmentation assembly      │
│         └── 5. Paragraph Assembly & Whitespace Normalization           │
│ ══════════════════════════════════════════════════════════════════════ │
│                  │                                                     │
│                  ▼                                                     │
│   [ ExtractedPageRaw with populated NormalizedText ]                   │
│                  │                                                     │
│                  ▼                                                     │
│   [ PHASE W3-C.7: IPassageChunker ] (Page-Bounded Passage Chunks)      │
│                  │                                                     │
│                  ▼                                                     │
│   [ IScholarLibraryService ] (Durable JSON Persistence in %APPDATA%)   │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Service Contracts & Public Interfaces

### 2.1 Interface Definition: `ITextNormalizer`
Located in `Axora.Desktop/Services/Contracts/ITextNormalizer.cs`:

```csharp
namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for deterministic, non-destructive text normalization and paragraph assembly.
/// Transforms raw extracted text into canonical, clean, and searchable normalized text
/// without mutating original RawText or altering scientific notation.
/// </summary>
public interface ITextNormalizer
{
    /// <summary>
    /// Normalizes raw text according to the specified options using general rules.
    /// </summary>
    string Normalize(string rawText, TextNormalizationOptions? options = null);

    /// <summary>
    /// Normalizes raw text with source-format-specific awareness (e.g. PDF wrapping, OCR fragments, DOCX markers).
    /// </summary>
    string Normalize(string rawText, DetectedDocumentFormat format, TextNormalizationOptions? options = null);

    /// <summary>
    /// Normalizes an extracted page in-place, assigning the resulting text to NormalizedText
    /// while leaving RawText completely unmutated.
    /// </summary>
    ExtractedPageRaw NormalizePage(ExtractedPageRaw rawPage, DetectedDocumentFormat format, TextNormalizationOptions? options = null);
}
```

### 2.2 Concrete Implementation: `TextNormalizer`
Located in `Axora.Desktop/Services/TextNormalizer.cs`:
- Thread-safe, stateless service registered as a Singleton in `App.xaml.cs`.
- Designed for high-throughput single-pass string processing using `StringBuilder` and `ReadOnlySpan<char>`.

---

## 3. Pipeline Stages & Normalization Architecture

The normalization pipeline is structured as 5 sequential, deterministic stages:

```
RawText ──► [ Stage 1: Sanitize ] ──► [ Stage 2: Line Endings ] ──► [ Stage 3: Glyphs & Ligatures ]
                                                                             │
NormalizedText ◄── [ Stage 5: Paragraph Assembly ] ◄── [ Stage 4: Structural Scanner ]
```

### Stage 1: Fast Sanitization & Control Character Removal
- **BOM & Zero-Width Stripping**:
  - `\uFEFF` (Byte Order Mark)
  - `\u200B` (Zero-Width Space)
  - `\u200C` (Zero-Width Non-Joiner)
  - `\u200D` (Zero-Width Joiner)
  - `\u200E` / `\u200F` (Directional marks)
- **Non-Printable Control Characters**:
  - ASCII `0x00`–`0x08`, `0x0B`–`0x0C` (FormFeed), `0x0E`–`0x1F`, and `0x7F` (DEL).
  - Explicitly preserves: `0x09` (Tab `\t`) and `0x0A` (Line Feed `\n`).

### Stage 2: Line Ending Canonicalization
- Converts all CRLF (`\r\n`) and lone CR (`\r`) to standard Unix LF (`\n`).
- Ensures that subsequent line-splitting and boundary detection operate on a single uniform newline character.

### Stage 3: Unicode & Ligature Processing
- **Typesetting Ligatures Unfolding** (active when `UnfoldTypesettingLigatures = true`):
  - `\uFB00` (`ﬀ`) -> `"ff"`
  - `\uFB01` (`ﬁ`) -> `"fi"`
  - `\uFB02` (`ﬂ`) -> `"fl"`
  - `\uFB03` (`ﬃ`) -> `"ffi"`
  - `\uFB04` (`ﬄ`) -> `"ffl"`
  - `\uFB05` (`ﬅ`) -> `"ft"`
  - `\uFB06` (`ﬆ`) -> `"st"`
- **Non-Breaking Spaces (NBSP)**:
  - `\u00A0` (No-Break Space) -> Standard space `\u0020`
  - `\u202F` (Narrow No-Break Space) -> Standard space `\u0020`
- **Soft Hyphen Handling**:
  - `\u00AD` (Soft Hyphen) -> stripped.
- **Unicode NFKC Decomposition** (active ONLY when `ApplyUnicodeNfkc = true`):
  - Decomposes compatibility characters and canonicalizes composition via `text.Normalize(NormalizationForm.FormKC)`.
  - Default is `false` to preserve mathematical superscripts (`x²`) and subscripts (`H₂O`).

### Stage 4: Source-Format-Aware Structural Scanner
The text is partitioned into logical blocks to ensure formatting context is respected:

```
Text Lines
  │
  ├── Is Fenced Code Block? (```) ──► Freeze block (bypass all whitespace/wrapping changes)
  │
  ├── Is Markdown Table? (| ... |) ──► Preserve row pipes, normalize cell padding
  │
  ├── Is Heading / List Marker?   ──► Ensure proper bounding newlines (\n\n)
  │
  └── Is Flowing Body Text?       ──► Pass to Stage 5 Paragraph Assembler
```

#### DOCX Structural Marker Reconciler:
- Recognizes structural tokens emitted by `DocxDocumentExtractorEngine`:
  - Headings: lines starting with `# `, `## `, `### ` are treated as block headings with `\n\n` boundaries.
  - Lists: lines starting with `- ` are preserved as bullet items with single newlines between consecutive items.
  - Tables: lines containing `|` delimiters with separator rows (`|---|`) are preserved as structured tables.
- **Literal Text Guard**: Literal `#`, `-`, or `|` characters occurring in the middle of text lines or sentences are untouched.

### Stage 5: Paragraph Assembly & Whitespace Normalization
- **Paragraph Delineation**:
  - Two or more consecutive newlines (`\n\n+`) indicate an intentional paragraph break.
- **Line Wrap Joining**:
  - In `PdfDigital`, `PdfScanned`, `PdfMixed`, `RasterImage`, and `MultiPageTiff`:
    - Visual line wraps within a single flowing paragraph are joined with a space.
    - Example:
      ```
      Line 1: "This paper presents a novel approach to"
      Line 2: "natural language understanding in academic"
      Line 3: "contexts."
      ```
      Assembles into:
      `"This paper presents a novel approach to natural language understanding in academic contexts."`
  - **Hyphenation Repair** (active when `RepairLinebreakHyphenation = true`):
    - Words ending with `-` at line break (e.g. `imple-\nmentation`) are joined as `implementation`.
    - Protected: hyphenated numbers (e.g. `10-20`), chemical compounds (e.g. `cis-`), and minus signs are not joined.
- **OCR Punctuation Spacing**:
  - Spurious spaces preceding punctuation are eliminated:
    `"word , next"` -> `"word, next"`
    `"concluded ."` -> `"concluded."`
    `"question ?"` -> `"question?"`
- **Whitespace Collapsing**:
  - Consecutive spaces/tabs on a single line are collapsed to a single space (`\u0020`).
  - Leading and trailing whitespace on each line is trimmed.

---

## 6. Page-Local Execution & Provenance Tracking

```csharp
public ExtractedPageRaw NormalizePage(
    ExtractedPageRaw rawPage, 
    DetectedDocumentFormat format, 
    TextNormalizationOptions? options = null)
{
    ArgumentNullException.ThrowIfNull(rawPage);
    options ??= new TextNormalizationOptions();

    // RawText is strictly read, NEVER modified
    string raw = rawPage.RawText;
    if (string.IsNullOrEmpty(raw))
    {
        return rawPage with { NormalizedText = string.Empty };
    }

    try
    {
        string normalized = Normalize(raw, format, options);
        return rawPage with { NormalizedText = normalized };
    }
    catch (Exception ex)
    {
        // Graceful failure fallback: preserve RawText, record warning
        return rawPage with 
        { 
            NormalizedText = raw,
            DiagnosticWarning = rawPage.DiagnosticWarning != null 
                ? $"{rawPage.DiagnosticWarning}; ERR_NORMALIZATION_FAILED: {ex.Message}"
                : $"ERR_NORMALIZATION_FAILED: {ex.Message}"
        };
    }
}
```

### Key Provenance Guarantees:
1. `rawPage.RawText` remains bit-for-bit identical to extractor output.
2. `rawPage.PageNumber` (1-indexed) is preserved.
3. `rawPage.PageSemantics` (`PhysicalPage` vs `LogicalSection`) is preserved.
4. `rawPage.WidthPt` and `HeightPt` remain untouched.
5. `rawPage.ExtractedViaOcr` and `Confidence` are preserved.

---

## 7. Performance & ReDoS Defense Architecture

1. **Linear-Time Processing**:
   - The primary sanitization and ligature unfolding passes use `StringBuilder` in $O(N)$ linear time.
2. **ReDoS (Regular Expression Denial of Service) Prevention**:
   - Normalization avoids backtracking regular expressions.
   - Any auxiliary regexes (e.g. punctuation spacing or hyphenation matching) MUST use:
     - Pre-compiled or source-generated regex (`[GeneratedRegex]`).
     - `RegexOptions.NonBacktracking` (supported natively in .NET 7/8/9) or explicit `matchTimeout: TimeSpan.FromSeconds(1)`.
3. **Memory Bounding**:
   - Because normalization is performed page-by-page, memory usage per operation is bounded by $O(\text{page\_length})$ (typically $<50 \text{ KB}$ per page).
   - Massive multi-page documents (e.g. 500-page books) never accumulate cross-page normalization buffers in memory.

---

## 8. Dependency Injection & Service Registration

In `Axora-Desktop-WinUI/Axora.Desktop/App.xaml.cs`:
```csharp
// ── Scholar Document Normalization Services (Phase W3-C.6) ───────────
services.AddSingleton<ITextNormalizer, TextNormalizer>();
```
- Registered as Singleton: `TextNormalizer` is stateless and thread-safe.
- Seamlessly injected into `IScholarExtractionOrchestrator` when executing the full extraction-normalization-chunking pipeline.
