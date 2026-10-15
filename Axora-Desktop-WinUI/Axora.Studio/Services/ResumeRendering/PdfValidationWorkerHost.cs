using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Axora.PdfValidation;
using Axora.Studio.Models.ResumeRendering;
using Axora.Studio.Services.Rendering;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Services.ResumeRendering;

// Infrastructure seam only; no DI/UI registration or validated-artifact construction.
internal sealed class PdfValidationWorkerHost
{
    private static readonly ConcurrentBag<object> Quarantine = [];
    private readonly object gate = new();
    private readonly PdfValidationWorkerRuntime runtime;
    private readonly string operations;
    private readonly IPdfFoundationProcess process;
    private Task<PdfFoundationResult>? active;
    private CancellationTokenSource? cancellation;
    private Task? shutdown;
    private bool closed, unresolved;
    internal Action<string>? WorkspaceSetupForTest { get; init; }
    internal PdfValidationWorkerHost() : this(new(), Path.Combine(PdfValidationWorkerRuntime.DefaultBase, "operations"), new PdfFoundationProcess()) { }
    internal PdfValidationWorkerHost(PdfValidationWorkerRuntime runtime, string ownedOperations, IPdfFoundationProcess process)
    { this.runtime = runtime; operations = ownedOperations; this.process = process; RendererWorkspace.RejectRedirects(operations); }

