using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Helpers;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// 100% Offline Document RAG (Retrieval-Augmented Generation) &amp; Semantic Chat Engine.
/// Architecture:
///   1. Sliding-Window Semantic Chunking (350-character window, 60-character stride overlap, sentence-boundary preserving).
///   2. DirectML / ONNX Embedding Generation (384-dimensional dense vectors).
///   3. Hardware SIMD Cosine Similarity Vector Search (AVX2 / AVX-512 / ARM NEON via SimdVectorHelper).
///   4. Multi-Passage Context Synthesis with grounded citation indexing and confidence attribution.
/// </summary>
public sealed class DocumentChatService : IDocumentChatService
{
    private readonly IWindowsAiService _embeddingService;
    private readonly ILogger<DocumentChatService> _logger;

    private readonly List<DocumentPassageChunk> _passageIndex = [];
    private readonly SemaphoreSlim _indexLock = new(1, 1);

    public bool HasIndexedDocument => _passageIndex.Count > 0;

    public DocumentChatService(IWindowsAiService embeddingService, ILogger<DocumentChatService> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task IndexDocumentAsync(string documentText, CancellationToken ct = default)
    {
        await _indexLock.WaitAsync(ct);
        try
        {
            _passageIndex.Clear();
            if (string.IsNullOrWhiteSpace(documentText)) return;

            // Step 1: Split by paragraphs, keeping structural breaks
            var paragraphs = documentText.Split(
                new[] { "\r\n\r\n", "\n\n", "\r\r" },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var chunkList = new List<string>();

            foreach (var para in paragraphs)
            {
                if (para.Length < 15) continue;

                if (para.Length <= 400)
                {
                    chunkList.Add(para);
                }
                else
                {
                    // Sliding window with sentence boundary preservation
                    var sentenceChunks = SplitIntoSlidingWindowChunks(para, targetChunkSize: 350, overlapSize: 60);
                    chunkList.AddRange(sentenceChunks);
                }
            }

            if (chunkList.Count == 0 && documentText.Trim().Length > 0)
            {
                chunkList.Add(documentText.Trim());
            }

            // Step 2: Generate DirectML embeddings for all chunks
            int chunkIndex = 0;
            foreach (var chunkText in chunkList)
            {
                ct.ThrowIfCancellationRequested();
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunkText, ct);

                _passageIndex.Add(new DocumentPassageChunk
                {
                    ChunkId = chunkIndex++,
                    Text = chunkText,
                    Embedding = embedding,
                    CharLength = chunkText.Length
                });
            }

            _logger.LogInformation("Indexed document into {Count} sliding-window semantic passages.", _passageIndex.Count);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<DocumentChatResult> QueryDocumentAsync(string userQuery, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return new DocumentChatResult
            {
                Answer = "Please enter a question to search the active document.",
                Confidence = 0.0,
                CitedPassages = []
            };
        }

        await _indexLock.WaitAsync(ct);
        List<DocumentPassageChunk> passagesSnapshot;
        try
        {
            if (_passageIndex.Count == 0)
            {
                return new DocumentChatResult
                {
                    Answer = "No document has been indexed yet. Import an image, PDF, or dictate notes first.",
                    Confidence = 0.0,
                    CitedPassages = []
                };
            }
            passagesSnapshot = [.. _passageIndex];
        }
        finally
        {
            _indexLock.Release();
        }

        // Step 1: Embed user query via DirectML
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(userQuery, ct);

        // Step 2: Rank all document chunks using SIMD hardware vectorization
        var scoredPassages = passagesSnapshot
            .Select(p => new
            {
                p.ChunkId,
                p.Text,
                Similarity = SimdVectorHelper.CosineSimilarity(queryEmbedding, p.Embedding)
            })
            .OrderByDescending(p => p.Similarity)
            .Take(4)
            .ToList();

        // Step 3: Low-confidence rejection threshold
        if (scoredPassages.Count == 0 || scoredPassages[0].Similarity < 0.12f)
        {
            return new DocumentChatResult
            {
                Answer = "No strongly relevant information was found in this document for the provided query.",
                Confidence = scoredPassages.FirstOrDefault()?.Similarity ?? 0.0,
                CitedPassages = []
            };
        }

        // Step 4: Synthesize grounded response with structured citations
        var best = scoredPassages[0];
        var answerBuilder = new StringBuilder();

        answerBuilder.AppendLine($"**Direct Context Match** (Confidence: {best.Similarity * 100:F1}%):");
        answerBuilder.AppendLine();
        answerBuilder.AppendLine(best.Text);

        var citations = new List<string> { best.Text };

        var supportingPassages = scoredPassages
            .Skip(1)
            .Where(p => p.Similarity >= 0.35f && !p.Text.Equals(best.Text, StringComparison.Ordinal))
            .ToList();

        if (supportingPassages.Count > 0)
        {
            answerBuilder.AppendLine();
            answerBuilder.AppendLine("**Supporting Passages:**");
            foreach (var sup in supportingPassages)
            {
                answerBuilder.AppendLine($"- *[Match {sup.Similarity * 100:F0}%]* {sup.Text}");
                citations.Add(sup.Text);
            }
        }

        return new DocumentChatResult
        {
            Answer = answerBuilder.ToString(),
            Confidence = best.Similarity,
            CitedPassages = citations
        };
    }

    /// <summary>
    /// Splits long text into overlapping chunks while preserving sentence boundaries.
    /// </summary>
    private static List<string> SplitIntoSlidingWindowChunks(string text, int targetChunkSize, int overlapSize)
    {
        var result = new List<string>();
        var sentences = text.Split(new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (sentences.Length <= 1)
        {
            // Fallback for text with no punctuation
            for (int i = 0; i < text.Length; i += Math.Max(1, targetChunkSize - overlapSize))
            {
                int len = Math.Min(targetChunkSize, text.Length - i);
                result.Add(text.Substring(i, len).Trim());
                if (i + len >= text.Length) break;
            }
            return result;
        }

        var currentChunk = new StringBuilder();
        var previousOverlap = string.Empty;

        foreach (var s in sentences)
        {
            var trimmed = s.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (currentChunk.Length + trimmed.Length > targetChunkSize && currentChunk.Length > 0)
            {
                result.Add(currentChunk.ToString().Trim());

                // Seed the next chunk with overlap from the tail of current chunk
                currentChunk.Clear();
                if (!string.IsNullOrEmpty(previousOverlap))
                {
                    currentChunk.Append(previousOverlap).Append(". ");
                }
            }

            currentChunk.Append(trimmed).Append(". ");
            previousOverlap = trimmed.Length > overlapSize ? trimmed[^overlapSize..] : trimmed;
        }

        if (currentChunk.Length > 0)
        {
            result.Add(currentChunk.ToString().Trim());
        }

        return result;
    }

    private sealed class DocumentPassageChunk
    {
        public int ChunkId { get; init; }
        public string Text { get; init; } = string.Empty;
        public float[] Embedding { get; init; } = [];
        public int CharLength { get; init; }
    }
}
