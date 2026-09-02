using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Axora.Desktop.Models;

/// <summary>
/// Point-in-time snapshot of live Windows OS hardware telemetry (CPU, Physical RAM, Storage, P2P Sockets).
/// Uses Win32 GlobalMemoryStatusEx and GetSystemTimes to match Windows Task Manager exactly.
/// </summary>
public sealed class SystemTelemetry
{
    /// <summary>System-wide CPU utilisation percentage [0–100].</summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>Formatted CPU string (e.g. "24.3%").</summary>
    public string CpuFormatted => $"{CpuUsagePercent:F1}%";

    /// <summary>Physical RAM currently in use across the entire Windows OS (MB).</summary>
    public double RamUsedMb { get; set; }

    /// <summary>Total installed physical RAM (MB).</summary>
    public double RamTotalMb { get; set; }

    /// <summary>RAM usage percentage [0–100].</summary>
    public double RamUsagePercent => RamTotalMb > 0 ? (RamUsedMb / RamTotalMb) * 100.0 : 0;

    /// <summary>Formatted RAM usage percentage (e.g. "83.1%").</summary>
    public string RamUsageFormatted => $"{RamUsagePercent:F1}%";

    /// <summary>Formatted RAM details (e.g. "13.0 / 15.7 GB").</summary>
    public string RamDetailsFormatted => $"{RamUsedMb / 1024.0:F1} / {RamTotalMb / 1024.0:F1} GB";

    /// <summary>Primary system drive used space (GB).</summary>
    public double StorageUsedGb { get; set; }

    /// <summary>Primary system drive total capacity (GB).</summary>
    public double StorageTotalGb { get; set; }

    /// <summary>Storage usage percentage [0–100].</summary>
    public double StorageUsagePercent => StorageTotalGb > 0 ? (StorageUsedGb / StorageTotalGb) * 100.0 : 0;

    /// <summary>Formatted Storage percentage (e.g. "87.4%").</summary>
    public string StorageUsageFormatted => $"{StorageUsagePercent:F1}%";

    /// <summary>Formatted Storage details (e.g. "334.8 / 382.9 GB").</summary>
    public string StorageDetailsFormatted => $"{StorageUsedGb:F1} / {StorageTotalGb:F1} GB";

    /// <summary>Number of currently active P2P connections.</summary>
    public int ActiveConnections { get; set; }

    /// <summary>UTC timestamp of this telemetry snapshot.</summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    private static long _prevIdleTime;
    private static long _prevSystemTime;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

    /// <summary>
    /// Reads live OS-level hardware telemetry matching Windows Task Manager.
    /// </summary>
    public static SystemTelemetry Capture(int activeConnections = 0)
    {
        // 1. Real Physical RAM via Win32 GlobalMemoryStatusEx
        double totalRamMb = 16384.0;
        double usedRamMb = 8192.0;

        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref memStatus))
        {
            totalRamMb = memStatus.ullTotalPhys / (1024.0 * 1024.0);
            var availRamMb = memStatus.ullAvailPhys / (1024.0 * 1024.0);
            usedRamMb = Math.Max(0, totalRamMb - availRamMb);
        }

        // 2. Real Overall System CPU Utilization via Win32 GetSystemTimes
        double cpuPercent = 0.0;
        if (GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
        {
            long systemTime = kernelTime + userTime;
            if (_prevSystemTime > 0)
            {
                long sysDelta = systemTime - _prevSystemTime;
                long idleDelta = idleTime - _prevIdleTime;
                if (sysDelta > 0)
                {
                    double busy = 1.0 - ((double)idleDelta / sysDelta);
                    cpuPercent = Math.Clamp(busy * 100.0, 0.0, 100.0);
                }
            }
            _prevIdleTime = idleTime;
            _prevSystemTime = systemTime;
        }

        // 3. Real Storage Capacity & Used Space
        var driveInfo = DriveInfo.GetDrives()
            .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed && d.RootDirectory.FullName.StartsWith("C", StringComparison.OrdinalIgnoreCase))
            ?? DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);

        var totalStorageGb = driveInfo != null ? driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0) : 512.0;
        var freeStorageGb = driveInfo != null ? driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0) : 178.0;
        var usedStorageGb = Math.Max(0, totalStorageGb - freeStorageGb);

        return new SystemTelemetry
        {
            CpuUsagePercent = cpuPercent,
            RamUsedMb = usedRamMb,
            RamTotalMb = totalRamMb,
            StorageUsedGb = usedStorageGb,
            StorageTotalGb = totalStorageGb,
            ActiveConnections = activeConnections
        };
    }
}