    internal Task<PdfFoundationResult> ProbeAsync(UnvalidatedResumePdf candidate, CancellationToken token = default)
    {
        lock (gate)
        {
            if (closed) return Task.FromResult(new PdfFoundationResult(PdfFoundationOutcome.AdmissionClosed));
            if (active is not null) return Task.FromResult(new PdfFoundationResult(PdfFoundationOutcome.Busy));
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var completion = new TaskCompletionSource<PdfFoundationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            active = completion.Task;
            _ = ExecuteAndRelease(candidate, cancellation, completion);
            return completion.Task;
        }
    }
    private async Task ExecuteAndRelease(UnvalidatedResumePdf candidate, CancellationTokenSource cancel, TaskCompletionSource<PdfFoundationResult> completion)
    {
        PdfFoundationResult result;
        try { result = await Task.Run(() => Execute(candidate, cancel.Token)).ConfigureAwait(false); }
        catch (Exception) { result = new(PdfFoundationOutcome.SettlementFailed, Settled: false, CleanupComplete: false); }
        lock (gate)
        {
            if (!result.Settled || !result.CleanupComplete || !result.InputSettled) { unresolved = true; closed = true; }
            if (cancel.IsCancellationRequested && result.Settled && result.CleanupComplete)
                result = result with { Outcome = PdfFoundationOutcome.Canceled, Observation = null };
            active = null; cancellation = null; cancel.Dispose(); completion.TrySetResult(result);
        }
    }
    private async Task<PdfFoundationResult> Execute(UnvalidatedResumePdf candidate, CancellationToken token)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        WindowsRendererSandbox? sandbox = null;
        PdfWorkerBundleLease? bundle = null;
        PdfValidationWorkspace? workspace = null;
        PdfFoundationRequest? request = null;
        byte[] pdf = [], frame = [];
        PdfFoundationResult result = new(PdfFoundationOutcome.StartupFailed);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (candidate is null) return new(PdfFoundationOutcome.ProtocolRejected);
            pdf = candidate.CopyBytesForValidation();
            if (PdfValidationProtocol.Hash(pdf) != candidate.PdfSha256) throw new InvalidDataException("CandidateIdentityRejected");
            sandbox = new();
            bundle = runtime.Verify(sandbox);
            sandbox.EnsureOwnedDirectory(operations, true);
            workspace = PdfValidationWorkspace.Create(operations, sandbox, WorkspaceSetupForTest);
            request = new(Guid.NewGuid(), candidate.OperationId, InputIdentity(candidate), PdfValidationWorkerRuntime.Manifest.Sha256, pdf);
            frame = PdfValidationProtocol.EncodeRequest(request);
            workspace.MarkRunning();
            result = new(PdfFoundationOutcome.StartupFailed, Settled: false, CleanupComplete: false, InputSettled: false);
            var native = await process.RunAsync(bundle, workspace, frame, linked.Token).ConfigureAwait(false);
            result = new(native.Outcome, Settled: native.Settled && native.InputSettled && native.FinalActive == 0, JobMembership: native.JobMembership, AppContainer: native.AppContainer,
                ZeroCapabilities: native.ZeroCapabilities, InputSettled: native.InputSettled, FinalActiveProcesses: native.FinalActive);
            try
            {
                if (native.Outcome == PdfFoundationOutcome.FoundationObserved && native.Settled && native.InputSettled && native.InputWritten &&
                    native.JobMembership && native.AppContainer && native.ZeroCapabilities && native.FinalActive == 0)
                {
                    using var replyStream = new MemoryStream(native.Output, writable: false);
                    PdfFoundationReply reply;
                    try { reply = PdfValidationProtocol.ReadReply(replyStream); }
                    catch (EndOfStreamException) { throw new InvalidDataException("PdfFoundationReplyTruncated"); }
                    PdfValidationProtocol.RequireReplyIdentity(reply, request, PdfValidationProtocol.Hash(frame));
                    result = result with { Outcome = reply.Status == PdfFoundationStatus.FoundationObserved ? PdfFoundationOutcome.FoundationObserved : PdfFoundationOutcome.DependencyRejected, Observation = reply };
                }
                else if (native.Outcome == PdfFoundationOutcome.FoundationObserved)
                    result = result with { Outcome = PdfFoundationOutcome.WorkerFailed };
            }
            finally { Array.Clear(native.Output); }
        }
        catch (OperationCanceledException) { result = result with { Outcome = PdfFoundationOutcome.Canceled, Observation = null }; }
        catch (FileNotFoundException) { result = result with { Outcome = PdfFoundationOutcome.RuntimeUnavailable, Observation = null }; }
        catch (DirectoryNotFoundException) { result = result with { Outcome = PdfFoundationOutcome.RuntimeUnavailable, Observation = null }; }
        catch (PdfFoundationSetupException) { result = new(PdfFoundationOutcome.CleanupFailed, CleanupComplete: false); }
        catch (RendererProvisioningException) { result = result with { Outcome = PdfFoundationOutcome.ProvisioningRequired, Observation = null }; }
        catch (InvalidDataException)
        { result = result with { Outcome = bundle is null ? PdfFoundationOutcome.RuntimeIntegrityFailure : PdfFoundationOutcome.ProtocolRejected, Observation = null }; }
        catch (Exception ex) when (RendererWorkspace.Expected(ex))
        { result = result with { Outcome = sandbox is null ? PdfFoundationOutcome.IsolationUnavailable : PdfFoundationOutcome.StartupFailed, Observation = null }; }
        finally
        {
            if (workspace is not null)
            {
                if (result.Settled && result.InputSettled) workspace.MarkSettled();
                bool cleaned = workspace.Cleanup();
                if (!cleaned) result = result with { Outcome = PdfFoundationOutcome.CleanupFailed, Observation = null, CleanupComplete = false };
                workspace.Dispose();
            }
            if (result.Settled && result.InputSettled)
            { bundle?.Dispose(); sandbox?.Dispose(); Array.Clear(frame); request?.Clear(); }
            else Quarantine.Add(new object?[] { bundle, workspace, sandbox, frame, request });
            Array.Clear(pdf);
        }
        if (deadline.IsCancellationRequested && !token.IsCancellationRequested && result.Settled && result.CleanupComplete)
            result = result with { Outcome = PdfFoundationOutcome.TimedOut, Observation = null };
        return result;
    }
    private static string InputIdentity(UnvalidatedResumePdf c)
    {
        using var bytes = new MemoryStream();
        using (var w = new Utf8JsonWriter(bytes))
        {
            w.WriteStartObject(); w.WriteString("domain", "AXORA.PdfValidation.FoundationInput"); w.WriteNumber("version", 1);
            w.WriteString("operationId", c.OperationId); w.WriteString("documentId", c.DocumentId); w.WriteNumber("revision", c.Revision);
            w.WriteString("snapshotSha256", c.SnapshotSha256); w.WriteString("templateId", c.TemplateId); w.WriteNumber("templateVersion", c.TemplateVersion);
            w.WriteString("templateSourceSha256", c.TemplateSourceSha256); w.WriteString("renderDataSha256", c.RenderDataSha256); w.WriteString("renderJobSha256", c.RenderJobSha256);
            w.WriteString("rendererId", c.RendererId); w.WriteString("rendererVersion", c.RendererVersion); w.WriteString("rendererExecutableSha256", c.RendererExecutableSha256);
            w.WriteString("fontChoice", c.FontChoice.ToString()); w.WriteString("requestedFont", c.RequestedFont); w.WriteString("effectiveFontProfile", c.EffectiveFontProfile);
            w.WriteString("pdfSha256", c.PdfSha256); w.WriteString("policySha256", PdfValidationProtocol.PolicySha256); w.WriteEndObject();
        }
        return PdfValidationProtocol.Hash(bytes.ToArray()); // Identity digest only crosses IPC; no Resume text/path metadata.
    }
    internal Task ShutdownAsync()
    {
        lock (gate)
        {
            if (shutdown is not null) return shutdown;
            closed = true; cancellation?.Cancel();
            return shutdown = unresolved ? Task.FromException(new IOException("PdfFoundationSettlementUnresolved")) : Settle(active);
        }
    }
    private static async Task Settle(Task<PdfFoundationResult>? task)
    {
        if (task is null) return;
        var result = await task.ConfigureAwait(false);
        if (!result.Settled || !result.CleanupComplete || !result.InputSettled) throw new IOException("PdfFoundationSettlementUnresolved");
    }
}

