using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.Content;
using PdfSharpCore.Pdf.Content.Objects;

namespace Axora.Desktop.Services;

/// <summary>
/// Robust local PDF text extraction engine.
/// Parses PDF content streams, handles font CMaps (/ToUnicode), CID-keyed fonts,
/// Type0 composite fonts, kerning arrays (TJ), and standard text positioning operators.
/// </summary>
public static class PdfTextExtractor
{
    private sealed class FontInfo
    {
        public bool IsTwoByte { get; set; }
        public Dictionary<int, string> CMap { get; } = new();
    }

    private static readonly Regex HexTokenRegex = new(@"<([0-9A-Fa-f]+)>", RegexOptions.Compiled);

    /// <summary>
    /// Extracts plain text from a single PDF page with font CMap resolution.
    /// </summary>
    public static string ExtractTextFromPage(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var fonts = ExtractFontCMaps(page);
        var sb = new StringBuilder();

        CSequence content;
        try
        {
            content = ContentReader.ReadContent(page);
        }
        catch
        {
            return string.Empty;
        }

        if (content == null || content.Count == 0)
        {
            return string.Empty;
        }

        string? currentFontKey = null;

        for (int i = 0; i < content.Count; i++)
        {
            var item = content[i];
            if (item is not COperator op) continue;

            string opName = op.OpCode.Name;

            switch (opName)
            {
                case "Tf":
                    if (op.Operands.Count > 0)
                    {
                        currentFontKey = op.Operands[0].ToString();
                    }
                    break;

                case "Tj":
                    if (op.Operands.Count > 0 && op.Operands[0] is CString cStr)
                    {
                        DecodeString(cStr.Value, currentFontKey, fonts, sb);
                    }
                    break;

                case "'":
                    AppendNewlineIfNotEmpty(sb);
                    if (op.Operands.Count > 0 && op.Operands[0] is CString cStrSingle)
                    {
                        DecodeString(cStrSingle.Value, currentFontKey, fonts, sb);
                    }
                    break;

                case "\"":
                    AppendNewlineIfNotEmpty(sb);
                    if (op.Operands.Count >= 3 && op.Operands[2] is CString cStrDouble)
                    {
                        DecodeString(cStrDouble.Value, currentFontKey, fonts, sb);
                    }
                    break;

                case "TJ":
                    if (op.Operands.Count > 0 && op.Operands[0] is CArray cArray)
                    {
                        for (int a = 0; a < cArray.Count; a++)
                        {
                            var elem = cArray[a];
                            if (elem is CString cs)
                            {
                                DecodeString(cs.Value, currentFontKey, fonts, sb);
                            }
                            else if (elem is CReal cr && cr.Value <= -100.0)
                            {
                                AppendSpaceIfNotEmpty(sb);
                            }
                            else if (elem is CInteger ci && ci.Value <= -100)
                            {
                                AppendSpaceIfNotEmpty(sb);
                            }
                        }
                    }
                    break;

                case "Td":
                case "TD":
                    if (op.Operands.Count >= 2)
                    {
                        double ty = GetNumericValue(op.Operands[1]);
                        if (Math.Abs(ty) > 0.01)
                        {
                            AppendNewlineIfNotEmpty(sb);
                        }
                    }
                    break;

                case "T*":
                    AppendNewlineIfNotEmpty(sb);
                    break;

                case "ET":
                    AppendNewlineIfNotEmpty(sb);
                    break;
            }
        }

        return sb.ToString();
    }

