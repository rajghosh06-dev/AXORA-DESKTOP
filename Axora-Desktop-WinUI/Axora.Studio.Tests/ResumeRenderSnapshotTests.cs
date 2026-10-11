using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Axora.Studio.Models;
using Axora.Studio.Models.ResumeRendering;
using Axora.Studio.Services;
using Axora.Studio.Services.ResumeRendering;

namespace Axora.Studio.Tests;

internal static class ResumeRenderSnapshotTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[] {
        "snapshot-fields", "identity-envelope", "identity-every-field", "identity-order-visibility", "deep-alias",
        "session-capture", "byte-ownership", "invalid-codec", "bullet-lines", "skill-items", "current-date",
        "adversarial-matrix", "unicode-distinction", "font-policy", "formatting-mappings", "canonical-schema",
        "golden-vectors", "culture-timezone", "domain-ambiguity", "max-newlines", "max-commas", "max-escaping",
        "collection-boundaries", "field-boundaries", "writer-byte-boundary", "pure-value-graph"
    });
    private static readonly DateTimeOffset Epoch = new(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static string Id(int value) => value.ToString("x32", CultureInfo.InvariantCulture);
    private static ResumeFile File(ResumeDocument? document = null) => new() { DocumentId = Id(900), Revision = 7,
        CreatedUtc = Epoch, ModifiedUtc = Epoch, Document = document ?? new() };
    private static ResumeRenderSnapshot Capture(ResumeDocument d) => ResumeSnapshotBuilder.Capture(File(d));
    private static ResumeCanonicalData Data(ResumeRenderSnapshot s) => ResumeRenderDataWriter.Write(s, ResumeRenderFontChoice.ExplicitEmbeddedSerifV1);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }
    private static void Failure(Checks c, Action action, ResumeRenderFailure failure, string name)
    {
        try { action(); c.That(false, name); }
        catch (ResumeRenderException ex) { c.That(ex.Failure == failure && ex.Message == ex.Code, name); }
    }
    private static ResumeDocument Full(string text = "SYNTHETIC") => new()
    {
        ResumeTitle = text + " title", Header = new() { FullName = text, ProfessionalTitle = text + " role", Email = text + "@example.invalid",
            Phone = text + " phone", Location = text + " location", LinkedIn = text + " label", LinkedInUrl = text + " linkedin",
            GitHub = text + " github label", GitHubUrl = text + " github", PortfolioUrl = text + " portfolio" }, Summary = text + " summary",
        Education = [new() { Id = Id(1), Institution = text + " institution", ScoreOrPercentage = text + " score", Degree = text + " degree", Specialization = text + " specialization", YearRange = text + " years" }],
        Experiences = [new() { Id = Id(2), Company = text + " company", RoleTitle = text + " role", Location = text + " location", StartDate = text + " start", EndDate = text + " end", IsCurrent = true, BulletsRaw = text + "\r\n- second\n\n", ProjectLink = text + " project" }],
        SkillCategories = [new() { Id = Id(3), CategoryName = text + " category", SkillsCsv = text + ", \tsecond, ," }],
        Projects = [new() { Id = Id(4), Title = text + " project", TechStack = text + " tech", DateRange = text + " dates", BulletsRaw = text + "\r* second", RepoUrl = text + " repo" }],
        Certifications = [new() { Id = Id(5), Title = text + " certificate", Issuer = text + " issuer", Date = text + " date", GradeOrScore = text + " grade", CredentialId = text + " credential", Description = text + " description", VerificationUrl = text + " verification" }],
        Achievements = [new() { Id = Id(6), Title = text + " achievement", Category = text + " category", Date = text + " date", Description = text + " description", Link = text + " link" }],
        Responsibilities = [new() { Id = Id(7), Role = text + " role", Organization = text + " organization", DateRange = text + " dates", BulletsRaw = text + "\n• second" }]
    };

    public static async Task RunAsync(Checks c)
    {
        await c.CaseAsync("snapshot-fields", () => Sync(() => {
            var source = Full(); var s = Capture(source);
            c.That(s.SnapshotSchemaVersion == 1 && s.DerivationVersion == 1 && s.DocumentId == Id(900) && s.Revision == 7, "Versioned captured identity");
            Compare(c, source, s.Document);
            c.That(s.Document.VisibleSectionOrder.SequenceEqual(source.SectionOrder), "All visible sections in saved order");
        }));
        await c.CaseAsync("identity-envelope", () => Sync(() => {
            var f = File(Full()); var baseline = ResumeSnapshotBuilder.Capture(f);
            foreach (var other in new[] { f with { CreatedUtc = Epoch.AddDays(-1), ModifiedUtc = Epoch.AddDays(1) },
                f with { TemplateId = "synthetic.alternate", TemplateVersion = 99 }, f with { TemplateId = "unpaired" },
                f with { ImportSha256 = new string('A', 64), RecoveredFromRevision = 3 } })
            {
                var s = ResumeSnapshotBuilder.Capture(other);
                c.That(s.SemanticSha256 == baseline.SemanticSha256 && s.SnapshotSha256 == baseline.SnapshotSha256, "Excluded envelope metadata leaves both identities unchanged");
            }
            foreach (var other in new[] { f with { DocumentId = Id(901) }, f with { Revision = 8 } })
            {
                var s = ResumeSnapshotBuilder.Capture(other);
                c.That(s.SemanticSha256 == baseline.SemanticSha256 && s.SnapshotSha256 != baseline.SnapshotSha256, "ID/revision bind snapshot only");
            }
        }));
        await c.CaseAsync("identity-every-field", () => Sync(() => {
            // Independent reflection enumerates ORIGINAL model properties only to detect omitted hash fields.
            var baseline = Capture(Full()); int count = 0;
            foreach (var mutation in Mutations())
            {
                var s = Capture(mutation);
                c.That(s.SemanticSha256 != baseline.SemanticSha256 && s.SnapshotSha256 != baseline.SnapshotSha256, "Every original string/boolean/enum/numeric semantic field binds both hashes"); count++;
            }
            c.That(count == 71, "All 71 original scalar fields independently enumerated for hash coverage");
        }));
        await c.CaseAsync("identity-order-visibility", () => Sync(() => {
            var d = Full() with { Education = [new() { Id = Id(21), Institution = "a" }, new() { Id = Id(22), Institution = "b" }], ShowProjects = false };
            var s = Capture(d);
            foreach (var v in new[] { d with { Education = [.. d.Education.Reverse()] }, d with { SectionOrder = [.. d.SectionOrder.Reverse()] },
                d with { ShowSummary = false }, d with { Projects = [d.Projects[0] with { Title = "hidden changed" }] } })
            { var changed = Capture(v); c.That(s.SemanticSha256 != changed.SemanticSha256 && s.SnapshotSha256 != changed.SnapshotSha256, "Order/visibility/hidden content binds both identities"); }
            var hidden = Capture(Hidden(d));
            c.That(hidden.Document.VisibleSectionOrder.IsEmpty && hidden.Document.Projects.Length == 1 && hidden.Document.SectionOrder.Length == 8, "Visibility never removes semantic data");
            foreach (string name in new[] { "Education", "Experiences", "SkillCategories", "Projects", "Certifications", "Achievements", "Responsibilities" })
            {
                var ordered = Collection(name, 2);
                var original = ((IEnumerable)typeof(ResumeDocument).GetProperty(name)!.GetValue(ordered)!).Cast<object>().ToArray();
                var reversed = SetCollection(new(), name, original.Reverse().ToArray());
                var before = Capture(ordered); var after = Capture(reversed);
                c.That(before.SemanticSha256 != after.SemanticSha256 && before.SnapshotSha256 != after.SnapshotSha256, "Every collection ordering changes identity");
                var copied = ((IEnumerable)typeof(ResumeSnapshotDocument).GetProperty(name)!.GetValue(after.Document)!).Cast<object>();
                c.That(copied.Select(x => x.GetType().GetProperty("Id")!.GetValue(x)).SequenceEqual(original.Reverse().Select(x => x.GetType().GetProperty("Id")!.GetValue(x))), "Every collection preserves reversed item ordering");
            }
        }));
        await c.CaseAsync("deep-alias", () => Sync(() => {
            var source = Full(); var snapshot = Capture(source); var oldHash = snapshot.SnapshotSha256;
            c.That(!ReferenceEquals(source.Header, snapshot.Document.Header) && !ReferenceEquals(source.Formatting, snapshot.Document.Formatting), "Nested root records are B2-owned copies");
            foreach (string name in new[] { "Education", "Experiences", "SkillCategories", "Projects", "Certifications", "Achievements", "Responsibilities" })
            {
                var original = ((IEnumerable)typeof(ResumeDocument).GetProperty(name)!.GetValue(source)!).Cast<object>().First();
                var copied = ((IEnumerable)typeof(ResumeSnapshotDocument).GetProperty(name)!.GetValue(snapshot.Document)!).Cast<object>().First();
                c.That(!ReferenceEquals(original, copied), "Collection item record is independently owned");
                var property = original.GetType().GetProperties().First(p => p.PropertyType == typeof(string) && p.Name != "Id");
                property.SetValue(original, "SYNTHETIC reflection alias attack");
            }
            typeof(ResumeHeader).GetProperty("FullName")!.SetValue(source.Header, "SYNTHETIC changed header");
            source = source with { Education = [], Formatting = new() { MarginInches = 3 } };
            c.That(snapshot.Document.Header.FullName == "SYNTHETIC" && snapshot.Document.Education[0].Institution == "SYNTHETIC institution" && snapshot.SnapshotSha256 == oldHash, "Source record/array replacements and alias mutation cannot alter snapshot");
            c.That(Hash(snapshot.CopySnapshotBytes()) == oldHash, "Captured bytes still bind immutable values");
            ResumeEducation[] backing = [new() { Id = Id(1), Institution = "SYNTHETIC original backing" }];
            var sectionBacking = Enum.GetValues<ResumeSection>();
            var wrapped = Capture(new() { Education = ImmutableCollectionsMarshal.AsImmutableArray(backing), SectionOrder = ImmutableCollectionsMarshal.AsImmutableArray(sectionBacking) });
            backing[0] = new() { Id = Id(2), Institution = "SYNTHETIC replacement" }; Array.Reverse(sectionBacking);
            c.That(wrapped.Document.Education[0].Id == Id(1) && wrapped.Document.SectionOrder[0] == ResumeSection.Summary, "Caller-owned original backing arrays cannot alias snapshot collections");
        }));
        await c.CaseAsync("session-capture", async () => {
            string root = Path.Combine(Path.GetTempPath(), "AXORA-B2A-SYNTHETIC-UNCREATED-" + Guid.NewGuid().ToString("N"));
            var codec = new ResumeCodec(); var store = new ResumeStore(new StudioPathService(root), codec);
            var session = new ResumeSession(store, codec, new ResumeFilePublisher(store, codec));
            await session.NewAsync(() => Task.FromResult(ResumeDeparture.Discard)); session.Edit(Full());
            var captured = session.Current!; var snapshot = ResumeSnapshotBuilder.Capture(captured);
            session.Edit(Full() with { Summary = "SYNTHETIC later edit" });
            c.That(session.Current!.Revision > captured.Revision && snapshot.Revision == captured.Revision && snapshot.Document.Summary == captured.Document.Summary, "Live session edits cannot change captured revision/data");
            c.That(!Directory.Exists(root) && store.Enumerations == 0, "Headless capture never creates/enumerates a store");
        });
        await c.CaseAsync("byte-ownership", () => Sync(() => {
            var s = Capture(Full()); var r = Data(s);
            foreach (var pair in new[] { (s.CopySemanticBytes(), s.SemanticSha256), (s.CopySnapshotBytes(), s.SnapshotSha256), (r.CopyBytes(), r.Sha256) })
            { pair.Item1[0] ^= 1; c.That(Hash(pair.Item1) != pair.Item2, "Caller can mutate its own copy"); }
            c.That(Hash(s.CopySemanticBytes()) == s.SemanticSha256 && Hash(s.CopySnapshotBytes()) == s.SnapshotSha256 && Hash(r.CopyBytes()) == r.Sha256, "Returned buffers cannot mutate canonical ownership");
            c.That(!ReferenceEquals(r.CopyBytes(), r.CopyBytes()), "Each canonical-data read returns a fresh buffer");
        }));
        await c.CaseAsync("invalid-codec", () => Sync(() => {
            var f = File();
            var invalid = new[] { f with { SchemaVersion = 2 }, f with { DocumentId = "../bad" }, f with { Revision = 0 },
                f with { CreatedUtc = default }, f with { ModifiedUtc = Epoch.AddDays(-1) }, f with { CreatedUtc = Epoch.ToOffset(TimeSpan.FromHours(1)) },
                f with { TemplateVersion = 0 }, f with { TemplateId = " " }, f with { ImportSha256 = "bad" }, f with { RecoveredFromRevision = 0 },
                f with { Document = null! }, File(new() { Header = null! }), File(new() { Formatting = null! }),
                File(new() { SectionOrder = [] }), File(new() { Education = default }), File(new() { Projects = [null!] }),
                File(new() { Summary = "\uD800" }), File(new() { Formatting = new() { MarginInches = double.NaN } }),
                File(new() { Formatting = new() { FontFamily = (ResumeFontPreference)99 } }), File(new() { Formatting = new() { AccentHexColor = "#GG0000" } }),
                File(new() { Education = [new() { Id = Id(1) }], Projects = [new() { Id = Id(1) }] }) };
            Failure(c, () => ResumeSnapshotBuilder.Capture(null!), ResumeRenderFailure.InvalidResumeInput, "Null input is typed invalid");
            foreach (var bad in invalid)
            {
                bool codecRejected = false; try { new ResumeCodec().Validate(bad); } catch (InvalidDataException) { codecRejected = true; }
                c.That(codecRejected, "Existing codec is validation authority");
                Failure(c, () => ResumeSnapshotBuilder.Capture(bad), ResumeRenderFailure.InvalidResumeInput, "Invalid codec input yields safe typed failure");
            }
        }));
        await c.CaseAsync("bullet-lines", () => Sync(() => {
            string raw = "\r\n- one\r* two\n• three\r\n1. four\n```\n \t\n";
            var d = Full() with { Experiences = [new() { Id = Id(2), BulletsRaw = raw }], Projects = [new() { Id = Id(4), BulletsRaw = raw }], Responsibilities = [new() { Id = Id(7), BulletsRaw = "" }] };
            var s = Capture(d); string[] expected = ["", "- one", "* two", "• three", "1. four", "```", " \t", ""];
            c.That(s.Document.Experiences[0].BulletLines.SequenceEqual(expected) && s.Document.Projects[0].BulletLines.SequenceEqual(expected), "CRLF/CR/LF retain markers, empty and whitespace tokens");
            c.That(s.Document.Experiences[0].BulletsRaw == raw && s.Document.Responsibilities[0].BulletLines.SequenceEqual(new[] { "" }), "Raw and empty bullet fields retained");
        }));
        await c.CaseAsync("skill-items", () => Sync(() => {
            string raw = " \tfirst\t ,,,\"quoted,split\",\n next ,\u00A0wide\u00A0, #read(\"x\"),";
            var s = Capture(Full() with { SkillCategories = [new() { Id = Id(3), SkillsCsv = raw }] });
            c.That(s.Document.SkillCategories[0].SkillsCsv == raw, "Raw CSV retained");
            c.That(s.Document.SkillCategories[0].Entries.SequenceEqual(new[] { "first", "", "", "\"quoted", "split\"", "\n next", "\u00A0wide\u00A0", "#read(\"x\")", "" }), "Comma-only split and ASCII space/TAB edge trim");
        }));
        await c.CaseAsync("current-date", () => Sync(() => {
            var d = Full(); var s = Capture(d); var noCurrent = Capture(d with { Experiences = [d.Experiences[0] with { IsCurrent = false }] });
            c.That(s.Document.Experiences[0].DisplayEndDate == "Present" && s.Document.Experiences[0].EndDate == d.Experiences[0].EndDate, "Current display token retains semantic EndDate");
            c.That(noCurrent.Document.Experiences[0].DisplayEndDate == d.Experiences[0].EndDate, "Noncurrent dates are literal");
            c.That(Capture(d with { Experiences = [d.Experiences[0] with { EndDate = "hidden end changed" }] }).SemanticSha256 != s.SemanticSha256, "Superseded raw EndDate still binds identity");
        }));
        await c.CaseAsync("adversarial-matrix", () => Sync(() => {
            string[] payloads = ["#read(\"../outside\")", "#include \"x\"", "#import \"@preview/x:1.0.0\"", "#eval(\"panic()\")", "#{a: 1}",
                "\"\\/{}[]`*_", "```\r\nline\rnext\n", "\0\u0001\u001F", "\u202Eabc\u2066\u2069", "\u200B\u200C\u200D", "नमस्ते", "🙂🚀e\u0301", "https://example.invalid/a?x=1",
                "file:///C:/private", "javascript:alert(1)", "data:text/plain,x", "C:\\SYNTHETIC\\resume", "\\\\server\\share", "\\\\?\\C:\\device", "..\\..\\outside", "@preview/package:0.0.1"];
            foreach (string payload in payloads)
            {
                var d = Full(payload); var s = Capture(d); Compare(c, d, s.Document);
                using var parsed = JsonDocument.Parse(Data(s).CopyBytes());
                var node = parsed.RootElement.GetProperty("document");
                c.That(node.GetProperty("header").GetProperty("fullName").GetString() == payload && node.GetProperty("summary").GetString() == d.Summary, "Adversarial header/summary parse back as literal data");
                CompareJson(c, d, node);
            }
            string longToken = new('x', ResumeLimits.Narrative);
            var longSnapshot = Capture(new() { Summary = longToken });
            using var longJson = JsonDocument.Parse(Data(longSnapshot).CopyBytes());
            c.That(longJson.RootElement.GetProperty("document").GetProperty("summary").GetString() == longToken, "16,384-character unbroken token is not sampled/truncated");
        }));
        await c.CaseAsync("unicode-distinction", () => Sync(() => {
            foreach (var pair in new[] { ("é", "e\u0301"), ("a\r\nb", "a\nb"), ("x", "x\0"), ("x", "x\u200D"), ("x", "\u202Ex"), ("x", " x ") })
            {
                var a = Capture(new() { Summary = pair.Item1 }); var b = Capture(new() { Summary = pair.Item2 });
                c.That(a.SemanticSha256 != b.SemanticSha256 && a.SnapshotSha256 != b.SnapshotSha256, "Raw normalization/control/newline distinctions remain hashed");
            }
        }));
        await c.CaseAsync("font-policy", () => Sync(() => {
            foreach (var font in Enum.GetValues<ResumeFontPreference>())
            {
                var s = Capture(new() { Formatting = new() { FontFamily = font } });
                Failure(c, () => ResumeRenderFormatting.Resolve(s), ResumeRenderFailure.UnsupportedFormattingPreference, "Default saved family availability is unsupported");
                Failure(c, () => ResumeRenderDataWriter.Write(s), ResumeRenderFailure.UnsupportedFormattingPreference, "Render data cannot silently opt into alternate font");
                var f = ResumeRenderFormatting.Resolve(s, ResumeRenderFontChoice.ExplicitEmbeddedSerifV1);
                c.That(f.RequestedFont == font && f.IntendedEffectiveFamily == "Libertinus Serif" && f.RequiresFontResolutionProof, "Explicit alternative records intention, not availability");
                c.That(s.Document.Formatting.FontFamily == font, "Render-only resolution leaves saved preference intact");
            }
            Failure(c, () => ResumeRenderFormatting.Resolve(Capture(new()), (ResumeRenderFontChoice)99), ResumeRenderFailure.InvalidFormattingSelection, "Unknown finite choice rejected");
        }));
        await c.CaseAsync("formatting-mappings", () => Sync(() => {
            int?[] targets = [1, 2, 3, null];
            foreach (var target in Enum.GetValues<ResumeTargetLength>())
            foreach (var density in Enum.GetValues<ResumeDensity>())
            foreach (double margin in new[] { .1, .65, Math.BitIncrement(.65), 3 })
            foreach (bool enabled in new[] { false, true })
            {
                var s = Capture(new() { Formatting = new() { TargetLength = target, SpacingMode = density, MarginInches = margin,
                    ShowDividers = enabled, CenterHeader = !enabled, UppercaseSectionTitles = enabled, AccentHexColor = "#aB09Ff" } });
                var f = ResumeRenderFormatting.Resolve(s, ResumeRenderFontChoice.ExplicitEmbeddedSerifV1);
                (double body, double leading, double gap) expected = density switch { ResumeDensity.Compact => (10, .55, .35), ResumeDensity.Standard => (11, .65, .5), _ => (12, .8, .7) };
                c.That(f.TargetPages == targets[(int)target] && (f.BodyPoints, f.LeadingEm, f.ItemGapEm) == expected && f.MarginInches == margin,
                    "All target/density/exact-margin pure mappings");
                c.That(f.ShowDividers == enabled && f.CenterHeader == !enabled && f.UppercaseSectionTitles == enabled && (f.AccentRed, f.AccentGreen, f.AccentBlue) == ((byte)171, (byte)9, (byte)255), "Booleans and mixed-case RGB parse exactly");
                using var json = JsonDocument.Parse(Data(s).CopyBytes());
                c.That(BitConverter.DoubleToInt64Bits(json.RootElement.GetProperty("document").GetProperty("formatting").GetProperty("marginInches").GetDouble()) == BitConverter.DoubleToInt64Bits(margin), "Canonical numbers retain exact validated margin bits");
            }
        }));
        await c.CaseAsync("canonical-schema", () => Sync(() => {
            var s = Capture(Full("नमस्ते\0\r\n🙂")); var data = Data(s); byte[] bytes = data.CopyBytes();
            c.That(bytes.All(b => b < 128) && bytes[0] == '{' && bytes[^1] == '}' && !bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }), "Canonical JSON is strict ASCII subset of UTF-8 without BOM");
            string text = Encoding.UTF8.GetString(bytes);
            c.That(text.Contains("\\u0000\\u000D\\u000A\\uD83D\\uDE42", StringComparison.Ordinal), "Fixed uppercase UTF-16 escape contract");
            using var parsed = JsonDocument.Parse(bytes); var root = parsed.RootElement;
            c.That(root.GetProperty("renderDataSchemaVersion").GetInt32() == 1 && root.GetProperty("expectedContentPolicyVersion").GetInt32() == 1 && root.GetProperty("snapshotSha256").GetString() == s.SnapshotSha256, "Render data binds schema, policy and captured identity");
            c.That(data.ByteLength == bytes.Length && data.Sha256 == Hash(bytes), "Owned byte length/hash are actual output identity");
            string[] keys = ["domain", "renderDataSchemaVersion", "expectedContentPolicyVersion", "snapshotSchemaVersion", "derivationVersion", "documentId", "revision", "semanticSha256", "snapshotSha256", "formattingIntent", "document"];
            c.That(root.EnumerateObject().Select(x => x.Name).SequenceEqual(keys), "Envelope property order fixed; no source/path/action fields");
        }));
        await c.CaseAsync("golden-vectors", () => Sync(() => {
            foreach (var vector in Vectors())
            {
                var s = Capture(vector.Value); var data = Data(s);
                Console.WriteLine("B2A-VECTOR " + JsonSerializer.Serialize(new { name = vector.Key, semantic = s.SemanticSha256, snapshot = s.SnapshotSha256, renderData = data.Sha256 }));
                var expected = Goldens[vector.Key];
                c.That(s.SemanticSha256 == expected[0] && s.SnapshotSha256 == expected[1] && data.Sha256 == expected[2], "Frozen independent golden bytes/hash triple: " + vector.Key);
                if (vector.Key == "minimal") c.That(s.CopySemanticBytes().SequenceEqual(Encoding.UTF8.GetBytes(MinimalSemanticJson)), "Explicit minimal golden byte vector");
            }
        }));
        await c.CaseAsync("culture-timezone", () => Sync(() => {
            var oldCulture = CultureInfo.CurrentCulture; var oldUi = CultureInfo.CurrentUICulture;
            var baseline = Capture(Full()); var data = Data(baseline).CopyBytes();
            try
            {
                foreach (var name in new[] { "", "en-US", "de-DE", "hi-IN" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name); CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                    var s = Capture(Full());
                    c.That(s.CopySemanticBytes().SequenceEqual(baseline.CopySemanticBytes()) && s.CopySnapshotBytes().SequenceEqual(baseline.CopySnapshotBytes()) && Data(s).CopyBytes().SequenceEqual(data), "Canonical bytes independent of per-thread culture");
                }
                foreach (var offset in new[] { TimeSpan.FromHours(-8), TimeSpan.FromHours(5.5), TimeSpan.FromHours(14) })
                {
                    var utc = Epoch.ToOffset(offset).ToUniversalTime(); var s = ResumeSnapshotBuilder.Capture(File(Full()) with { CreatedUtc = utc, ModifiedUtc = utc.AddDays(30) });
                    c.That(s.SnapshotSha256 == baseline.SnapshotSha256, "Local display timezone/date never enters canonical identity");
                }
            }
            finally { CultureInfo.CurrentCulture = oldCulture; CultureInfo.CurrentUICulture = oldUi; }
        }));
        await c.CaseAsync("domain-ambiguity", () => Sync(() => {
            var pairs = new[] {
                (new ResumeDocument { Header = new() { FullName = "ab", Location = "c" } }, new ResumeDocument { Header = new() { FullName = "a", Location = "bc" } }),
                (new ResumeDocument { Summary = "summary" }, new ResumeDocument { ResumeTitle = "summary", Summary = "" }),
                (new ResumeDocument { Summary = "onePage" }, new ResumeDocument { Formatting = new() { TargetLength = ResumeTargetLength.TwoPages } }),
                (new ResumeDocument { SkillCategories = [new() { Id = Id(1), SkillsCsv = "a,b" }] }, new ResumeDocument { SkillCategories = [new() { Id = Id(1), SkillsCsv = "a, b" }] }) };
            foreach (var (left, right) in pairs) c.That(Capture(left).SemanticSha256 != Capture(right).SemanticSha256, "Typed/property-delimited domains prevent concatenation ambiguity");
            var s = Capture(new()); c.That(s.SemanticSha256 != s.SnapshotSha256 && s.SnapshotSha256 != Data(s).Sha256, "Semantic/snapshot/render domains separated");
            Failure(c, () => Capture(new() { Summary = null! }), ResumeRenderFailure.InvalidResumeInput, "Null is rejected, never coerced to empty");
        }));
        foreach (var mode in new[] { "newlines", "commas", "escaping" })
            await c.CaseAsync("max-" + mode, () => Sync(() => MeasureMaximum(c, mode)));
        await c.CaseAsync("collection-boundaries", () => Sync(() => {
            foreach (string name in new[] { "Education", "Experiences", "SkillCategories", "Projects", "Certifications", "Achievements", "Responsibilities" })
            {
                var d = Collection(name, 50); c.That(CountEntries(Capture(d).Document) == 50, "Each collection accepts exactly fifty items");
                Failure(c, () => Capture(Collection(name, 51)), ResumeRenderFailure.InvalidResumeInput, "Each collection rejects fifty-one items via codec");
            }
            var max = Maximum("newlines"); c.That(CountEntries(Capture(max).Document) == 200, "Maximum 200 total items accepted");
            Failure(c, () => Capture(max with { Certifications = [new() { Id = Id(999) }] }), ResumeRenderFailure.InvalidResumeInput, "201 total items rejected via codec");
        }));
        await c.CaseAsync("field-boundaries", () => Sync(() => {
            foreach (var spec in new (int limit, Func<string, ResumeDocument> build)[] {
                (200, x => new() { ResumeTitle = x }), (512, x => new() { Header = new() { FullName = x } }),
                (2048, x => new() { Education = [new() { Id = Id(1), Institution = x }] }), (16384, x => new() { Summary = x }) })
            {
                c.That(Capture(spec.build(new string('x', spec.limit))).SemanticByteLength > spec.limit, "Exact original field limit preserved");
                Failure(c, () => Capture(spec.build(new string('x', spec.limit + 1))), ResumeRenderFailure.InvalidResumeInput, "Original field limit plus one rejected");
            }
            var max = Maximum("newlines");
            Failure(c, () => Capture(max with { ResumeTitle = max.ResumeTitle + "x" }), ResumeRenderFailure.InvalidResumeInput, "Aggregate 100,001 rejected without clamping");
        }));
        await c.CaseAsync("writer-byte-boundary", () => Sync(() => WriterBounds(c)));
        await c.CaseAsync("pure-value-graph", () => Sync(() => {
            var assembly = typeof(ResumeRenderSnapshot).Assembly;
            var types = assembly.GetTypes().Where(x => x.Namespace == typeof(ResumeRenderSnapshot).Namespace && !x.IsEnum && !typeof(Exception).IsAssignableFrom(x));
            foreach (var type in types)
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var t = p.PropertyType;
                c.That(!t.IsArray && t != typeof(ResumeFile) && t != typeof(ResumeDocument) && !typeof(Stream).IsAssignableFrom(t) && !typeof(IDictionary).IsAssignableFrom(t), "Public B2 value graph exposes no mutable buffer/persistence/handle");
            }
            c.That(typeof(ResumeRenderSnapshot).GetConstructors().Length == 0 && typeof(ResumeCanonicalData).GetConstructors().Length == 0, "Captured identity and owned data cannot be publicly forged");
        }));
    }

    private static void Compare(Checks c, object original, object copy)
    {
        foreach (var p in original.GetType().GetProperties())
        {
            if (original is ResumeDocument && p.Name.StartsWith("Show", StringComparison.Ordinal))
            {
                string key = p.Name[4..]; var v = (ResumeSnapshotDocument)copy;
                c.That(Equals(p.GetValue(original), typeof(ResumeSnapshotVisibility).GetProperty(key)!.GetValue(v.Visibility)), "Original visibility preserved"); continue;
            }
            object value = p.GetValue(original)!; object target = copy.GetType().GetProperty(p.Name)!.GetValue(copy)!;
            if (value is string || value.GetType().IsValueType && value is not IEnumerable) c.That(Equals(value, target), "Original scalar retained: " + p.Name);
            else if (value is IEnumerable sequence)
            {
                var a = sequence.Cast<object>().ToArray(); var b = ((IEnumerable)target).Cast<object>().ToArray(); c.That(a.Length == b.Length, "Original collection length retained");
                for (int i = 0; i < a.Length; i++) { if (a[i].GetType().IsEnum) c.That(Equals(a[i], b[i]), "Section order retained"); else Compare(c, a[i], b[i]); }
            }
            else Compare(c, value, target);
        }
    }
    private static void CompareJson(Checks c, object original, JsonElement node)
    {
        foreach (var p in original.GetType().GetProperties())
        {
            if (original is ResumeDocument && p.Name.StartsWith("Show", StringComparison.Ordinal)) continue;
            string key = char.ToLowerInvariant(p.Name[0]) + p.Name[1..]; object value = p.GetValue(original)!;
            if (value is string text) c.That(node.GetProperty(key).GetString() == text, "Parsed canonical text equals original: " + key);
            else if (value is IEnumerable sequence)
            {
                var values = sequence.Cast<object>().ToArray(); var array = node.GetProperty(key).EnumerateArray().ToArray();
                c.That(values.Length == array.Length, "Parsed JSON retains collection cardinality");
                for (int i = 0; i < values.Length; i++) if (!values[i].GetType().IsEnum) CompareJson(c, values[i], array[i]);
            }
            else if (!value.GetType().IsValueType) CompareJson(c, value, node.GetProperty(key));
        }
    }
    private static IEnumerable<ResumeDocument> Mutations()
    {
        // Each candidate starts fresh, then mutates one original init-only property using a test-only reflection seam.
        var paths = new List<(string? parent, int index, string property)>();
        foreach (var p in typeof(ResumeDocument).GetProperties())
        {
            if (p.PropertyType == typeof(string) || p.PropertyType == typeof(bool)) paths.Add((null, -1, p.Name));
            else if (p.Name is "Header" or "Formatting") foreach (var q in p.PropertyType.GetProperties()) paths.Add((p.Name, -1, q.Name));
            else if (p.Name != "SectionOrder") foreach (var q in p.PropertyType.GenericTypeArguments[0].GetProperties()) paths.Add((p.Name, 0, q.Name));
        }
        foreach (var path in paths)
        {
            var d = Full(); object target = d;
            if (path.parent is not null) target = typeof(ResumeDocument).GetProperty(path.parent)!.GetValue(d)!;
            if (path.index == 0) target = ((IEnumerable)target).Cast<object>().First();
            var p = target.GetType().GetProperty(path.property)!; object value = p.GetValue(target)!;
            object changed = value switch { string x => path.property == "Id" ? Id(777) : path.property == "AccentHexColor" ? "#ABCDEF" : x + "!",
                bool x => !x, double x => x + .01, _ when value.GetType().IsEnum => Enum.ToObject(value.GetType(), ((int)value + 1) % Enum.GetValues(value.GetType()).Length), _ => throw new InvalidOperationException() };
            p.SetValue(target, changed); yield return d;
        }
    }
    private static ResumeDocument Hidden(ResumeDocument d) => d with { ShowSummary = false, ShowEducation = false, ShowExperience = false,
        ShowSkills = false, ShowProjects = false, ShowCertifications = false, ShowAchievements = false, ShowResponsibilities = false };
    private static IEnumerable<KeyValuePair<string, ResumeDocument>> Vectors()
    {
        yield return new("minimal", new()); yield return new("full", Full()); yield return new("hidden", Hidden(Full()));
        yield return new("reverse", Full() with { SectionOrder = [.. Enum.GetValues<ResumeSection>().Reverse()] });
        yield return new("lines-csv", Full("\r\n\r\n,\t ,")); yield return new("unicode", Full("नमस्ते🙂e\u0301\0\u202E\u200D"));
        foreach (var target in Enum.GetValues<ResumeTargetLength>()) foreach (var font in Enum.GetValues<ResumeFontPreference>()) foreach (var density in Enum.GetValues<ResumeDensity>())
            yield return new($"enums-{(int)target}-{(int)font}-{(int)density}", new() { Formatting = new() { TargetLength = target, FontFamily = font, SpacingMode = density } });
        yield return new("max-newlines", Maximum("newlines")); yield return new("max-commas", Maximum("commas")); yield return new("max-escaping", Maximum("escaping"));
    }
    private static ResumeDocument Collection(string name, int count)
    {
        var property = typeof(ResumeDocument).GetProperty(name)!; Type entry = property.PropertyType.GenericTypeArguments[0];
        var items = Array.CreateInstance(entry, count);
        for (int i = 0; i < count; i++) { object item = Activator.CreateInstance(entry)!; entry.GetProperty("Id")!.SetValue(item, Id(i + 1)); items.SetValue(item, i); }
        return SetCollection(new(), name, items.Cast<object>().ToArray());
    }
    private static ResumeDocument SetCollection(ResumeDocument d, string name, object[] items)
    {
        var property = typeof(ResumeDocument).GetProperty(name)!; Type entry = property.PropertyType.GenericTypeArguments[0];
        var typed = Array.CreateInstance(entry, items.Length); for (int i = 0; i < items.Length; i++) typed.SetValue(items[i], i);
        var method = typeof(ImmutableArray).GetMethods().Single(x => x.Name == "CreateRange" && x.IsGenericMethodDefinition && x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType.IsGenericType && x.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        property.SetValue(d, method.MakeGenericMethod(entry).Invoke(null, [typed])); return d;
    }
    private static ResumeDocument Maximum(string mode)
    {
        var d = new ResumeDocument { ResumeTitle = "SYNTHETIC MAX", Education = [.. Enumerable.Range(1, 50).Select(i => new ResumeEducation { Id = Id(i) })],
            Experiences = [.. Enumerable.Range(51, 50).Select(i => new ResumeExperience { Id = Id(i) })],
            Projects = [.. Enumerable.Range(101, 50).Select(i => new ResumeProject { Id = Id(i) })],
            SkillCategories = [.. Enumerable.Range(151, 50).Select(i => new ResumeSkill { Id = Id(i) })] };
        int remaining = ResumeLimits.AggregateText - (200 * 32 + d.ResumeTitle.Length + d.Formatting.AccentHexColor.Length);
        if (mode == "commas")
        {
            d = d with { SkillCategories = [.. d.SkillCategories.Select(x => { int size = Math.Min(remaining, 2048); remaining -= size; return x with { SkillsCsv = new string(',', size) }; })] };
        }
        else d = d with { Experiences = [.. d.Experiences.Select(x => { int size = Math.Min(remaining, 16384); remaining -= size; return x with { BulletsRaw = new string(mode == "newlines" ? '\n' : '\u0001', size) }; })] };
        if (remaining != 0) throw new InvalidOperationException("Maximum fixture did not consume aggregate budget.");
        return d;
    }
    private static int CountEntries(ResumeSnapshotDocument d) => d.Education.Length + d.Experiences.Length + d.SkillCategories.Length + d.Projects.Length + d.Certifications.Length + d.Achievements.Length + d.Responsibilities.Length;
    private static void MeasureMaximum(Checks c, string mode)
    {
        var d = Maximum(mode); new ResumeCodec().Validate(File(d));
        long before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        var s = Capture(d); var data = Data(s); watch.Stop(); long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        int items = s.Document.Experiences.Sum(x => x.BulletLines.Length) + s.Document.Projects.Sum(x => x.BulletLines.Length) + s.Document.Responsibilities.Sum(x => x.BulletLines.Length) + s.Document.SkillCategories.Sum(x => x.Entries.Length);
        using var json = JsonDocument.Parse(data.CopyBytes());
        c.That(CountEntries(s.Document) == 200 && s.SemanticByteLength <= ResumeRenderLimits.CanonicalBytes && s.SnapshotByteLength <= ResumeRenderLimits.CanonicalBytes && data.ByteLength <= ResumeRenderLimits.CanonicalBytes, "Maximum legal shape remains within frozen byte bounds");
        c.That(items <= ResumeRenderLimits.DerivedItems && json.RootElement.GetProperty("document").GetProperty("experiences").GetArrayLength() == 50, "Maximum derivation bounded without sampling");
        Console.WriteLine("B2A-MEASUREMENT " + JsonSerializer.Serialize(new { mode, aggregateUtf16 = 100000, entries = 200, semanticBytes = s.SemanticByteLength, snapshotBytes = s.SnapshotByteLength,
            renderDataBytes = data.ByteLength, derivedItems = items, allocatedBytes = allocated, elapsedMilliseconds = watch.Elapsed.TotalMilliseconds, expectedContent = "deferred, not implemented" }));
    }
    private const string MinimalSemanticJson = """
{"domain":"AXORA.Resume.Semantic","version":1,"document":{"resumeTitle":"Untitled Resume","header":{"fullName":"","professionalTitle":"","email":"","phone":"","location":"","linkedIn":"","linkedInUrl":"","gitHub":"","gitHubUrl":"","portfolioUrl":""},"summary":"","formatting":{"targetLength":"onePage","fontFamily":"segoeUI","spacingMode":"standard","marginInches":0.65,"showDividers":true,"centerHeader":true,"uppercaseSectionTitles":true,"accentHexColor":"#000000"},"education":[],"experiences":[],"skillCategories":[],"projects":[],"certifications":[],"achievements":[],"responsibilities":[],"visibility":{"summary":true,"education":true,"experience":true,"skills":true,"projects":true,"certifications":true,"achievements":true,"responsibilities":true},"sectionOrder":["summary","education","experience","skills","projects","certifications","achievements","responsibilities"]}}
""";

    // Independently encoded Python oracle, reviewed against version-1 schema; never generated at test runtime.
    private static readonly IReadOnlyDictionary<string, string[]> Goldens = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["minimal"] = ["D2F59CB18C70D5C1C770843AF110501621A27F17893740D79FE9ABD52A7C48D0", "63810D5DA1DF878FA7D58A1E3E395751F35CAFC72018A24C878D71F226DA62A8", "3BA9A6339563AF563FCA5D7FE3E9B704B7D3B3A4736B52365D7F0A87FA16096B"],
        ["full"] = ["6D49AD763135F7F0469558809C88B51A723B2FD77C319E62A06E90AAD3B1568D", "1C2C95FAD2C926684F459A52C35377AD106FC4A588FBDB750F391DC3DA300FEC", "1B69FE4B0EDAACE57CCFC1A61EA385EF0464C9784E563D7C4F4F752E9E188C33"],
        ["hidden"] = ["8870E5F51DDA67152E461C95617949336D150B927DAA5977A726FED9E9809E39", "0FFF1C9AD0B24B9162FF0B8FEF3892C32DA89E63DF184E6694F8E25845DB5529", "372EB69D8A4C23DB5CB38D33FED12B094CE14C44B1ABB8E1B650F7AA28146356"],
        ["reverse"] = ["F8B6A0C95F087BF87804F88EF6B5AF1CC422F0CB358D3FE8A0979A3429EE4C91", "4FE28A4E69E61EF71E494AC89863E7758A83F57D2CD496F5A73DDC0E33C2AC9E", "04A648DA0F4041F547B7D06C9BFE45B4CC2D70D8FE0500A6971D1B260CC4CCD3"],
        ["lines-csv"] = ["4D0F8E2B93ED16CE21F4082A6199012E1AE3AE09B2E6F5D4A0F86665CB09A4A3", "BEA6B98BB086D15F34B0316847F627FD9FE59339F57E9F8A4AFC93302C536074", "26A65668DC43C537B71FBF62B48ACCCFD5B7B2E3F1AD5CF90C0BEBE95FE82529"],
        ["unicode"] = ["CF7B4BF5CADB9D0EEF517FE3BF21C34EFBEDE571E77DCDA2D4F3B9D1DC42FB1C", "ABD7C6A087691D93A88DB0DDC7A24BEAE65C31C2C0EB0E61B821500CC6581C42", "B3538D237B2F0A7FB83242299A7C973E1D33BF8168BDF391C1BC8F287DFACFF9"],
        ["enums-0-0-0"] = ["E53D2B795E8AA75E880840D134A8E072DE3935D5EF8A525D03A8E3B9E1AFE296", "1A8B3D23F0C5C60628819695B3B8159F52B8538D6099779716DB5249ACAC792E", "FC3AC5E6DCA5F82A3274F9F3A5C8E6A68B3CB625E78D8796774E445EB7409C6A"],
        ["enums-0-0-1"] = ["D2F59CB18C70D5C1C770843AF110501621A27F17893740D79FE9ABD52A7C48D0", "63810D5DA1DF878FA7D58A1E3E395751F35CAFC72018A24C878D71F226DA62A8", "3BA9A6339563AF563FCA5D7FE3E9B704B7D3B3A4736B52365D7F0A87FA16096B"],
        ["enums-0-0-2"] = ["81CA2CDAE24B77604D5A569EBED28155BFF003DD21DA8A88FE860F40630DA548", "60619CE91C4A3E06A8F2DEC29390764209EAEED2E43C0BD7A57E99BF29E68CCF", "CF4663F4B303524DB998CA575B853E8E226CEB10D9E67FB27F7B91B6E38EFFC6"],
        ["enums-0-1-0"] = ["BBAFD73B3A41383EDD52A10D3628DDB11D11669DB60327B48BC4AD3E32844D98", "CED26C8C837F91987B85E16F2E2FDC55CD032BB30FDAA70B33286C2B7EB223BD", "93B0F40D410542FEB07A57B04E017B2741FD2FFA95F6A1B7BDC6407D34C5D06C"],
        ["enums-0-1-1"] = ["EA2B47B72029C95BCF9FE927BF500BFC46E5BE4C42CE5B8B0A97F16FDBEE3D35", "2539693D1700F419455B3CC353E7AF2A08A1F749746EA19426E0E0E23D367351", "0D835CDB77FD3024A8ABAA3FAFAD21DF484874B2560566B139369DA68154B63C"],
        ["enums-0-1-2"] = ["30324428412F9256837283B67D69AF3DABD920225A29F766BF36F3BAEEE41E33", "75453A2CD31EA523C8CAAE1019872CEF9F240C834E45B908CF4A2F17876F2E7F", "0AF1AD627A92E9B7152AB08EF6E015A4B213904A9DDF87A23D9415FFF89E673F"],
        ["enums-0-2-0"] = ["1F4DBF65870BD1450A296E16A5DD62D74D1A13651C084DC07E128D6A736BCCE8", "5EDB803184945D12AC0924300F6BE1B2BF6FB8D732653C2B931A732086BCB451", "F8A0B4665ED6E6AAA58C879E8A5C734DC5A0216C0F84ED6603DF0CD947DEF755"],
        ["enums-0-2-1"] = ["0126C3CB8EB0E578C44010BFDC405F0F29F6FB5A05AD0B4B26AA929EB07A741E", "AB4D83208C03DC69D585FBA87126630E57798312F0C73B76AE518A1D851091C1", "21BE0F6CC2D185B3198FEB2EA2752C553332EC5DCC7D536A7014C1119D1369B9"],
        ["enums-0-2-2"] = ["9357A729CC7A4F5E574CF150CDE2ADCECF2171D531A816A3B79EB2C955C130BE", "F8B09CA61CD0FC64455E619663515C581208212515E39644AC22CCBEEC8DC6C6", "50636114D0F5F6C333F9A711A2376294A89BDEDA12FC1B08B828A3A732A0DBA9"],
        ["enums-0-3-0"] = ["A859A05E9AD2A976D2189B97866612BCD0A38F5B0FD7E49C0621A79EC8DBAEF1", "9FE946999CA38FCC510D192114CFBC1216AADB3446AAB5FB7906745973965D45", "45FB947F5441E582C9CB3C020208B741DF57C9CBC0982833EB201AB0A077335D"],
        ["enums-0-3-1"] = ["EEE4185A8B003E70C85A5E322475D7B46BFB562711CCDB96359C56C2658CF77B", "1E5557BFD342691BC23396933CD4A7729E844EF08A9B858BB760D93EF7D8820C", "6E8F0B7B8B8F32E27AE4B2F3BEBE67223CFB0F83B7D3B117DE1406BE17B04929"],
        ["enums-0-3-2"] = ["2BB877E48F6CC94D04F80D0E83E96754711EE7BA432D67092E7D01F3DE9F9E63", "841ABC923253C07F1EFAFFB24897F05669231FCAAE300D98160D1B338B6EEBAB", "2AB8067335C0D7C612D08F4C48ACA30C18D8CCE80D3DF8AF7496137F2E395340"],
        ["enums-0-4-0"] = ["5C7BA0F83D8589E15838A8107CB1DAFB9EF5DDBD9687985847E51C08BED27176", "3B306708E30AD3464F1EB9C0A885CD2F80E06B6A01E119F3BD7CE515EB03EF1D", "22994ED245F16869114E0F9050AF2EC540E7B64F24405F78D6EFC1A7BB7D1F9D"],
        ["enums-0-4-1"] = ["15945D78AD08906A2B9978CF32608C0C88707F3C51E7E64D700917D6EDB69301", "592498DD399CE819E9B8CB2E066BFCC30132DB85DAC580014176E6DB44DE7DCA", "7F850B7659EFAE1F5716E02F91E1D6FB9D12E7871C498CC829E1BF748838E466"],
        ["enums-0-4-2"] = ["B622A531D7F3848EB378442F39FB3BB06F7D9DB94F72E446B3B6DDEA9C82C187", "9C16D37520AE7180AC1F7410326B479D082D23312F8D997960A7D4666173F708", "196BB78D9D112C3A70B6311F2554A9A805C136B09EE11E6926B07045C5795B5C"],
        ["enums-1-0-0"] = ["02176A56461E5BF247008A4D7CF89C82F987C627A8F25C42D851AE67FF6C7431", "B71F1510FEFA004548AB225331A98E3A2A4E49534AD059DAEEF3A6F96DD8192B", "0139D76567C4F2300843929EEC9AF1E9E80A4D74615819D03A46F01CA6D05AC1"],
        ["enums-1-0-1"] = ["7F37F0165209B12E0EF3360C6096B40C0B5AF6822F2FE606F20D23927ED31A00", "2EC39A29CFB72AB984FC2B99ED65976B923B2DC37272F9938EABDD8652ED34C3", "EC20BE9D622B592465DCFFE058499E0AFC26F261097DBFCEF381EDAEC5D0877B"],
        ["enums-1-0-2"] = ["1394842B32579E0CCF9932DF77B9CF0E84CB5081524D6E78643404984BDEECB6", "186F87698405A7987D56AFCE7AC29757B9443C9ADAC2004CCCBA026D83E30508", "42A87AAD97EFC3662CD7FAC6D6286E69D5B9D56807CF96DB86B39DEBE7E62017"],
        ["enums-1-1-0"] = ["90B0F632061BA829F4F2624371FBA3B8C7C57B8142E65B4BF60DCCFC164354D0", "9EB0C2D92881705FF2FDC2080AFC5071CB97E0AB5B2C1C9A56C098DD9D863222", "6EBCC7AAA83A3402D6011B99AD14F81B02C98A515D448CC30BFF591687442D20"],
        ["enums-1-1-1"] = ["CF7CC94774B3C43FDF36CA17F9ED74B0FF604055DA540AE2E70D2BFFC6E78EAA", "D73D8D841D974CDFC267B28928B7FFD7EA5B918D2B9977EE238E71F8AC6F72AC", "38F16CCC577D1D6685875BF55C6680714561E1517EE8D23A3A1FE28A91942656"],
        ["enums-1-1-2"] = ["0B323EA57903524AE6EEF32A3C869F80D8C9B5292C6A72E5AE3482FADA934FF8", "5B91FD2F6C25AE8E1A66567CD03F5180833E3DBD420C6E26EBDEA5A4854B8A9B", "BD78869682E0C831361B225B94D006D100EEA0AA2BE36B0824B0E214A6B91DF5"],
        ["enums-1-2-0"] = ["C3FAD3BFF36A6DE30D76EF1004E3E55C063E82154372ED860BE6BB1B03B66BD4", "3DB35CACFC8443655E659A48B944782E4B37C4136DDCE717524F78F2AC48A730", "89700AE81ECB706B4EFC6494D8874EE2A0EE042BD95B1649199E6E2AC3627324"],
        ["enums-1-2-1"] = ["FB559D7D9E4566D11F909761ADEC5B5C848F75C18B5DC5D69E830C79E9338F6B", "6FB180F954B80BAD42DF515FDD8581B0E871BA8C0B7C3FBB2CD2E9F2BC04C13C", "FAAD6169FDD7AF135A2FE18A7DBAE933614651EC501E0165C0A7FA08D6F93002"],
        ["enums-1-2-2"] = ["7BD5EB339F009049BAD3B4F83ADABE64F7F135F244EC4EC96AE702150A71143F", "87BB6080E26F6D8D68939206F46B7901640E5C518D033D27864534F9513C644D", "64B89C2F10D0611CDFEA93C3BBAC711172950C87C803ECFC349C72054F2CA3EF"],
        ["enums-1-3-0"] = ["B6206CDF1D790978437D80AD80C26C4B4B87BC968D703A6459C1E9119C522CAF", "61E70FB951C4CBFBDC2BE3171C9E0E01D56898C06B2F5F047DF0E6B77E37031C", "614C9081444B1B0E8B92809C5C9C7757714A56EE3868BEF7FE5DD5B321CB4FD6"],
        ["enums-1-3-1"] = ["06453945CA89CF4391E99D272EC0BB27852C9A15A91F82FE9CD89E621F88B230", "B17CE1338ACE138925C408847E87935FE6DA9DE25A088545DB54EC70C51A5538", "0A666924FE415BBAC713959FDDE8E806CE32C65874627B020B06B3475498E2A4"],
        ["enums-1-3-2"] = ["F8AA895357726B9C6D3462C85985BDE4EB4054CF5024D839B81298816D9B3B43", "C5C2D2A5B193257DB467CA6DDBBA19090D8B96D0D4DB500363538EC26080F93D", "77619A1B20308787CC51DDB81491876BA7BF66DA2AB127EA824613A8008FCB37"],
        ["enums-1-4-0"] = ["84C48117BD3A3235297C1CE053D2FDA5C45345FBCEB258FF88C6C0E85C20B0A4", "FF2F8A0516D6F49D20F7433183BD18E7171C80F3FF2AC90438DEBC7D1A47BD69", "026F95708BD37FEACEB452660EE947DD0B41C807156DCCC4F9AFF4D72561B1F3"],
        ["enums-1-4-1"] = ["9D7AB55AE5C66E080A6C067152A40246D85A0F3096809F59919DAB3417FB1B70", "3E41683AFA0023182B6022A218E50DCE21F11477963FA565B1FD43930DCDB5EE", "9CF846CC5505CB4B3197A24E3473DE132CA016C29EB0FDF47F17C6625ED085FB"],
        ["enums-1-4-2"] = ["056F26EAC784CCF625B23F4CF095BEA698778E6984234782A75D81AB0156231E", "A9C5B627A15A79DD5937F22F83D15B646C4409C362A44FD14E1247BC8EBF1F46", "F007FBD657302E0AB1B8E349B3BE48CCC19D637772424E40919B3B3C0D806C63"],
        ["enums-2-0-0"] = ["73BDCD2707DA07084F9DB7FE6E97A12AC054F8067ABFA655892AF3D219FB3C5F", "2C19AEE1BA470392C309F448E886F8E2F92CD903CDAA556D45364515F6C87E75", "3EC8BD67E8E70614D782723010C77FED7000A8A0DA8B043FB33F87B54F0C18F2"],
        ["enums-2-0-1"] = ["7B8D2BD22210B3A81D8CCDB1E2A4798BB66FF2274239E9F3BECEB7CB77F35EC2", "815836C5B0F2B81A777AD3AFA3745A524E25EEC94EDB842698C989A2DA4C0919", "9E702322A939437B37694B2AA2CD58E98CE00AEB5DDC973D6D95002D8A4AB863"],
        ["enums-2-0-2"] = ["C32F893C3A4254047F860F88D535FE5AA9EFD53F0A318C0D24DB47EECEFEC638", "CD435E5A9A45AFF43BBC173946FB8C5D22904ABA48132862741926C22FF09865", "A140C564C5B92EFFBFB45C592F34DA3199F8B32BCE2EC38A52729C3868DC3077"],
        ["enums-2-1-0"] = ["1B07B92942A6EC6B67EF3915BFEDB065645AF2250213B577103938E7D195E315", "B3D4966912D6A4351E9B40467BAD1628426CAA40898514EFAEDE0B8579BFDDD5", "66E0C74AEB81190A18D91EE0520CF46075F5D784470FEDD1F295DBA4C336B096"],
        ["enums-2-1-1"] = ["AE97CC6CF9AF434BCF8F69C93F524B1FE0BDDCD7724DCABB8E21BA7A9D14995E", "8A8B63B507534C63080B393C3AFFA110740C9566FFC9ECA468E1B4434AFBCFCA", "6E9F88EF8FAA724E416CCBDF5F8257CDA7BB8B7E9D2DC613E8FD28B6BDE94E60"],
        ["enums-2-1-2"] = ["689D8B153C4273EBE4E604B60ACA0C94697C64DB608E3F5D51F678BE9938C3DC", "432150D202958583D196EF5FADEB7C74B4F7255F12BED578F6E130EEA0267C8B", "F4D5AB9639BB26502D1FA6916846738320D897BECA0CBD0DDAEAFBE361DB8EB9"],
        ["enums-2-2-0"] = ["3FC63F5C3167A1A982B80692C46BC32B28027C385ED04E3A6E4F20B8B58FCCFF", "999854090D9EA2C0688DC28B60A161C26DA242FEF36668204861C1556DB1FE09", "278F2AEDF28E58EDD8F717557CBE7C01523EDAAB3D8507BAC43AE76CE0D9240A"],
        ["enums-2-2-1"] = ["293FE8B097CCB15F30507C2F72BE30765AA1B80849B2662A22951216D2C7C8A8", "8E3C82321239CCA21DF46B54E37398D5C64922B5CFDFD1BF1800C7D0720423F0", "A3BA3163794F8A5BC510FBE1ED1830259699F834D17F4D9A8693730FB53286D1"],
        ["enums-2-2-2"] = ["DE63B9F288B37D8117F3B38A638AA0BF90A3AB08C7E79D6F9AED5067DE8EFF8A", "93CD000D573521B35D62ABB25866FBD43421346D6758461D05F4F9C0C198AF9F", "1B74457C7C98F215AA03F10DBFE85E08F9B61F31635525628D3993B19280570D"],
        ["enums-2-3-0"] = ["1438941CE872C13E209F5DFDB2AF8D659C642FF41D0CCC7F148A8ED2A3899C5A", "9C56F35372ABF9C3A171559E666BE8FC2A4015E160CB283A1EA6C41FA004CA6C", "E1C1DC9F09D4D895F38906D8AD799D63162C30FB184F383C265E09A5B5AFB3D2"],
        ["enums-2-3-1"] = ["97F2A6017C5F07CC1F21032F6E2435DFF7CF37338B997A96CEE3F1DF1B1592C6", "9D8A5E26D0F428D34E2A6A37D9A21A9D4D43C22B2B0DF6EBFFC87D7DE6E64A87", "B951504EA166918857882734933F7CECEA86F5AFED99B427A33B1DF6160E1C8C"],
        ["enums-2-3-2"] = ["5E52A34F0B67866D54FCB2B08E3DC6FC1460C5F673DC270EBFB34BF6A44825EE", "B385A1F5FC96D631E0C36C2FB20963A8BB309A82AB07849C1E69DD9A531799DE", "58D9B1E09F52A60C868F32852975DE8F004C6543E9DEF7A17C1C8E6C3E303B3E"],
        ["enums-2-4-0"] = ["D3DB5A2839E808E08E36FCCBE1D26079294F80BB1D937D77D0B34F2340AE0FD8", "E922AEC6E9B7461F36324FAD73B3540DB5E0A3311C8EBEACCF6691E5E04A4B4D", "AE849F4A1420BDAE68BCD21DB4C217BE947212895A3A2D3C90B37321B09D5E87"],
        ["enums-2-4-1"] = ["D09AC5324E044C66FC7DAD69E4E893C706B882449A7ED878B369A1337276E1D0", "9612BDDF8C48288A11C9C1073A4B18378823E339DA102C34077BF60A3B54BE9C", "95F4F01642BF1739F762DC887E8BDDBACC9CA80DC8CEEF7ECD12D3F763DD4056"],
        ["enums-2-4-2"] = ["EC45B3E5E886837DED29B4E13876B212D01A2CCD97598C57EE1D900B0FD98E77", "F3AD5F8557AD5479534D18526766D6A24DE257E0EF0FC175DA8E584A10E5218F", "A438A2FDB6D32AD7280FF203C1B83069B5B4DDF3572DF3DA1829AD832E612CC9"],
        ["enums-3-0-0"] = ["67D82D1D882E88801FAF15D0C54544E9C6C3E4B7B33D49085442F5F48329424B", "15AA10AB741E6FD38F63166EEA44D4B21C7B502134E916BE6949D47FCFB47376", "8A79B9772ECF95AC2AF1CAFB016D9D5D7E4382CCE0AFB466C387EDC1B3D8C44E"],
        ["enums-3-0-1"] = ["70F8A61C5492B4185DEA1CFD226ECB1FB0470CA2E59D4E3B3917C16481EB1FE6", "CC412B897DD18E456A177031FDE7D339847CD7CD640C14994F95E35C2D45AF48", "D9BE173FD61CF158ADBA4D6F39F4B9A5C0F2A6A6D2D95715DE06DA931B5CE066"],
        ["enums-3-0-2"] = ["B8B024CAA946704652131CAF2505960B7BC55E398214CA57D7E79600C0616E54", "B4F8A846D50AF9B80459C3F2F278A88E90CA26BB8BD658855C44861175759E2A", "47BE3C152A42AD872A3450287A4FE7961884922E119B0B85C59BB251489E5443"],
        ["enums-3-1-0"] = ["BCE246E71196A0491D35F28D602438CCF8E44823E09663556D23D731DF44C235", "0F3FE41140745D748EBEEFD5A1C8E9B7732EA15DEDE8D7D4E3160020F6B00C22", "74C30072E3BEB510A468E1332D139DE95434073C6FDBD8E1E078340F6A732F9A"],
        ["enums-3-1-1"] = ["ADAD42D2E59BA29E534194F4E3AAFA1D439FF6C9505F6F20A947CD7A1F1407C7", "85E49726D5A0BEC26B37E6F5593D1D825BC0CFD30ECAFE04D5211C67B6D247BD", "10FB2D22BC60EEF45FDF8CC433A3D5F8B52819783558A7BE20EFC46F9B3AE2D9"],
        ["enums-3-1-2"] = ["82204DE5F70F361CC49A819E14463AAC5F4731B8DC3BCB3B119DC4C938CF05BA", "11F1C0C081A60F053D83570FFBBD3C59183FA6170099F5A079EA0B39F5CB1FDA", "7B4A9888C339B714C9B51B3A1151AE220B4F71385BB5A76A0C270382479A305B"],
        ["enums-3-2-0"] = ["1B0BFE58B9333518D3ECBFF146A6B912FCEBA8C91ED08EC01856A9B95B170C65", "6B26D62879B3EAE22650B6FF226001444BAAC228710BFF350F832129107B3E0A", "716EAD44C44DD2E08B8DD589C0C1A41D52996B29B53A57747122B072C9F4D4EB"],
        ["enums-3-2-1"] = ["765DFC03B33E6F6D850350C705BD1A9389B82CF5BD21A6ADD7B798163AF47DB0", "9A49F91EC5EC30BC6F6D2088D3225ABB4D813E156BF9F13C5D81C3C9A385788A", "78183BA2B5158150D613A9AE650FD89CCEB1A91488F123EEED7E03F75454C5AD"],
        ["enums-3-2-2"] = ["5363157270F98A0B5FF6462746C49C0227DD8AA4528FBC304710AC4F90D0BD5D", "60E1B5FCB12C92D0830E6D85AB32FFDDA7ADF59021E1FAD61C4AD137B7C0BDE5", "FFB6DCBF1BA6B2520EDEB4E268E70E78A557455350139F0D014ACB6853149F6C"],
        ["enums-3-3-0"] = ["EFF1E56D0AF02F13E790C19CC9656B235A7521FE198E6AAED65F48D5B2791BA9", "9D5B1A8DD467EFF6DBD4573A6A7A9EDE8FC473F7D7E996A19AFF68D40872A061", "B13F689754B9760638A0D09D09C9305DF3858947C5B8C2A026ACA60AC484B26E"],
        ["enums-3-3-1"] = ["239D5AEBC83C18D72D8939130F2658C7477BFA8560E5607D986E040B15CD8979", "3CEE13D82CAF1135B1C328DDE6A37BD27D8CC7425CBE70C0DDC120B64E9FFD94", "DD072DBD1D1EAF317B23B5D432DA1F87607B680BB0238C9E5FA5A0B121B557D3"],
        ["enums-3-3-2"] = ["8204DCF56EDE71AB4EA780A1559EC68CFBDD2CDAC1BB1B08B3BCB00E728F79CB", "E4584B6E1654AD911C312643D604E90AD11EADCB24D9031088FC9B65AADCC47F", "79C95411BD810AFAEEC8907B3A48C0E8A94129F70311EF0EC091C73B89308576"],
        ["enums-3-4-0"] = ["053AADF1504DF455FD1F50E1C13C18D49159A551F3A72D21C0CB30B8293C977F", "A24A86574382223E89E4660E2CC3BF383F6CF641949F582A1A214AF775D8F2E6", "6D9B1AF877A4ED3B8379D3C502377944F8CF5368DEF4EFB36A59CC198477A73E"],
        ["enums-3-4-1"] = ["62A3F40A83D55456FD9BE5364A8B8F3A44711466D4C64722CD5F4F7F82092A8E", "541EA2D4F0E78F5F72A7716CC4728C0F5A45C79DF9B34FE2E01725B2B37D5FA7", "DF53723DECD7EA5BA4ADDE1385115FFF8A3B09084085B5FB6309937D98D3F005"],
        ["enums-3-4-2"] = ["9DF11E61CD80E54F93A50E40E0AD7C92226FC54044A7DE757F654F06F4F94D06", "651F799748ED18CEB72D71654A648DDD7677FBC176B01024286807F3B5738E33", "EF55F4F2F0F5F8BF7CF9ED21EC10F2B2B2AD613C6E330DC140927A1470E972E1"],
        ["max-newlines"] = ["9D6845705BFCE1FDB5AAD9CCA78BC28844D226FC1257955763FA96E291CC9FBE", "C314C52C801EA7AE187D1DF1198A8FFE48182A788ADD1A69A40327D34D4348F1", "4BBA5E7B3C9A2F6AF98382F3D2FEFA2BF2C440DD1EAE2F1EEFB9B145E2A340A5"],
        ["max-commas"] = ["9A5C81F3538C8F5B04EEE0DA1528743D7F92FD6CC25A85EACB2FA1C04A1D95C4", "0C56839665B3FAC018B86395378D83CA479FDA8E71A125ACF437327A4DE58BFD", "C4ACF77CF8316713A21812DAEC3A3F460016B482BAD1D58C2B9DCA0F50D12677"],
        ["max-escaping"] = ["FE2B10433819E8E1FE1ED8D0EA061AAB8514042C28D2AAA76036204989E1B5EC", "B1B4DB68915CD1713E2A182ADAEFD374AD890ABE4B2735E1324364969276D128", "6DF8D830F3E4F5F04016227CCD00CC6B2136C49F4BB2F84CEADA711698805B3E"],
    };

    private static void WriterBounds(Checks c)
    {
        var type = typeof(ResumeRenderSnapshot).Assembly.GetType("Axora.Studio.Services.ResumeRendering.ResumeCanonicalJson")!;
        foreach (var failure in new[] { ResumeRenderFailure.SnapshotLimitExceeded, ResumeRenderFailure.RenderDataLimitExceeded })
        {
            object Create() => Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [failure], CultureInfo.InvariantCulture)!;
            var write = type.GetMethod("String", BindingFlags.Instance | BindingFlags.NonPublic)!; var finish = type.GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var exact = Create(); write.Invoke(exact, [new string('x', ResumeRenderLimits.CanonicalBytes - 2)]);
            c.That(((byte[])finish.Invoke(exact, null)!).Length == ResumeRenderLimits.CanonicalBytes, "Bounded encoder accepts exact 4 MiB including syntax");
            try { write.Invoke(Create(), [new string('x', ResumeRenderLimits.CanonicalBytes - 1)]); c.That(false, "Over-limit encoder must reject"); }
            catch (TargetInvocationException ex) { c.That(ex.InnerException is ResumeRenderException e && e.Failure == failure, "4 MiB plus one yields correct typed failure without truncation"); }
        }
    }
}
