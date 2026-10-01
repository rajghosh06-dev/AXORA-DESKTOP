using System.Text.Json;
using System.Text.Json.Serialization;
using Axora.Studio.Models;
using Axora.Studio.Services.Contracts;

namespace Axora.Studio.Services;

public sealed class StudioSettingsService : IDisposable
{
    private const int MaximumSettingsBytes = 65536;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<StudioTheme>(allowIntegerValues: false) }
    };
    private readonly StudioPathService _paths;
    private readonly StudioWriterLease _lease;
    private readonly ISettingsFilePublisher _publisher;
    private readonly SemaphoreSlim _io = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _gate = new();
    private readonly HashSet<Task> _operations = [];
    private bool _closed;
    private bool _disposed;
    public StudioSettings Current { get; private set; } = new();
    public bool CanSave { get; private set; } = true;
    public string? Warning { get; private set; }
    // Test-only boundary signal; unset in production composition and immutable after construction.
    public Func<CancellationToken, Task>? BeforeFinalRecheckForTest { get; init; }

    public StudioSettingsService(StudioPathService paths, StudioWriterLease lease, ISettingsFilePublisher publisher)
        => (_paths, _lease, _publisher) = (paths, lease, publisher);

    public Task<SettingsLoadResult> LoadAsync(CancellationToken token = default) =>
        Admit(async ct =>
        {
            await _io.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var primary = await ReadAsync(_paths.Settings, ct).ConfigureAwait(false);
                CanSave = primary.CanWrite;
                Warning = null;
                if (!primary.CanWrite)
                {
                    Current = new();
                    Warning = primary.ReadOnlyWarning;
                }
                else if (primary.Valid) Current = primary.Settings!;
                else if (primary.State == SettingsReadState.Missing) Current = new();
                else
                {
                    var backup = await ReadAsync(_paths.Backup, ct).ConfigureAwait(false);
                    Current = backup.Valid ? backup.Settings! : new();
                    Warning = backup.Valid
                        ? "Settings contain invalid content. Using a validated backup; original bytes are preserved."
                        : "Settings contain invalid content. Using defaults; original bytes are preserved.";
                }
                return new SettingsLoadResult(Current, CanSave, Warning);
            }
            finally { _io.Release(); }
        }, token);

    public Task<SettingsSaveResult> SaveAsync(StudioTheme theme, CancellationToken token = default) =>
        Admit<SettingsSaveResult>(async ct =>
        {
            await _io.WaitAsync(ct).ConfigureAwait(false);
            string? stage = null;
            try
            {
                if (!_lease.IsAcquired) throw new InvalidOperationException("Studio writer lease is not held.");
                if (!Enum.IsDefined(theme)) throw new ArgumentOutOfRangeException(nameof(theme));
                var primary = await ReadAsync(_paths.Settings, ct).ConfigureAwait(false);
                CanSave = primary.CanWrite;
                if (!primary.CanWrite)
                {
                    Warning = primary.ReadOnlyWarning;
                    return new(false, Warning!);
                }
                var settings = new StudioSettings { Theme = theme };
                stage = Path.Combine(_paths.Root, $".settings.{Guid.NewGuid():N}.tmp");
                await using (var stream = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct).ConfigureAwait(false);
                    await stream.FlushAsync(ct).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }
                var staged = await ReadAsync(stage, ct).ConfigureAwait(false);
                if (!staged.Valid || staged.Settings != settings)
                    throw new InvalidDataException("Staged settings did not validate.");
                // Recheck after staging as well as before it. The lease serializes
                // cooperating Studio writers; this is not an atomic guard against
                // arbitrary external changes after the final read.
                if (BeforeFinalRecheckForTest is { } beforeFinalRecheck)
                    await beforeFinalRecheck(ct).ConfigureAwait(false);
                var destination = await ReadAsync(_paths.Settings, ct).ConfigureAwait(false);
                CanSave = destination.CanWrite;
                if (!destination.CanWrite)
                {
                    Warning = destination.ReadOnlyWarning;
                    return new(false, Warning!);
                }
                if (primary.State == SettingsReadState.Missing && destination.State != SettingsReadState.Missing)
                    return new(false, "Settings appeared during save. Nothing was overwritten; retry after reviewing them.");
                ct.ThrowIfCancellationRequested();
                // A corrupt primary receives its own recovery file; don't replace
                // a valid .bak with corrupt bytes during an explicit repair/save.
                string backup = destination.Valid ? _paths.Backup
                    : Path.Combine(_paths.Root, $"settings.corrupt.{Guid.NewGuid():N}.json");
                _publisher.Commit(stage, _paths.Settings, backup);
                stage = null;
                // Cancellation after the commit point must not misreport a published save.
                Current = settings;
                Warning = null;
                return new(true, "Saved. Theme will be restored next time Studio starts.");
            }
            catch (OperationCanceledException) { return new(false, "Save canceled. Previous settings are unchanged."); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return new(false, $"Save failed ({ex.GetType().Name}). Previous settings remain recoverable.");
            }
            finally
            {
                if (stage is not null)
                {
                    try { File.Delete(stage); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    { System.Diagnostics.Debug.WriteLine($"Owned settings stage retained: {ex.GetType().Name}"); }
                }
                _io.Release();
            }
        }, token);

    private Task<T> Admit<T>(Func<CancellationToken, Task<T>> operation, CancellationToken caller)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_closed) return Task.FromException<T>(new InvalidOperationException("Studio settings are stopping."));
            _operations.Add(completion.Task);
        }
        _ = RunAsync();
        return completion.Task;
        async Task RunAsync()
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, _stopping.Token);
            try { completion.TrySetResult(await operation(linked.Token).ConfigureAwait(false)); }
            catch (OperationCanceledException) { completion.TrySetCanceled(linked.Token); }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { lock (_gate) _operations.Remove(completion.Task); }
        }
    }

    public Task StopAsync()
    {
        Task[] pending;
        lock (_gate) { _closed = true; pending = _operations.ToArray(); }
        _stopping.Cancel();
        return DrainAsync(pending);
        static async Task DrainAsync(Task[] operations)
        {
            // Individual failures are delivered to their caller; joining is ownership proof.
            foreach (var task in operations) { try { await task.ConfigureAwait(false); } catch { } }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_operations.Any(t => !t.IsCompleted))
                throw new InvalidOperationException("Settings work must settle before DI disposal.");
            _closed = _disposed = true;
        }
        _stopping.Dispose();
        _io.Dispose();
    }

    private enum SettingsReadState { Missing, ValidCurrent, ConfirmedCorrupt, UnsupportedSchema, Unreadable, Unclassified }
    private sealed record ReadResult(SettingsReadState State, StudioSettings? Settings = null, string? ReadOnlyWarning = null)
    {
        public bool Valid => State == SettingsReadState.ValidCurrent;
        public bool CanWrite => State is SettingsReadState.Missing or SettingsReadState.ValidCurrent or SettingsReadState.ConfirmedCorrupt;
    }
    private static async Task<ReadResult> ReadAsync(string path, CancellationToken token)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            byte[] buffer = new byte[MaximumSettingsBytes + 1];
            int count = 0;
            while (count < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(count), token).ConfigureAwait(false);
                if (read == 0) break;
                count += read;
            }
            // The schema cannot safely be established within the bounded reader.
            // Preserve this file read-only rather than risk downgrading a future schema.
            if (count > MaximumSettingsBytes) return new(SettingsReadState.Unclassified, ReadOnlyWarning:
                "Settings exceed Studio's validation limit. The original is read-only; saving is disabled. Nothing was overwritten.");
            using var doc = JsonDocument.Parse(buffer.AsMemory(0, count));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new(SettingsReadState.ConfirmedCorrupt);
            var versions = doc.RootElement.EnumerateObject().Where(property => property.NameEquals("schemaVersion")).ToArray();
            if (versions.Length > 1)
                return new(SettingsReadState.Unclassified, ReadOnlyWarning:
                    "Settings contain ambiguous schema versions. The original is read-only; saving is disabled. Nothing was overwritten.");
            if (versions.Length == 0 || versions[0].Value.ValueKind != JsonValueKind.Number)
                return new(SettingsReadState.ConfirmedCorrupt);
            var version = versions[0].Value;
            // Only the current integer encoding 1 is supported. Every other JSON
            // number (including values too large for Int64) fails closed; no
            // floating-point rounding, truncation, or coercion is used.
            if (!version.TryGetInt32(out int schema) || schema != 1)
                return new(SettingsReadState.UnsupportedSchema, ReadOnlyWarning:
                    "Settings use an unsupported schema version. The original is read-only; saving is disabled. Nothing was overwritten.");
            if (!doc.RootElement.TryGetProperty("theme", out var theme) || theme.ValueKind != JsonValueKind.String ||
                theme.GetString() is not ("System" or "Light" or "Dark") ||
                !Enum.TryParse<StudioTheme>(theme.GetString(), ignoreCase: false, out var value) || !Enum.IsDefined(value))
                return new(SettingsReadState.ConfirmedCorrupt);
            return new(SettingsReadState.ValidCurrent, new StudioSettings { Theme = value });
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        { return new(SettingsReadState.Missing); }
        catch (JsonException)
        { return new(SettingsReadState.ConfirmedCorrupt); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new(SettingsReadState.Unreadable, ReadOnlyWarning:
                $"Settings could not be safely read ({ex.GetType().Name}). The original is read-only; saving is disabled. Nothing was overwritten.");
        }
    }
}