    private static void AppendSpaceIfNotEmpty(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[^1] != ' ' && sb[^1] != '\n' && sb[^1] != '\r')
        {
            sb.Append(' ');
        }
    }

    private static void AppendNewlineIfNotEmpty(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[^1] != '\n')
        {
            sb.AppendLine();
        }
    }

    private static double GetNumericValue(CObject obj)
    {
        if (obj is CReal r) return r.Value;
        if (obj is CInteger integer) return integer.Value;
        return 0.0;
    }

    private static Dictionary<string, FontInfo> ExtractFontCMaps(PdfPage page)
    {
        var result = new Dictionary<string, FontInfo>(StringComparer.OrdinalIgnoreCase);

        var resources = page.Resources;
        if (resources == null) return result;

        var fontDict = resources.Elements.GetDictionary("/Font");
        if (fontDict == null) return result;

        foreach (var keyItem in fontDict.Elements.KeyNames)
        {
            string keyName = keyItem.ToString();
            var fontObj = fontDict.Elements.GetDictionary(keyName);
            if (fontObj == null) continue;

            var info = new FontInfo();

            string subtype = fontObj.Elements.GetName("/Subtype") ?? string.Empty;
            string encoding = fontObj.Elements.GetName("/Encoding") ?? string.Empty;

            info.IsTwoByte = subtype.Equals("/Type0", StringComparison.OrdinalIgnoreCase) ||
                             encoding.IndexOf("Identity", StringComparison.OrdinalIgnoreCase) >= 0;

            if (fontObj.Elements.ContainsKey("/ToUnicode"))
            {
                var toUnicode = fontObj.Elements.GetDictionary("/ToUnicode");
                if (toUnicode?.Stream?.UnfilteredValue != null)
                {
                    ParseToUnicodeCMap(toUnicode.Stream.UnfilteredValue, info);
                }
            }

            result[keyName] = info;
        }

        return result;
    }

    private static void ParseToUnicodeCMap(byte[] streamBytes, FontInfo info)
    {
        string cMapText;
        try
        {
            cMapText = Encoding.UTF8.GetString(streamBytes);
        }
        catch
        {
            return;
        }

        var lines = cMapText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        bool inBfRange = false;
        bool inBfChar = false;

        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.Contains("beginbfrange", StringComparison.OrdinalIgnoreCase))
            {
                inBfRange = true;
                continue;
            }
            if (trimmed.Contains("endbfrange", StringComparison.OrdinalIgnoreCase))
            {
                inBfRange = false;
                continue;
            }
            if (trimmed.Contains("beginbfchar", StringComparison.OrdinalIgnoreCase))
            {
                inBfChar = true;
                continue;
            }
            if (trimmed.Contains("endbfchar", StringComparison.OrdinalIgnoreCase))
            {
                inBfChar = false;
                continue;
            }

            if (inBfRange)
            {
                // Format A: <srcLo> <srcHi> <dstLo>
                // Format B: <srcLo> <srcHi> [ <dst1> <dst2> ... ]
                if (trimmed.Contains('['))
                {
                    ParseBfRangeWithArray(trimmed, info);
                }
                else
                {
                    var matches = HexTokenRegex.Matches(trimmed);
                    if (matches.Count >= 3)
                    {
                        try
                        {
                            int srcLo = Convert.ToInt32(matches[0].Groups[1].Value, 16);
                            int srcHi = Convert.ToInt32(matches[1].Groups[1].Value, 16);
                            int dstLo = Convert.ToInt32(matches[2].Groups[1].Value, 16);

                            if (srcLo > 255 || srcHi > 255)
                            {
                                info.IsTwoByte = true;
                            }

                            for (int c = srcLo; c <= srcHi; c++)
                            {
                                info.CMap[c] = char.ConvertFromUtf32(dstLo + (c - srcLo));
                            }
                        }
                        catch { /* skip malformed range */ }
                    }
                }
            }
            else if (inBfChar)
            {
                // Format: <src> <dstHex>
                var matches = HexTokenRegex.Matches(trimmed);
                if (matches.Count >= 2)
                {
                    try
                    {
                        int src = Convert.ToInt32(matches[0].Groups[1].Value, 16);
                        string dstHex = matches[1].Groups[1].Value;

                        if (src > 255)
                        {
                            info.IsTwoByte = true;
                        }

                        info.CMap[src] = DecodeHexToUnicodeString(dstHex);
                    }
                    catch { /* skip malformed char */ }
                }
            }
        }
    }

    private static void ParseBfRangeWithArray(string line, FontInfo info)
    {
        var matches = HexTokenRegex.Matches(line);
        if (matches.Count < 3) return;

        try
        {
            int srcLo = Convert.ToInt32(matches[0].Groups[1].Value, 16);
            int srcHi = Convert.ToInt32(matches[1].Groups[1].Value, 16);

            if (srcLo > 255 || srcHi > 255)
            {
                info.IsTwoByte = true;
            }

            int count = Math.Min(srcHi - srcLo + 1, matches.Count - 2);
            for (int i = 0; i < count; i++)
            {
                string dstHex = matches[2 + i].Groups[1].Value;
                info.CMap[srcLo + i] = DecodeHexToUnicodeString(dstHex);
            }
        }
        catch { /* skip malformed range */ }
    }

    private static string DecodeHexToUnicodeString(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return string.Empty;

        // Try parsing as sequence of UTF-16BE code units (4 hex chars each)
        if (hex.Length % 4 == 0)
        {
            var sb = new StringBuilder(hex.Length / 4);
            for (int i = 0; i < hex.Length; i += 4)
            {
                int codeUnit = Convert.ToInt32(hex.Substring(i, 4), 16);
                sb.Append((char)codeUnit);
            }
            return sb.ToString();
        }

        // Fallback to direct UTF-32 scalar value
        int u = Convert.ToInt32(hex, 16);
        return char.ConvertFromUtf32(u);
    }

    private static void DecodeString(
        string val,
        string? fontKey,
        Dictionary<string, FontInfo> fonts,
        StringBuilder sb)
    {
        if (string.IsNullOrEmpty(val)) return;

        // Check for UTF-16BE BOM (\u00FE\u00FF)
        if (val.Length >= 2 && val[0] == '\u00FE' && val[1] == '\u00FF')
        {
            for (int i = 2; i + 1 < val.Length; i += 2)
            {
                char ch = (char)(((int)val[i] << 8) | (int)val[i + 1]);
                sb.Append(ch);
            }
            return;
        }

        FontInfo? font = null;
        if (!string.IsNullOrEmpty(fontKey))
        {
            fonts.TryGetValue(fontKey, out font);
        }

        // If font not found by key, try the first font in the dictionary
        if (font == null && fonts.Count > 0)
        {
            foreach (var kvp in fonts)
            {
                font = kvp.Value;
                break;
            }
        }

        bool isTwoByte = font != null && (font.IsTwoByte || font.CMap.Count > 0);

        if (isTwoByte)
        {
            for (int i = 0; i < val.Length; i += 2)
            {
                if (i + 1 < val.Length)
                {
                    int cid = ((int)val[i] << 8) | (int)val[i + 1];
                    if (font != null && font.CMap.TryGetValue(cid, out var mapped))
                    {
                        sb.Append(mapped);
                    }
                    else if (cid >= 32 && cid <= 126)
                    {
                        sb.Append((char)cid);
                    }
                }
                else
                {
                    int cid = (int)val[i];
                    if (font != null && font.CMap.TryGetValue(cid, out var mapped))
                    {
                        sb.Append(mapped);
                    }
                    else if (cid >= 32 && cid <= 126)
                    {
                        sb.Append((char)cid);
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < val.Length; i++)
            {
                int code = (int)val[i] & 0xFF;
                if (font != null && font.CMap.TryGetValue(code, out var mapped))
                {
                    sb.Append(mapped);
                }
                else if (code >= 32 && code <= 126)
                {
                    sb.Append((char)code);
                }
                else if (code == '\t' || code == '\r' || code == '\n')
                {
                    sb.Append((char)code);
                }
            }
        }
    }
}
