using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Axora.Desktop.Helpers;

/// <summary>
/// Deterministic BERT wordPiece tokenizer for all-MiniLM-L6-v2 (INV-W3D-04, AUD2-W3D-01).
/// Implements greedy longest-match-first WordPiece segmentation against the canonical
/// 30,522-token BERT uncased vocabulary.
/// </summary>
public static class WordPieceTokenizer
{
    public const long PadTokenId = 0L;
    public const long UnkTokenId = 100L;
    public const long ClsTokenId = 101L;
    public const long SepTokenId = 102L;
    public const long MaskTokenId = 103L;

    public const string PadToken = "[PAD]";
    public const string UnkToken = "[UNK]";
    public const string ClsToken = "[CLS]";
    public const string SepToken = "[SEP]";
    public const string MaskToken = "[MASK]";

    private static readonly Lazy<Dictionary<string, int>> VocabLazy = new(LoadVocabulary);
    private static readonly Lazy<string[]> IdToTokenLazy = new(BuildIdToToken);

    public static int VocabularySize => VocabLazy.Value.Count;

    public static bool TryGetTokenId(string token, out int id) =>
        VocabLazy.Value.TryGetValue(token, out id);

    /// <summary>
    /// Tokenizes input text into standard BERT input IDs bounded by maxLen (default: 512).
    /// Always prefixes [CLS] (101) and appends [SEP] (102).
    /// </summary>
    public static long[] TokenizeToIds(string text, int maxLen = 512)
    {
        var (inputIds, _, _) = Tokenize(text, maxLen);
        return inputIds;
    }

    /// <summary>
    /// Performs full BERT tokenization returning input_ids, attention_mask, and token_type_ids.
    /// </summary>
    public static (long[] InputIds, long[] AttentionMask, long[] TokenTypeIds) Tokenize(string text, int maxLen = 512)
    {
        if (maxLen < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLen), "Maximum sequence length must be at least 2 for [CLS] and [SEP].");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            long[] emptyIds = [ClsTokenId, SepTokenId];
            long[] emptyMask = [1L, 1L];
            long[] emptyType = [0L, 0L];
            return (emptyIds, emptyMask, emptyType);
        }

        var vocab = VocabLazy.Value;
        var words = BasicTokenize(text);
        int maxContentTokens = maxLen - 2; // Reserve slots for [CLS] and [SEP]
        var tokenIds = new List<long>(Math.Min(words.Count * 2 + 2, maxLen)) { ClsTokenId };

        foreach (var word in words)
        {
            if (tokenIds.Count - 1 >= maxContentTokens)
            {
                break;
            }

            var subwordIds = WordPieceTokenizeWord(word, vocab);
            foreach (var id in subwordIds)
            {
                tokenIds.Add(id);
                if (tokenIds.Count - 1 >= maxContentTokens)
                {
                    break;
                }
            }
        }

        tokenIds.Add(SepTokenId);

        long[] ids = [.. tokenIds];
        long[] mask = new long[ids.Length];
        long[] type = new long[ids.Length];
        Array.Fill(mask, 1L);

        return (ids, mask, type);
    }

    /// <summary>
    /// Basic tokenization: converts text to lowercase, splits whitespace, and isolates punctuation.
    /// </summary>
    public static List<string> BasicTokenize(string text)
     {
        var tokens = new List<string>();
        var sb = new StringBuilder();

        foreach (char ch in text.ToLowerInvariant())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else if (IsPunctuation(ch))
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
                tokens.Add(ch.ToString());
            }
            else
            {
                sb.Append(ch);
            }
        }

        if (sb.Length > 0)
        {
            tokens.Add(sb.ToString());
        }

        return tokens;
    }

    private static List<long> WordPieceTokenizeWord(string word, Dictionary<string, int> vocab)
    {
        if (word.Length > 100)
        {
            return [UnkTokenId];
        }

        var subwords = new List<long>();
        int start = 0;

        while (start < word.Length)
        {
            int end = word.Length;
            string? curSubstr = null;
            long matchId = -1L;

            while (start < end)
            {
                string sub = word[start..end];
                if (start > 0)
                {
                    sub = "##" + sub;
                }

                if (vocab.TryGetValue(sub, out int id))
                {
                    curSubstr = sub;
                    matchId = id;
                    break;
                }

                end--;
            }

            if (curSubstr == null)
            {
                return [UnkTokenId];
            }

            subwords.Add(matchId);
            start = end;
        }

        return subwords;
    }

    private static bool IsPunctuation(char ch)
    {
        int cp = (int)ch;
        if ((cp >= 33 && cp <= 47) || (cp >= 58 && cp <= 64) ||
            (cp >= 91 && cp <= 96) || (cp >= 123 && cp <= 126))
        {
            return true;
        }

        return char.IsPunctuation(ch) || char.IsSymbol(ch);
    }

    private static Dictionary<string, int> LoadVocabulary()
    {
        var vocab = new Dictionary<string, int>(32768, StringComparer.Ordinal);

        // 1. Check local application Assets path
        string assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "all-MiniLM-L6-v2", "vocab.txt");
        if (File.Exists(assetsPath))
        {
            PopulateFromLines(vocab, File.ReadLines(assetsPath, Encoding.UTF8));
            if (vocab.Count >= 30000) return vocab;
        }

        // 2. Check %APPDATA%\Axora\Capabilities\Models\all-MiniLM-L6-v2\vocab.txt
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Axora", "Capabilities", "Models", "all-MiniLM-L6-v2", "vocab.txt");
        if (File.Exists(appDataPath))
        {
            PopulateFromLines(vocab, File.ReadLines(appDataPath, Encoding.UTF8));
            if (vocab.Count >= 30000) return vocab;
        }

        // 3. Fallback: load from Embedded Resource
        var asm = typeof(WordPieceTokenizer).Assembly;
        var resourceNames = asm.GetManifestResourceNames();
        string? vocabResource = resourceNames.FirstOrDefault(r => r.EndsWith("vocab.txt", StringComparison.OrdinalIgnoreCase));

        if (vocabResource != null)
        {
            using var stream = asm.GetManifestResourceStream(vocabResource);
            if (stream != null)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string? line;
                int idx = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (!string.IsNullOrEmpty(t) && !vocab.ContainsKey(t))
                    {
                        vocab[t] = idx;
                    }
                    idx++;
                }
                if (vocab.Count >= 30000) return vocab;
            }
        }

        // 4. Emergency minimal BERT fallback if vocabulary file missing
        vocab[PadToken] = 0;
        vocab[UnkToken] = 100;
        vocab[ClsToken] = 101;
        vocab[SepToken] = 102;
        vocab[MaskToken] = 103;

        return vocab;
    }

    private static void PopulateFromLines(Dictionary<string, int> vocab, IEnumerable<string> lines)
    {
        int idx = 0;
        foreach (var line in lines)
        {
            string t = line.Trim();
            if (!string.IsNullOrEmpty(t) && !vocab.ContainsKey(t))
            {
                vocab[t] = idx;
            }
            idx++;
        }
    }

    private static string[] BuildIdToToken()
    {
        var vocab = VocabLazy.Value;
        var array = new string[vocab.Count];
        foreach (var kvp in vocab)
        {
            if (kvp.Value >= 0 && kvp.Value < array.Length)
            {
                array[kvp.Value] = kvp.Key;
            }
        }
        return array;
    }
}
