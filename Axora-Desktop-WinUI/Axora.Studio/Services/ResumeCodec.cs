using System.Collections.Immutable;
using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Axora.Studio.Models;

namespace Axora.Studio.Services;

public sealed class ResumeCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly string[] Collections = ["Education", "Experiences", "SkillCategories", "Projects", "Certifications", "Achievements", "Responsibilities"];

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static bool IsId(string? value) => value is { Length: 32 } && value.Any(c => c != '0') && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    public static bool HasUnsupportedSchemaPrefix(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) bytes = bytes[3..];
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 16 });
        try
        {
            while (reader.Read())
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1 && reader.ValueTextEquals("SchemaVersion"))
                {
                    if (!reader.Read()) break;
                    if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int version) || version != 1) return true;
                }
        }
        catch (JsonException) { /* A malformed tail does not erase an already observed unsupported version. */ }
        return false;
    }

    public byte[] Encode(ResumeFile file)
    {
        Validate(file);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(file, Options);
        if (bytes.Length > ResumeLimits.JsonBytes) throw new InvalidDataException("Document exceeds JSON limit.");
        return bytes;
    }
    public ResumeFile Decode(ReadOnlySpan<byte> bytes)
    {
        var root = Parse(bytes);
        Require(root, "SchemaVersion", "DocumentId", "Revision", "CreatedUtc", "ModifiedUtc", "Document");
        if (root["SchemaVersion"]?.GetValue<int>() != 1) throw new InvalidDataException("Unsupported Resume schema.");
        RequireIds(root["Document"] as JsonObject ?? throw new InvalidDataException("Missing document."));
        var file = root.Deserialize<ResumeFile>(Options) ?? throw new InvalidDataException("Missing envelope.");
        Validate(file);
        return file;
    }
    public ResumeDocument DecodeLegacy(ReadOnlySpan<byte> capturedBytes)
    {
        var root = Parse(capturedBytes);
        // This is a dedicated persistence reader, never the legacy observable object graph.
        // A versioned envelope cannot be reinterpreted as unversioned legacy content.
        if (root.ContainsKey("SchemaVersion")) throw new InvalidDataException("Versioned input requires managed Open.");
        Require(root, "ResumeTitle", "Header");
        string sourceHash = Hash(capturedBytes);
        foreach (string collection in Collections)
        {
            if (!root.TryGetPropertyValue(collection, out var value)) continue;
            if (value is not JsonArray array || array.Count > ResumeLimits.EntriesPerCollection)
                throw new InvalidDataException("Invalid legacy collection.");
            for (int index = 0; index < array.Count; index++)
            {
                if (array[index] is not JsonObject item) throw new InvalidDataException("Invalid legacy entry.");
                if (!item.ContainsKey("Id"))
                    item["Id"] = Hash(StrictUtf8.GetBytes(sourceHash + ":" + collection + ":" + index.ToString(CultureInfo.InvariantCulture)))[..32].ToLowerInvariant();
                // Actual legacy serializers emit this computed helper. It is redundant, never authoritative.
                if (collection is "Experiences" or "Projects" or "Responsibilities" && item.Remove("BulletsLines", out var helper))
                {
                    if (helper is not JsonArray lines || lines.Any(x => x is null || x.GetValueKind() != JsonValueKind.String))
                        throw new InvalidDataException("Invalid computed bullet helper.");
                }
            }
        }
        var legacy = root.Deserialize<LegacyResumeData>(Options) ?? throw new InvalidDataException("Missing legacy document.");
        var document = legacy.ToDocument();
        ValidateDocument(document);
        return document;
    }
    private static JsonObject Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length > ResumeLimits.JsonBytes) throw new InvalidDataException("Invalid JSON size.");
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) bytes = bytes[3..];
        _ = StrictUtf8.GetString(bytes); // Reject malformed UTF-8, never replace it silently.
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 16 });
        var names = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) names.Push(new(StringComparer.Ordinal));
            if (reader.TokenType == JsonTokenType.EndObject) names.Pop();
            if (reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName)
            {
                ValidateEscapes(StrictUtf8.GetString(reader.ValueSpan));
                string text = reader.GetString()!;
                ValidateUnicode(text);
                if (reader.TokenType == JsonTokenType.PropertyName && !names.Peek().Add(text))
                    throw new InvalidDataException("Duplicate JSON property.");
            }
        }
        return JsonNode.Parse(bytes, documentOptions: new JsonDocumentOptions { MaxDepth = 16 }) as JsonObject
            ?? throw new InvalidDataException("JSON root must be an object.");
    }
    private static void ValidateEscapes(string raw)
    {
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] != '\\') continue;
            if (++i >= raw.Length) throw new InvalidDataException("Invalid string escape.");
            if (raw[i] != 'u') continue;
            int unit = int.Parse(raw.AsSpan(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            i += 4;
            if (unit is >= 0xDC00 and <= 0xDFFF) throw new InvalidDataException("Unpaired Unicode surrogate.");
            if (unit is >= 0xD800 and <= 0xDBFF)
            {
                if (i + 6 >= raw.Length || raw[i + 1] != '\\' || raw[i + 2] != 'u') throw new InvalidDataException("Unpaired Unicode surrogate.");
                int low = int.Parse(raw.AsSpan(i + 3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (low is < 0xDC00 or > 0xDFFF) throw new InvalidDataException("Unpaired Unicode surrogate.");
                i += 6;
            }
        }
    }
    private static void Require(JsonObject root, params string[] names)
    {
        if (names.Any(name => !root.ContainsKey(name) || root[name] is null)) throw new InvalidDataException("Required data missing.");
    }
    private static void RequireIds(JsonObject document)
    {
        foreach (string name in Collections)
            if (document[name] is JsonArray array)
                foreach (var item in array) Require(item as JsonObject ?? throw new InvalidDataException("Invalid entry."), "Id");
    }
    public void Validate(ResumeFile file)
    {
        if (file.SchemaVersion != 1 || !IsId(file.DocumentId) || file.Revision < 1 || file.CreatedUtc == default
            || file.ModifiedUtc < file.CreatedUtc || file.CreatedUtc.Offset != TimeSpan.Zero || file.ModifiedUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Invalid envelope identity/version/timestamps.");
        if (file.ImportSha256 is not null && !IsHash(file.ImportSha256)) throw new InvalidDataException("Invalid provenance.");
        if (file.RecoveredFromRevision is < 1 || file.TemplateVersion is < 1) throw new InvalidDataException("Invalid metadata.");
        if (file.TemplateId is not null && (file.TemplateId.Length > ResumeLimits.Scalar || string.IsNullOrWhiteSpace(file.TemplateId)))
            throw new InvalidDataException("Invalid presentation metadata.");
        if (file.TemplateId is not null) ValidateUnicode(file.TemplateId);
        ValidateDocument(file.Document);
    }
    public void ValidateDocument(ResumeDocument document)
    {
        if (document is null || document.Header is null || document.Formatting is null) throw new InvalidDataException("Missing semantic data.");
        if (document.Education.IsDefault || document.Experiences.IsDefault || document.SkillCategories.IsDefault || document.Projects.IsDefault
            || document.Certifications.IsDefault || document.Achievements.IsDefault || document.Responsibilities.IsDefault
            || document.SectionOrder.IsDefault || document.SectionOrder.Length != 8 || !document.SectionOrder.ToHashSet().SetEquals(Enum.GetValues<ResumeSection>()))
            throw new InvalidDataException("Invalid collections/order.");
        var preferences = document.Formatting;
        if (!Enum.IsDefined(preferences.TargetLength) || !Enum.IsDefined(preferences.FontFamily) || !Enum.IsDefined(preferences.SpacingMode)
            || !double.IsFinite(preferences.MarginInches) || preferences.MarginInches is < .1 or > 3
            || preferences.AccentHexColor is not { Length: 7 } color || color[0] != '#' || !color[1..].All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid formatting preferences.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int entries = 0, text = 0;
        Walk(document, 0, ref entries, ref text, ids);
        if (entries > ResumeLimits.EntriesTotal || text > ResumeLimits.AggregateText) throw new InvalidDataException("Document limit exceeded.");
    }
    private static void Walk(object value, int depth, ref int entries, ref int text, HashSet<string> ids)
    {
        if (depth > 8) throw new InvalidDataException("Semantic nesting exceeds limit.");
        foreach (var property in value.GetType().GetProperties())
        {
            object? child = property.GetValue(value);
            if (child is null) throw new InvalidDataException("Null semantic field.");
            if (child is string field)
            {
                ValidateUnicode(field);
                int ceiling = property.Name == "ResumeTitle" ? ResumeLimits.Title : property.Name is "Summary" or "BulletsRaw" or "Description"
                    ? ResumeLimits.Narrative : value is ResumeHeader || property.Name.EndsWith("Url", StringComparison.Ordinal) || property.Name is "ProjectLink" or "Link"
                    ? ResumeLimits.Contact : ResumeLimits.Scalar;
                if (field.Length > ceiling) throw new InvalidDataException("Field exceeds length limit.");
                text = checked(text + field.Length);
                if (property.Name == "Id" && (!IsId(field) || !ids.Add(field))) throw new InvalidDataException("Invalid/duplicate entry identity.");
            }
            else if (child is IEnumerable array)
            {
                int count = 0;
                foreach (object? item in array)
                {
                    if (++count > ResumeLimits.EntriesPerCollection || item is null) throw new InvalidDataException("Invalid/bounded collection.");
                    if (item.GetType().IsEnum) continue;
                    entries++;
                    Walk(item, depth + 1, ref entries, ref text, ids);
                }
            }
            else if (!child.GetType().IsValueType) Walk(child, depth + 1, ref entries, ref text, ids);
        }
    }
    // Dedicated legacy root DTO; shared nested records are persistence values, never observable models.
    private sealed record LegacyResumeData
    {
    public string ResumeTitle { get; init; } = "Untitled Resume";
    public ResumeHeader Header { get; init; } = new();
    public string Summary { get; init; } = "";
    public ResumePreferences Formatting { get; init; } = new();
    public ImmutableArray<ResumeEducation> Education { get; init; } = [];
    public ImmutableArray<ResumeExperience> Experiences { get; init; } = [];
    public ImmutableArray<ResumeSkill> SkillCategories { get; init; } = [];
    public ImmutableArray<ResumeProject> Projects { get; init; } = [];
    public ImmutableArray<ResumeCertification> Certifications { get; init; } = [];
    public ImmutableArray<ResumeAchievement> Achievements { get; init; } = [];
    public ImmutableArray<ResumeResponsibility> Responsibilities { get; init; } = [];
    public bool ShowSummary { get; init; } = true;
    public bool ShowEducation { get; init; } = true;
    public bool ShowExperience { get; init; } = true;
    public bool ShowSkills { get; init; } = true;
    public bool ShowProjects { get; init; } = true;
    public bool ShowCertifications { get; init; } = true;
    public bool ShowAchievements { get; init; } = true;
    public bool ShowResponsibilities { get; init; } = true;
    public ImmutableArray<ResumeSection> SectionOrder { get; init; } = [.. Enum.GetValues<ResumeSection>()];
        public ResumeDocument ToDocument() => new()
        {
            ResumeTitle = ResumeTitle,
            Header = Header,
            Summary = Summary,
            Formatting = Formatting,
            Education = Education,
            Experiences = Experiences,
            SkillCategories = SkillCategories,
            Projects = Projects,
            Certifications = Certifications,
            Achievements = Achievements,
            Responsibilities = Responsibilities,
            ShowSummary = ShowSummary,
            ShowEducation = ShowEducation,
            ShowExperience = ShowExperience,
            ShowSkills = ShowSkills,
            ShowProjects = ShowProjects,
            ShowCertifications = ShowCertifications,
            ShowAchievements = ShowAchievements,
            ShowResponsibilities = ShowResponsibilities,
            SectionOrder = SectionOrder,
        };
    }
    private static void ValidateUnicode(string field)
    {
        for (int i = 0; i < field.Length; i++)
            if (char.IsSurrogate(field[i]) && (!char.IsHighSurrogate(field[i]) || ++i >= field.Length || !char.IsLowSurrogate(field[i])))
                throw new InvalidDataException("Unpaired Unicode surrogate.");
    }
    public static bool SemanticallyEqual(ResumeDocument left, ResumeDocument right) =>
        JsonSerializer.SerializeToUtf8Bytes(left, Options).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, Options));
}
