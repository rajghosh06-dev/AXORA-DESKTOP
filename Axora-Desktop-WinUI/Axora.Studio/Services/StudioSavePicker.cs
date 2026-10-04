using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using Axora.Studio.Models;
using Axora.Studio.Services.Contracts;
using Microsoft.UI.Dispatching;

namespace Axora.Studio.Services;

/// <summary>Operation-scoped COM and owner-bound consent. All native methods run on the captured UI STA.</summary>
public sealed class StudioSavePicker : IStudioSavePicker
{
    public const int ErrorCanceled = unchecked((int)0x800704C7);
    public const uint RequiredFlags = 0x40 | 0x10000 | 0x800 | 0x8 | 0x4 | 0x2000000;
    public sealed record DialogSettings(nint Owner, uint Flags, string FilterName, string Pattern, string Extension, string SuggestedName);
    // Narrow adapter seams; neither replaces publication nor provides file I/O.
    public interface IStaDispatcher { bool HasThreadAccess { get; } bool Post(Action action); }
    public interface IDialog : IDisposable { int Show(nint owner); string GetPath(); void Close(); }
    private readonly IStaDispatcher _dispatcher;
    private readonly Func<DialogSettings, IDialog> _create;
    private readonly Func<nint, Guid, ExportDestinationPlan, IDialog> _confirm;
    private readonly Func<nint, bool> _validOwner;
    private readonly object _gate = new();
    private IDialog? _active;
    private bool _pending;
    private readonly Action<string>? _diagnostic;

    public StudioSavePicker(Action<string>? diagnostic = null) : this(new UiStaDispatcher(), s => new NativeSaveDialog(s),
        (owner, id, plan) => new NativeConfirmation(owner, id, plan), IsWindow) { _diagnostic = diagnostic; }
    public StudioSavePicker(IStaDispatcher dispatcher, Func<DialogSettings, IDialog> create,
        Func<nint, Guid, ExportDestinationPlan, IDialog> confirm, Func<nint, bool> validOwner)
    { (_dispatcher, _create, _confirm, _validOwner) = (dispatcher, create, confirm, validOwner); }

