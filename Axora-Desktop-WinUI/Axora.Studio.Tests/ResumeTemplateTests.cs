using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Buffers.Binary;
using System.Reflection;
using Axora.Studio.Models;
using Axora.Studio.Models.ResumeRendering;
using Axora.Studio.Services.ResumeRendering;
using Axora.Studio.Models.Rendering;
using Axora.Studio.Services.Rendering;

namespace Axora.Studio.Tests;

internal static class ResumeTemplateTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[] {
        "b2b-font-inventory", "b2b-catalog", "b2b-selection", "b2b-resource-integrity", "b2b-template-ownership", "b2b-capability-audit", "b2b-font-cmap-coverage" });
    public static async Task RunAsync(Checks c)
    {
        await c.CaseAsync("b2b-font-inventory", async () => {
            using var sandbox = new WindowsRendererSandbox();
            string source = Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_RUNTIME_SOURCE")
                ?? throw new InvalidOperationException("Authenticated external runtime provisioning is required");
            string parent = Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_ROOT")
                ?? throw new InvalidOperationException("External native test root is required");
            string root = Path.Combine(parent, "b2b-font-" + Guid.NewGuid().ToString("N"));
            TypstRuntimeVerifier.Provision(source, root, sandbox);
            using var runtime = new TypstRuntimeVerifier(root).Verify(sandbox);
            using var workspace = RendererWorkspace.Create(Path.Combine(root, "operations"), Guid.NewGuid(), RendererFixture.Ascii, sandbox);
            var result = await new WindowsRendererProcess().RunAsync(new(runtime,
                ["fonts", "--ignore-system-fonts", "--variants"], workspace, false, TimeSpan.FromSeconds(10), 32768), default);
            string fonts = Encoding.UTF8.GetString(result.Output);
            Console.WriteLine("B2B-FONT-INVENTORY " + JsonSerializer.Serialize(new { result.Outcome, result.Settled,
                result.JobMembership, result.AppContainer, result.ZeroCapabilities, fonts }));
            c.That(result.Outcome == RendererOutcome.Completed && result.Settled && result.JobMembership && result.AppContainer && result.ZeroCapabilities,
                "Embedded font inventory uses the authenticated B1 AppContainer/Job boundary");
            c.That(fonts.Contains("Libertinus Serif", StringComparison.Ordinal), "Intended embedded serif family exists without system fonts");
            string serif = fonts.Split("Libertinus Serif\n", StringSplitOptions.None)[1].Split("\nNew Computer Modern", StringSplitOptions.None)[0];
            c.That(serif.Contains("Style: Normal, Weight: 400") && serif.Contains("Style: Normal, Weight: 700"), "Actual embedded family includes both required regular/bold faces");
            c.That(workspace.Cleanup(), "Font inventory workspace settles and cleans");
            Array.Clear(result.Output);
        });
        await c.CaseAsync("b2b-catalog", () => {
            var catalog = ResumeTemplateCatalog.Embedded;
            c.That(catalog.Entries.Length == 2 && catalog.Entries.Select(x => x.TemplateId).SequenceEqual(new[] { "axora.simple", "axora.technical" }), "Exactly two finite app-owned templates");
            foreach (var m in catalog.Entries)
            {
                c.That(m.TemplateVersion == 1 && m.RenderDataSchemaVersion == 1 && m.MinimumTypst == "0.15.1" && m.RendererCompatibilityContract == 1, "Template schema/runtime compatibility pinned");
                c.That(m.FontProfile == "LibertinusSerifRegularBoldV1" && m.SourceByteLength is > 0 and <= 65536, "Finite font profile and exact bounded source");
                Console.WriteLine("B2B-TEMPLATE " + JsonSerializer.Serialize(m));
            }
            return Task.CompletedTask;
        });
        await c.CaseAsync("b2b-selection", async () => {
            var catalog = ResumeTemplateCatalog.Embedded;
            c.That(catalog.ResolvePersisted(null, null) == ResumeTemplateChoice.SimpleV1, "Null/null resolves simple for rendering only");
            c.That(catalog.ResolvePersisted("axora.simple", 1) == ResumeTemplateChoice.SimpleV1 && catalog.ResolvePersisted("axora.technical", 1) == ResumeTemplateChoice.TechnicalV1, "Exact explicit ID/version selection");
            foreach (var pair in new (string?, int?)[] { ("unknown", 1), ("axora.simple", 2), ("AXORA.SIMPLE", 1), ("", 1), ("axora.technical", 0) })
                await c.ThrowsAsync<ResumeTemplateException>(() => { catalog.ResolvePersisted(pair.Item1, pair.Item2); return Task.CompletedTask; }, "Unknown identity/version rejected");
            foreach (var pair in new (string?, int?)[] { (null, 1), ("axora.simple", null) })
            {
                try { catalog.ResolvePersisted(pair.Item1, pair.Item2); c.That(false, "Unpaired persisted identity rejected"); }
                catch (ResumeTemplateException ex) { c.That(ex.Failure == ResumeTemplateFailure.InvalidPresentationSelection, "Unpaired persisted identity typed"); }
            }
            await c.ThrowsAsync<ResumeTemplateException>(() => { catalog.Resolve((ResumeTemplateChoice)99); return Task.CompletedTask; }, "Invalid finite selection rejected");
        });
        await c.CaseAsync("b2b-resource-integrity", async () => {
            string[] names = ResumeTemplateCatalog.Manifests.Select(x => x.LogicalResourceName).ToArray();
            Stream? Original(string name) => typeof(ResumeTemplateCatalog).Assembly.GetManifestResourceStream(name);
            foreach (var identities in new[] { names.Take(1).ToArray(), new[] { names[0], names[0] }, names.Append("Axora.Studio.Templates.Resume.Unknown.typ").ToArray() })
                await c.ThrowsAsync<ResumeTemplateException>(() => { _ = new ResumeTemplateCatalog(identities, Original); return Task.CompletedTask; }, "Missing/duplicate/unexpected resource identity rejected");
            await c.ThrowsAsync<ResumeTemplateException>(() => { _ = new ResumeTemplateCatalog(names, _ => null); return Task.CompletedTask; }, "Missing resource stream rejected");
            foreach (int size in new[] { 0, 1, 65537 })
                await c.ThrowsAsync<ResumeTemplateException>(() => { _ = new ResumeTemplateCatalog(names, _ => new MemoryStream(new byte[size])); return Task.CompletedTask; }, "Short/oversize resource rejected");
            byte[] source = ResumeTemplateCatalog.Embedded.Resolve(ResumeTemplateChoice.SimpleV1).CopySource(); source[0] ^= 1;
            await c.ThrowsAsync<ResumeTemplateException>(() => { _ = new ResumeTemplateCatalog(names, name => name == names[0] ? new MemoryStream(source) : Original(name)); return Task.CompletedTask; }, "Exact-length resource tamper/SHA mismatch rejected");
        });
        await c.CaseAsync("b2b-template-ownership", () => {
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
            {
                var template = ResumeTemplateCatalog.Embedded.Resolve(choice); var bytes = template.CopySource();
                c.That(Convert.ToHexString(SHA256.HashData(bytes)) == template.Manifest.TemplateSourceSha256, "Actual embedded bytes authenticate against reviewed pin");
                Array.Fill(bytes, (byte)0);
                c.That(Convert.ToHexString(SHA256.HashData(template.CopySource())) == template.Manifest.TemplateSourceSha256, "Caller cannot mutate template source authority");
            }
            return Task.CompletedTask;
        });
        await c.CaseAsync("b2b-capability-audit", () => {
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
            {
                string source = Encoding.UTF8.GetString(ResumeTemplateCatalog.Embedded.Resolve(choice).CopySource());
                // Lex strings/comments before inspecting identifiers and call sites. This supplements
                // the complete manual source/call-graph review and the native access/injection controls.
                var tokens = Regex.Matches(source, "//[^\\r\\n]*|\\\"(?:\\\\.|[^\\\"\\\\])*\\\"|[A-Za-z_][A-Za-z_0-9-]*|[^\\s]")
                    .Select(x => x.Value).Where(x => !x.StartsWith("//", StringComparison.Ordinal)).ToArray();
                string[] forbidden = ["read", "csv", "yaml", "xml", "cbor", "toml", "include", "import", "image", "bibliography", "plugin", "eval", "link", "heading", "outline", "label"];
                c.That(!tokens.Select((value, index) => (value, index)).Any(x => forbidden.Contains(x.value, StringComparer.Ordinal) &&
                    !(x.index > 0 && tokens[x.index - 1] == "." && (x.index + 1 == tokens.Length || tokens[x.index + 1] != "("))),
                    "No file/code/action/transitive capability reference in complete trusted program; JSON link property is a value");
                int json = Array.IndexOf(tokens, "json");
                c.That(tokens.Count(x => x == "json") == 1 && tokens.Skip(json).Take(4).SequenceEqual(new[] { "json", "(", "\"resume.json\"", ")" }), "Sole file capability is literal json resume.json");
                c.That(source.Contains("fallback: false") && source.Contains("ligatures: false") && source.Contains("hyphenate: false") && source.Contains("smartquote(enabled: false)") && !source.Contains("style: \"italic\""), "Exact serif profile, literal quotes and only proved regular/bold styles");
                int features = Array.IndexOf(tokens, "features");
                c.That(features > Array.IndexOf(tokens, "text") && features < Array.IndexOf(tokens, "smartquote") &&
                    tokens.Count(x => x == "features") == 1 && tokens.Skip(features).Take(7).SequenceEqual(new[] { "features", ":", "(", "calt", ":", "0", ")" }),
                    "Both reviewed embedded templates explicitly disable calt in the global text profile");
            }
            return Task.CompletedTask;
        });
        await c.CaseAsync("b2b-font-cmap-coverage", () => {
            byte[] executable = System.IO.File.ReadAllBytes(Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_RUNTIME_SOURCE")!);
            c.That(Convert.ToHexString(SHA256.HashData(executable)) == RendererRuntimeManifest.ExecutableSha256, "Font coverage is decoded only from the authenticated external executable");
            var regular = DecodeCmap(executable, 35963824); var bold = DecodeCmap(executable, 36300956);
            regular.IntersectWith(bold);
            var ranges = ((ValueTuple<int, int>[])typeof(ResumeTypstCompiler).GetField("GlyphRanges", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!);
            var pinned = ranges.SelectMany(x => Enumerable.Range(x.Item1, x.Item2 - x.Item1 + 1)).ToHashSet();
            c.That(regular.Count == 2185 && pinned.SetEquals(regular), "Compiler glyph boundary equals independently decoded regular/bold cmap intersection");
            c.That("éĀñ e\u0301 • – — £ ¥ € © ™".EnumerateRunes().All(ResumeTypstCompiler.SupportsScalar), "Latin/punctuation/combining/bullet/common-currency probes are supported by both selected faces");
            c.That("हिन्दी😀₹\0\u202E\u200B".EnumerateRunes().Any(x => !ResumeTypstCompiler.SupportsScalar(x)), "Hindi/emoji/missing rupee/control probes are not silently admitted");
            c.That(!ResumeTypstCompiler.SupportsScalar(new Rune('א')) && !ResumeTypstCompiler.SupportsScalar(new Rune('Ж')),
                "Cmap presence does not admit unproved scripts under the conservative Latin B2B policy");
            Console.WriteLine("B2B-FONT-CMAP " + JsonSerializer.Serialize(new { family = "Libertinus Serif", styles = new[] { "Normal 400", "Normal 700" }, regularBoldIntersection = regular.Count,
                regularOffset = 35963824, boldOffset = 36300956, noFontFileExtracted = true }));
            return Task.CompletedTask;
        });
    }
    // Independent big-endian OpenType cmap decoder; reads memory only, no font staging/parser package.
    private static HashSet<int> DecodeCmap(byte[] bytes, int font)
    {
        int H(int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
        int U(int offset) => checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
        if (Encoding.ASCII.GetString(bytes, font, 4) != "OTTO") throw new InvalidDataException("PinnedFontOffset");
        int cmap = 0;
        for (int i = 0; i < H(font + 4); i++)
        { int entry = font + 12 + i * 16; if (Encoding.ASCII.GetString(bytes, entry, 4) == "cmap") cmap = font + U(entry + 8); }
        if (cmap == 0) throw new InvalidDataException("MissingEmbeddedCmap");
        var coverage = new HashSet<int>();
        for (int i = 0; i < H(cmap + 2); i++)
        {
            int record = cmap + 4 + i * 8; if (H(record) is not (0 or 3)) continue;
            int table = cmap + U(record + 4), format = H(table);
            if (format == 12)
                for (int j = 0; j < U(table + 12); j++)
                { int row = table + 16 + j * 12; for (int cp = U(row); cp <= U(row + 4); cp++) if (U(row + 8) + cp - U(row) != 0) coverage.Add(cp); }
            else if (format == 4)
            {
                int segments = H(table + 6) / 2, end = table + 14, start = end + segments * 2 + 2,
                    delta = start + segments * 2, offset = delta + segments * 2;
                for (int j = 0; j < segments; j++)
                for (int cp = H(start + j * 2); cp <= Math.Min(H(end + j * 2), 65534); cp++)
                {
                    int displacement = H(offset + j * 2), shift = H(delta + j * 2);
                    int glyph = displacement == 0 ? (cp + shift) & 65535 : H(offset + j * 2 + displacement + (cp - H(start + j * 2)) * 2);
                    if (displacement != 0 && glyph != 0) glyph = (glyph + shift) & 65535;
                    if (glyph != 0) coverage.Add(cp);
                }
            }
        }
        return coverage;
    }
    internal static ResumeRenderSnapshot Snapshot(ResumeDocument? document = null) => ResumeSnapshotBuilder.Capture(new ResumeFile {
        DocumentId = new string('1', 32), Revision = 7, CreatedUtc = new(2020, 1, 2, 0, 0, 0, TimeSpan.Zero),
        ModifiedUtc = new(2020, 1, 2, 0, 0, 0, TimeSpan.Zero), Document = document ?? new() });
    internal static ResumeDocument Full(string text = "SYNTHETIC") => new() {
        ResumeTitle = text + " Title", Header = new() { FullName = text + " Name", ProfessionalTitle = text + " Role", Email = text + " Email",
            Phone = text + " Phone", Location = text + " Location", LinkedIn = text + " LinkedIn", LinkedInUrl = text + " LinkedInUrl",
            GitHub = text + " GitHub", GitHubUrl = text + " GitHubUrl", PortfolioUrl = text + " Portfolio" }, Summary = text + " Summary",
        Education = [new() { Institution = text + " Institution", Degree = text + " Degree", Specialization = text + " Specialization", ScoreOrPercentage = text + " Score", YearRange = text + " Years" }],
        Experiences = [new() { Company = text + " Company", RoleTitle = text + " Job", Location = text + " Office", StartDate = "2020", EndDate = text + " OldEnd", IsCurrent = true, BulletsRaw = text + " Work\nSecondWork", ProjectLink = text + " WorkUrl" }],
        SkillCategories = [new() { CategoryName = text + " Category", SkillsCsv = text + ",SecondSkill" }],
        Projects = [new() { Title = text + " Project", TechStack = text + " Technology", DateRange = text + " ProjectDates", BulletsRaw = text + " Build\nSecondBuild", RepoUrl = text + " Repo" }],
        Certifications = [new() { Title = text + " Certificate", Issuer = text + " Issuer", Date = text + " AwardDate", GradeOrScore = text + " Grade", CredentialId = text + " Credential", Description = text + " Description", VerificationUrl = text + " Verification" }],
        Achievements = [new() { Title = text + " Achievement", Category = text + " AchievementCategory", Date = text + " AchievementDate", Description = text + " AchievementDescription", Link = text + " AchievementUrl" }],
        Responsibilities = [new() { Role = text + " Responsibility", Organization = text + " Organization", DateRange = text + " ResponsibilityDates", BulletsRaw = text + " Lead\nSecondLead" }]
    };
}
