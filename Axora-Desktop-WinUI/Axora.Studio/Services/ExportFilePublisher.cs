using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Axora.Studio.Models;
using Axora.Studio.Services.Contracts;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Services;

/// <summary>Local fixed NTFS only. No constructor I/O. Publication is never implemented by truncation/copy/delete-first.
/// Path-based Move/Replace are not a compare-and-swap against external writers; displaced backup verification detects that final window.</summary>
public sealed class ExportFilePublisher : IExportFilePublisher
{
    // Content identity never confers ownership. Only verified, successful native replacement can confer cleanup provenance.
    private enum BackupProvenance
    {
        NoBackupObserved, ReservationCreated, ReservationValidated, UnexpectedBeforeNative, NativeReplacementAttempted,
        ObservedAfterNativeSuccess, AmbiguousAfterPublicationException, SuccessfulReplacementVerified
    }
    private readonly string[] _protectedRoots;
    private readonly ConcurrentDictionary<ExportFileIdentity, byte> _owned = new();
    private readonly Action<ExportBoundary, ExportOperationContext>? _boundary;
    private readonly Action<ExportOperationContext>? _finalStageValidated;
    private readonly Action<ExportOperationContext>? _reservationPreparing;
    private readonly Action<ExportOperationContext>? _beforeNativeReplacement;
    public ExportFilePublisher(StudioPathService paths, Action<ExportBoundary, ExportOperationContext>? boundary = null,
        Action<ExportOperationContext>? finalStageValidated = null, Action<ExportOperationContext>? reservationPreparing = null,
        Action<ExportOperationContext>? beforeNativeReplacement = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _protectedRoots = [paths.Root, AppContext.BaseDirectory, Path.GetDirectoryName(Environment.ProcessPath)!];
        _boundary = boundary; // Instance-scoped, dormant unless explicitly supplied. Does not replace filesystem APIs.
        _finalStageValidated = finalStageValidated; // Narrow completion seam; never replaces validation or publication.
        _reservationPreparing = reservationPreparing;
        _beforeNativeReplacement = beforeNativeReplacement; // Dormant deterministic seam for the residual same-user race.
    }
    public ExportPreparation Prepare(string destination, FlashcardExportFormat format, CancellationToken token = default)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            var (canonical, parent) = ValidateDestination(destination, format);
            var current = Inspect(canonical, token);
            if (current is not null && _owned.ContainsKey(current.Identity)) throw new InvalidDataException("Owned artifact");
            return new(new(canonical, format, parent, current), null, "DestinationPrepared");
        }
        catch (OperationCanceledException) { return new(null, ExportResultState.Canceled, "PreparationCanceled"); }
        catch (Exception ex) when (Expected(ex)) { return new(null, ExportResultState.InvalidDestination,
            "DestinationInspectionFailed:" + ex.GetType().Name + ":" + (ex is Win32Exception native ? native.NativeErrorCode : ex.HResult)); }
    }
    public Task<ExportPublicationResult> PublishAsync(ExportDestinationPlan plan, FlashcardExportSnapshot snapshot, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan); ArgumentNullException.ThrowIfNull(snapshot);
        // Do not pass token to Task.Run: even pre-cancellation must return a settled typed result.
        return Task.Run(() => Publish(plan, snapshot, token));
    }
    private ExportPublicationResult Publish(ExportDestinationPlan plan, FlashcardExportSnapshot snapshot, CancellationToken token)
    {
        Guid id = Guid.NewGuid(); string? stage = null, backup = null, recovery = null;
        ExportFileIdentity? stageId = null, recoveryId = null;
        ExportFingerprint? validated = null;
        ExportFingerprint? reservation = null;
        bool reservationCreated = false;
        SafeFileHandle? parentPin = null, recoveryPin = null;
        bool admitted = false, attempted = false, retain = false, stageCreated = false, recoveryCreated = false;
        bool replacementSucceeded = false;
        BackupProvenance backupProvenance = BackupProvenance.NoBackupObserved;
        int commitGate = 0; // 0 = precommit, 1 = cancellation won, 2 = commit admission won.
        using var cancellationRegistration = token.Register(() => Interlocked.CompareExchange(ref commitGate, 1, 0));
        var warnings = new List<string>();
        ExportResultState state = ExportResultState.Rejected; string reason = "Unsettled";
        string destinationObservation = "NotInspected", stageObservation = "NotCreated", backupObservation = "NotCreated";
        ExportOperationContext Context() => new(id, plan.Destination, stage, backup);
        void Boundary(ExportBoundary value) => _boundary?.Invoke(value, Context());
        try
        {
            token.ThrowIfCancellationRequested();
            if (plan.Existing is not null && !plan.ReplacementApproved) { reason = "ReplacementNotApproved"; return Finish(); }
            if (!MatchesPlan(plan, token)) { state = ExportResultState.DestinationChanged; reason = "DestinationChangedBeforeStage"; return Finish(); }
            string parent = Path.GetDirectoryName(plan.Destination)!;
            parentPin = Open(parent, directory: true, share: 3);
            if (Identity(Information(parentPin)) != plan.ParentIdentity) throw new InvalidDataException("Parent changed");
            stage = Path.Combine(parent, $".axora-export-{id:N}-{Guid.NewGuid():N}.stage");
            try
            {
                using var file = new FileStream(stage, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.SequentialScan);
                stageCreated = true;
                stageId = Identity(Information(file.SafeFileHandle)); _owned.TryAdd(stageId, 0);
                Boundary(ExportBoundary.StageOpened);
                FlashcardExportCodec.Write(file, snapshot, plan.Format, token);
                Boundary(ExportBoundary.AfterStageWrite); token.ThrowIfCancellationRequested();
                Boundary(ExportBoundary.BeforeDurableFlush); file.Flush(flushToDisk: true);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (Expected(ex)) { state = ExportResultState.SerializationFailed; reason = "StageWriteFailed"; return Finish(); }
            try
            {
                Boundary(ExportBoundary.BeforeStageValidation);
                validated = Inspect(stage, token) ?? throw new InvalidDataException("Stage absent");
                if (validated.Identity != stageId) throw new InvalidDataException("Stage identity changed");
                ValidateArtifact(stage, validated, snapshot, plan.Format, token);
                Boundary(ExportBoundary.AfterStageValidation);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (Expected(ex)) { state = ExportResultState.StageValidationFailed; reason = "StageValidationFailed"; return Finish(); }
            // Complete all substantial stage work before recovery preparation and the FINAL destination check.
            // This second reopening catches changes made after the first validation, including identity swaps.
            try { ValidateArtifact(stage, validated, snapshot, plan.Format, token); _finalStageValidated?.Invoke(Context()); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (Expected(ex)) { state = ExportResultState.StageValidationFailed; reason = "StageChangedBeforeCommit"; return Finish(); }
            if (plan.Existing is not null)
            {
                recovery = Path.Combine(parent, $".axora-export-recovery-{id:N}-{Guid.NewGuid():N}");
                if (!Native.CreateDirectory(recovery, IntPtr.Zero)) throw NativeError(); // Exclusive ownership; never reuse an existing directory.
                recoveryCreated = true;
                recoveryPin = Open(recovery, directory: true, share: 3);
                recoveryId = Identity(Information(recoveryPin)); _owned.TryAdd(recoveryId, 0);
                backup = Path.Combine(recovery, $"displaced-{Guid.NewGuid():N}.original");
                _reservationPreparing?.Invoke(Context());
                using var placeholder = new FileStream(backup, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                reservationCreated = true;
                reservation = Fingerprint(placeholder.SafeFileHandle, CancellationToken.None); // Capture ownership of the empty slot even if cancellation just won.
                _owned.TryAdd(reservation.Identity, 0);
                backupProvenance = BackupProvenance.ReservationCreated;
            }
            Boundary(ExportBoundary.BeforeDestinationRecheck);
            token.ThrowIfCancellationRequested();
            if (!MatchesPlan(plan, token)) { state = ExportResultState.DestinationChanged; reason = "DestinationChangedBeforeCommit"; return Finish(); }
            RequireOwnedReservation();
            // No full stage hash/grammar parse or general callback between this final check and admission.
            token.ThrowIfCancellationRequested();
            if (Interlocked.CompareExchange(ref commitGate, 2, 0) != 0) throw new OperationCanceledException(token);
            admitted = true; // Linearization boundary: no cancellation checks/awaits from here through settlement.
            try
            {
                Boundary(ExportBoundary.CommitAdmitted);
                RequireOwnedReservation();
                _beforeNativeReplacement?.Invoke(Context());
                attempted = true;
                if (backup is null) File.Move(stage, plan.Destination, overwrite: false);
                else
                {
                    backupProvenance = BackupProvenance.NativeReplacementAttempted;
                    File.Replace(stage, plan.Destination, backup, ignoreMetadataErrors: false);
                    replacementSucceeded = true;
                    _owned.TryRemove(reservation!.Identity, out _); // Native success consumed the placeholder, not a retained owned file.
                    backupProvenance = BackupProvenance.ObservedAfterNativeSuccess;
                }
                Boundary(ExportBoundary.AfterNativePublication);
            }
            catch (Exception ex) when (Expected(ex))
            {
                // Native exceptions can follow partial publication. Inspect actual artifacts before classifying.
                var dest = Observe(plan.Destination, out destinationObservation);
                var staged = Observe(stage, out stageObservation);
                var displaced = backup is null ? null : Observe(backup, out backupObservation);
                if (backup is not null && !DefiniteAbsent(backup))
                    backupProvenance = BackupProvenance.AmbiguousAfterPublicationException;
                bool unchanged = destinationObservation != "UnknownInspectionFailed" && stageObservation != "UnknownInspectionFailed"
                    && Same(dest, plan.Existing) && Same(staged, validated)
                    && (backup is null || reservationCreated && Same(displaced, reservation));
                state = unchanged ? ExportResultState.PublicationFailed
                    : Same(dest, validated) && (backup is null || Same(displaced, plan.Existing))
                        ? ExportResultState.PublishedButVerificationFailed : ExportResultState.Indeterminate;
                reason = "PublicationExceptionInspected"; retain = !unchanged; return Finish();
            }
            retain = true;
            // Validate the actual displaced original before treating replacement as approved success.
            if (backup is not null)
            {
                var displaced = Observe(backup, out backupObservation);
                if (!replacementSucceeded || !reservationCreated || reservation is null || !Same(displaced, plan.Existing)
                    || displaced!.Identity == reservation.Identity || !DefiniteAbsent(stage))
                { state = ExportResultState.Indeterminate; reason = "DisplacedFileMismatch"; return Finish(); }
            }
            try
            {
                Boundary(ExportBoundary.BeforeFinalVerification);
                ValidateArtifact(plan.Destination, validated, snapshot, plan.Format, CancellationToken.None);
                destinationObservation = "VerifiedPublishedArtifact";
                state = ExportResultState.Published; reason = "PublicationVerified"; retain = false;
                if (replacementSucceeded) backupProvenance = BackupProvenance.SuccessfulReplacementVerified;
            }
            catch (Exception ex) when (Expected(ex))
            { state = ExportResultState.PublishedButVerificationFailed; reason = "PublishedArtifactVerificationFailed"; }
        }
        catch (OperationCanceledException) when (!admitted) { state = ExportResultState.Canceled; reason = "CanceledBeforeCommit"; }
        catch (Exception ex) when (Expected(ex))
        { state = admitted ? ExportResultState.Indeterminate : ExportResultState.InvalidDestination; reason = "OperationInspectionFailed"; retain = admitted; }
        return Finish();

        ExportPublicationResult Finish()
        {
            // Reservation ownership and verified displaced-original ownership are separate facts. No fingerprint
            // equality can grant ownership of an unexpected occupant or ambiguous native-exception backup.
            bool retainRecovery = retain;
            bool cleanReservation = false;
            if (backup is not null && backupProvenance != BackupProvenance.SuccessfulReplacementVerified && !DefiniteAbsent(backup))
            {
                var current = Observe(backup, out backupObservation);
                cleanReservation = !retain && reservationCreated && reservation is not null && Same(current, reservation)
                    && (!attempted || state == ExportResultState.PublicationFailed);
                if (!cleanReservation)
                {
                    retainRecovery = true;
                    backupProvenance = admitted && attempted ? BackupProvenance.AmbiguousAfterPublicationException : BackupProvenance.UnexpectedBeforeNative;
                }
            }
            string? note = null;
            if (retainRecovery && recovery is not null && recoveryId is not null)
            {
                try
                {
                    // Disclosed user-owned recovery metadata only, never card text, title, hash or a production log.
                    using var dir = Open(recovery, directory: true, share: 3);
                    VerifyRecoveryContainer(dir);
                    note = Path.Combine(recovery, "recovery.json");
                    byte[] metadata = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        operationId = id, format = plan.Format.ToString(), state = state.ToString(), reasonCode = reason,
                        targetName = Path.GetFileName(plan.Destination), stageName = Path.GetFileName(stage), backupName = Path.GetFileName(backup)
                    });
                    if (metadata.Length > 4096) throw new InvalidDataException("Recovery metadata exceeds 4 KiB");
                    using var file = new FileStream(note, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    _owned.TryAdd(Identity(Information(file.SafeFileHandle)), 0); file.Write(metadata); file.Flush(flushToDisk: true);
                }
                catch (Exception ex) when (Expected(ex)) { warnings.Add("RecoveryNoteWriteFailed"); }
            }
            recoveryPin?.Dispose(); parentPin?.Dispose();
            if (stageCreated && stageId is null) warnings.Add("StageOwnershipInspectionFailed");
            if (recoveryCreated && recoveryId is null) warnings.Add("RecoveryOwnershipInspectionFailed");
            if (reservationCreated && reservation is null) warnings.Add("ReservationOwnershipInspectionFailed");
            // All cleanup is exact-identity, handle-bound, and synchronous before result/task settlement.
            if (!retain)
            {
                if (stageId is not null && stage is not null) Cleanup(stage, stageId, null);
                if (cleanReservation) Cleanup(backup!, reservation!.Identity, reservation, placeholder: true);
                if (backup is not null && plan.Existing is not null && replacementSucceeded && admitted && attempted
                    && state == ExportResultState.Published && backupProvenance == BackupProvenance.SuccessfulReplacementVerified)
                    Cleanup(backup, plan.Existing.Identity, plan.Existing);
                if (!retainRecovery && recoveryId is not null && recovery is not null) Cleanup(recovery, recoveryId, null, directory: true);
            }
            var paths = new List<string>();
            if (retainRecovery || warnings.Count > 0)
            {
                foreach (string? path in new[] { stage, backup, note, recovery })
                    if (path is not null && !DefiniteAbsent(path)) paths.Add(path);
            }
            // Result paths are for a future disclosed UI; this class writes no production log or card-content sidecar.
            if (destinationObservation == "NotInspected") _ = Observe(plan.Destination, out destinationObservation);
            if (stage is not null) _ = Observe(stage, out stageObservation);
            if (backup is not null) _ = Observe(backup, out backupObservation);
            return new(id, plan.Format, state, reason, admitted, attempted, paths.AsReadOnly(), warnings.AsReadOnly(),
                destinationObservation, stageObservation, backupObservation);
        }
        void VerifyRecoveryContainer(SafeFileHandle handle)
        {
            var info = Information(handle);
            if (recovery is null || recoveryId is null || !_owned.ContainsKey(recoveryId) || Identity(info) != recoveryId
                || (info.Attributes & 0x10u) == 0 || UnsafeAttributes(info.Attributes)
                || !Canonical(handle).Equals(recovery, StringComparison.OrdinalIgnoreCase)
                || backup is null || !string.Equals(Path.GetDirectoryName(backup), recovery, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Recovery ownership/location changed");
        }
        void RequireOwnedReservation()
        {
            if (backup is null) return;
            using var dir = Open(recovery!, directory: true, share: 3);
            VerifyRecoveryContainer(dir);
            using var slot = Open(backup, directory: false, share: 3);
            var info = Information(slot);
            if (!reservationCreated || reservation is null || !_owned.ContainsKey(reservation.Identity)
                || Identity(info) != reservation.Identity || Length(info) != 0 || LastWrite(info) != reservation.LastWrite
                || info.Links != 1 || (info.Attributes & (1u | 0x10u)) != 0 || UnsafeAttributes(info.Attributes)
                || !Canonical(slot).Equals(backup, StringComparison.OrdinalIgnoreCase))
            {
                backupProvenance = BackupProvenance.UnexpectedBeforeNative;
                throw new InvalidDataException("Backup reservation ownership changed");
            }
            backupProvenance = BackupProvenance.ReservationValidated;
        }
        void Cleanup(string path, ExportFileIdentity expectedId, ExportFingerprint? expectedFingerprint, bool directory = false, bool placeholder = false)
        {
            try
            {
                Boundary(ExportBoundary.Cleanup);
                if (path == backup)
                {
                    bool reservationAuthority = placeholder && !retain && reservationCreated && reservation is not null
                        && expectedId == reservation.Identity && (!attempted || state == ExportResultState.PublicationFailed);
                    bool displacedAuthority = !placeholder && replacementSucceeded && admitted && attempted && state == ExportResultState.Published
                        && backupProvenance == BackupProvenance.SuccessfulReplacementVerified;
                    if (!reservationAuthority && !displacedAuthority)
                        throw new InvalidDataException("Backup lacks successful publication provenance");
                    using var dir = Open(recovery!, directory: true, share: 3);
                    VerifyRecoveryContainer(dir); // Pin the exact owned container through handle-bound file deletion.
                    DeleteOwned(path, expectedId, expectedFingerprint, directory);
                }
                else DeleteOwned(path, expectedId, expectedFingerprint, directory);
                _owned.TryRemove(expectedId, out _);
            }
            catch (Exception ex) when (Expected(ex)) { warnings.Add(directory ? "RecoveryDirectoryCleanupFailed" : "OwnedFileCleanupFailed"); }
        }
    }
    private bool MatchesPlan(ExportDestinationPlan plan, CancellationToken token)
    {
        var (canonical, parent) = ValidateDestination(plan.Destination, plan.Format);
        return canonical.Equals(plan.Destination, StringComparison.OrdinalIgnoreCase) && parent == plan.ParentIdentity
            && Same(Inspect(canonical, token), plan.Existing);
    }
    private (string Path, ExportFileIdentity Parent) ValidateDestination(string input, FlashcardExportFormat format)
    {
        _ = ExportFileNameSanitizer.Extension(format); _ = FlashcardLimits.StrictUtf8.GetByteCount(input);
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(input) || input.Length < 4 ||
            !char.IsAsciiLetter(input[0]) || input[1] != ':' || input[2] != '\\' || input[3..].Contains(':') || input.Contains('/'))
            throw new InvalidDataException("Unsupported path form");
        string full = Path.GetFullPath(input);
        if (!Path.GetExtension(full).Equals(ExportFileNameSanitizer.Extension(format), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Extension mismatch");
        foreach (string part in input[3..].Split('\\'))
            if (part.Length == 0 || part.Length > 255 || part.EndsWith(' ') || part.EndsWith('.') || ExportFileNameSanitizer.IsReserved(part)
                || part.Any(c => char.IsControl(c) || "<>\"|?*".Contains(c))) throw new InvalidDataException("Invalid path component");
        var volumePath = new StringBuilder(32768);
        if (!Native.GetVolumePathName(full, volumePath, volumePath.Capacity) || Native.GetDriveType(volumePath.ToString()) != 3)
            throw new InvalidDataException("Local fixed volume required");
        var fs = new StringBuilder(256);
        if (!Native.GetVolumeInformation(volumePath.ToString(), null, 0, out _, out _, out _, fs, fs.Capacity) || fs.ToString() != "NTFS")
            throw new InvalidDataException("NTFS required");
        string parentPath = Path.GetDirectoryName(full)!;
        ExportFileIdentity parentId = default!; string canonicalParent = "";
        // Open every ancestor without following reparse points. Aliases (8.3, SUBST) resolve through handles.
        var ancestors = new Stack<string>();
        for (string? current = parentPath; current is not null; current = Path.GetDirectoryName(current)) ancestors.Push(current);
        foreach (string ancestor in ancestors)
        {
            using var handle = Open(ancestor, directory: true); var info = Information(handle);
            if ((info.Attributes & 0x10) == 0 || UnsafeAttributes(info.Attributes)) throw new InvalidDataException("Unsafe parent");
            parentId = Identity(info); if (_owned.ContainsKey(parentId)) throw new InvalidDataException("Owned parent");
            canonicalParent = Canonical(handle);
        }
        string canonical = Path.Combine(canonicalParent, Path.GetFileName(full));
        int recoveryRelativeLength = $".axora-export-recovery-{Guid.Empty:N}-{Guid.Empty:N}\\displaced-{Guid.Empty:N}.original".Length;
        if (canonical.Length > 32766 || canonicalParent.Length + 1 + recoveryRelativeLength > 32766)
            throw new InvalidDataException("Destination leaves insufficient space for bounded sibling recovery paths");
        foreach (string root in _protectedRoots)
            if (Within(canonical, CanonicalRoot(root))) throw new InvalidDataException("Protected destination");
        return (canonical, parentId);
    }
    private static bool Within(string candidate, string root) => candidate.Equals(root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    private static string CanonicalRoot(string root)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)); var suffix = new Stack<string>(); string current = full;
        while (true)
        {
            try { using var handle = Open(current, directory: true, noFollow: false); string value = Canonical(handle); foreach (var part in suffix) value = Path.Combine(value, part); return value; }
            catch (Win32Exception ex) when (ex.NativeErrorCode is 2 or 3)
            { suffix.Push(Path.GetFileName(current)); current = Path.GetDirectoryName(current) ?? throw new InvalidDataException("Unknown protected root"); }
        }
    }
    private static bool UnsafeAttributes(uint attrs) => (attrs & (0x400u | 0x1000u | 0x40000u | 0x400000u)) != 0;
    private static ExportFingerprint? Inspect(string path, CancellationToken token)
    {
        // Inspect attributes before requesting content access. Never hydrate a known offline/recall placeholder.
        using var metadata = Native.CreateFile(path, 0x80, 7, IntPtr.Zero, 3, 0x00200000 | 0x02000000 | 0x00100000, IntPtr.Zero);
        if (metadata.IsInvalid)
        { int error = Marshal.GetLastWin32Error(); if (error == 2) return null; throw new Win32Exception(error); }
        ValidateOrdinary(Information(metadata));
        using var handle = Native.CreateFile(path, 0x80000000, 7, IntPtr.Zero, 3, 0x00200000 | 0x02000000 | 0x00100000, IntPtr.Zero);
        if (handle.IsInvalid)
        { int error = Marshal.GetLastWin32Error(); if (error == 2) return null; throw new Win32Exception(error); }
        return Fingerprint(handle, token);
    }
    private static void ValidateOrdinary(Native.FileInfo info)
    {
        if ((info.Attributes & (0x10u | 1u)) != 0 || UnsafeAttributes(info.Attributes) || info.Links != 1 || Identity(info).FileId == 0)
            throw new InvalidDataException("Unsupported file attributes or links");
    }
    private static ExportFingerprint Fingerprint(SafeFileHandle handle, CancellationToken token)
    {
        var before = Information(handle);
        ValidateOrdinary(before);
        long length = Length(before);
        if (length > FlashcardExportCodec.ArtifactByteLimit) throw new InvalidDataException("Existing/staged file exceeds 64 MiB");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // RandomAccess leaves the borrowed handle and its seek position owned by its caller.
        byte[] buffer = new byte[65536]; long offset = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested(); int n = RandomAccess.Read(handle, buffer, offset); if (n == 0) break;
            offset += n; if (offset > FlashcardExportCodec.ArtifactByteLimit) throw new InvalidDataException("File grew beyond limit");
            hash.AppendData(buffer, 0, n);
        }
        var after = Information(handle);
        if (Identity(before) != Identity(after) || length != offset || length != Length(after) || LastWrite(before) != LastWrite(after))
            throw new InvalidDataException("File changed during inspection");
        return new(Canonical(handle), Identity(after), length, LastWrite(after), Convert.ToHexString(hash.GetHashAndReset()));
    }
    private static void ValidateArtifact(string path, ExportFingerprint expected, FlashcardExportSnapshot snapshot,
        FlashcardExportFormat format, CancellationToken token)
    {
        using var handle = Native.CreateFile(path, 0x80000000, 1, IntPtr.Zero, 3, 0x00200000 | 0x00100000, IntPtr.Zero);
        if (handle.IsInvalid) throw NativeError();
        using var file = new FileStream(handle, FileAccess.Read, 65536, isAsync: false);
        if (!Same(Fingerprint(file.SafeFileHandle, token), expected)) throw new InvalidDataException("Artifact fingerprint mismatch");
        FlashcardExportCodec.Validate(file, snapshot, format, token);
        if (!Same(Fingerprint(file.SafeFileHandle, token), expected)) throw new InvalidDataException("Artifact changed during validation");
    }
    private static bool Same(ExportFingerprint? a, ExportFingerprint? b) => a is null && b is null || a is not null && b is not null
        && a.Identity == b.Identity && a.Length == b.Length && a.LastWrite == b.LastWrite && a.Sha256 == b.Sha256;
    private static ExportFingerprint? Observe(string path, out string observation)
    {
        try { var value = Inspect(path, CancellationToken.None); observation = value is null ? "Absent" : "PresentInspected"; return value; }
        catch (Exception ex) when (Expected(ex)) { observation = "UnknownInspectionFailed"; return null; }
    }
    private static bool DefiniteAbsent(string path)
    {
        using var handle = Native.CreateFile(path, 0x80, 7, IntPtr.Zero, 3, 0x00200000 | 0x02000000, IntPtr.Zero);
        return handle.IsInvalid && Marshal.GetLastWin32Error() == 2;
    }
    private static void DeleteOwned(string path, ExportFileIdentity identity, ExportFingerprint? fingerprint, bool directory)
    {
        using var handle = Native.CreateFile(path, 0x10000u | (directory ? 0x80u : 0x80000000u), 0, IntPtr.Zero, 3,
            0x00200000u | 0x02000000u | 0x00100000u, IntPtr.Zero);
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); if (error == 2) return; throw new Win32Exception(error); }
        var info = Information(handle);
        if (Identity(info) != identity || UnsafeAttributes(info.Attributes) || (!directory && info.Links != 1))
            throw new InvalidDataException("Cleanup identity mismatch");
        if (fingerprint is not null && !Same(Fingerprint(handle, CancellationToken.None), fingerprint))
            throw new InvalidDataException("Cleanup fingerprint mismatch");
        byte disposition = 1;
        if (!Native.SetFileInformationByHandle(handle, 4, ref disposition, 1)) throw NativeError();
        // Directory deletion fails if nonempty. No enumeration sweep and no recursive deletion.
    }
    private static SafeFileHandle Open(string path, bool directory, uint share = 7, bool noFollow = true)
    {
        var handle = Native.CreateFile(path, 0x80, share, IntPtr.Zero, 3, (noFollow ? 0x00200000u : 0) | 0x00100000u | (directory ? 0x02000000u : 0), IntPtr.Zero);
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); } return handle;
    }
    private static Native.FileInfo Information(SafeFileHandle handle)
    { if (!Native.GetFileInformationByHandle(handle, out var info)) throw NativeError(); return info; }
    private static ExportFileIdentity Identity(Native.FileInfo info) => new(info.Volume, ((ulong)info.IndexHigh << 32) | info.IndexLow);
    private static long Length(Native.FileInfo info) => checked((long)(((ulong)info.SizeHigh << 32) | info.SizeLow));
    private static long LastWrite(Native.FileInfo info) => ((long)info.WriteHigh << 32) | info.WriteLow;
    private static string Canonical(SafeFileHandle handle)
    {
        var text = new StringBuilder(32768); uint n = Native.GetFinalPathNameByHandle(handle, text, text.Capacity, 0);
        if (n == 0 || n >= text.Capacity) throw NativeError();
        string path = text.ToString(); if (!path.StartsWith("\\\\?\\") || path.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unsupported canonical path");
        return path[4..];
    }
    private static Win32Exception NativeError() => new(Marshal.GetLastWin32Error());
    private static bool Expected(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception or ArgumentException
        or InvalidOperationException or NotSupportedException or System.Text.Json.JsonException or OperationCanceledException;
    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct FileInfo
        {
            public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
                Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfo info);
        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, int count, uint flags);
        [DllImport("kernel32.dll", EntryPoint = "GetVolumePathNameW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetVolumePathName(string path, StringBuilder volumePath, int count);
        [DllImport("kernel32.dll", EntryPoint = "GetVolumeInformationW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetVolumeInformation(string root, StringBuilder? name, int nameLength, out uint serial,
            out uint componentLength, out uint flags, StringBuilder fs, int fsLength);
        [DllImport("kernel32.dll", EntryPoint = "GetDriveTypeW", CharSet = CharSet.Unicode)] internal static extern uint GetDriveType(string root);
        [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateDirectory(string path, IntPtr security);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, ref byte data, uint size);
    }
}
