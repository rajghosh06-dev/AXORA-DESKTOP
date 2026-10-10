using Axora.Studio.Services.Contracts;
using System.Runtime.InteropServices;

namespace Axora.Studio.Services;

public sealed class ResumeFilePicker : IResumeFilePicker
{
    private bool _pending;
    public Task<string?> SelectImportAsync(nint owner)
    {
        if (_pending || owner == 0 || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("Import picker unavailable.");
        _pending = true;
        try
        {
            // An owned desktop dialog avoids dependence on the packaged WinRT picker broker.
            // Show runs on the owner STA and pumps its native modal loop; no worker-thread UI.
            using var dialog = new OpenDialog();
            int result = dialog.Show(owner);
            if (result == unchecked((int)0x800704C7)) return Task.FromResult<string?>(null);
            Marshal.ThrowExceptionForHR(result);
            return Task.FromResult<string?>(dialog.Path());
        }
        finally { _pending = false; }
    }
    private sealed class OpenDialog : IDisposable
    {
        private nint _instance;
        public OpenDialog()
        {
            try
            {
                Guid clsid = new("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"), iid = new("D57C7288-D4AD-4768-BE02-9D969532D960");
                Marshal.ThrowExceptionForHR(CoCreateInstance(in clsid, 0, 1, in iid, out _instance));
                // FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_FILEMUSTEXIST | FOS_DONTADDTORECENT.
                Marshal.ThrowExceptionForHR(Method<UIntMethod>(_instance, 9)(_instance, 0x02001840));
                nint filter = Marshal.AllocHGlobal(Marshal.SizeOf<Filter>());
                try
                {
                    Marshal.StructureToPtr(new Filter { Name = "Resume JSON", Pattern = "*.json" }, filter, false);
                    Marshal.ThrowExceptionForHR(Method<FilterMethod>(_instance, 4)(_instance, 1, filter));
                }
                finally { Marshal.DestroyStructure<Filter>(filter); Marshal.FreeHGlobal(filter); }
                Marshal.ThrowExceptionForHR(Method<StringMethod>(_instance, 17)(_instance, "Import Resume Copy"));
            }
            catch { Dispose(); throw; }
        }
        public int Show(nint owner) => Method<ShowMethod>(_instance, 3)(_instance, owner);
        public string Path()
        {
            nint item = 0, text = 0;
            try
            {
                Marshal.ThrowExceptionForHR(Method<ResultMethod>(_instance, 20)(_instance, out item));
                Marshal.ThrowExceptionForHR(Method<DisplayMethod>(item, 5)(item, 0x80058000, out text));
                return Marshal.PtrToStringUni(text) ?? throw new InvalidOperationException("Import selection unavailable.");
            }
            finally { if (text != 0) Marshal.FreeCoTaskMem(text); if (item != 0) Marshal.Release(item); }
        }
        public void Dispose() { nint instance = _instance; _instance = 0; if (instance != 0) Marshal.Release(instance); }
        // IModalWindow/IFileDialog prefix slots from the Windows SDK ShObjIdl_core.h.
        private static T Method<T>(nint instance, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Filter
        { [MarshalAs(UnmanagedType.LPWStr)] public string Name; [MarshalAs(UnmanagedType.LPWStr)] public string Pattern; }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int ShowMethod(nint self, nint owner);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int UIntMethod(nint self, uint value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int FilterMethod(nint self, uint count, nint filters);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)] private delegate int StringMethod(nint self, [MarshalAs(UnmanagedType.LPWStr)] string value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int ResultMethod(nint self, out nint item);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int DisplayMethod(nint self, uint format, out nint text);
        [DllImport("ole32.dll")] private static extern int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);
    }
}
