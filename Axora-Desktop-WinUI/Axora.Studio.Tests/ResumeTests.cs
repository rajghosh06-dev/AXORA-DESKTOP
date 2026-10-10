using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Axora.Studio.Models;
using Axora.Studio.Services;

namespace Axora.Studio.Tests;

internal static class ResumeTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[]
    {
        "codec-roundtrip", "legacy-all-fields", "invalid-input", "unicode", "bounds", "identity-collision",
        "same-title", "import-preservation", "import-idempotence", "import-backup-failure", "import-backup-collision",
        "staging-failure", "reopen-failure", "replacement-failure", "revision-backup-failure", "destination-change", "backup-rotation-recovery",
        "corrupt-recovery", "future-protection", "dirty-semantics", "departure-guard", "concurrent-save", "save-copy",
        "privacy", "lazy-store", "shutdown-retention", "undo-recent", "editor-all-fields", "invalid-editor-draft", "fallback-context", "observer-failure", "missing-document-recovery", "redirected-root", "stage-collision-ownership",
        "f1-import-owned", "f1-import-failed", "f1-recovery-owned", "f1-recovery-failed", "f1-replacement-close",
        "f2-maintenance-ownership", "f2-maintenance-utf8", "f2-maintenance-json", "f2-maintenance-type", "f2-maintenance-future", "f2-maintenance-later-edit",
        "f3-unselected-json", "f3-unselected-utf8", "f3-unselected-identity", "f3-unselected-future", "f3-unselected-unreadable", "f3-unselected-ownership", "f3-duplicate-revision",
        "x1-future", "x1-semantic", "x1-filename", "x1-import-future", "x1-import-semantic", "x1-import-collision",
        "x2-json", "x2-future", "x2-identity", "x2-ownership", "x2-preserve-failure", "x2-identical", "x2-collision", "x2-degraded", "x2-bound"
    });
    private static Task<ResumeDeparture> Discard() => Task.FromResult(ResumeDeparture.Discard);
    private static Task<ResumeDeparture> Cancel() => Task.FromResult(ResumeDeparture.Cancel);
    private static Task<ResumeDeparture> Save() => Task.FromResult(ResumeDeparture.Save);
    private static Task<bool> Consent(ResumeDocument _) => Task.FromResult(true);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static async Task RunAsync(Checks c)
    {
        await c.CaseAsync("codec-roundtrip", () =>
        {
            var codec = new ResumeCodec(); var file = new ResumeFile { Document = CompleteDocument() };
            var decoded = codec.Decode(codec.Encode(file));
            c.That(ResumeCodec.SemanticallyEqual(decoded.Document, file.Document), "Every persisted semantic field/collection roundtrips");
            c.That(decoded.DocumentId == file.DocumentId && decoded.Revision == file.Revision, "Envelope identity/revision roundtrip");
            return Task.CompletedTask;
        });
        await c.CaseAsync("legacy-all-fields", () =>
        {
            var codec = new ResumeCodec(); var document = CompleteDocument();
            var node = JsonSerializer.SerializeToNode(document)!.AsObject(); node.Remove("SectionOrder");
            foreach (string name in new[] { "Experiences", "Projects", "Responsibilities" }) node[name]![0]!["BulletsLines"] = new JsonArray("redundant helper");
            byte[] bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
            var imported = codec.DecodeLegacy(bytes);
            c.That(ResumeCodec.SemanticallyEqual(imported, document), "Actual legacy numeric formatting and all seven collections are lossless");
            node["Education"]![0]!.AsObject().Remove("Id");
            bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
            c.That(codec.DecodeLegacy(bytes).Education[0].Id == codec.DecodeLegacy(bytes).Education[0].Id, "Missing legacy ID receives deterministic identity");
            return Task.CompletedTask;
        });
        await c.CaseAsync("invalid-input", async () =>
        {
            var codec = new ResumeCodec();
            foreach (string input in new[] { "", "{", "[]", "null", "{\"ResumeTitle\":\"a\",\"ResumeTitle\":\"b\",\"Header\":{}}",
                "{\"ResumeTitle\":4,\"Header\":{}}", "{\"ResumeTitle\":\"a\",\"Header\":{},\"Education\":null}",
                "{\"ResumeTitle\":\"a\",\"Header\":{},\"Formatting\":{\"MarginInches\":1e999}}",
                "{\"ResumeTitle\":\"a\",\"Header\":{},\"Unknown\":3}" })
                await Reject(c, () => codec.DecodeLegacy(Encoding.UTF8.GetBytes(input)), "Malformed/type/duplicate/nonfinite/unknown input rejected");
            var duplicate = CompleteDocument() with { Projects = [new ResumeProject { Id = CompleteDocument().Education[0].Id }] };
            duplicate = duplicate with { Projects = [new ResumeProject { Id = duplicate.Education[0].Id }] };
            await Reject(c, () => codec.ValidateDocument(duplicate), "Ambiguous IDs across collections rejected");
            await Reject(c, () => codec.ValidateDocument(new() { Education = [new() { Id = "../private" }] }), "Malformed entry identity rejected");
        });
        await c.CaseAsync("unicode", async () =>
        {
            var codec = new ResumeCodec(); var doc = new ResumeDocument { Summary = "नमस्ते 漢字 🙂 e\u0301" };
            c.That(codec.Decode(codec.Encode(new() { Document = doc })).Document.Summary == doc.Summary, "Unicode scalar sequences preserved");
            foreach (string invalid in new[] { "{\"ResumeTitle\":\"\\uD800\",\"Header\":{}}", "{\"ResumeTitle\":\"\\uDC00\",\"Header\":{}}" })
                await Reject(c, () => codec.DecodeLegacy(Encoding.UTF8.GetBytes(invalid)), "Unpaired escaped surrogate rejected");
            await Reject(c, () => codec.ValidateDocument(new() { Summary = "\uD800" }), "In-memory unpaired surrogate rejected");
            await Reject(c, () => codec.DecodeLegacy(new byte[] { 0xFF, 0xFE }), "Invalid UTF8 rejected");
            c.That(codec.DecodeLegacy(Encoding.UTF8.GetBytes("{\"ResumeTitle\":\"\\uD83D\\uDE42\",\"Header\":{}}")).ResumeTitle == "🙂", "Escaped surrogate pair accepted");
        });
        await c.CaseAsync("bounds", async () =>
        {
            var codec = new ResumeCodec(); byte[] minimal = Encoding.UTF8.GetBytes("{\"ResumeTitle\":\"a\",\"Header\":{}}");
            byte[] limit = Enumerable.Repeat((byte)' ', ResumeLimits.JsonBytes).ToArray(); minimal.CopyTo(limit, 0);
            c.That(codec.DecodeLegacy(limit).ResumeTitle == "a", "Exact 1MiB input boundary accepted");
            await Reject(c, () => codec.DecodeLegacy(new byte[ResumeLimits.JsonBytes + 1]), "Above 1MiB rejected before parsing");
            foreach (var doc in new ResumeDocument[] {
                new() { ResumeTitle = new('t',201) }, new() { Header = new() { Email = new('e',513) } },
                new() { Summary = new('n',16385) }, new() { Education = [new() { Institution = new('s',2049) }] },
                new() { Education = [.. Enumerable.Range(0,51).Select(_ => new ResumeEducation())] },
                new() { Experiences = [.. Enumerable.Range(0,22).Select(_ => new ResumeExperience { BulletsRaw = new('n',5000) })] },
                new() { Education = Many<ResumeEducation>(), Experiences = Many<ResumeExperience>(), Projects = Many<ResumeProject>(), SkillCategories = Many<ResumeSkill>(), Certifications = [new()] },
                new() { SectionOrder = [ResumeSection.Summary] }, new() { Formatting = new() { FontFamily = (ResumeFontPreference)99 } } })
                await Reject(c, () => codec.ValidateDocument(doc), "Field/aggregate/collection/order/style bounds enforced");
            codec.ValidateDocument(new() { ResumeTitle = new('t',200), Summary = new('n',16384), Header = new() { Email = new('e',512) } });
            c.That(true, "Exact scalar boundaries accepted");
        });
        await c.CaseAsync("identity-collision", async () =>
        {
            using var f = new Fixture(); var file = new ResumeFile(); byte[] bytes = f.Codec.Encode(file);
            await f.Publisher.PublishAsync(new(file, null));
            await Reject(c, () => f.Store.DocumentPath("../invalid"), "Identity cannot escape managed root");
            await c.ThrowsAsync<IOException>(() => f.Publisher.PublishAsync(new(file with { Document = new() { ResumeTitle = "changed" } }, null)), "Actual managed ID collision rejected");
            c.That((await File.ReadAllBytesAsync(f.Store.DocumentPath(file.DocumentId))).SequenceEqual(bytes), "Collision preserves original bytes");
        });
        await c.CaseAsync("stage-collision-ownership", async () =>
        {
            using var f = new Fixture(); await f.SavedAsync(); byte[] before = await f.BytesAsync();
            const string stageIdentity = "0123456789abcdef0123456789abcdef";
            f.Publisher.StageIdentityForTest = () => stageIdentity;
            string stage = f.Store.DocumentPath(f.Session.Current!.DocumentId) + "." + stageIdentity + ".stage";
            byte[] foreign = Encoding.UTF8.GetBytes("foreign pre-existing stage sentinel");
            await File.WriteAllBytesAsync(stage, foreign);
            f.Session.Edit(f.Session.Current.Document with { Summary = "attempted collision save" });
            c.That((await f.Session.SaveAsync()).Kind == ResumeResultKind.Failed && f.Session.IsDirty,
                "Stage CreateNew collision fails and retains dirty editor");
            c.That((await File.ReadAllBytesAsync(stage)).SequenceEqual(foreign) && (await f.BytesAsync()).SequenceEqual(before),
                "Cleanup never deletes an unowned colliding stage or changes prior document");
        });
        await c.CaseAsync("same-title", async () =>
        {
            using var f = new Fixture(); var one = new ResumeFile { Document = new() { ResumeTitle = "SAME: / title" } };
            var two = new ResumeFile { Document = one.Document };
            await f.Publisher.PublishAsync(new(one,null)); await f.Publisher.PublishAsync(new(two,null));
            var list = await f.Store.ListAsync();
            c.That(list.Count == 2 && list.Select(x => x.DocumentId).Distinct().Count() == 2, "Same title creates independent managed identities");
            await f.Session.OpenAsync(one.DocumentId, Discard); f.Session.Edit(one.Document with { ResumeTitle = "same: / TITLE" });
            c.That((await f.Session.SaveAsync()).Success && f.Session.Current!.DocumentId == one.DocumentId, "Title change keeps storage identity");
        });
        await c.CaseAsync("import-preservation", async () =>
        {
            using var f = new Fixture(); byte[] original = LegacyBytes(); string source = Path.Combine(f.Base,"legacy.json");
            await File.WriteAllBytesAsync(source, original); byte[] snapshot = await ResumeStore.CaptureAsync(source);
            await File.WriteAllTextAsync(source,"changed later"); File.Delete(source);
            var result = await f.Session.ImportAsync(snapshot,Discard,Consent);
            c.That(result.Kind == ResumeResultKind.Imported && ResumeCodec.SemanticallyEqual(result.File!.Document, f.Codec.DecodeLegacy(original)), "Captured import survives source changes/disappearance");
            c.That((await File.ReadAllBytesAsync(Path.Combine(f.Store.Root,"Imports",ResumeCodec.Hash(original)+".json"))).SequenceEqual(original), "Raw import backup exactly matches captured bytes");
            await c.ThrowsAsync<FileNotFoundException>(() => ResumeStore.CaptureAsync(source), "Source disappearance before capture fails safely");
            await File.WriteAllBytesAsync(source,original); await f.Session.ImportAsync(await ResumeStore.CaptureAsync(source),Discard,Consent);
            c.That((await File.ReadAllBytesAsync(source)).SequenceEqual(original), "Legacy source bytes unchanged by import");
        });
        await c.CaseAsync("import-idempotence", async () =>
        {
            using var f = new Fixture(); byte[] bytes = LegacyBytes();
            var first = await f.Session.ImportAsync(bytes,Discard,Consent); var second = await f.Session.ImportAsync(bytes,Discard,Consent);
            c.That(second.Kind == ResumeResultKind.ExistingImport && first.File!.DocumentId == second.File!.DocumentId, "Identical reimport explicitly opens existing copy");
            var changed = await f.Session.ImportAsync(LegacyBytes("changed source"),Discard,Consent);
            c.That(changed.Kind == ResumeResultKind.Imported && changed.File!.DocumentId != first.File!.DocumentId && (await f.Store.ListAsync()).Count == 2, "Changed bytes create a separate consented copy");
            await File.WriteAllTextAsync(f.Store.DocumentPath(first.File!.DocumentId), "{");
            c.That((await f.Session.ImportAsync(bytes,Discard,Consent)).Kind == ResumeResultKind.Failed && (await f.Store.ListAsync()).Count == 2,
                "Unreadable identical imported copy cannot silently duplicate or overwrite");
        });
        await c.CaseAsync("import-backup-failure", async () =>
        {
            using var f = new Fixture(); f.Publisher.BeforePhaseForTest = phase => phase == "import-backup" ? Task.FromException(new IOException("private sentinel")) : Task.CompletedTask;
            var result = await f.Session.ImportAsync(LegacyBytes(),Discard,Consent);
            c.That(result.Kind == ResumeResultKind.Failed && !Directory.Exists(f.Store.Root), "Backup failure prevents managed import publication");
        });
        await c.CaseAsync("import-backup-collision", async () =>
        {
            using var f = new Fixture(); byte[] bytes = LegacyBytes(); string backup = Path.Combine(f.Store.Root,"Imports",ResumeCodec.Hash(bytes)+".json");
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!); await File.WriteAllTextAsync(backup,"other bytes");
            c.That((await f.Session.ImportAsync(bytes,Discard,Consent)).Kind == ResumeResultKind.Failed, "Different-byte hash-addressed backup collision rejects import");
            c.That(!Directory.EnumerateFiles(f.Store.Root,"*.json").Any() && await File.ReadAllTextAsync(backup) == "other bytes", "Collision neither publishes nor destroys foreign backup");
        });
        foreach (var pair in new[] { ("staging-failure","stage"), ("reopen-failure","reopen"), ("replacement-failure","replace"), ("revision-backup-failure","revision-backup") })
            await c.CaseAsync(pair.Item1, async () =>
            {
                using var f = new Fixture(); await f.SavedAsync(); byte[] prior = await f.BytesAsync();
                f.Session.Edit(f.Session.Current!.Document with { Summary = "new unsaved content" });
                f.Publisher.BeforePhaseForTest = async phase =>
                {
                    if (phase != pair.Item2) return;
                    if (phase == "reopen") await File.WriteAllTextAsync(Directory.EnumerateFiles(f.Store.Root,"*.stage").Single(),"{");
                    else throw new IOException("controlled failure");
                };
                c.That((await f.Session.SaveAsync()).Kind == ResumeResultKind.Failed && f.Session.IsDirty, "Publication failure retains dirty session");
                c.That((await f.BytesAsync()).SequenceEqual(prior) && f.Session.Current!.Document.Summary == "new unsaved content", "Failure preserves prior file and editor content");
                c.That(!await f.Session.GuardDepartureAsync(Save), "Failed Save blocks departure");
            });
        await c.CaseAsync("destination-change", async () =>
        {
            using var f = new Fixture(); await f.SavedAsync(); f.Session.Edit(f.Session.Current!.Document with { Summary = "new" });
            var foreign = f.Codec.Encode(f.Session.Current with { Document = new() { Summary = "external revision" }, Revision = 100 });
            f.Publisher.BeforePhaseForTest = phase => phase == "replace" ? File.WriteAllBytesAsync(f.Store.DocumentPath(f.Session.Current!.DocumentId), foreign) : Task.CompletedTask;
            c.That((await f.Session.SaveAsync()).Kind == ResumeResultKind.Failed, "Final destination recheck rejects external change");
            c.That((await f.BytesAsync()).SequenceEqual(foreign), "Externally changed destination never overwritten");
        });
        await c.CaseAsync("backup-rotation-recovery", async () =>
        {
            using var f = new Fixture(); await f.SavedAsync(); string id = f.Session.Current!.DocumentId;
            for (int i=1;i<=7;i++) { f.Session.Edit(f.Session.Current!.Document with { Summary = "revision "+i }); c.That((await f.Session.SaveAsync()).Success,"Revision save succeeds"); }
            var backups = f.Store.Backups(id); c.That(backups.Count == 5,"Exactly five verified revision backups retained");
            var revisions = new List<long>(); foreach(string path in backups) revisions.Add((await f.Store.ReadBackupAsync(id,path)).Revision);
            c.That(revisions.SequenceEqual(new long[] {7,6,5,4,3}),"Revision backups ordered newest first, oldest rotated");
            var result = await f.Session.RecoverAsync(id,backups[^1],Discard);
            c.That(result.Kind == ResumeResultKind.Recovered && result.File!.Revision == 9 && result.File.RecoveredFromRevision == 3,"Recovery republishes validated content as new revision");
            c.That((await f.Store.ReadAsync(id)).File.Document.Summary == "revision 2","Recovery roundtrips through production store");
        });
        await c.CaseAsync("corrupt-recovery", async () =>
        {
            using var f = new Fixture(); await f.SavedAsync(); f.Session.Edit(f.Session.Current!.Document with { Summary = "next" }); await f.Session.SaveAsync();
            string id = f.Session.Current!.DocumentId; string backup = f.Store.Backups(id).Single(); await File.WriteAllTextAsync(f.Store.DocumentPath(id),"{");
            c.That((await f.Session.RecoverAsync(id,backup,Discard)).Kind == ResumeResultKind.Recovered,"Malformed current file recovers from verified prior revision");
            c.That(Directory.EnumerateFiles(Path.Combine(f.Store.Root,"Quarantine",id)).Count()==1,"Corrupt prior bytes preserved separately");
            await File.WriteAllBytesAsync(backup,f.Codec.Encode(new ResumeFile { DocumentId=id,Document=new() { Summary="tampered" } }));
            await c.ThrowsAsync<InvalidDataException>(() => f.Store.ReadBackupAsync(id,backup),"Tampered hash-addressed revision rejected");
        });
        await c.CaseAsync("future-protection", async () =>
        {
            using var f = new Fixture(); await f.SavedAsync(); f.Session.Edit(f.Session.Current!.Document with { Summary="next" }); await f.Session.SaveAsync();
            string id=f.Session.Current!.DocumentId; var node=JsonNode.Parse(await f.BytesAsync())!; node["SchemaVersion"]=2;
            byte[] future=Encoding.UTF8.GetBytes(node.ToJsonString()); await File.WriteAllBytesAsync(f.Store.DocumentPath(id),future);
            c.That((await f.Session.OpenAsync(id,Discard)).Kind==ResumeResultKind.Failed,"Future managed schema cannot open for downgrade editing");
            c.That((await f.Session.RecoverAsync(id,f.Store.Backups(id).Single(),Discard)).Kind==ResumeResultKind.Failed,"Recovery cannot overwrite future schema");
            c.That((await f.BytesAsync()).SequenceEqual(future),"Future bytes remain protected");
            byte[] truncatedFuture = Encoding.UTF8.GetBytes("{\"SchemaVersion\":2,\"Document\":");
            await File.WriteAllBytesAsync(f.Store.DocumentPath(id), truncatedFuture);
            c.That((await f.Session.RecoverAsync(id,f.Store.Backups(id).Single(),Discard)).Kind==ResumeResultKind.Failed
                && (await f.BytesAsync()).SequenceEqual(truncatedFuture), "Malformed tail cannot erase an observed future schema during recovery");
            await Reject(c,()=>f.Codec.DecodeLegacy(future),"Managed envelope never reinterpreted as legacy");
        });
        await c.CaseAsync("dirty-semantics", async () =>
        {
            using var f=new Fixture(); await f.SavedAsync(CompleteDocument()); var baseline=f.Session.Current!.Document;
            foreach(var doc in new[] { baseline with { ResumeTitle="edited" },baseline with { Header=baseline.Header with { Email="edited" } },
                baseline with { Summary="edited" },baseline with { Education=baseline.Education.Add(new()) },
                baseline with { ShowProjects=!baseline.ShowProjects },baseline with { SectionOrder=[..baseline.SectionOrder.Reverse()] },
                baseline with { Formatting=baseline.Formatting with { SpacingMode=ResumeDensity.Compact } } })
            { f.Session.Edit(doc); c.That(f.Session.IsDirty,"Text/collections/visibility/order/preferences/title cause semantic dirty"); f.Session.Edit(baseline); c.That(!f.Session.IsDirty,"Semantic revert clears dirty independently of revision/event count"); }
            c.That((await f.Session.SaveAsync()).Success,"Unchanged Save is an idempotent success");
        });
        await c.CaseAsync("departure-guard", async () =>
        {
            using var f=new Fixture(); await f.SavedAsync(); string id=f.Session.Current!.DocumentId;
            f.Session.Edit(f.Session.Current.Document with { Summary="dirty" });
            c.That(!await f.Session.GuardDepartureAsync(Cancel) && f.Session.IsDirty,"Cancel retains current dirty session");
            c.That((await f.Session.NewAsync(Cancel)).Kind==ResumeResultKind.Cancelled && f.Session.Current!.DocumentId==id,"New cannot silently discard");
            c.That((await f.Session.OpenAsync(id,Cancel)).Kind==ResumeResultKind.Cancelled,"Second Open routes through same dirty guard");
            c.That((await f.Session.ImportAsync(LegacyBytes(),Cancel,Consent)).Kind==ResumeResultKind.Cancelled,"Import routes through same dirty guard");
            c.That(!await f.Session.PrepareCloseAsync(Cancel) && !f.Session.IsClosed,"Close Cancel retains open admission");
            c.That(await f.Session.GuardDepartureAsync(Discard) && !f.Session.IsDirty && f.Session.Current!.Document.Summary=="","Explicit Discard restores saved baseline");
            f.Session.Edit(f.Session.Current.Document with { Summary="save before leave" });
            c.That(await f.Session.GuardDepartureAsync(Save) && !f.Session.IsDirty,"Save guard commits before allowing departure");
        });
        await c.CaseAsync("concurrent-save", async () =>
        {
            using var f=new Fixture(); await f.SavedAsync(); var entered=Signal(); var release=Signal();
            f.Session.Edit(f.Session.Current!.Document with { Summary="captured" });
            f.Publisher.BeforePhaseForTest=async phase=>{if(phase=="stage"){entered.TrySetResult();await release.Task;}};
            var save=f.Session.SaveAsync();await entered.Task;
            f.Session.Edit(f.Session.Current!.Document with { Summary="later edits" });
            c.That((await f.Session.SaveAsync()).Kind==ResumeResultKind.Busy,"Second Save returns typed Busy without queue");
            c.That((await f.Session.NewAsync(Discard)).Kind==ResumeResultKind.Busy,"Active mutation blocks replacement");
            release.SetResult();c.That((await save).Success && f.Session.IsDirty,"Successful captured save leaves later semantic edits dirty");
            c.That((await f.Store.ReadAsync(f.Session.Current!.DocumentId)).File.Document.Summary=="captured" && f.Session.Current.Document.Summary=="later edits","Published snapshot and live editor remain distinct");
        });
        await c.CaseAsync("save-copy", async () =>
        {
            using var f=new Fixture();await f.SavedAsync();string original=f.Session.Current!.DocumentId;byte[] bytes=await f.BytesAsync();
            f.Session.Edit(f.Session.Current.Document with { Summary="copy" }); var copied=await f.Session.SaveAsync(true);
            c.That(copied.Success && f.Session.Current!.DocumentId!=original && !f.Session.IsDirty,"Save Copy creates and activates a new saved identity");
            c.That((await File.ReadAllBytesAsync(f.Store.DocumentPath(original))).SequenceEqual(bytes),"Save Copy leaves original bytes unchanged");
        });
        await c.CaseAsync("privacy", async () =>
        {
            using var f=new Fixture();await f.SavedAsync(new() { Summary="PRIVATE_SENTINEL_EMAIL@example.invalid" });
            f.Session.Edit(f.Session.Current!.Document with { Summary="PRIVATE_SENTINEL_PHONE_123" });
            f.Publisher.BeforePhaseForTest=_=>Task.FromException(new IOException("PRIVATE_SENTINEL_PATH_"+f.Base));await f.Session.SaveAsync();
            string log=string.Join("\n",f.Log);
            c.That(!log.Contains("PRIVATE_SENTINEL",StringComparison.Ordinal) && !log.Contains(f.Base,StringComparison.Ordinal),"Production diagnostics exclude content/private paths/exception messages");
            c.That(log.Contains("exceptionType=IOException",StringComparison.Ordinal),"Safe exception type diagnostic retained");
        });
        await c.CaseAsync("lazy-store", async () =>
        {
            using var f=new Fixture();c.That(!Directory.Exists(f.Store.Root) && f.Store.Enumerations==0,"Foundation construction performs no directory scan/write");
            await f.SavedAsync(); await File.WriteAllTextAsync(Path.Combine(f.Store.Root,Guid.NewGuid().ToString("N")+".json"),"{");
            var entries=await f.Store.ListAsync();c.That(entries.Count==2 && entries.Count(x=>x.CanOpen)==1,"Corrupt dashboard entry does not crash healthy listing");
            for(int i=0;i<100;i++) await File.WriteAllTextAsync(Path.Combine(f.Store.Root,Guid.NewGuid().ToString("N")+".json"),"{");
            c.That((await f.Store.ListAsync()).Count==100 && (await f.Store.ListAsync(1)).Count==2,"Dashboard pagination bounded to 100 documents");
        });
        await c.CaseAsync("shutdown-retention", async () =>
        {
            using var f=new Fixture();await f.SavedAsync();var entered=Signal();var release=Signal();
            f.Session.Edit(f.Session.Current!.Document with { Summary="retained publication" });
            f.Publisher.BeforePhaseForTest=async phase=>{if(phase=="stage"){entered.SetResult();await release.Task;}};
            var save=f.Session.SaveAsync();await entered.Task;var close=f.Session.PrepareCloseAsync(Save);
            c.That(!close.IsCompleted,"Close retains admitted save before dirty decision/final shutdown");
            release.SetResult();await save;c.That(await close && f.Session.IsClosed,"Settled save allows normal close");
            c.That((await f.Session.SaveAsync()).Kind==ResumeResultKind.Closed,"Closed admission rejects new publication");
            c.That((await f.Store.ReadAsync(f.Session.Current!.DocumentId)).File.Document.Summary=="retained publication","Retained save validates on reopen");
        });
        await c.CaseAsync("undo-recent", async () =>
        {
            using var f=new Fixture();await f.SavedAsync();
            for(int i=0;i<40;i++) f.Session.Edit(f.Session.Current!.Document with { Summary="edit "+i });
            int count=0;while(f.Session.Undo())count++;
            c.That(count==30,"Undo retains only 30 snapshots");
            for(int i=0;i<25;i++) await f.Session.NewAsync(Discard);
            c.That(f.Session.Recent.Count==20,"Recent identities bounded to 20");
        });
        await c.CaseAsync("editor-all-fields", async () =>
        {
            using var f=new Fixture();await f.SavedAsync(CompleteDocument());
            var vm=new Axora.Studio.ViewModels.ResumeViewModel(f.Session,f.Store,new NullPicker());vm.RebuildEditor();
            var fields=vm.Groups.SelectMany(group=>group.Fields.Concat(group.Items.SelectMany(item=>item.Fields))).ToArray();
            int expected=typeof(ResumeHeader).GetProperties().Length+2+8+typeof(ResumePreferences).GetProperties().Length+
                new[]{typeof(ResumeEducation),typeof(ResumeExperience),typeof(ResumeSkill),typeof(ResumeProject),typeof(ResumeCertification),typeof(ResumeAchievement),typeof(ResumeResponsibility)}.Sum(type=>type.GetProperties().Count(property=>property.Name!="Id"));
            c.That(fields.Length==expected && fields.Select(field=>field.Path).Distinct().Count()==expected,"Editor exposes every accepted semantic scalar exactly once");
            foreach(var field in fields.Where(field=>field.ValueType==typeof(string) && !field.Path.EndsWith("AccentHexColor",StringComparison.Ordinal)))field.Text="native-free edited "+field.Path;
            c.That(f.Session.IsDirty && fields.All(field=>string.IsNullOrEmpty(field.Error)),"Every semantic text field edits through the production session");
            await vm.SaveCommand.ExecuteAsync(null);
            c.That(!f.Session.IsDirty && (await f.Store.ReadAsync(f.Session.Current!.DocumentId)).File.Document.Certifications[0].CredentialId.Contains("edited"),"Full editor fields Save/reopen through production codec/publisher");
            var education=vm.Groups.Single(group=>group.Title=="Education");education.AddCommand!.Execute(null);
            c.That(f.Session.Current!.Document.Education.Length==2,"Add entry is session-owned");
            vm.Groups.Single(group=>group.Title=="Education").Items[1].UpCommand.Execute(null);
            c.That(f.Session.Current.Document.Education[0].Institution=="","Entry order changes semantic document");
            vm.Groups.Single(group=>group.Title=="Education").Items[0].RemoveCommand.Execute(null);
            c.That(f.Session.Current.Document.Education.Length==1,"Remove entry edits draft without deleting managed document");
        });
        await c.CaseAsync("invalid-editor-draft", async () =>
        {
            using var f=new Fixture();await f.SavedAsync();var vm=new Axora.Studio.ViewModels.ResumeViewModel(f.Session,f.Store,new NullPicker());vm.RebuildEditor();
            var accent=vm.Groups.Single(group=>group.Title=="Preferences").Fields.Single(field=>field.Path.EndsWith("AccentHexColor",StringComparison.Ordinal));
            accent.Text="#";c.That(f.Session.HasInvalidDraft && f.Session.IsDirty && accent.Error!="","Incomplete typed preference remains visible and dirty");
            byte[] prior=await f.BytesAsync();await vm.SaveCopyCommand.ExecuteAsync(null);
            c.That(accent.Text=="#" && f.Session.HasInvalidDraft && (await f.BytesAsync()).SequenceEqual(prior),"Failed Save Copy preserves invalid editor text and original bytes");
            c.That(!await f.Session.GuardDepartureAsync(Save),"Invalid draft blocks Save departure even when persisted semantics were clean");
            accent.Text="#AABBCC";await vm.SaveCommand.ExecuteAsync(null);
            c.That(!f.Session.IsDirty && !f.Session.HasInvalidDraft,"Corrected preference can Save normally");
            accent.Text="bad";await f.Session.GuardDepartureAsync(Discard);
            c.That(!f.Session.IsDirty && vm.Groups.Single(group=>group.Title=="Preferences").Fields.Single(field=>field.Path.EndsWith("AccentHexColor",StringComparison.Ordinal)).Text=="#AABBCC","Explicit Discard restores valid baseline into editor");
        });
        await c.CaseAsync("fallback-context", async () =>
        {
            using var f=new Fixture();await f.SavedAsync();f.Session.Edit(f.Session.Current!.Document with {Summary="lost UI loop retention"});
            var entered=Signal();var release=Signal();f.Publisher.BeforePhaseForTest=async phase=>{if(phase=="stage"){entered.SetResult();await release.Task.ConfigureAwait(false);}};
            var context=new CaptiveContext();var prior=SynchronizationContext.Current;Task<ResumeResult> save;
            try{SynchronizationContext.SetSynchronizationContext(context);save=f.Session.SaveAsync();}finally{SynchronizationContext.SetSynchronizationContext(prior);}
            await entered.Task;Task settlement=f.Session.StopAsync();release.SetResult();await settlement;
            c.That((await save).Success && context.Posts==0,"Integrity-critical production continuations settle without an abandoned UI context");
        });
        await c.CaseAsync("observer-failure", async () =>
        {
            using var f=new Fixture();f.Session.Changed+=(_,_)=>throw new InvalidOperationException("PRIVATE_OBSERVER_TEXT");
            await f.Session.NewAsync(Discard);c.That((await f.Session.SaveAsync()).Success && !f.Session.IsActive,"Throwing UI observer cannot strand admitted publication");
            c.That(!string.Join("\n",f.Log).Contains("PRIVATE_OBSERVER_TEXT",StringComparison.Ordinal),"Observer failure diagnostic excludes exception message");
        });
        await c.CaseAsync("missing-document-recovery", async () =>
        {
            using var f=new Fixture();await f.SavedAsync();f.Session.Edit(f.Session.Current!.Document with {Summary="next"});await f.Session.SaveAsync();
            string id=f.Session.Current!.DocumentId;File.Delete(f.Store.DocumentPath(id));
            var entries=await f.Store.ListAsync();c.That(entries.Count==1 && !entries[0].CanOpen,"Missing current document remains discoverable through retained revision identity");
            var vm=new Axora.Studio.ViewModels.ResumeViewModel(f.Session,f.Store,new NullPicker());
            await vm.ReviewRecoveryCommand.ExecuteAsync(entries[0]);c.That(vm.RecoveryEntries.Count==1,"Dashboard offers verified recovery even when Open is unavailable");
            c.That((await f.Session.RecoverAsync(id,vm.RecoveryEntries[0].Backup,Discard)).Kind==ResumeResultKind.Recovered && (await f.Store.ReadAsync(id)).File.Document.Summary=="","Missing document recovery republishes verified semantic data");
        });
        await c.CaseAsync("redirected-root", async () =>
        {
            using var f=new Fixture();string outside=Path.Combine(f.Base,"legacy-owned");Directory.CreateDirectory(outside);Directory.CreateDirectory(Path.GetDirectoryName(f.Store.Root)!);
            var info=new System.Diagnostics.ProcessStartInfo("cmd.exe") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            info.ArgumentList.Add("/c");info.ArgumentList.Add("mklink");info.ArgumentList.Add("/J");info.ArgumentList.Add(f.Store.Root);info.ArgumentList.Add(outside);
            using var process=System.Diagnostics.Process.Start(info)!;await process.WaitForExitAsync();
            c.That(process.ExitCode==0,"Test-owned NTFS junction fixture constructed");
            try
            {
                await f.Session.NewAsync(Discard);c.That((await f.Session.SaveAsync()).Kind==ResumeResultKind.Failed,"Redirected managed root cannot write a legacy/external directory");
                c.That(!Directory.EnumerateFileSystemEntries(outside).Any(),"Redirected target bytes/files remain untouched");
                await c.ThrowsAsync<IOException>(()=>f.Store.ListAsync(),"Redirected managed library cannot be scanned as Studio-owned data");
            }
            finally { Directory.Delete(f.Store.Root); } // Only the exact test-owned junction, never its target recursively.
        });
        foreach (bool recovery in new[] { false, true }) foreach (bool fail in new[] { false, true })
            await c.CaseAsync("f1-" + (recovery ? "recovery" : "import") + (fail ? "-failed" : "-owned"), () => ReplacementOwnershipAsync(c, recovery, fail));
        await c.CaseAsync("f1-replacement-close", () => ReplacementCloseAsync(c));
        foreach (string kind in new[] { "ownership", "utf8", "json", "type", "future" })
            await c.CaseAsync("f2-maintenance-" + kind, () => MaintenanceWarningAsync(c, kind));
        await c.CaseAsync("f2-maintenance-later-edit", () => MaintenanceWarningAsync(c, "ownership", true));
        foreach (string kind in new[] { "json", "utf8", "identity", "future", "unreadable", "ownership" })
            await c.CaseAsync("f3-unselected-" + kind, () => RecoveryIsolationAsync(c, kind));
        foreach (string kind in new[] { "future", "semantic", "filename", "import-future", "import-semantic", "import-collision" })
            await c.CaseAsync("x1-" + kind, () => ManagedIsolationAsync(c, kind));
        foreach (string kind in new[] { "json", "future", "identity", "ownership", "preserve-failure", "identical", "collision", "degraded", "bound" })
            await c.CaseAsync("x2-" + kind, () => BoundedMaintenanceAsync(c, kind));
        await c.CaseAsync("f3-duplicate-revision", async () =>
        {
            using var f = new Fixture(); await f.SavedAsync();
            f.Session.Edit(f.Session.Current!.Document with { Summary = "current" }); await f.Session.SaveAsync();
            string id = f.Session.Current.DocumentId, selected = f.Store.Backups(id).Single();
            var selectedFile = await f.Store.ReadBackupAsync(id, selected);
            byte[] alternate = f.Codec.Encode(selectedFile with { Document = selectedFile.Document with { Summary = "different same-revision content" } });
            string duplicate = Path.Combine(Path.GetDirectoryName(selected)!, selectedFile.Revision.ToString("D20") + "_" + ResumeCodec.Hash(alternate) + ".json");
            await File.WriteAllBytesAsync(duplicate, alternate);
            var result = await f.Session.RecoverAsync(id, selected, Discard);
            c.That(result.Success && result.File!.Revision > 2 && ResumeCodec.SemanticallyEqual(result.File.Document, selectedFile.Document), "Duplicate owned revision numbers never change the selected content or downgrade the new revision");
            c.That((await File.ReadAllBytesAsync(duplicate)).SequenceEqual(alternate), "Unselected duplicate revision artifact is preserved");
        });
    }
    private static async Task ManagedIsolationAsync(Checks c, string kind)
    {
        using var f = new Fixture(); await f.SavedAsync();
        var healthy = f.Session.Current!;
        string badId = Guid.NewGuid().ToString("N");
        byte[] source = LegacyBytes("PRIVATE_SENTINEL X1 import"), bad = f.Codec.Encode(healthy with { DocumentId = badId });
        var node = JsonNode.Parse(bad)!;
        if (kind.Contains("future", StringComparison.Ordinal)) node["SchemaVersion"] = 2;
        else node["DocumentId"] = "invalid-id";
        bad = Encoding.UTF8.GetBytes(node.ToJsonString());
        string path = Path.Combine(f.Store.Root, kind == "filename" ? "not-an-identity.json" : badId + ".json");
        await File.WriteAllBytesAsync(path, bad);
        if (kind.StartsWith("import-", StringComparison.Ordinal))
        {
            c.That(await f.Store.FindImportAsync(ResumeCodec.Hash(source)) is null, "X1 unrelated invalid candidate does not abort provenance scan");
            if (kind == "import-collision")
            {
                string occupied = f.Store.DocumentPath(ResumeCodec.Hash(source)[..32].ToLowerInvariant());
                await File.WriteAllBytesAsync(occupied, bad);
                using (var locked = new FileStream(occupied, FileMode.Open, FileAccess.Read, FileShare.None))
                    c.That((await f.Session.ImportAsync(source, Discard, Consent)).Kind == ResumeResultKind.Failed, "X1 unreadable deterministic import target fails safely");
                c.That((await File.ReadAllBytesAsync(occupied)).SequenceEqual(bad) && Directory.GetFiles(f.Store.Root, "*.json").Length == 3, "X1 occupied import identity is neither overwritten nor duplicated");
                c.That((await f.Session.ImportAsync(source, Discard, Consent)).Kind == ResumeResultKind.Failed, "X1 invalid deterministic target also fails safely");
            }
            else c.That((await f.Session.ImportAsync(source, Discard, Consent)).Success, "X1 unrelated invalid managed document cannot block reviewed copy import");
        }
        else
        {
            var listed = await f.Store.ListAsync();
            c.That(listed.Any(x => x.DocumentId == healthy.DocumentId && x.CanOpen) && listed.Any(x => !x.CanOpen), "X1 healthy dashboard entry survives individual invalid document");
        }
        c.That((await File.ReadAllBytesAsync(path)).SequenceEqual(bad), "X1 excluded candidate bytes remain untouched");
        for (int i = Directory.GetFiles(f.Store.Root, "*.json").Length; i <= ResumeLimits.DashboardDocuments; i++)
            await File.WriteAllTextAsync(Path.Combine(f.Store.Root, Guid.NewGuid().ToString("N") + ".json"), "{");
        await c.ThrowsAsync<InvalidDataException>(() => f.Store.ListAsync(), "X1 library-wide listing ceiling is not swallowed");
        await c.ThrowsAsync<InvalidDataException>(() => f.Store.FindImportAsync(new string('A', 64)), "X1 library-wide provenance ceiling is not swallowed");
    }
    private static string QuarantinePath(Fixture f, byte[] bytes) => Path.Combine(f.Store.Root, "Quarantine", f.Session.Current!.DocumentId, "revision_" + ResumeCodec.Hash(bytes) + ".json");
    private static async Task BoundedMaintenanceAsync(Checks c, string kind)
    {
        using var f = new Fixture(); await f.SavedAsync();
        for (int i = 0; i < 5; i++) { f.Session.Edit(f.Session.Current!.Document with { Summary = "normal " + i }); await f.Session.SaveAsync(); }
        string id = f.Session.Current!.DocumentId, folder = Path.Combine(f.Store.Root, "Revisions", id), suspect = f.Store.Backups(id).Last();
        byte[] damaged = DamagedBackup(kind is "future" or "identity" or "ownership" ? kind : "json", await File.ReadAllBytesAsync(suspect), f.Codec);
        await File.WriteAllBytesAsync(suspect, damaged);
        string quarantine = QuarantinePath(f, damaged);
        byte[] collision = Encoding.UTF8.GetBytes("PRIVATE_SENTINEL foreign quarantine collision");
        if (kind is "identical" or "collision") { Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!); await File.WriteAllBytesAsync(quarantine, kind == "identical" ? damaged : collision); }
        if (kind == "preserve-failure") f.Publisher.BeforePhaseForTest = phase => phase == "quarantine-preserve" ? Task.FromException(new IOException("PRIVATE_SENTINEL quarantine failure")) : Task.CompletedTask;
        f.Session.Edit(f.Session.Current.Document with { Summary = "save one" }); var first = await f.Session.SaveAsync();
        c.That(first.Kind == ResumeResultKind.Saved && first.Message.Contains("maintenance", StringComparison.OrdinalIgnoreCase)
            && (await f.Store.ReadAsync(id)).File.Document.Summary == "save one", "X2 first destination is committed with truthful maintenance warning");
        bool preserved = File.Exists(suspect) && (await File.ReadAllBytesAsync(suspect)).SequenceEqual(damaged)
            || File.Exists(quarantine) && (await File.ReadAllBytesAsync(quarantine)).SequenceEqual(damaged);
        c.That(preserved, "X2 suspect bytes preserved before any active removal");
        if (kind is "preserve-failure" or "collision")
            c.That(File.Exists(suspect) && (await File.ReadAllBytesAsync(suspect)).SequenceEqual(damaged), "X2 failed preservation never deletes original");
        else c.That(!File.Exists(suspect) && (await File.ReadAllBytesAsync(quarantine)).SequenceEqual(damaged), "X2 verified hash-addressed quarantine removes only preserved active artifact");
        f.Session.Edit(f.Session.Current.Document with { Summary = "save two" }); c.That((await f.Session.SaveAsync()).Success, "X2 second modifying Save succeeds");
        c.That(f.Store.Backups(id).Count >= 1, "X2 valid recovery choices survive second Save and maintenance warning");
        for (int i = 0; i < 8; i++) { f.Session.Edit(f.Session.Current.Document with { Summary = "repeat " + i }); c.That((await f.Session.SaveAsync()).Success, "X2 repeated modifying Save commits"); }
        c.That(Directory.GetFiles(folder, "*.json").Length <= ResumeLimits.RevisionBackups + (kind is "preserve-failure" or "collision" ? 1 : 0), "X2 repeated Saves keep active revisions bounded");
        string selected = f.Store.Backups(id).First(); var selectedFile = await f.Store.ReadBackupAsync(id, selected); long before = f.Session.Current.Revision;
        var recovered = await f.Session.RecoverAsync(id, selected, Discard);
        c.That(recovered.Success && recovered.File!.Revision > before && ResumeCodec.SemanticallyEqual(recovered.File.Document, selectedFile.Document), "X2 selected valid content recovers monotonically after repeated Saves");
        if (kind == "collision") c.That((await File.ReadAllBytesAsync(quarantine)).SequenceEqual(collision), "X2 different quarantine collision is never overwritten");
        f.Publisher.BeforePhaseForTest = null;
        if (kind == "collision") File.Delete(quarantine); // Test-owned obstruction only.
        f.Session.Edit(f.Session.Current.Document with { Summary = "maintenance repaired" }); await f.Session.SaveAsync();
        c.That((await File.ReadAllBytesAsync(quarantine)).SequenceEqual(damaged) && Directory.GetFiles(folder, "*.json").Length <= 5, "X2 later maintenance preserves exact evidence and restores five active revisions");
        if (kind is "degraded" or "bound")
        {
            byte[] valid = await File.ReadAllBytesAsync(f.Store.Backups(id).First());
            int target = kind == "bound" ? 32 : 7;
            for (int i = Directory.GetFiles(folder, "*.json").Length; i < target; i++) await File.WriteAllBytesAsync(Path.Combine(folder, "invalid-" + i + ".json"), valid);
            c.That(f.Store.Backups(id).Count > 0, "X2 bounded degraded enumeration remains discoverable beyond six artifacts");
            f.Session.Edit(f.Session.Current.Document with { Summary = "bounded admission" }); byte[] disk = await f.BytesAsync(); var saved = await f.Session.SaveAsync();
            if (kind == "bound") c.That(saved.Kind == ResumeResultKind.Failed && (await f.BytesAsync()).SequenceEqual(disk) && Directory.GetFiles(folder, "*.json").Length == 32, "X2 full artifact budget rejects growth before destination commit");
            else
            {
                c.That(saved.Success && Directory.GetFiles(folder, "*.json").Length == 5, "X2 high-sorting invalid filenames cannot displace five valid recovery revisions");
                foreach (string remaining in f.Store.Backups(id))
                    c.That((await f.Store.ReadBackupAsync(id, remaining)).DocumentId == id, "X2 every retained active revision is strictly verified after degraded maintenance");
            }
            if (kind == "bound")
            {
                await File.WriteAllBytesAsync(Path.Combine(folder, "one-beyond-budget.json"), valid);
                await c.ThrowsAsync<InvalidDataException>(() => Task.FromResult(f.Store.Backups(id)), "X2 scan ceiling remains enforced beyond the finite artifact budget");
            }
        }
        c.That(!string.Join("\n", f.Log).Contains("PRIVATE_SENTINEL", StringComparison.Ordinal) && !string.Join("\n", f.Log).Contains(f.Base, StringComparison.Ordinal), "X2 diagnostics exclude content filenames paths and raw failures");
    }
    private static bool EditingAvailable(object owner) => owner.GetType().GetProperty("CanEdit")?.GetValue(owner) as bool? ?? true;
    private static bool RejectsEdit(Action action)
    { try { action(); return false; } catch (InvalidOperationException) { return true; } }
    private static async Task ReplacementOwnershipAsync(Checks c, bool recovery, bool fail)
    {
        using var f = new Fixture(); await f.SavedAsync(CompleteDocument());
        if (recovery) { f.Session.Edit(f.Session.Current!.Document with { Summary = "current revision" }); await f.Session.SaveAsync(); }
        var before = f.Session.Current!; byte[] diskBefore = await f.BytesAsync();
        var vm = new Axora.Studio.ViewModels.ResumeViewModel(f.Session, f.Store, new NullPicker()); vm.RebuildEditor();
        var title = vm.Groups[0].Fields[0]; string titleBefore = title.Text;
        var accent = vm.Groups.Single(g => g.Title == "Preferences").Fields.Single(x => x.Path.EndsWith("AccentHexColor", StringComparison.Ordinal));
        string accentBefore = accent.Text;
        var education = vm.Groups.Single(g => g.Title == "Education");
        var entered = Signal(); var release = Signal();
        f.Publisher.BeforePhaseForTest = async phase =>
        {
            if (phase != "stage") return;
            entered.TrySetResult(); await release.Task.ConfigureAwait(false);
            if (fail) throw new IOException("PRIVATE_SENTINEL replacement fixture failure");
        };
        var replacing = recovery ? f.Session.RecoverAsync(before.DocumentId, f.Store.Backups(before.DocumentId).First(), Discard)
            : f.Session.ImportAsync(LegacyBytes("Replacement fixture"), Discard, Consent);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            c.That(!EditingAvailable(f.Session) && !EditingAvailable(vm), "Replacement admission freezes session and VM availability before publication");
            c.That(RejectsEdit(() => f.Session.Edit(before.Document with { Summary = "must not be admitted" })), "Programmatic text Edit rejects replacement ownership");
            c.That(RejectsEdit(() => f.Session.Edit(before.Document with { Education = before.Document.Education.Add(new()) })), "Programmatic structural Edit rejects replacement ownership");
            c.That(!f.Session.Undo(), "Undo cannot mutate an admitted replacement");
            title.Text = "must not be accepted"; accent.Text = "#";
            education.AddCommand!.Execute(null); education.Items[0].UpCommand.Execute(null); education.Items[0].RemoveCommand.Execute(null);
            education.UpCommand!.Execute(null); vm.UndoCommand.Execute(null);
            c.That(title.Text == titleBefore && accent.Text == accentBefore && !f.Session.HasInvalidDraft, "Frozen VM fields reject valid and invalid draft input without accepting it");
            c.That(RejectsEdit(() => f.Session.SetInvalidDraft(true)), "Programmatic invalid-draft mutation also rejects replacement ownership");
            c.That(ResumeCodec.SemanticallyEqual(f.Session.Current!.Document, before.Document) && f.Session.Current.Revision == before.Revision, "Text/Add/Remove/Reorder/Undo attempts leave the old document exactly intact");
        }
        finally { release.TrySetResult(); }
        var result = await replacing;
        c.That(result.Success == !fail && EditingAvailable(f.Session) && EditingAvailable(vm), "Replacement settlement always restores editing availability");
        if (fail)
        {
            c.That(f.Session.Current == before && !f.Session.IsDirty && !f.Session.HasInvalidDraft && (await f.BytesAsync()).SequenceEqual(diskBefore), "Replacement failure retains original active state and authoritative bytes");
            title.Text = "editing after failure";
            c.That(f.Session.Current!.Document.ResumeTitle == "editing after failure" && f.Session.IsDirty, "Original editor accepts edits again after replacement failure");
        }
        else
        {
            c.That(!f.Session.IsDirty && (recovery ? f.Session.Current!.Document.Summary != before.Document.Summary : f.Session.Current!.DocumentId != before.DocumentId), "Captured replacement becomes the clean active document");
            var active = f.Session.Current!; title.Text = "stale field"; education.AddCommand!.Execute(null);
            c.That(f.Session.Current == active && !f.Session.HasInvalidDraft, "Stale old editor fields/actions cannot mutate the replacement document");
            vm.RebuildEditor(); vm.Groups[0].Fields[0].Text = "editing replacement";
            c.That(f.Session.Current!.Document.ResumeTitle == "editing replacement" && f.Session.IsDirty, "Rebuilt editor edits the new active document");
        }
        // The replacement fix must not freeze ordinary Save or Save Copy.
        foreach (bool copy in new[] { false, true })
        {
            f.Publisher.BeforePhaseForTest = null; await f.Session.SaveAsync(); vm.RebuildEditor();
            vm.Groups[0].Fields[0].Text = "snapshot " + copy;
            var saveEntered = Signal(); var saveRelease = Signal();
            f.Publisher.BeforePhaseForTest = async phase => { if (phase == "stage") { saveEntered.SetResult(); await saveRelease.Task.ConfigureAwait(false); } };
            var saving = f.Session.SaveAsync(copy); await saveEntered.Task;
            c.That(EditingAvailable(f.Session) && EditingAvailable(vm), "Ordinary Save/Save Copy keeps semantic controls available");
            vm.Groups[0].Fields[0].Text = "later UI edit " + copy;
            saveRelease.SetResult(); var saved = await saving;
            c.That(saved.Success && f.Session.IsDirty && f.Session.Current!.Document.ResumeTitle == "later UI edit " + copy
                && (await f.Store.ReadAsync(f.Session.Current.DocumentId)).File.Document.ResumeTitle == "snapshot " + copy, "Save/Save Copy preserves accepted later VM edits as dirty");
        }
    }
    private static async Task ReplacementCloseAsync(Checks c)
    {
        foreach (bool recovery in new[] { false, true })
        {
            using var f = new Fixture(); await f.SavedAsync();
            f.Session.Edit(f.Session.Current!.Document with { Summary = "current" }); await f.Session.SaveAsync();
            var entered = Signal(); var release = Signal();
            f.Publisher.BeforePhaseForTest = async phase => { if (phase == "stage") { entered.TrySetResult(); await release.Task.ConfigureAwait(false); } };
            var replacing = recovery ? f.Session.RecoverAsync(f.Session.Current!.DocumentId, f.Store.Backups(f.Session.Current.DocumentId).First(), Discard)
                : f.Session.ImportAsync(LegacyBytes(), Discard, Consent);
            await entered.Task; var close = f.Session.PrepareCloseAsync(Cancel);
            c.That(!close.IsCompleted && !EditingAvailable(f.Session), "Close retains held replacement and frozen semantic admission");
            release.SetResult(); c.That((await replacing).Success && await close && f.Session.IsClosed, "Replacement settles cleanly before close completes");
            c.That(ResumeCodec.SemanticallyEqual((await f.Store.ReadAsync(f.Session.Current!.DocumentId)).File.Document, f.Session.Current.Document), "Closed replacement reopens valid committed content");
        }
    }
    private static byte[] DamagedBackup(string kind, byte[] original, ResumeCodec codec)
    {
        if (kind == "json") return Encoding.UTF8.GetBytes("{");
        if (kind == "utf8") return [0xFF, 0xFE];
        var node = JsonNode.Parse(original)!;
        if (kind == "type") node["SchemaVersion"] = "wrong type";
        else if (kind == "future") node["SchemaVersion"] = 2;
        else if (kind == "identity") node["DocumentId"] = Guid.NewGuid().ToString("N");
        else node["Document"]!["Summary"] = "PRIVATE_SENTINEL ownership mismatch";
        return Encoding.UTF8.GetBytes(node.ToJsonString());
    }
    private static async Task MaintenanceWarningAsync(Checks c, string kind, bool laterEdit = false)
    {
        using var f = new Fixture(); await f.SavedAsync();
        for (int i = 1; i <= 5; i++) { f.Session.Edit(f.Session.Current!.Document with { Summary = "revision " + i }); await f.Session.SaveAsync(); }
        string suspect = f.Store.Backups(f.Session.Current!.DocumentId).Last();
        byte[] damaged = DamagedBackup(kind, await File.ReadAllBytesAsync(suspect), f.Codec); await File.WriteAllBytesAsync(suspect, damaged);
        f.Session.Edit(f.Session.Current.Document with { Summary = "committed snapshot" });
        var entered = Signal(); var release = Signal();
        if (laterEdit) f.Publisher.BeforePhaseForTest = async phase => { if (phase == "maintenance") { entered.SetResult(); await release.Task.ConfigureAwait(false); } };
        var saving = f.Session.SaveAsync();
        if (laterEdit)
        {
            await entered.Task;
            c.That((await f.Store.ReadAsync(f.Session.Current.DocumentId)).File.Document.Summary == "committed snapshot", "Maintenance barrier is causally after the atomic destination commit");
            c.That(EditingAvailable(f.Session), "Ordinary Save remains editable after commit and before a maintenance warning");
            f.Session.Edit(f.Session.Current.Document with { Summary = "later edit" }); release.SetResult();
        }
        var result = await saving; var persisted = await f.Store.ReadAsync(f.Session.Current.DocumentId);
        c.That(persisted.File.Document.Summary == "committed snapshot", "Destination commit precedes the maintenance fault");
        c.That(result.Kind == ResumeResultKind.Saved && result.Message.Contains("maintenance", StringComparison.OrdinalIgnoreCase)
            && !f.Session.Status.Contains("failed · current", StringComparison.Ordinal), "Post-commit fault returns Saved with a truthful maintenance warning");
        c.That(f.Session.IsDirty == laterEdit && (!laterEdit || f.Session.Current.Document.Summary == "later edit"), "Committed baseline promotion preserves any later edits");
        c.That(!File.Exists(suspect) && (await File.ReadAllBytesAsync(QuarantinePath(f, damaged))).SequenceEqual(damaged), "Suspect maintenance artifact is preserved byte-for-byte in verified quarantine");
        f.Publisher.BeforePhaseForTest = null; f.Session.Edit(f.Session.Current.Document with { Summary = "next save" });
        c.That((await f.Session.SaveAsync()).Success && (await f.Store.ReadAsync(f.Session.Current.DocumentId)).File.Document.Summary == "next save", "Immediate next modifying Save uses the promoted committed hash");
        c.That((await File.ReadAllBytesAsync(QuarantinePath(f, damaged))).SequenceEqual(damaged) && f.Store.Backups(f.Session.Current.DocumentId).Count <= ResumeLimits.RevisionBackups, "Retry never destroys suspect evidence or poisons bounded recovery");
        c.That(!string.Join("\n", f.Log).Contains("PRIVATE_SENTINEL", StringComparison.Ordinal) && !string.Join("\n", f.Log).Contains(f.Base, StringComparison.Ordinal), "Maintenance warning diagnostics reveal neither content nor private paths");
        if (laterEdit)
        {
            f.Session.Edit(f.Session.Current.Document with { Summary = "fatal exception control" });
            f.Publisher.BeforePhaseForTest = phase => phase == "maintenance" ? Task.FromException(new OutOfMemoryException("Controlled exception type, no memory exhaustion")) : Task.CompletedTask;
            await c.ThrowsAsync<OutOfMemoryException>(() => f.Session.SaveAsync(), "Fatal process exception types are never swallowed or relabeled Failed");
            c.That(!f.Session.IsActive, "Fatal exception propagation still settles admitted ownership");
        }
    }
    private static async Task RecoveryIsolationAsync(Checks c, string kind)
    {
        using var f = new Fixture(); await f.SavedAsync();
        for (int i = 1; i <= 2; i++) { f.Session.Edit(f.Session.Current!.Document with { Summary = "revision " + i }); await f.Session.SaveAsync(); }
        string id = f.Session.Current!.DocumentId; var candidates = f.Store.Backups(id);
        string selected = candidates.First(), suspect = candidates.Last();
        var selectedFile = await f.Store.ReadBackupAsync(id, selected); byte[] original = await File.ReadAllBytesAsync(suspect);
        byte[] damaged = kind == "unreadable" ? original : DamagedBackup(kind, original, f.Codec);
        await File.WriteAllBytesAsync(suspect, damaged);
        ResumeResult result;
        using (var held = kind == "unreadable" ? new FileStream(suspect, FileMode.Open, FileAccess.Read, FileShare.None) : null)
            result = await f.Session.RecoverAsync(id, selected, Discard);
        c.That(result.Kind == ResumeResultKind.Recovered, "Valid selected revision survives an invalid unselected " + kind + " candidate");
        var persisted = await f.Store.ReadAsync(id);
        c.That(persisted.File.Revision > 3 && ResumeCodec.SemanticallyEqual(persisted.File.Document, selectedFile.Document), "Recovery uses selected validated content and a monotonic new revision");
        c.That((await File.ReadAllBytesAsync(suspect)).SequenceEqual(damaged), "Invalid unselected artifact remains preserved");
        c.That((await f.Session.OpenAsync(id, Discard)).Success, "Recovered document reopens through the production store");
        // Negative controls: invalid SELECTED content must never be skipped in favor of a good candidate.
        byte[] before = await f.BytesAsync(); await File.WriteAllTextAsync(selected, "{");
        c.That((await f.Session.RecoverAsync(id, selected, Discard)).Kind == ResumeResultKind.Failed && (await f.BytesAsync()).SequenceEqual(before), "Malformed selected backup still fails without publication");
        var future = JsonNode.Parse(f.Codec.Encode(selectedFile))!; future["SchemaVersion"] = 2; await File.WriteAllTextAsync(selected, future.ToJsonString());
        c.That((await f.Session.RecoverAsync(id, selected, Discard)).Kind == ResumeResultKind.Failed && (await f.BytesAsync()).SequenceEqual(before), "Unsupported selected backup still fails without publication");
    }
    // Explicit test-executable native fixture mode. Production Studio has no command-line fault switches.
    internal static int RunNativeChild(string fixtureRoot)
    {
        fixtureRoot=Path.GetFullPath(fixtureRoot);
        string token=Path.GetFileName(fixtureRoot);
        if(!token.StartsWith("native-",StringComparison.Ordinal) || !ResumeCodec.IsId(token[7..]) ||
            !File.Exists(Path.Combine(fixtureRoot,"test-owned.marker")))return 2;
        int exit=1;
        var thread=new Thread(()=>
        {
            App? app=null;var paths=new StudioPathService(Path.Combine(fixtureRoot,"AppData"));var log=new StudioDiagnostics(paths);
            using var hold=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\Axora-M2-"+token+"-hold");
            using var entered=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\Axora-M2-"+token+"-entered");
            using var release=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\Axora-M2-"+token+"-release");
            using var fail=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\Axora-M2-"+token+"-fail");
            try
            {
                Environment.SetEnvironmentVariable("APPDATA",Path.Combine(fixtureRoot,"AppData"));
                Environment.SetEnvironmentVariable("LOCALAPPDATA",Path.Combine(fixtureRoot,"LocalAppData"));
                Directory.CreateDirectory(Path.Combine(fixtureRoot,"LocalAppData"));
                WinRT.ComWrappersSupport.InitializeComWrappers();
                Microsoft.UI.Xaml.Application.Start(_=>
                {
                    SynchronizationContext.SetSynchronizationContext(new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
                    app=new App(paths,log,services=>Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Axora.Studio.Services.Contracts.IResumeFilePublisher>(services,sp=>
                    {
                        var store=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ResumeStore>(sp);
                        var codec=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ResumeCodec>(sp);
                        var publisher=new ResumeFilePublisher(store,codec);
                        publisher.BeforePhaseForTest=async phase=>
                        {
                            if(phase!="stage")return;
                            if(hold.WaitOne(0)){entered.Set();await Task.Run(()=>release.WaitOne()).ConfigureAwait(false);}
                            if(fail.WaitOne(0))throw new IOException("Controlled native fixture failure");
                        };
                        return publisher;
                    }));
                });
                SynchronizationContext.SetSynchronizationContext(null);
                app?.ShutdownAsync().GetAwaiter().GetResult();exit=Environment.ExitCode;
            }
            catch(Exception ex){log.Write("Native fixture exceptionType="+ex.GetType().Name);exit=1;}
            finally{SynchronizationContext.SetSynchronizationContext(null);app?.ShutdownAsync().GetAwaiter().GetResult();}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();return exit;
    }
    private static ImmutableArray<T> Many<T>() where T:new()=>[..Enumerable.Range(0,50).Select(_=>new T())];
    private static async Task Reject(Checks c,Action action,string name)
    { try { action(); c.That(false,name); } catch(Exception ex) when(ex is not OutOfMemoryException) { c.That(true,name); } await Task.CompletedTask; }
    internal static ResumeDocument CompleteDocument()
    {
        T Fill<T>() where T:new()
        {
            var value=new T();foreach(var property in typeof(T).GetProperties().Where(x=>x.PropertyType==typeof(string) && x.Name!="Id"))property.SetValue(value,"fixture "+property.Name);
            return value;
        }
        return new ResumeDocument
        {
            ResumeTitle="Generic test resume",Header=Fill<ResumeHeader>(),Summary="Generic narrative 🙂",
            Education=[Fill<ResumeEducation>()],Experiences=[Fill<ResumeExperience>() with {IsCurrent=true}],SkillCategories=[Fill<ResumeSkill>()],
            Projects=[Fill<ResumeProject>()],Certifications=[Fill<ResumeCertification>()],Achievements=[Fill<ResumeAchievement>()],Responsibilities=[Fill<ResumeResponsibility>()],
            ShowSummary=false,ShowEducation=false,ShowSkills=false,ShowAchievements=false,
            Formatting=new() {TargetLength=ResumeTargetLength.ThreePages,FontFamily=ResumeFontPreference.Georgia,SpacingMode=ResumeDensity.Relaxed,MarginInches=.8,ShowDividers=false,CenterHeader=false,UppercaseSectionTitles=false,AccentHexColor="#3478AB"}
        };
    }
    internal static byte[] LegacyBytes(string? title=null)
    {
        var node=JsonSerializer.SerializeToNode(CompleteDocument() with {ResumeTitle=title??"Legacy fixture"})!.AsObject();node.Remove("SectionOrder");
        return Encoding.UTF8.GetBytes(node.ToJsonString());
    }
    private sealed class NullPicker : Axora.Studio.Services.Contracts.IResumeFilePicker { public Task<string?> SelectImportAsync(nint owner)=>Task.FromResult<string?>(null); }
    private sealed class CaptiveContext : SynchronizationContext
    {
        public int Posts;
        public override void Post(SendOrPostCallback callback,object? state)=>Interlocked.Increment(ref Posts);
    }
    internal sealed class Fixture:IDisposable
    {
        public string Base {get;}=Path.Combine(Path.GetTempPath(),"axora-m2-"+Guid.NewGuid().ToString("N"));
        public ResumeCodec Codec {get;}=new();
        public ResumeStore Store {get;}
        public ResumeFilePublisher Publisher {get;}
        public ResumeSession Session {get;}
        public List<string> Log {get;}=[];
        public Fixture(){Directory.CreateDirectory(Base);Store=new(new StudioPathService(Base),Codec);Publisher=new(Store,Codec);Session=new(Store,Codec,Publisher,Log.Add);}
        public async Task SavedAsync(ResumeDocument? document=null){await Session.NewAsync(Discard);if(document is not null)Session.Edit(document);var saved=await Session.SaveAsync();if(!saved.Success)throw new InvalidOperationException("Fixture save failed.");}
        public Task<byte[]> BytesAsync()=>File.ReadAllBytesAsync(Store.DocumentPath(Session.Current!.DocumentId));
        public void Dispose(){if(Path.GetFullPath(Base).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(Base,true);}
    }
}
