using System.Globalization;
using System.Text;
using System.Text.Json;
using Axora.Studio.Models;

namespace Axora.Studio.Services;

/// <summary>Schema-specific streaming writers and independent grammar readers. Streams remain caller-owned.</summary>
public static class FlashcardExportCodec
{
    public const long ArtifactByteLimit = 64L * 1024 * 1024;
    public const string TimestampPattern = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    private static readonly byte[] Bom = [0xef, 0xbb, 0xbf];
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public static void Write(Stream output, FlashcardExportSnapshot snapshot, FlashcardExportFormat format, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot); _ = ExportFileNameSanitizer.Extension(format);
        using var bounded = new ByteBudgetStream(output, leaveOpen: true);
        token.ThrowIfCancellationRequested();
        if (format == FlashcardExportFormat.AxoraJson) WriteJson(bounded, snapshot, token);
        else
        {
            if (format == FlashcardExportFormat.Csv) bounded.Write(Bom);
            using var writer = new StreamWriter(bounded, Utf8, 65536, leaveOpen: true);
            writer.Write(format == FlashcardExportFormat.Csv ? "Front,Back,Difficulty,IntervalDays\r\n"
                : "#separator:Tab\n#html:false\n#columns:Front\tBack\n");
            foreach (var card in snapshot.Deck.Cards)
            {
                token.ThrowIfCancellationRequested();
                char separator = format == FlashcardExportFormat.Csv ? ',' : '\t';
                Quoted(writer, card.Front); writer.Write(separator); Quoted(writer, card.Back);
                if (format == FlashcardExportFormat.Csv)
                {
                    writer.Write(','); Quoted(writer, card.Difficulty.ToString()); writer.Write(',');
                    Quoted(writer, card.IntervalDays.ToString(CultureInfo.InvariantCulture));
                }
                writer.Write(format == FlashcardExportFormat.Csv ? "\r\n" : "\n");
                writer.Flush();
            }
            writer.Flush();
        }
        token.ThrowIfCancellationRequested();
    }
    private static void Quoted(TextWriter writer, string value)
    {
        writer.Write('"');
        foreach (char c in value) { writer.Write(c); if (c == '"') writer.Write('"'); }
        writer.Write('"');
    }
    private static string Stamp(DateTimeOffset time) => time.ToString(TimestampPattern, CultureInfo.InvariantCulture);
    private static void NullableStamp(Utf8JsonWriter writer, string property, DateTimeOffset? time)
    { if (time is { } value) writer.WriteString(property, Stamp(value)); else writer.WriteNull(property); }
    private static void WriteJson(Stream output, FlashcardExportSnapshot snapshot, CancellationToken token)
    {
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject(); writer.WriteString("format", FlashcardExportSnapshot.Format);
        writer.WriteNumber("schemaVersion", FlashcardExportSnapshot.SchemaVersion); writer.WriteString("exportedAtUtc", Stamp(snapshot.ExportedAtUtc));
        writer.WriteStartObject("deck"); writer.WriteString("deckId", snapshot.Deck.DeckId);
        writer.WriteString("title", snapshot.Deck.Title); writer.WriteString("description", snapshot.Deck.Description);
        NullableStamp(writer, "lastStudiedUtc", snapshot.Deck.LastStudiedUtc); writer.WriteStartArray("cards"); writer.Flush();
        foreach (var card in snapshot.Deck.Cards)
        {
            token.ThrowIfCancellationRequested(); writer.WriteStartObject();
            writer.WriteString("cardId", card.CardId); writer.WriteString("front", card.Front); writer.WriteString("back", card.Back);
            writer.WriteString("difficulty", card.Difficulty.ToString()); writer.WriteNumber("reviewCount", card.ReviewCount);
            writer.WriteNumber("easeFactor", card.EaseFactor); writer.WriteNumber("intervalDays", card.IntervalDays);
            NullableStamp(writer, "lastReviewedUtc", card.LastReviewedUtc); NullableStamp(writer, "nextReviewUtc", card.NextReviewUtc);
            writer.WriteEndObject(); writer.Flush();
        }
        writer.WriteEndArray(); writer.WriteEndObject(); writer.WriteEndObject(); writer.Flush();
    }
    public static void Validate(Stream input, FlashcardExportSnapshot snapshot, FlashcardExportFormat format, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot); _ = ExportFileNameSanitizer.Extension(format);
        using var bounded = new ByteBudgetStream(input, leaveOpen: true);
        if (format == FlashcardExportFormat.AxoraJson) ValidateJson(bounded, snapshot, token);
        else
        {
            if (format == FlashcardExportFormat.Csv)
                foreach (byte value in Bom) Require(bounded.ReadByte() == value, "CSV BOM");
            using var reader = new StreamReader(bounded, Utf8, detectEncodingFromByteOrderMarks: false, 65536, leaveOpen: true);
            Expect(reader, format == FlashcardExportFormat.Csv ? "Front,Back,Difficulty,IntervalDays\r\n"
                : "#separator:Tab\n#html:false\n#columns:Front\tBack\n");
            foreach (var card in snapshot.Deck.Cards)
            {
                token.ThrowIfCancellationRequested(); char delimiter = format == FlashcardExportFormat.Csv ? ',' : '\t';
                Require(ReadQuoted(reader) == card.Front, "Front mismatch"); Require(reader.Read() == delimiter, "Field separator");
                Require(ReadQuoted(reader) == card.Back, "Back mismatch");
                if (format == FlashcardExportFormat.Csv)
                {
                    Require(reader.Read() == ',', "Field separator"); Require(ReadQuoted(reader) == card.Difficulty.ToString(), "Difficulty mismatch");
                    Require(reader.Read() == ',', "Field separator");
                    Require(ReadQuoted(reader) == card.IntervalDays.ToString(CultureInfo.InvariantCulture), "Interval mismatch");
                    Require(reader.Read() == '\r', "CRLF record separator");
                }
                Require(reader.Read() == '\n', "Record separator");
            }
            Require(reader.Read() == -1, "Extra record/data");
        }
        token.ThrowIfCancellationRequested();
    }
    private static void Expect(TextReader reader, string text)
    { foreach (char c in text) Require(reader.Read() == c, "Invalid header"); }
    private static string ReadQuoted(TextReader reader)
    {
        Require(reader.Read() == '"', "Quoted field required"); var value = new StringBuilder();
        while (true)
        {
            int c = reader.Read(); Require(c >= 0, "Unterminated quoted field");
            if (c == '"') { if (reader.Peek() != '"') break; reader.Read(); }
            Require(value.Length < FlashcardLimits.FieldScalars * 2, "Oversized field"); value.Append((char)c);
        }
        return value.ToString();
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidDataException(reason); }

    private static void ValidateJson(Stream input, FlashcardExportSnapshot expected, CancellationToken token)
    {
        var r = new JsonTokens(input, token); r.Expect(JsonTokenType.StartObject);
        var properties = new HashSet<string>(StringComparer.Ordinal);
        while (r.Next() != JsonTokenType.EndObject)
        {
            string name = r.Property(properties);
            switch (name)
            {
                case "format": Require(r.String() == FlashcardExportSnapshot.Format, "Invalid format"); break;
                case "schemaVersion": Require(r.Integer() == 1, "Invalid schema"); break;
                case "exportedAtUtc": Require(r.Time(false) == expected.ExportedAtUtc, "Export timestamp mismatch"); break;
                case "deck": ReadDeck(r, expected.Deck); break;
                default: throw new InvalidDataException("Unknown envelope property");
            }
        }
        Require(properties.SetEquals(["format", "schemaVersion", "exportedAtUtc", "deck"]), "Missing envelope property");
        Require(r.Next() == JsonTokenType.None, "Extra JSON");
    }
    private static void ReadDeck(JsonTokens r, FlashcardExportDeck expected)
    {
        r.Expect(JsonTokenType.StartObject); var properties = new HashSet<string>(StringComparer.Ordinal);
        while (r.Next() != JsonTokenType.EndObject)
        {
            switch (r.Property(properties))
            {
                case "deckId": Require(r.String() == expected.DeckId, "Deck ID mismatch"); break;
                case "title": Require(r.String() == expected.Title, "Title mismatch"); break;
                case "description": Require(r.String() == expected.Description, "Description mismatch"); break;
                case "lastStudiedUtc": Require(r.Time(true) == expected.LastStudiedUtc, "Study timestamp mismatch"); break;
                case "cards":
                    r.Expect(JsonTokenType.StartArray); int index = 0;
                    while (r.Next() != JsonTokenType.EndArray)
                    { Require(index < expected.Cards.Count, "Extra card"); ReadCard(r, expected.Cards[index++]); }
                    Require(index == expected.Cards.Count, "Missing card"); break;
                default: throw new InvalidDataException("Unknown deck property");
            }
        }
        Require(properties.SetEquals(["deckId", "title", "description", "lastStudiedUtc", "cards"]), "Missing deck property");
    }
    private static void ReadCard(JsonTokens r, FlashcardExportCard expected)
    {
        Require(r.Type == JsonTokenType.StartObject, "Card object required"); var properties = new HashSet<string>(StringComparer.Ordinal);
        string? id = null, front = null, back = null; CardDifficulty difficulty = (CardDifficulty)(-1);
        int reviews = -1, interval = 0; double ease = double.NaN; DateTimeOffset? last = null, next = null;
        while (r.Next() != JsonTokenType.EndObject)
        {
            switch (r.Property(properties))
            {
                case "cardId": id = r.String(); break;
                case "front": front = r.String(); break;
                case "back": back = r.String(); break;
                case "difficulty": difficulty = r.String() switch { "Easy" => CardDifficulty.Easy, "Medium" => CardDifficulty.Medium,
                    "Hard" => CardDifficulty.Hard, _ => throw new InvalidDataException("Invalid difficulty") }; break;
                case "reviewCount": reviews = r.Integer(); break;
                case "easeFactor": ease = r.Number(); break;
                case "intervalDays": interval = r.Integer(); break;
                case "lastReviewedUtc": last = r.Time(true); break;
                case "nextReviewUtc": next = r.Time(true); break;
                default: throw new InvalidDataException("Unknown card property");
            }
        }
        Require(properties.SetEquals(["cardId", "front", "back", "difficulty", "reviewCount", "easeFactor", "intervalDays", "lastReviewedUtc", "nextReviewUtc"]), "Missing card property");
        var actual = new FlashcardExportCard(id!, front!, back!, difficulty, reviews, ease, interval, last, next);
        actual.Validate(); Require(actual == expected, "Card mismatch");
    }

    /// <summary>Incremental Utf8JsonReader with at most one bounded token buffered; never a whole-document DOM.</summary>
    private sealed class JsonTokens(Stream input, CancellationToken token)
    {
        private readonly byte[] _buffer = new byte[131072];
        private int _start, _end; private bool _final;
        private JsonReaderState _state = new(new JsonReaderOptions { MaxDepth = 8 });
        public JsonTokenType Type { get; private set; }
        private string? _text;
        public JsonTokenType Next()
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var reader = new Utf8JsonReader(_buffer.AsSpan(_start, _end - _start), _final, _state);
                if (reader.Read())
                {
                    Type = reader.TokenType;
                    _text = Type is JsonTokenType.String or JsonTokenType.PropertyName ? reader.GetString()
                        : Type == JsonTokenType.Number ? Utf8.GetString(reader.ValueSpan) : null;
                    _start += checked((int)reader.BytesConsumed); _state = reader.CurrentState; return Type;
                }
                _start += checked((int)reader.BytesConsumed); _state = reader.CurrentState;
                if (_final) return Type = JsonTokenType.None;
                int remaining = _end - _start; Require(remaining < _buffer.Length, "Oversized JSON token");
                _buffer.AsSpan(_start, remaining).CopyTo(_buffer); _start = 0; _end = remaining;
                int read = input.Read(_buffer, _end, _buffer.Length - _end); _end += read; _final = read == 0;
            }
        }
        public void Expect(JsonTokenType type) => Require(Next() == type, "JSON type mismatch");
        public string Property(HashSet<string> properties)
        {
            Require(Type == JsonTokenType.PropertyName && _text is not null, "Property required");
            Require(properties.Add(_text!), "Duplicate property"); return _text!;
        }
        public string String() { Expect(JsonTokenType.String); return _text!; }
        public int Integer()
        {
            Expect(JsonTokenType.Number);
            Require(int.TryParse(_text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value), "Integer required"); return value;
        }
        public double Number()
        {
            Expect(JsonTokenType.Number);
            Require(double.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value), "Finite number required"); return value;
        }
        public DateTimeOffset? Time(bool nullable)
        {
            var type = Next(); if (nullable && type == JsonTokenType.Null) return null;
            Require(type == JsonTokenType.String && DateTimeOffset.TryParseExact(_text, TimestampPattern, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _), "Invalid UTC timestamp");
            var value = DateTimeOffset.ParseExact(_text!, TimestampPattern, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            FlashcardLimits.Timestamp(value); return value;
        }
    }
    private sealed class ByteBudgetStream(Stream inner, bool leaveOpen) : Stream
    {
        private long _bytes;
        private void Count(int count) { _bytes = checked(_bytes + count); if (_bytes > ArtifactByteLimit) throw new InvalidDataException("Artifact exceeds 64 MiB."); }
        public override int Read(byte[] buffer, int offset, int count) { int n = inner.Read(buffer, offset, count); Count(n); return n; }
        public override int Read(Span<byte> buffer) { int n = inner.Read(buffer); Count(n); return n; }
        public override void Write(byte[] buffer, int offset, int count) { Count(count); inner.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Count(buffer.Length); inner.Write(buffer); }
        public override void Flush() => inner.Flush();
        protected override void Dispose(bool disposing) { if (disposing && !leaveOpen) inner.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => inner.CanRead; public override bool CanWrite => inner.CanWrite; public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => _bytes; set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
    }
}
