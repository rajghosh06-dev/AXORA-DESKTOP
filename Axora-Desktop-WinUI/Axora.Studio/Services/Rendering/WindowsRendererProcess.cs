using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Axora.Studio.Models.Rendering;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Services.Rendering;

internal sealed class WindowsRendererProcess : IRendererProcess
{
    internal Action<string>? BeforePhaseForTest { get; init; }
    public Task<RendererProcessResult> RunAsync(RendererLaunch launch, CancellationToken token) => Task.Run(() => Run(launch, token));
    private RendererProcessResult Run(RendererLaunch launch, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        var settlement = new Stopwatch();
        WindowsRendererSandbox? sandbox = null;
        WindowsRendererJob? job = null;
        Pipe? stdout = null, stderr = null;
        SafeFileHandle? input = null, process = null, thread = null;
        IntPtr attributes = IntPtr.Zero, environment = IntPtr.Zero, handleList = IntPtr.Zero, security = IntPtr.Zero;
        bool attributesInitialized = false, created = false, assigned = false, treeSettled = true, readersSettled = true;
        bool appContainer = false, zeroCapabilities = false, finalized = true;
        Task<byte[]>? outputRead = null, errorRead = null;
        RendererOutcome outcome = RendererOutcome.Completed;
        byte[] output = [];
        long diagnosticBytes = 0;
        int overflow = 0;
        uint exit = 0, processCount = 0;
        ulong peak = 0;
        try
        {
            token.ThrowIfCancellationRequested();
            if (launch.OutputLimit < 1 || launch.OutputLimit > RendererLimits.Pdf || launch.DiagnosticLimit < 1 || launch.DiagnosticLimit > RendererLimits.Diagnostics || launch.Timeout <= TimeSpan.Zero || launch.Timeout > TimeSpan.FromSeconds(60))
                throw new ArgumentException("Invalid process limits");
            launch.Workspace.Validate();
            BeforePhaseForTest?.Invoke("sandbox");
            sandbox = new();
            BeforePhaseForTest?.Invoke("sandbox-ready");
            job = new();
            stdout = new(); stderr = new();
            var sa = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
            input = CreateFileW("NUL", 0x80000000, 3, ref sa, 3, 0, IntPtr.Zero);
            if (input.IsInvalid) throw new Win32Exception();
            nuint size = 0; InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            if (size == 0) throw new Win32Exception();
            attributes = Marshal.AllocHGlobal(checked((int)size));
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref size)) throw new Win32Exception();
            attributesInitialized = true;
            handleList = Marshal.AllocHGlobal(IntPtr.Size * 3);
            Marshal.WriteIntPtr(handleList, 0, input.DangerousGetHandle());
            Marshal.WriteIntPtr(handleList, IntPtr.Size, stdout.Write.DangerousGetHandle());
            Marshal.WriteIntPtr(handleList, IntPtr.Size * 2, stderr.Write.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(attributes, 0, 0x20002, handleList, (nuint)(IntPtr.Size * 3), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            security = Marshal.AllocHGlobal(Marshal.SizeOf<SecurityCapabilities>());
            Marshal.StructureToPtr(new SecurityCapabilities { Sid = sandbox.Sid }, security, false);
            if (!UpdateProcThreadAttribute(attributes, 0, 0x20009, security, (nuint)Marshal.SizeOf<SecurityCapabilities>(), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            var info = new StartupInfoEx { Info = new() { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100,
                Input = input.DangerousGetHandle(), Output = stdout.Write.DangerousGetHandle(), Error = stderr.Write.DangerousGetHandle() }, Attributes = attributes };
            environment = Marshal.StringToHGlobalUni(string.Join('\0', new[] {
                "APPDATA=" + launch.Workspace.Input, "LOCALAPPDATA=" + launch.Workspace.Input,
                "SystemRoot=" + Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "TEMP=" + launch.Workspace.Scratch, "TMP=" + launch.Workspace.Scratch, "USERPROFILE=" + launch.Workspace.Input }) + "\0\0");
            string executable = launch.Runtime.Path;
            var command = new StringBuilder(Quote(executable) + " " + string.Join(" ", launch.Arguments.Select(Quote)));
            BeforePhaseForTest?.Invoke("before-create");
            launch.Workspace.Validate();
            sandbox.ValidateProfileBoundary();
            launch.Runtime.VerifyAgain(); // Hash the locked executable immediately before the native launch.
            if (!CreateProcessW(executable, command, IntPtr.Zero, IntPtr.Zero, true, 0x80000 | 0x400 | 0x4 | 0x08000000,
                environment, launch.Workspace.Input, ref info, out var native)) throw new Win32Exception();
            process = new(native.Process, ownsHandle: true); thread = new(native.Thread, ownsHandle: true);
            created = true; treeSettled = false; launch.Workspace.MarkRunning();
            BeforePhaseForTest?.Invoke("before-assign");
            job.Assign(process); assigned = true;
            (appContainer, zeroCapabilities) = InspectToken(process, sandbox.Sid);
            if (!appContainer || !zeroCapabilities) throw new InvalidDataException("Isolation token mismatch");
            stdout.Write.Dispose(); stderr.Write.Dispose(); input.Dispose();
            outputRead = Read(stdout.Read, launch.OutputLimit, launch.Pdf);
            errorRead = Read(stderr.Read, launch.DiagnosticLimit, false);
            readersSettled = false;
            BeforePhaseForTest?.Invoke("before-resume");
            if (token.IsCancellationRequested) outcome = RendererOutcome.Canceled;
            else if (ResumeThread(thread) != 1) throw new Win32Exception();
            thread.Dispose();
            long deadline = clock.ElapsedMilliseconds + checked((long)launch.Timeout.TotalMilliseconds);
            while (outcome == RendererOutcome.Completed)
            {
                uint wait = WaitForSingleObject(process, 0);
                if (wait == 0) break;
                if (wait != 0x102) throw new Win32Exception();
                if (token.IsCancellationRequested) outcome = RendererOutcome.Canceled;
                else if (Volatile.Read(ref overflow) != 0) outcome = overflow == 1 ? RendererOutcome.OutputLimitExceeded : RendererOutcome.DiagnosticLimitExceeded;
                else if (clock.ElapsedMilliseconds >= deadline) outcome = RendererOutcome.TimedOut;
                else { launch.Workspace.Validate(); sandbox.ValidateProfileBoundary(); Thread.Sleep(5); }
            }
            if (!GetExitCodeProcess(process, out exit)) throw new Win32Exception();
            if (outcome == RendererOutcome.Completed && exit != 0) outcome = RendererOutcome.RendererFailed;
            launch.Workspace.Validate();
            sandbox.ValidateProfileBoundary();
        }
        catch (OperationCanceledException) { outcome = RendererOutcome.Canceled; }
        catch (Exception ex) when (RendererWorkspace.Expected(ex))
        { outcome = ex is InvalidDataException ? RendererOutcome.UnexpectedArtifacts : sandbox is null ? RendererOutcome.IsolationUnavailable : !created || !assigned ? RendererOutcome.StartupFailed : RendererOutcome.RendererFailed; }
        finally
        {
            settlement.Start();
            // Every branch, including assignment/resume failure, settles ownership before producing a result.
            if (created && process is not null)
            {
                try
                {
                    if (assigned) job!.Terminate();
                    else if (!TerminateProcess(process, 0xA202) && WaitForSingleObject(process, 0) != 0) throw new Win32Exception();
                    if (WaitForSingleObject(process, 2000) != 0) throw new IOException("Root settlement failed");
                    if (!GetExitCodeProcess(process, out exit) || exit == 259) throw new IOException("Final exit state unresolved");
                    while (assigned && job!.Observe().Active != 0 && settlement.ElapsedMilliseconds < 2500) Thread.Sleep(5);
                    treeSettled = !assigned || job!.Observe().Active == 0;
                    if (!treeSettled) throw new IOException("Tree settlement failed");
                    if (assigned) { var observed = job!.Observe(); peak = observed.Peak; processCount = observed.Total; }
                }
                catch (Exception ex) when (RendererWorkspace.Expected(ex)) { treeSettled = false; }
            }
            stdout?.Write.Dispose(); stderr?.Write.Dispose(); input?.Dispose(); thread?.Dispose(); process?.Dispose();
            // Closing the last job handle is an additional kill-on-close guarantee, never proof of settlement.
            job?.Dispose();
            try
            {
                var readers = new[] { outputRead, errorRead }.Where(x => x is not null).Cast<Task<byte[]>>().ToArray();
                var all = Task.WhenAll(readers);
                if (!all.Wait(2000))
                {
                    if (stdout is not null && !stdout.Read.IsClosed && outputRead?.IsCompleted == false) CancelIoEx(stdout.Read, IntPtr.Zero);
                    if (stderr is not null && !stderr.Read.IsClosed && errorRead?.IsCompleted == false) CancelIoEx(stderr.Read, IntPtr.Zero);
                    if (!all.Wait(2000)) throw new IOException("Pipe readers did not settle");
                }
                all.GetAwaiter().GetResult();
                readersSettled = true;
                if (outputRead is not null) output = outputRead.GetAwaiter().GetResult();
            }
            catch (Exception) { readersSettled = false; }
            stdout?.Dispose(); stderr?.Dispose();
            if (attributesInitialized) DeleteProcThreadAttributeList(attributes);
            if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
            if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
            if (handleList != IntPtr.Zero) Marshal.FreeHGlobal(handleList);
            if (security != IntPtr.Zero) Marshal.FreeHGlobal(security);
            try { sandbox?.ValidateProfileBoundary(); }
            catch (Exception ex) when (RendererWorkspace.Expected(ex)) { treeSettled = false; }
            sandbox?.Dispose();
            finalized = treeSettled && readersSettled;
            if (finalized) launch.Workspace.MarkSettled();
        }
        if (!finalized) outcome = RendererOutcome.SettlementFailed;
        else if (overflow != 0) outcome = overflow == 1 ? RendererOutcome.OutputLimitExceeded : RendererOutcome.DiagnosticLimitExceeded;
        return new(outcome, exit, outcome == RendererOutcome.Completed ? output : [], diagnosticBytes,
            clock.Elapsed.TotalMilliseconds, settlement.Elapsed.TotalMilliseconds, peak, processCount, assigned, appContainer, zeroCapabilities, finalized);

        Task<byte[]> Read(SafeFileHandle handle, int limit, bool binary) => Task.Factory.StartNew(() =>
        {
            using var stream = new FileStream(handle, FileAccess.Read, 8192, isAsync: false);
            using var memory = new MemoryStream(); byte[] buffer = new byte[8192]; int count;
            while ((count = stream.Read(buffer)) != 0)
            {
                long diagnostics = binary ? 0 : Interlocked.Add(ref diagnosticBytes, count);
                if (memory.Length + count > limit || diagnostics > launch.DiagnosticLimit)
                {
                    Interlocked.CompareExchange(ref overflow, binary ? 1 : 2, 0);
                    break; // Supervisor kills the tree; reader owns no job handle.
                }
                memory.Write(buffer, 0, count);
            }
            return memory.ToArray();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);
    internal static string Quote(string value)
    {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes); slashes = 0; result.Append(character);
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    private static (bool Container, bool NoCapabilities) InspectToken(SafeFileHandle process, IntPtr expectedSid)
    {
        if (!OpenProcessToken(process, 8, out var token)) throw new Win32Exception();
        using (token)
        {
            IntPtr flag = Marshal.AllocHGlobal(IntPtr.Size), capabilities = IntPtr.Zero, container = IntPtr.Zero;
            try
            {
                if (!GetTokenInformation(token, 29, flag, 4, out _)) throw new Win32Exception();
                bool contained = Marshal.ReadInt32(flag) == 1;
                _ = GetTokenInformation(token, 31, IntPtr.Zero, 0, out int sidSize);
                if (sidSize < IntPtr.Size) throw new Win32Exception();
                container = Marshal.AllocHGlobal(sidSize);
                if (!GetTokenInformation(token, 31, container, sidSize, out _) || !EqualSid(Marshal.ReadIntPtr(container), expectedSid))
                    throw new InvalidDataException("AppContainer identity mismatch");
                _ = GetTokenInformation(token, 30, IntPtr.Zero, 0, out int size);
                capabilities = Marshal.AllocHGlobal(size);
                if (!GetTokenInformation(token, 30, capabilities, size, out _)) throw new Win32Exception();
                return (contained, Marshal.ReadInt32(capabilities) == 0);
            }
            finally { Marshal.FreeHGlobal(flag); if (capabilities != IntPtr.Zero) Marshal.FreeHGlobal(capabilities); if (container != IntPtr.Zero) Marshal.FreeHGlobal(container); }
        }
    }
    private sealed class Pipe : IDisposable
    {
        internal SafeFileHandle Read { get; }
        internal SafeFileHandle Write { get; }
        internal Pipe()
        {
            var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
            if (!CreatePipe(out var read, out var write, ref security, 0)) throw new Win32Exception();
            Read = read; Write = write;
            if (!SetHandleInformation(Read, 1, 0)) { Dispose(); throw new Win32Exception(); }
        }
        public void Dispose() { Read.Dispose(); Write.Dispose(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityCapabilities { public IntPtr Sid, Capabilities; public uint Count, Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfo
    {
        public int Size; public IntPtr Reserved, Desktop, Title; public uint X, Y, Width, Height, CharsX, CharsY, Fill, Flags;
        public ushort Show, ReservedBytes; public IntPtr ReservedPointer, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo Info; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, ref SecurityAttributes security, uint disposition, uint flags, IntPtr template);
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
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(SafeFileHandle process, uint access, out SafeFileHandle token);
    [DllImport("advapi32.dll")] private static extern bool EqualSid(IntPtr first, IntPtr second);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(SafeFileHandle token, int information, IntPtr buffer, int bytes, out int required);
}

