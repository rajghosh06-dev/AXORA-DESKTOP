using Axora.Studio.Models;
using Axora.Studio.Services.Contracts;

namespace Axora.Studio.Services;

public sealed class ResumeSession
{
    private readonly ResumeStore _store;
    private readonly ResumeCodec _codec;
    private readonly IResumeFilePublisher _publisher;
    private readonly Action<string>? _diagnostic;
    private readonly object _gate = new();
    private ResumeFile? _current, _saved;
    private string? _savedHash;
    private bool _closed, _closing, _departurePending, _invalidDraft, _replacement;
    private long _activationVersion;
    private TaskCompletionSource? _operation;
    private readonly List<ResumeDocument> _undo = [];
    private readonly List<string> _recent = [];
    public event EventHandler? Changed;
    public ResumeFile? Current { get { lock (_gate) return _current; } }
    public bool IsDirty { get { lock (_gate) return Dirty(); } }
    public bool IsActive { get { lock (_gate) return _operation is not null; } }
    public bool IsClosed { get { lock (_gate) return _closed; } }
    public bool HasInvalidDraft { get { lock (_gate) return _invalidDraft; } }
    public bool CanEdit { get { lock (_gate) return EditingAvailable(); } }
    public long ActivationVersion { get { lock (_gate) return _activationVersion; } }
    public IReadOnlyList<string> Recent { get { lock (_gate) return _recent.ToArray(); } }
    public string Status { get; private set; } = "No active Resume";
    public ResumeSession(ResumeStore store, ResumeCodec codec, IResumeFilePublisher publisher, Action<string>? diagnostic = null)
    { _store = store; _codec = codec; _publisher = publisher; _diagnostic = diagnostic; }
    private bool Dirty() => _current is not null && (_invalidDraft || _saved is null || !ResumeCodec.SemanticallyEqual(_current.Document, _saved.Document));
    private bool EditingAvailable() => !_closed && !_departurePending && !_replacement && _current is not null;
    public void SetInvalidDraft(bool invalid)
    {
        lock (_gate)
        {
            if (!EditingAvailable()) throw new InvalidOperationException("Resume editing unavailable.");
            _invalidDraft = invalid; Status = invalid ? "Unsaved changes · correct invalid fields before saving" : Dirty() ? "Unsaved changes" : "Saved";
        }
        Notify();
    }
    public void Edit(ResumeDocument document)
    {
        _codec.ValidateDocument(document);
        lock (_gate)
        {
            if (!EditingAvailable()) throw new InvalidOperationException("Resume editing unavailable.");
            var current = _current!;
            if (ResumeCodec.SemanticallyEqual(document, current.Document)) return;
            _undo.Add(current.Document);
            while (_undo.Count > ResumeLimits.UndoCount || _undo.Sum(d => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(d).Length) > ResumeLimits.UndoBytes)
                _undo.RemoveAt(0);
            _current = current with { Document = document, Revision = checked(current.Revision + 1), ModifiedUtc = DateTimeOffset.UtcNow };
            Status = _operation is null ? (Dirty() ? "Unsaved changes" : "Saved") : "Saving · newer edits are unsaved";
        }
        Notify();
    }
    public bool Undo()
    {
        lock (_gate)
        {
            if (!EditingAvailable() || _undo.Count == 0) return false;
            var document = _undo[^1];
            var restored = _current! with { Document = document, Revision = checked(_current.Revision + 1), ModifiedUtc = DateTimeOffset.UtcNow };
            _undo.RemoveAt(_undo.Count - 1); _current = restored;
            Status = _operation is null ? (Dirty() ? "Unsaved changes" : "Saved") : "Saving · newer edits are unsaved";
        }
        Notify();
        return true;
    }
    public async Task<ResumeResult> NewAsync(Func<Task<ResumeDeparture>> decision)
    {
        if (!await GuardDepartureAsync(decision)) return RejectedDeparture();
        return await MutateAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var file = new ResumeFile { CreatedUtc = now, ModifiedUtc = now };
            lock (_gate) Activate(file, null, null);
            await Task.CompletedTask;
            return new(ResumeResultKind.Opened, "New Resume · unsaved", file);
        }, replacement: true);
    }
    public async Task<ResumeResult> OpenAsync(string id, Func<Task<ResumeDeparture>> decision)
    {
        // Read errors never discard the current dirty document: validate candidate first.
        ResumeRead read;
        try { read = await _store.ReadAsync(id); }
        catch (Exception ex) { return Failure(ex); }
        if (!await GuardDepartureAsync(decision)) return RejectedDeparture();
        return await MutateAsync(() => { lock (_gate) Activate(read.File, read.File, read.Sha256); return Task.FromResult(new ResumeResult(ResumeResultKind.Opened, "Saved", read.File)); }, replacement: true);
    }
    public async Task<ResumeResult> ImportAsync(ReadOnlyMemory<byte> bytes, Func<Task<ResumeDeparture>> decision,
        Func<ResumeDocument, Task<bool>> consent)
    {
        byte[] captured = bytes.ToArray();
        ResumeDocument document;
        try { document = _codec.DecodeLegacy(captured); }
        catch (Exception ex) { return Failure(ex); }
        if (!await consent(document)) return new(ResumeResultKind.Cancelled, "Import cancelled");
        if (!await GuardDepartureAsync(decision)) return RejectedDeparture();
        return await MutateAsync(async () =>
        {
            string hash = ResumeCodec.Hash(captured);
            var existing = await _store.FindImportAsync(hash).ConfigureAwait(false);
            if (existing is not null)
            {
                lock (_gate) Activate(existing.File, existing.File, existing.Sha256);
                return new(ResumeResultKind.ExistingImport, "Opened existing imported copy", existing.File);
            }
            var now = DateTimeOffset.UtcNow;
            // Source-derived identity also prevents silent duplication when a previous imported copy is unreadable.
            var file = new ResumeFile { DocumentId = hash[..32].ToLowerInvariant(), Document = document, ImportSha256 = hash, CreatedUtc = now, ModifiedUtc = now };
            await _publisher.PreserveImportAsync(captured, hash).ConfigureAwait(false);
            var published = await _publisher.PublishAsync(new(file, null)).ConfigureAwait(false);
            lock (_gate) Activate(file, file, published.Sha256);
            return new(ResumeResultKind.Imported, published.BackupMaintenanceFailed ? "Imported copy · source preserved · backup maintenance warning" : "Imported copy · source preserved", file);
        }, replacement: true);
    }
    public Task<ResumeResult> SaveAsync(bool copy = false) => SaveCoreAsync(copy, false);
    private Task<ResumeResult> SaveCoreAsync(bool copy, bool departureOwner) => MutateAsync(async () =>
    {
        ResumeFile captured;
        string? expected;
        lock (_gate)
        {
            captured = _current ?? throw new InvalidOperationException("No active Resume.");
            if (_invalidDraft) throw new InvalidDataException("Invalid editor fields must be corrected before Save.");
            if (!copy && !Dirty()) return new(ResumeResultKind.Saved, "Saved", captured);
            expected = copy ? null : _savedHash;
            if (copy)
            {
                var now = DateTimeOffset.UtcNow;
                captured = captured with { DocumentId = Guid.NewGuid().ToString("N"), Revision = 1, CreatedUtc = now, ModifiedUtc = now, ImportSha256 = null, RecoveredFromRevision = null };
            }
        }
        var published = await _publisher.PublishAsync(new(captured, expected)).ConfigureAwait(false);
        lock (_gate)
        {
            // Only the captured semantic baseline is saved. Edits during publication remain dirty.
            var laterDocument = _current!.Document;
            if (copy) _current = captured with { Document = laterDocument, Revision = ResumeCodec.SemanticallyEqual(laterDocument, captured.Document) ? 1 : 2 };
            _saved = captured;
            _savedHash = published.Sha256;
            Remember(captured.DocumentId);
        }
        return new(ResumeResultKind.Saved, published.BackupMaintenanceFailed ? "Saved · backup maintenance warning"
            : copy ? "Saved copy · this copy is now active" : "Saved", captured);
    }, departureOwner);
    public async Task<ResumeResult> RecoverAsync(string id, string backup, Func<Task<ResumeDeparture>> decision)
    {
        ResumeFile recovery;
        try
        {
            recovery = await _store.ReadBackupAsync(id, backup);
        }
        catch (Exception ex) { return Failure(ex); }
        if (!await GuardDepartureAsync(decision)) return RejectedDeparture();
        return await MutateAsync(async () =>
        {
            // Capture current authority after any departure Save, under replacement ownership.
            _store.EnsureOwnedPath(_store.DocumentPath(id));
            byte[]? currentBytes;
            try { currentBytes = await ResumeStore.CaptureAsync(_store.DocumentPath(id)).ConfigureAwait(false); }
            catch (FileNotFoundException) { currentBytes = null; }
            long currentRevision = recovery.Revision;
            lock (_gate) if (_current?.DocumentId == id) currentRevision = Math.Max(currentRevision, _current.Revision);
            try
            {
                if (currentBytes is not null)
                {
                    var current = _codec.Decode(currentBytes);
                    if (current.DocumentId != id || current.CreatedUtc != recovery.CreatedUtc) throw new InvalidDataException("Recovery lineage mismatch.");
                    currentRevision = Math.Max(currentRevision, current.Revision);
                }
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException) { }
            int excluded = 0;
            foreach (string candidate in _store.Backups(id))
            {
                if (candidate == backup) continue; // The selected snapshot was strictly verified before the guard.
                try
                {
                    var other = await _store.ReadBackupAsync(id, candidate).ConfigureAwait(false);
                    if (other.CreatedUtc != recovery.CreatedUtc) throw new InvalidDataException("Recovery lineage mismatch.");
                    currentRevision = Math.Max(currentRevision, other.Revision);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                    or System.Text.Json.JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
                {
                    excluded++;
                    Log($"Resume phase=recovery-candidate-excluded exceptionType={ex.GetType().Name}");
                }
            }
            var file = recovery with { Revision = checked(currentRevision + 1), ModifiedUtc = DateTimeOffset.UtcNow, RecoveredFromRevision = recovery.Revision };
            var published = await _publisher.PublishAsync(new(file, currentBytes is null ? null : ResumeCodec.Hash(currentBytes), true)).ConfigureAwait(false);
            lock (_gate) Activate(file, file, published.Sha256);
            string warning = excluded > 0 ? " · invalid saved revisions retained" : "";
            if (published.BackupMaintenanceFailed) warning += " · backup maintenance warning";
            return new(ResumeResultKind.Recovered, "Recovered verified revision" + warning, file);
        }, replacement: true);
    }
    public async Task<bool> GuardDepartureAsync(Func<Task<ResumeDeparture>> decision, bool closeOwner = false)
    {
        lock (_gate)
        {
            if (_closed || (_closing && !closeOwner) || _departurePending || _operation is not null) { Status = _closed ? "Closed" : "Busy"; return false; }
            if (!Dirty()) return true;
            _departurePending = true;
        }
        try
        {
            ResumeDeparture answer = await decision();
            if (answer == ResumeDeparture.Cancel) return false;
            if (answer == ResumeDeparture.Save) return (await SaveCoreAsync(false, true)).Success && !IsDirty;
            lock (_gate)
            {
                _current = _saved;
                _activationVersion++;
                _invalidDraft = false;
                _undo.Clear();
                Status = _current is null ? "No active Resume" : "Saved";
            }
            Notify();
            return true;
        }
        finally { lock (_gate) _departurePending = false; }
    }
    public async Task<bool> PrepareCloseAsync(Func<Task<ResumeDeparture>> decision)
    {
        Task pending;
        lock (_gate)
        {
            if (_closing || _closed) return _closed;
            _closing = true;
            pending = _operation?.Task ?? Task.CompletedTask;
        }
        try
        {
            await pending;
            if (!await GuardDepartureAsync(decision, true)) return false;
            await StopAsync();
            return true;
        }
        finally { lock (_gate) { if (!_closed) _closing = false; } }
    }
    public Task StopAsync()
    {
        lock (_gate) { _closed = true; return _operation?.Task ?? Task.CompletedTask; }
    }
    private async Task<ResumeResult> MutateAsync(Func<Task<ResumeResult>> action, bool departureOwner = false, bool replacement = false)
    {
        TaskCompletionSource admitted;
        lock (_gate)
        {
            if (_closed) return new(ResumeResultKind.Closed, "Resume admission closed");
            if (_operation is not null || ((_departurePending || _closing) && !departureOwner)) return new(ResumeResultKind.Busy, "Busy · wait for the admitted operation");
            _operation = admitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _replacement = replacement;
            Status = replacement ? "Opening Resume · editing paused" : "Saving";
        }
        Notify();
        var operationId = Guid.NewGuid().ToString("N");
        Log($"Resume operation={operationId} phase=admitted schema=1");
        try
        {
            ResumeResult result = await action().ConfigureAwait(false);
            lock (_gate) Status = Dirty() ? "Unsaved changes · " + result.Message : result.Message;
            Log($"Resume operation={operationId} phase=settled result={result.Kind}");
            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        { return Failure(ex, operationId); }
        finally
        {
            lock (_gate) { _operation = null; _replacement = false; }
            admitted.TrySetResult();
            Notify();
        }
    }
    private void Activate(ResumeFile current, ResumeFile? saved, string? hash)
    { _current = current; _saved = saved; _savedHash = hash; _invalidDraft = false; _undo.Clear(); _activationVersion++; Remember(current.DocumentId); }
    private void Remember(string id) { _recent.Remove(id); _recent.Insert(0, id); if (_recent.Count > ResumeLimits.RecentDocuments) _recent.RemoveAt(_recent.Count - 1); }
    private ResumeResult RejectedDeparture() => new(IsActive ? ResumeResultKind.Busy : ResumeResultKind.Cancelled, Status == "Busy" ? "Busy" : "Departure cancelled");
    private ResumeResult Failure(Exception ex, string? operationId = null)
    {
        Status = "Save/open/import failed · current content retained";
        Log($"Resume operation={operationId ?? "none"} phase=failed exceptionType={ex.GetType().Name}");
        Notify();
        return new(ResumeResultKind.Failed, Status);
    }
    private void Notify()
    {
        if (Changed is not { } listeners) return;
        foreach (EventHandler listener in listeners.GetInvocationList())
            try { listener(this, EventArgs.Empty); }
            catch (Exception ex) { Log($"Resume phase=observer-failed exceptionType={ex.GetType().Name}"); }
    }
    private void Log(string safeMessage) { try { _diagnostic?.Invoke(safeMessage); } catch { /* Diagnostics cannot break publication ownership. */ } }
}
