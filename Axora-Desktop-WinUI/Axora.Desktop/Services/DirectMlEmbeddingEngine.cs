using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Axora.Desktop.Helpers;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Production embedding engine implementing 3-tier execution provider hierarchy:
/// Tier 1: Direct3D 12 GPU acceleration via DirectML.
/// Tier 2: Multithreaded CPU execution via ONNX Runtime parallel mode.
/// Tier 3: Deterministic Class A Lexical Feature Projection when neural model is absent.
/// Enforces attention-masked mean pooling and strict L2 unit normalization (||v||2 = 1.0 +/- 10^-5).
/// </summary>
public sealed class DirectMlEmbeddingEngine : IEmbeddingEngine, IEmbeddingCapabilityStateProvider, IDisposable
{
    private readonly ILogger<DirectMlEmbeddingEngine> _logger;
    private readonly SemaphoreSlim _deviceRecoveryLock = new(1, 1);
    private InferenceSession? _session;
    private bool _disposed;

    public string ModelId { get; private set; } = "class_a_lexical";
    public string ModelFingerprint { get; private set; } = "class_a_lexical_hashing";
    public int Dimension => 384;
    public EmbeddingExecutionProvider ActiveProvider { get; private set; } = EmbeddingExecutionProvider.LexicalHeuristic;

    public bool IsNeuralModelInstalled { get; private set; }
    public bool IsDirectMlSupported { get; private set; }

    /// <summary>
    /// Test seam allowing simulated GPU / DirectML device loss (TDR) to test CPU fallback and lexical recovery.
    /// </summary>
    public bool ForceDirectMlFailureForTesting { get; set; }

    public DirectMlEmbeddingEngine(
        ILogger<DirectMlEmbeddingEngine>? logger = null,
        string? explicitModelPath = null,
        bool allowDirectMl = true)
    {
        _logger = logger ?? NullLogger<DirectMlEmbeddingEngine>.Instance;
        InitializeSession(explicitModelPath, allowDirectMl);
    }

    private void InitializeSession(string? explicitModelPath, bool allowDirectMl)
    {
        string? resolvedPath = explicitModelPath;

        if (string.IsNullOrEmpty(resolvedPath))
        {
            // Search order: %APPDATA%\Axora\Capabilities\Models\all-MiniLM-L6-v2\model.onnx
            // then AppContext.BaseDirectory\Assets\Models\all-MiniLM-L6-v2\model.onnx
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Axora", "Capabilities", "Models", "all-MiniLM-L6-v2", "model.onnx");

            string assetsPath = Path.Combine(
                AppContext.BaseDirectory, "Assets", "Models", "all-MiniLM-L6-v2", "model.onnx");

            if (File.Exists(appDataPath))
            {
                resolvedPath = appDataPath;
            }
            else if (File.Exists(assetsPath))
            {
                resolvedPath = assetsPath;
            }
        }

        if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
        {
            _logger.LogInformation("Neural model not found. Initializing Class A Lexical Feature Projector.");
            IsNeuralModelInstalled = false;
            IsDirectMlSupported = false;
            ActiveProvider = EmbeddingExecutionProvider.LexicalHeuristic;
            ModelId = "class_a_lexical";
            ModelFingerprint = "class_a_lexical_hashing";
            return;
        }

        IsNeuralModelInstalled = true;
        ModelId = "all-MiniLM-L6-v2";

        try
        {
            using var fileStream = File.OpenRead(resolvedPath);
            byte[] hashBytes = SHA256.HashData(fileStream);
            ModelFingerprint = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch
        {
            ModelFingerprint = "model_fingerprint_read_error";
        }

        if (allowDirectMl)
        {
            try
            {
                var dmlOpts = new SessionOptions();
                dmlOpts.AppendExecutionProvider_DML(deviceId: 0);
                dmlOpts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                _session = new InferenceSession(resolvedPath, dmlOpts);
                ActiveProvider = EmbeddingExecutionProvider.DirectML;
                IsDirectMlSupported = true;
                _logger.LogInformation("Initialized DirectML execution provider on D3D12 device 0.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DirectML execution provider unavailable. Falling back to CPU provider.");
                IsDirectMlSupported = false;
            }
        }

        try
        {
            var cpuOpts = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_PARALLEL
            };
            _session = new InferenceSession(resolvedPath, cpuOpts);
            ActiveProvider = EmbeddingExecutionProvider.Cpu;
            _logger.LogInformation("Initialized CPU execution provider (parallel execution mode).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CPU ONNX session. Falling back to Class A Lexical Projector.");
            _session = null;
            ActiveProvider = EmbeddingExecutionProvider.LexicalHeuristic;
            ModelId = "class_a_lexical";
            ModelFingerprint = "class_a_lexical_hashing";
        }
    }