internal interface IPdfFoundationProcess
{
    Task<PdfFoundationNativeResult> RunAsync(PdfWorkerBundleLease bundle, PdfValidationWorkspace workspace, byte[] frame, CancellationToken token);
}
internal sealed record PdfFoundationNativeResult(PdfFoundationOutcome Outcome, byte[] Output, bool Settled = true,
    bool JobMembership = false, bool AppContainer = false, bool ZeroCapabilities = false, bool InputSettled = true, bool InputWritten = false, uint FinalActive = 0);

// Fixed executable + empty command line. Reuses V2 sandbox/Job objects without modifying B1.
internal sealed class PdfFoundationProcess : IPdfFoundationProcess
{
    private static readonly ConcurrentBag<object> Quarantine = [];
    internal Action? BeforeResumeForTest { get; init; }
    public Task<PdfFoundationNativeResult> RunAsync(PdfWorkerBundleLease bundle, PdfValidationWorkspace workspace, byte[] frame, CancellationToken token)
        => Task.Run(() => Run(bundle, workspace, frame, token));
    private PdfFoundationNativeResult Run(PdfWorkerBundleLease bundle, PdfValidationWorkspace workspace, byte[] frame, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        WindowsRendererSandbox? sandbox = null; WindowsRendererJob? job = null;
        Pipe? stdin = null, stdout = null, stderr = null;
        SafeFileHandle? process = null, thread = null;
        Task<byte[]>? outputRead = null; Task? errorRead = null; Task<bool>? inputWrite = null;
        IntPtr attributes = IntPtr.Zero, environment = IntPtr.Zero, handles = IntPtr.Zero, security = IntPtr.Zero;
        bool initialized = false, created = false, assigned = false, treeSettled = true, pipesSettled = true, inputSettled = true, inputWritten = false;
        bool appContainer = false, zeroCapabilities = false; uint finalActive = 0, exit = 0;
        int overflow = 0; byte[] output = [];
        PdfFoundationOutcome outcome = PdfFoundationOutcome.FoundationObserved;
        try
        {
            token.ThrowIfCancellationRequested(); workspace.Validate(); sandbox = new(); job = new();
            stdin = new(input: true); stdout = new(); stderr = new();
            nuint size = 0; InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            if (size == 0) throw new Win32Exception();
            attributes = Marshal.AllocHGlobal(checked((int)size));
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref size)) throw new Win32Exception();
            initialized = true; handles = Marshal.AllocHGlobal(IntPtr.Size * 3);
            Marshal.WriteIntPtr(handles, 0, stdin.Read.DangerousGetHandle());
            Marshal.WriteIntPtr(handles, IntPtr.Size, stdout.Write.DangerousGetHandle());
            Marshal.WriteIntPtr(handles, IntPtr.Size * 2, stderr.Write.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(attributes, 0, 0x20002, handles, (nuint)(IntPtr.Size * 3), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            security = Marshal.AllocHGlobal(Marshal.SizeOf<SecurityCapabilities>());
            Marshal.StructureToPtr(new SecurityCapabilities { Sid = sandbox.Sid }, security, false);
            if (!UpdateProcThreadAttribute(attributes, 0, 0x20009, security, (nuint)Marshal.SizeOf<SecurityCapabilities>(), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            var startup = new StartupInfoEx { Info = new() { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100,
                Input = stdin.Read.DangerousGetHandle(), Output = stdout.Write.DangerousGetHandle(), Error = stderr.Write.DangerousGetHandle() }, Attributes = attributes };
            environment = Marshal.StringToHGlobalUni(string.Join('\0', new[] {
                "APPDATA=" + workspace.Root, "LOCALAPPDATA=" + workspace.Root, "USERPROFILE=" + workspace.Root,
                "SystemRoot=" + Environment.GetFolderPath(Environment.SpecialFolder.Windows), "TEMP=" + workspace.Scratch, "TMP=" + workspace.Scratch,
                "DOTNET_EnableDiagnostics=0", "DOTNET_MULTILEVEL_LOOKUP=0" }) + "\0\0");
            bundle.VerifyAgain(); workspace.Validate(); sandbox.ValidateProfileBoundary();
            if (!CreateProcessW(bundle.Executable, new StringBuilder(WindowsRendererProcess.Quote(bundle.Executable)), IntPtr.Zero, IntPtr.Zero, true,
                0x80000 | 0x400 | 0x4 | 0x08000000, environment, workspace.Root, ref startup, out var native)) throw new Win32Exception();
            process = new(native.Process, true); thread = new(native.Thread, true); created = true; treeSettled = false; workspace.MarkRunning();
            job.Assign(process); assigned = true;
            (appContainer, zeroCapabilities) = InspectToken(process, sandbox.Sid);
            if (!appContainer || !zeroCapabilities) throw new InvalidDataException("PdfWorkerIsolationRejected");
            stdin.Read.Dispose(); stdout.Write.Dispose(); stderr.Write.Dispose();
            outputRead = Task.Factory.StartNew(() => ReadOutput(stdout.Read), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            errorRead = Task.Factory.StartNew(() => DrainErrors(stderr.Read), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            inputWrite = Task.Factory.StartNew(() => WriteInput(stdin.Write), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            pipesSettled = false; inputSettled = false;
            BeforeResumeForTest?.Invoke();
            if (token.IsCancellationRequested) outcome = PdfFoundationOutcome.Canceled;
            else if (ResumeThread(thread) != 1) throw new Win32Exception();
            thread.Dispose();
            while (outcome == PdfFoundationOutcome.FoundationObserved)
            {
                uint wait = WaitForSingleObject(process, 0);
                if (wait == 0) break;
                if (wait != 0x102) throw new Win32Exception();
                if (token.IsCancellationRequested) outcome = PdfFoundationOutcome.Canceled;
                else if (Volatile.Read(ref overflow) != 0) outcome = PdfFoundationOutcome.PipeLimit;
                else if (clock.Elapsed >= TimeSpan.FromSeconds(60)) outcome = PdfFoundationOutcome.TimedOut;
                else { workspace.Validate(); sandbox.ValidateProfileBoundary(); Thread.Sleep(10); }
            }
            if (!GetExitCodeProcess(process, out exit)) throw new Win32Exception();
            if (outcome == PdfFoundationOutcome.FoundationObserved && exit != 0) outcome = PdfFoundationOutcome.WorkerFailed;
            workspace.Validate(); sandbox.ValidateProfileBoundary();
        }
        catch (OperationCanceledException) { outcome = PdfFoundationOutcome.Canceled; }
        catch (Exception ex) when (RendererWorkspace.Expected(ex))
        { outcome = sandbox is null ? PdfFoundationOutcome.IsolationUnavailable : !created || !assigned ? PdfFoundationOutcome.StartupFailed : PdfFoundationOutcome.WorkerFailed; }
        finally
        {
            var settle = Stopwatch.StartNew();
            if (created && process is not null)
            {
                try
                {
                    if (assigned) job!.Terminate();
                    else if (!TerminateProcess(process, 0xA202) && WaitForSingleObject(process, 0) != 0) throw new IOException();
                    if (WaitForSingleObject(process, 2000) != 0 || !GetExitCodeProcess(process, out exit) || exit == 259) throw new IOException();
                    while (assigned && job!.Observe().Active != 0 && settle.ElapsedMilliseconds < 2500) Thread.Sleep(5);
                    finalActive = assigned ? job!.Observe().Active : 0; treeSettled = finalActive == 0;
                }
                catch (Exception ex) when (RendererWorkspace.Expected(ex)) { treeSettled = false; }
            }
            stdin?.Read.Dispose(); stdout?.Write.Dispose(); stderr?.Write.Dispose(); thread?.Dispose(); process?.Dispose(); job?.Dispose();
            try
            {
                var readers = Task.WhenAll(new Task?[] { outputRead, errorRead }.Where(t => t is not null).Cast<Task>());
                if (!readers.Wait(2000))
                {
                    if (stdout is not null && outputRead?.IsCompleted == false) CancelIoEx(stdout.Read, IntPtr.Zero);
                    if (stderr is not null && errorRead?.IsCompleted == false) CancelIoEx(stderr.Read, IntPtr.Zero);
                    if (!readers.Wait(2000)) throw new IOException();
                }
                readers.GetAwaiter().GetResult(); pipesSettled = true;
                if (outputRead is not null) output = outputRead.GetAwaiter().GetResult();
                inputSettled = inputWrite is null || inputWrite.Wait(2000);
                if (!inputSettled && stdin is not null) { CancelIoEx(stdin.Write, IntPtr.Zero); inputSettled = inputWrite!.Wait(2000); }
                inputWritten = inputWrite is not null && inputSettled && inputWrite.GetAwaiter().GetResult();
            }
            catch (Exception) { pipesSettled = false; inputSettled = inputWrite is null || inputWrite.IsCompleted; }
            if (pipesSettled && inputSettled) { stdin?.Dispose(); stdout?.Dispose(); stderr?.Dispose(); }
            else Quarantine.Add(new object?[] { stdin, stdout, stderr, inputWrite, outputRead, errorRead, frame });
            if (initialized) DeleteProcThreadAttributeList(attributes);
            if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
            if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
            if (handles != IntPtr.Zero) Marshal.FreeHGlobal(handles);
            if (security != IntPtr.Zero) Marshal.FreeHGlobal(security);
            try { sandbox?.ValidateProfileBoundary(); }
            catch (Exception ex) when (RendererWorkspace.Expected(ex)) { treeSettled = false; }
            sandbox?.Dispose();
            if (treeSettled && pipesSettled && inputSettled) workspace.MarkSettled();
        }
        bool settled = treeSettled && pipesSettled && inputSettled;
        if (!settled) outcome = PdfFoundationOutcome.SettlementFailed;
        else if (overflow != 0) outcome = PdfFoundationOutcome.PipeLimit;
        if (outcome != PdfFoundationOutcome.FoundationObserved) { Array.Clear(output); output = []; }
        return new(outcome, output, settled, assigned, appContainer, zeroCapabilities, inputSettled, inputWritten, finalActive);

        bool WriteInput(SafeFileHandle handle)
        { try { using var stream = new FileStream(handle, FileAccess.Write, 8192, false); stream.Write(frame); stream.Flush(); return true; } catch (Exception ex) when (RendererWorkspace.Expected(ex)) { return false; } }
        byte[] ReadOutput(SafeFileHandle handle)
        {
            using var stream = new FileStream(handle, FileAccess.Read, 4096, false); using var memory = new MemoryStream();
            byte[] buffer = new byte[4096]; int count;
            while ((count = stream.Read(buffer)) != 0)
            { if (memory.Length + count > PdfValidationProtocol.ReplyLimit) { Interlocked.Exchange(ref overflow, 1); return []; } memory.Write(buffer, 0, count); }
            return memory.ToArray();
        }
        void DrainErrors(SafeFileHandle handle)
        {
            using var stream = new FileStream(handle, FileAccess.Read, 4096, false); byte[] buffer = new byte[4096]; int count, total = 0;
            while ((count = stream.Read(buffer)) != 0)
            { total += count; Array.Clear(buffer); if (total > 128 * 1024) { Interlocked.Exchange(ref overflow, 1); break; } }
        }
    }
    private static (bool Container, bool NoCapabilities) InspectToken(SafeFileHandle process, IntPtr expectedSid)
    {
        if (!OpenProcessToken(process, 8, out var token)) throw new Win32Exception();
        using (token)
        {
            IntPtr flag = Marshal.AllocHGlobal(4), container = IntPtr.Zero, capabilities = IntPtr.Zero;
            try
            {
                if (!GetTokenInformation(token, 29, flag, 4, out _)) throw new Win32Exception();
                bool contained = Marshal.ReadInt32(flag) == 1;
                _ = GetTokenInformation(token, 31, IntPtr.Zero, 0, out int sidSize);
                if (sidSize < IntPtr.Size || sidSize > 65536) throw new Win32Exception();
                container = Marshal.AllocHGlobal(sidSize);
                if (!GetTokenInformation(token, 31, container, sidSize, out _) || !EqualSid(Marshal.ReadIntPtr(container), expectedSid)) throw new InvalidDataException();
                _ = GetTokenInformation(token, 30, IntPtr.Zero, 0, out int size);
                if (size < 4 || size > 65536) throw new Win32Exception();
                capabilities = Marshal.AllocHGlobal(size);
                if (!GetTokenInformation(token, 30, capabilities, size, out _)) throw new Win32Exception();
                return (contained, Marshal.ReadInt32(capabilities) == 0);
            }
            finally { Marshal.FreeHGlobal(flag); if (container != IntPtr.Zero) Marshal.FreeHGlobal(container); if (capabilities != IntPtr.Zero) Marshal.FreeHGlobal(capabilities); }
        }
    }
    private sealed class Pipe : IDisposable
    {
        internal SafeFileHandle Read { get; }
        internal SafeFileHandle Write { get; }
        internal Pipe(bool input = false)
        {
            var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
            if (!CreatePipe(out var read, out var write, ref security, 0)) throw new Win32Exception(); Read = read; Write = write;
            if (!SetHandleInformation(input ? Write : Read, 1, 0)) { Dispose(); throw new Win32Exception(); }
        }
        public void Dispose() { Read.Dispose(); Write.Dispose(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityCapabilities { public IntPtr Sid, Capabilities; public uint Count, Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfo
    { public int Size; public IntPtr Reserved, Desktop, Title; public uint X, Y, Width, Height, CharsX, CharsY, Fill, Flags; public ushort Show, ReservedBytes; public IntPtr ReservedPointer, Input, Output, Error; }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo Info; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, ref SecurityAttributes security, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returned);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcessW(string application, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(SafeFileHandle thread);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(SafeFileHandle process, out uint exit);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(SafeFileHandle process, uint exit);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(SafeFileHandle process, uint access, out SafeFileHandle token);
    [DllImport("advapi32.dll")] private static extern bool EqualSid(IntPtr first, IntPtr second);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(SafeFileHandle token, int information, IntPtr buffer, int bytes, out int required);
}
