using System.ComponentModel;
using System.Runtime.InteropServices;
using Axora.Studio.Models.Rendering;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Services.Rendering;

internal sealed class WindowsRendererJob : IDisposable
{
    internal SafeFileHandle Handle { get; }
    internal WindowsRendererJob()
    {
        Handle = CreateJobObjectW(IntPtr.Zero, null);
        if (Handle.IsInvalid) throw new Win32Exception();
        var information = new ExtendedLimits { Basic = new() { Flags = 0x2000 | 0x200 | 0x8, ActiveProcessLimit = 4 }, JobMemoryLimit = (nuint)RendererLimits.JobMemory };
        if (!SetInformationJobObject(Handle, 9, ref information, Marshal.SizeOf<ExtendedLimits>())) { Handle.Dispose(); throw new Win32Exception(); }
    }
    internal void Assign(SafeFileHandle process)
    {
        if (!AssignProcessToJobObject(Handle, process) || !IsProcessInJob(process, Handle, out bool member) || !member)
            throw new Win32Exception();
    }
    internal void Terminate()
    {
        if (!TerminateJobObject(Handle, 0xA201)) throw new Win32Exception();
    }
    internal (uint Active, uint Total, ulong Peak) Observe()
    {
        if (!QueryAccounting(Handle, 1, out var accounting, Marshal.SizeOf<Accounting>(), IntPtr.Zero)
            || !QueryLimits(Handle, 9, out var limits, Marshal.SizeOf<ExtendedLimits>(), IntPtr.Zero)) throw new Win32Exception();
        return (accounting.ActiveProcesses, accounting.TotalProcesses, (ulong)limits.PeakJobMemoryUsed);
    }
    public void Dispose() => Handle.Dispose(); // Last handle closes the owned job; never inherited by children.
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {
        public long ProcessTime, JobTime; public uint Flags; public nuint MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcessLimit; public nuint Affinity; public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    {
        public BasicLimits Basic; public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Accounting
    {
        public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime; public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObjectW(IntPtr security, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref ExtendedLimits info, int bytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeFileHandle process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool IsProcessInJob(SafeFileHandle process, SafeFileHandle job, out bool member);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
    [DllImport("kernel32.dll", EntryPoint = "QueryInformationJobObject", SetLastError = true)] private static extern bool QueryAccounting(SafeFileHandle job, int kind, out Accounting info, int bytes, IntPtr returned);
    [DllImport("kernel32.dll", EntryPoint = "QueryInformationJobObject", SetLastError = true)] private static extern bool QueryLimits(SafeFileHandle job, int kind, out ExtendedLimits info, int bytes, IntPtr returned);
}