    /// <inheritdoc/>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = await GenerateBatchEmbeddingsAsync(new[] { text ?? string.Empty }, ct);
        return results[0];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<float[]>> GenerateBatchEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ct.ThrowIfCancellationRequested();

        if (texts.Count < 1 || texts.Count > 32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(texts),
                $"Batch size must be between 1 and 32. Provided count: {texts.Count}. [Code: ERR_BATCH_SIZE_OUT_OF_RANGE]");
        }

        if (ActiveProvider == EmbeddingExecutionProvider.LexicalHeuristic || _session == null)
        {
            return await Task.Run(() =>
            {
                var outputs = new float[texts.Count][];
                for (int b = 0; b < texts.Count; b++)
                {
                    ct.ThrowIfCancellationRequested();
                    outputs[b] = GenerateClassALexicalVector(texts[b]);
                }
                return (IReadOnlyList<float[]>)outputs;
            }, ct);
        }

        return await Task.Run(async () =>
        {
            try
            {
                if (ForceDirectMlFailureForTesting)
                {
                    throw new InvalidOperationException("Simulated DXGI_ERROR_DEVICE_REMOVED (0x887A0005) device loss for testing.");
                }

                return ExecuteSessionBatch(texts, ct);
            }
            catch (Exception ex) when (ActiveProvider == EmbeddingExecutionProvider.DirectML || ForceDirectMlFailureForTesting)
            {
                _logger.LogWarning(ex, "DirectML inference failure detected (possible device reset/TDR). Falling back to CPU provider.");
                ForceDirectMlFailureForTesting = false;
                await RecoverToCpuProviderAsync();

                if (ActiveProvider == EmbeddingExecutionProvider.LexicalHeuristic || _session == null)
                {
                    var outputs = new float[texts.Count][];
                    for (int b = 0; b < texts.Count; b++)
                    {
                        ct.ThrowIfCancellationRequested();
                        outputs[b] = GenerateClassALexicalVector(texts[b]);
                    }
                    return (IReadOnlyList<float[]>)outputs;
                }

                return ExecuteSessionBatch(texts, ct);
            }
        }, ct);
    }

    private IReadOnlyList<float[]> ExecuteSessionBatch(IReadOnlyList<string> texts, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int batchSize = texts.Count;
        var tokenizedBatch = new long[batchSize][];
        int maxSeqLen = 1;

        for (int b = 0; b < batchSize; b++)
        {
            tokenizedBatch[b] = SimpleTokenize(texts[b]);
            if (tokenizedBatch[b].Length > maxSeqLen)
            {
                maxSeqLen = tokenizedBatch[b].Length;
            }
        }

        var inputIds = new DenseTensor<long>(new[] { batchSize, maxSeqLen });
        var attentionMask = new DenseTensor<long>(new[] { batchSize, maxSeqLen });
        var tokenTypeIds = new DenseTensor<long>(new[] { batchSize, maxSeqLen });

        for (int b = 0; b < batchSize; b++)
        {
            var tokens = tokenizedBatch[b];
            for (int i = 0; i < maxSeqLen; i++)
            {
                if (i < tokens.Length)
                {
                    inputIds[b, i] = tokens[i];
                    attentionMask[b, i] = 1L;
                }
                else
                {
                    inputIds[b, i] = 0L; // PAD
                    attentionMask[b, i] = 0L;
                }
                tokenTypeIds[b, i] = 0L;
            }
        }

        var inputs = new List<NamedOnnxValue>(3)
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };

        if (_session!.InputMetadata.ContainsKey("token_type_ids"))
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds));
        }

        using var results = _session.Run(inputs);
        var outputTensor = results[0].AsTensor<float>();

        var embeddings = new float[batchSize][];

        if (outputTensor.Dimensions.Length == 2)
        {
            // Integrated pooling: [BatchSize, 384]
            for (int b = 0; b < batchSize; b++)
            {
                var vec = new float[384];
                for (int d = 0; d < 384; d++)
                {
                    vec[d] = outputTensor[b, d];
                }
                NormalizeL2(vec);
                embeddings[b] = vec;
            }
        }
        else
        {
            // Unpooled token hidden states: [BatchSize, MaxSeqLen, 384]
            // Execute attention-mask-aware mean pooling (INV-W3D-29)
            for (int b = 0; b < batchSize; b++)
            {
                embeddings[b] = PoolAndNormalize(outputTensor, attentionMask, b, maxSeqLen);
            }
        }

        return embeddings;
    }

    private static float[] PoolAndNormalize(
        Tensor<float> hiddenStates,
        Tensor<long> attentionMask,
        int batchIndex,
        int seqLen)
    {
        var pooled = new float[384];
        float sumMask = 0.0f;

        for (int i = 0; i < seqLen; i++)
        {
            long maskVal = attentionMask[batchIndex, i];
            if (maskVal > 0)
            {
                sumMask += 1.0f;
                for (int d = 0; d < 384; d++)
                {
                    pooled[d] += hiddenStates[batchIndex, i, d];
                }
            }
        }

        float divisor = Math.Max(sumMask, 1.0f);
        for (int d = 0; d < 384; d++)
        {
            pooled[d] /= divisor;
        }

        NormalizeL2(pooled);
        return pooled;
    }

    /// <summary>
    /// Executes attention-mask-aware mean pooling and strict L2 normalization on an in-memory 3D tensor fixture.
    /// Used by TEST-W3D-29 for deterministic unit testing without requiring an active ONNX session.
    /// </summary>
    public static float[] PoolAndNormalizeExplicit(
        float[,,] hiddenStates,
        long[,] attentionMask,
        int batchIndex,
        int seqLen)
    {
        var pooled = new float[384];
        float sumMask = 0.0f;

        for (int i = 0; i < seqLen; i++)
        {
            long maskVal = attentionMask[batchIndex, i];
            if (maskVal > 0)
            {
                sumMask += 1.0f;
                for (int d = 0; d < 384; d++)
                {
                    pooled[d] += hiddenStates[batchIndex, i, d];
                }
            }
        }

        float divisor = Math.Max(sumMask, 1.0f);
        for (int d = 0; d < 384; d++)
        {
            pooled[d] /= divisor;
        }

        NormalizeL2(pooled);
        return pooled;
    }

    private static void NormalizeL2(float[] vector)
    {
        float magnitude = SimdVectorHelper.Magnitude(vector);
        if (magnitude > 1e-12f)
        {
            float invMag = 1.0f / magnitude;
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] *= invMag;
            }
        }
    }

    /// <summary>
    /// Class A Deterministic Lexical Feature Projection.
    /// In accordance with INV-W3D-28, this is purely a deterministic lexical hashing projector
    /// and makes NO claim to neural semantic understanding.
    /// </summary>
    public static float[] GenerateClassALexicalVector(string text)
    {
        var vec = new float[384];
        if (string.IsNullOrWhiteSpace(text))
        {
            return vec;
        }

        var words = text.Split(
            [' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '-', '(', ')', '[', ']', '\"', '\''],
            StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            return vec;
        }

        for (int i = 0; i < words.Length; i++)
        {
            string w = words[i].ToLowerInvariant();
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(w));
            uint h1 = BitConverter.ToUInt32(hash, 0);
            uint h2 = BitConverter.ToUInt32(hash, 4);

            int bucket = (int)(h1 % 384);
            float sign = (h2 % 2 == 0) ? 1.0f : -1.0f;
            vec[bucket] += sign / (float)Math.Sqrt(words.Length);
        }

        NormalizeL2(vec);
        return vec;
    }

    private async Task RecoverToCpuProviderAsync()
    {
        await _deviceRecoveryLock.WaitAsync();
        try
        {
            if (ActiveProvider == EmbeddingExecutionProvider.Cpu)
            {
                return;
            }

            _session?.Dispose();
            _session = null;

            string modelPath = Path.Combine(
                AppContext.BaseDirectory, "Assets", "Models", "all-MiniLM-L6-v2", "model.onnx");

            if (!File.Exists(modelPath))
            {
                modelPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Axora", "Capabilities", "Models", "all-MiniLM-L6-v2", "model.onnx");
            }

            if (File.Exists(modelPath))
            {
                try
                {
                    var cpuOpts = new SessionOptions
                    {
                        GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                        ExecutionMode = ExecutionMode.ORT_PARALLEL
                    };
                    _session = new InferenceSession(modelPath, cpuOpts);
                    ActiveProvider = EmbeddingExecutionProvider.Cpu;
                    _logger.LogInformation("Successfully recovered session to CPU execution provider.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize CPU ONNX session during recovery. Falling back to Class A Lexical Projector.");
                    _session = null;
                    ActiveProvider = EmbeddingExecutionProvider.LexicalHeuristic;
                    ModelId = "class_a_lexical";
                    ModelFingerprint = "class_a_lexical_hashing";
                }
            }
            else
            {
                _session = null;
                ActiveProvider = EmbeddingExecutionProvider.LexicalHeuristic;
                ModelId = "class_a_lexical";
                ModelFingerprint = "class_a_lexical_hashing";
                _logger.LogWarning("Model file missing during recovery. Operating under Class A Lexical Projector.");
            }
        }
        finally
        {
            _deviceRecoveryLock.Release();
        }
    }

    /// <summary>
    /// Deterministic BERT WordPiece tokenization for all-MiniLM-L6-v2 (INV-W3D-04, AUD2-W3D-01).
    /// Uses the authentic 30,522-token BERT uncased vocabulary packaged in Assets/Embedded Resources.
    /// </summary>
    public static long[] SimpleTokenize(string text)
    {
        return WordPieceTokenizer.TokenizeToIds(text);
    }

    /// <summary>
    /// Deterministic BERT tokenization method exposed for unit testing and cross-process verification.
    /// </summary>
    public static long[] TokenizeDeterministic(string text)
    {
        return WordPieceTokenizer.TokenizeToIds(text);
    }

    public string GetCapabilityStatusBadgeText()
    {
        return IsNeuralModelInstalled
            ? (ActiveProvider == EmbeddingExecutionProvider.DirectML ? "Neural Engine (DirectML)" : "Neural Engine (CPU)")
            : "Ready (Lexical Only)";
    }

    public string GetActiveProviderDescription()
    {
        return ActiveProvider switch
        {
            EmbeddingExecutionProvider.DirectML => "DirectML — GPU Accelerated",
            EmbeddingExecutionProvider.Cpu => "CPU — Parallel ONNX Runtime",
            EmbeddingExecutionProvider.LexicalHeuristic => "Class A Lexical Feature Projector (Offline)",
            EmbeddingExecutionProvider.RemoteOptIn => "Remote Opt-In API Provider",
            _ => "Unknown"
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session?.Dispose();
            _deviceRecoveryLock.Dispose();
            _disposed = true;
        }
    }
}