    public static DialogSettings Settings(nint owner, FlashcardExportFormat format, string suggestedName)
    {
        string extension = ExportFileNameSanitizer.Extension(format);
        return new(owner, RequiredFlags, format switch { FlashcardExportFormat.Csv => "CSV (*.csv)", FlashcardExportFormat.AnkiText => "Anki-format text (*.txt)", _ => "AXORA JSON (*.json)" },
            "*" + extension, extension[1..], ExportFileNameSanitizer.Suggest(suggestedName, format));
    }
    public Task<StudioPickerResult> SelectAsync(nint owner, FlashcardExportFormat format, string suggestedName, CancellationToken token) =>
        Dispatch(owner, () => _create(Settings(owner, format, suggestedName)), extractPath: true, token);
    public async Task<StudioPickerState> ConfirmReplacementAsync(nint owner, Guid operationId, ExportDestinationPlan plan, CancellationToken token)
    {
        if (operationId == Guid.Empty || plan.Existing is null || plan.ReplacementApproved) return StudioPickerState.Failed;
        return (await Dispatch(owner, () => _confirm(owner, operationId, plan), extractPath: false, token).ConfigureAwait(false)).State;
    }
    private Task<StudioPickerResult> Dispatch(nint owner, Func<IDialog> create, bool extractPath, CancellationToken token)
    {
        var completion = new TaskCompletionSource<StudioPickerResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_pending) return Task.FromResult(new StudioPickerResult(StudioPickerState.Failed, ReasonCode: "PickerBusy"));
            _pending = true;
        }
        if (!_dispatcher.Post(() =>
        {
            StudioPickerResult result = new(StudioPickerState.Failed, ReasonCode: "UnsettledPicker");
            IDialog? dialog = null;
            CancellationTokenRegistration registration = default;
            try
            {
                if (!_dispatcher.HasThreadAccess) throw new InvalidOperationException("Wrong picker apartment");
                if (owner == 0 || !_validOwner(owner)) throw new ArgumentException("Invalid owner");
                token.ThrowIfCancellationRequested();
                dialog = create();
                lock (_gate) _active = dialog;
                registration = token.Register(RequestCancel);
                token.ThrowIfCancellationRequested();
                int hr = dialog.Show(owner);
                if (hr == ErrorCanceled || token.IsCancellationRequested) result = new(StudioPickerState.Canceled);
                else if (hr < 0) result = new(StudioPickerState.Failed, ReasonCode: "DialogHRESULT:" + hr);
                else if (extractPath)
                {
                    string path = dialog.GetPath();
                    result = string.IsNullOrWhiteSpace(path) ? new(StudioPickerState.Failed, ReasonCode: "MissingPath") : new(StudioPickerState.Selected, path);
                }
                else result = new(StudioPickerState.Selected);
            }
            catch (OperationCanceledException) { result = new(StudioPickerState.Canceled); }
            catch (Exception ex) { result = new(StudioPickerState.Failed, ReasonCode: "PickerFailure:" + ex.GetType().Name + ":" + ex.HResult + ":" + ex.Data["PickerPhase"]); }
            finally
            {
                registration.Dispose();
                lock (_gate) _active = null;
                try { dialog?.Dispose(); }
                catch (Exception ex) { result = new(StudioPickerState.Failed, ReasonCode: "PickerReleaseFailed:" + ex.GetType().Name); }
                lock (_gate) _pending = false;
            }
            try { _diagnostic?.Invoke("Picker terminal; state=" + result.State + "; reason=" + result.ReasonCode); } catch (Exception) { }
            completion.TrySetResult(result);
        }))
        {
            lock (_gate) _pending = false;
            completion.TrySetResult(new(StudioPickerState.Failed, ReasonCode: "DispatcherUnavailable"));
        }
        return completion.Task;
    }
    public void RequestCancel()
    {
        IDialog? dialog; lock (_gate) dialog = _active;
        if (dialog is null) return;
        try { _diagnostic?.Invoke("Picker cancel requested; owningSTA=" + _dispatcher.HasThreadAccess); } catch (Exception) { }
        void CloseOnSta()
        {
            lock (_gate) if (!ReferenceEquals(_active, dialog)) return;
            try
            {
                try { _diagnostic?.Invoke("Picker native Close entered on owning STA"); } catch (Exception) { }
                dialog.Close();
                try { _diagnostic?.Invoke("Picker native Close returned on owning STA"); } catch (Exception) { }
            }
            catch (Exception) { /* Task remains owned until Show returns and release completes. */ }
        }
        // Show runs synchronously inside a DispatcherQueue callback. Its native modal loop can
        // deliver the window close event without draining another callback on that same queue.
        // Already on the owning STA: close directly so shutdown does not wait behind Show.
        if (_dispatcher.HasThreadAccess) CloseOnSta();
        else _dispatcher.Post(CloseOnSta);
    }
    private sealed class UiStaDispatcher : IStaDispatcher
    {
        private readonly DispatcherQueue _queue;
        public UiStaDispatcher()
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("Save As requires UI STA");
            _queue = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("No UI dispatcher");
        }
        public bool HasThreadAccess => _queue.HasThreadAccess && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA;
        public bool Post(Action action) => _queue.TryEnqueue(() => action());
    }
    private sealed class NativeSaveDialog : IDialog
    {
        // Own the CoCreateInstance IFileSaveDialog reference directly; class GUID and offsets follow
        // the Windows SDK FileSaveDialog coclass and IFileSaveDialog declarations.
        private nint _dialog;
        private nint _owner;
        private bool _closeRequested;
        public NativeSaveDialog(DialogSettings settings)
        {
            string phase = "CoCreateInstance";
            try
            {
                Guid clsid = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B"), iid = new("84BCCD23-5FDE-4CDB-AEA4-AF64B83D78AB");
                Marshal.ThrowExceptionForHR(CoCreateInstance(in clsid, 0, 1, in iid, out _dialog));
                phase = "SetOptions";
                Marshal.ThrowExceptionForHR(Method<UIntMethod>(_dialog, 9)(_dialog, settings.Flags));
                nint filter = Marshal.AllocHGlobal(Marshal.SizeOf<FilterSpec>());
                phase = "SetFileTypes";
                try
                {
                    Marshal.StructureToPtr(new FilterSpec { Name = settings.FilterName, Pattern = settings.Pattern }, filter, false);
                    Marshal.ThrowExceptionForHR(Method<FilterMethod>(_dialog, 4)(_dialog, 1, filter));
                }
                finally { Marshal.DestroyStructure<FilterSpec>(filter); Marshal.FreeHGlobal(filter); }
                Marshal.ThrowExceptionForHR(Method<UIntMethod>(_dialog, 5)(_dialog, 1));
                phase = "SetDefaultExtension";
                Marshal.ThrowExceptionForHR(Method<StringMethod>(_dialog, 22)(_dialog, settings.Extension));
                Marshal.ThrowExceptionForHR(Method<StringMethod>(_dialog, 15)(_dialog, settings.SuggestedName));
                phase = "SetTitle";
                Marshal.ThrowExceptionForHR(Method<StringMethod>(_dialog, 17)(_dialog, "Save Flashcards"));
            }
            catch (Exception ex) { ex.Data["PickerPhase"] = phase; Dispose(); throw; }
        }
        public int Show(nint owner) { _owner = owner; return Method<ShowMethod>(_dialog, 3)(_dialog, owner); }
        public string GetPath()
        {
            nint item = 0, text = 0;
            try
            {
                Marshal.ThrowExceptionForHR(Method<ResultMethod>(_dialog, 20)(_dialog, out item));
                Marshal.ThrowExceptionForHR(Method<DisplayNameMethod>(item, 5)(item, 0x80058000, out text));
                return Marshal.PtrToStringUni(text) ?? throw new InvalidOperationException("Empty filesystem path");
            }
            finally { if (text != 0) Marshal.FreeCoTaskMem(text); if (item != 0) Marshal.Release(item); }
        }
        public void Close()
        {
            if (_dialog == 0 || _closeRequested) return;
            _closeRequested = true;
            nint window = 0, oleWindow = 0;
            try
            {
                // Obtain this exact active dialog's HWND; never enumerate or close a guessed window.
                Guid iid = new("00000114-0000-0000-C000-000000000046"); // IOleWindow
                if (Marshal.QueryInterface(_dialog, in iid, out oleWindow) >= 0 && oleWindow != 0
                    && Method<ResultMethod>(oleWindow, 3)(oleWindow, out nint candidate) >= 0)
                    window = candidate;
            }
            finally { if (oleWindow != 0) Marshal.Release(oleWindow); }
            try { Marshal.ThrowExceptionForHR(Method<CloseMethod>(_dialog, 23)(_dialog, ErrorCanceled)); }
            finally
            {
                // The native Close call can return during the owner's canceled close event while
                // Save As remains visible. Queue its ordinary cancel on the same still-live dialog.
                // Show/release must still finish before the picker task can settle.
                if (window != 0 && IsWindow(window) && GetWindow(window, 4) == _owner)
                    PostMessage(window, 0x10, 0, 0);
            }
        }
        public void Dispose() { nint dialog = _dialog; _dialog = 0; _owner = 0; if (dialog != 0) Marshal.Release(dialog); }
        private static T Method<T>(nint instance, int slot) where T : Delegate =>
            Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int ShowMethod(nint self, nint owner);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int UIntMethod(nint self, uint value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int FilterMethod(nint self, uint count, nint filters);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)] private delegate int StringMethod(nint self, [MarshalAs(UnmanagedType.LPWStr)] string value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int ResultMethod(nint self, out nint item);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int DisplayNameMethod(nint self, uint format, out nint text);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int CloseMethod(nint self, int hr);
        [DllImport("ole32.dll")] private static extern int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);
    }
    private sealed class NativeConfirmation : IDialog
    {
        private readonly nint _owner;
        private readonly Guid _operationId;
        private readonly ExportDestinationPlan _plan;
        // Dormant, instance-scoped construction seams. They never replace native publication or path inspection.
        private readonly Action<int, string>? _buttonBoundary;
        private readonly Func<nint, int, (int HResult, int Selected)>? _invokeDialog;
        private readonly Action<string>? _memoryEvent;
        private nint _window;
        private bool _close;
        public NativeConfirmation(nint owner, Guid operationId, ExportDestinationPlan plan,
            Action<int, string>? buttonBoundary = null,
            Func<nint, int, (int HResult, int Selected)>? invokeDialog = null,
            Action<string>? memoryEvent = null)
        {
            (_owner, _operationId, _plan) = (owner, operationId, plan);
            (_buttonBoundary, _invokeDialog, _memoryEvent) = (buttonBoundary, invokeDialog, memoryEvent);
        }
        public int Show(nint currentOwner)
        {
            if (_owner != currentOwner || _operationId == Guid.Empty || _plan.Existing is null) return unchecked((int)0x80070057);
            // This operation's exact plan is displayed and returned to its coordinator; consent is never cached.
            var callback = new TaskDialogCallback((hwnd, notification, _, _, _) =>
            { if (notification == 0) { _window = hwnd; if (_close) PostMessage(hwnd, 0x466, 101, 0); } return 0; });
            string[] labels = ["Replace", "Cancel"];
            int buttonSize = Marshal.SizeOf<TaskDialogButton>();
            if (buttonSize != checked(sizeof(int) + IntPtr.Size) || Marshal.OffsetOf<TaskDialogButton>(nameof(TaskDialogButton.Text)) != (nint)4)
                throw new InvalidOperationException("Unexpected native TaskDialog button layout");
            int byteCount = checked(buttonSize * labels.Length);
            if (byteCount <= 0) throw new InvalidOperationException("Invalid native button allocation size");
            nint[] ownedText = new nint[labels.Length];
            nint memory = Marshal.AllocHGlobal(byteCount);
            if (memory == 0) throw new OutOfMemoryException();
            Exception? primaryError = null;
            try
            {
                Record("ArrayAllocated");
                // AllocHGlobal does not clear memory. No uninitialized slot is ever interpreted by cleanup.
                Marshal.Copy(new byte[byteCount], 0, memory, byteCount);
                for (int i = 0; i < labels.Length; i++)
                {
                    _buttonBoundary?.Invoke(i, "BeforeText");
                    ownedText[i] = Marshal.StringToHGlobalUni(labels[i]);
                    if (ownedText[i] == 0) throw new OutOfMemoryException("Native consent text allocation failed");
                    Record("TextAllocated:" + i);
                    _buttonBoundary?.Invoke(i, "AfterText");
                    nint entry = IntPtr.Add(memory, checked(i * buttonSize));
                    Marshal.WriteInt32(entry, 0, i == 0 ? 100 : 101);
                    Marshal.WriteIntPtr(entry, 4, ownedText[i]);
                    Record("EntryReady:" + i);
                    _buttonBoundary?.Invoke(i, "AfterEntry");
                }
                var config = new TaskDialogConfig { Size = (uint)Marshal.SizeOf<TaskDialogConfig>(), Owner = _owner, Flags = 0x8 | 0x1000,
                    Title = "Replace exported file?", MainInstruction = "Replace this existing file?", Content = _plan.Destination + "\n\nStudio will verify that this file is still unchanged before publication.",
                    ButtonCount = 2, Buttons = memory, DefaultButton = 101, Callback = callback };
                int hr, selected;
                if (_invokeDialog is null) hr = ShowTaskDialog(ref config, out selected);
                else (hr, selected) = _invokeDialog(memory, config.DefaultButton);
                GC.KeepAlive(callback);
                return hr < 0 ? hr : selected == 100 ? 0 : ErrorCanceled;
            }
            catch (Exception ex) { primaryError = ex; throw; }
            finally
            {
                _window = 0;
                Exception? cleanupError = null;
                for (int i = 0; i < ownedText.Length; i++)
                {
                    nint text = ownedText[i];
                    if (text == 0) continue;
                    ownedText[i] = 0; // Retire this operation's ownership before invoking native free.
                    try { Marshal.FreeHGlobal(text); Record("TextFreed:" + i); }
                    catch (Exception ex) { cleanupError ??= ex; }
                }
                try { Marshal.FreeHGlobal(memory); Record("ArrayFreed"); }
                catch (Exception ex) { cleanupError ??= ex; }
                if (primaryError is null && cleanupError is not null) ExceptionDispatchInfo.Capture(cleanupError).Throw();
            }
        }
        private void Record(string value) { try { _memoryEvent?.Invoke(value); } catch (Exception) { } }
        public string GetPath() => throw new NotSupportedException();
        public void Close() { _close = true; if (_window != 0) PostMessage(_window, 0x466, 101, 0); }
        public void Dispose() { _window = 0; }
        private static int ShowTaskDialog(ref TaskDialogConfig config, out int selected)
        {
            // This unpackaged app has no v6 common-controls dependency. Use the installed Windows
            // assembly for this STA call only; never write a manifest or change the process default.
            string store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS");
            string manifest = Directory.EnumerateFiles(Path.Combine(store, "Manifests"),
                    "amd64_microsoft.windows.common-controls_6595b64144ccf1df_6.0.*.manifest")
                .Where(p => File.Exists(Path.Combine(store, Path.GetFileNameWithoutExtension(p), "comctl32.dll")))
                .OrderByDescending(p => Version.Parse(Path.GetFileName(p).Split('_')[3]))
                .FirstOrDefault() ?? throw new InvalidOperationException("Windows common controls unavailable");
            string assembly = Path.Combine(store, Path.GetFileNameWithoutExtension(manifest));
            var context = new ActivationContext { Size = (uint)Marshal.SizeOf<ActivationContext>(), Flags = 4,
                Source = manifest, AssemblyDirectory = assembly };
            nint handle = CreateActCtx(ref context);
            if (handle == -1) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            nuint cookie = 0; nint library = 0; bool activated = false;
            try
            {
                if (!ActivateActCtx(handle, out cookie)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                activated = true;
                library = LoadLibraryEx(Path.Combine(assembly, "comctl32.dll"), 0, 0x100 | 0x800);
                if (library == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                nint entry = GetProcAddress(library, "TaskDialogIndirect");
                if (entry == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                return Marshal.GetDelegateForFunctionPointer<TaskDialogMethod>(entry)(ref config, out selected, out _, out _);
            }
            finally
            {
                if (library != 0) FreeLibrary(library);
                if (activated) DeactivateActCtx(0, cookie);
                ReleaseActCtx(handle);
            }
        }
    }
    // Flattened IModalWindow / IFileDialog / IFileSaveDialog order verified against Windows SDK
    // 10.0.26100.0 um/ShObjIdl_core.h (19686 and 20026). PreserveSig only for explicit HRESULT handling.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct FilterSpec
    { [MarshalAs(UnmanagedType.LPWStr)] public string Name; [MarshalAs(UnmanagedType.LPWStr)] public string Pattern; }
    [ComImport, Guid("84BCCD23-5FDE-4CDB-AEA4-AF64B83D78AB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileSaveDialog
    {
        [PreserveSig] int Show(nint owner);
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] FilterSpec[] filters);
        void SetFileTypeIndex(uint index); void GetFileTypeIndex(out uint index);
        void Advise(nint events, out uint cookie); void Unadvise(uint cookie);
        void SetOptions(uint flags); void GetOptions(out uint flags);
        void SetDefaultFolder(IShellItem item); void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item); void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name); void GetFileName(out nint name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetResult(out IShellItem item); void AddPlace(IShellItem item, uint placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        [PreserveSig] int Close(int hr); void SetClientGuid(in Guid guid); void ClearClientData(); void SetFilter(nint filter);
        void SetSaveAsItem(IShellItem item); void SetProperties(nint store);
        void SetCollectedProperties(nint list, [MarshalAs(UnmanagedType.Bool)] bool appendDefault);
        void GetProperties(out nint store); void ApplyProperties(IShellItem item, nint store, nint owner, nint sink);
    }
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint context, in Guid handler, in Guid iid, out nint value);
        void GetParent(out IShellItem parent); void GetDisplayName(uint kind, out nint value);
        void GetAttributes(uint mask, out uint attributes); void Compare(IShellItem other, uint hint, out int order);
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int TaskDialogCallback(nint hwnd, uint notification, nuint wParam, nint lParam, nint data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)] private struct TaskDialogButton
    { public int Id; public nint Text; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)] private struct TaskDialogConfig
    {
        public uint Size; public nint Owner, Instance; public uint Flags, CommonButtons;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Title; public nint MainIcon;
        [MarshalAs(UnmanagedType.LPWStr)] public string? MainInstruction;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Content;
        public uint ButtonCount; public nint Buttons; public int DefaultButton; public uint RadioCount; public nint RadioButtons; public int DefaultRadio;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Verification, ExpandedInformation, ExpandedControlText, CollapsedControlText;
        public nint FooterIcon;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Footer;
        public TaskDialogCallback? Callback; public nint CallbackData; public uint Width;
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(nint hwnd, uint message, nuint wParam, nint lParam);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int TaskDialogMethod(ref TaskDialogConfig config, out int button, out int radio, [MarshalAs(UnmanagedType.Bool)] out bool verification);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ActivationContext
    {
        public uint Size, Flags;
        [MarshalAs(UnmanagedType.LPWStr)] public string Source;
        public ushort Architecture, Language;
        [MarshalAs(UnmanagedType.LPWStr)] public string AssemblyDirectory;
        public nint ResourceName, ApplicationName, Module;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateActCtxW")] private static extern nint CreateActCtx(ref ActivationContext context);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ActivateActCtx(nint context, out nuint cookie);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeactivateActCtx(uint flags, nuint cookie);
    [DllImport("kernel32.dll")] private static extern void ReleaseActCtx(nint context);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadLibraryExW")] private static extern nint LoadLibraryEx(string path, nint file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)] private static extern nint GetProcAddress(nint library, string name);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FreeLibrary(nint library);
}
