using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Axora.Desktop.Helpers;

/// <summary>
/// High-reliability multi-tier native Windows file and folder picker dialogs for unpackaged WinUI 3.
/// Executes directly on the WinUI 3 UI thread with WinRT Windows.Storage.Pickers + HWND initialization,
/// and automatic Win32 comdlg32/shell32 fallback.
/// </summary>
public static class NativeFilePickerHelper
{
    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static IntPtr GetActiveWindowHandle()
    {
        if (App.MainWindowHandle != IntPtr.Zero)
        {
            return App.MainWindowHandle;
        }

        try
        {
            if (App.MainAppWindow != null)
            {
                return WindowNative.GetWindowHandle(App.MainAppWindow);
            }
        }
        catch { /* fallback */ }

        try
        {
            var fg = GetForegroundWindow();
            if (fg != IntPtr.Zero) return fg;
        }
        catch { /* fallback */ }

        try
        {
            return GetActiveWindow();
        }
        catch { /* fallback */ }

        return IntPtr.Zero;
    }

    private static async Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> func)
    {
        var dq = App.MainAppWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dq != null && !dq.HasThreadAccess)
        {
            var tcs = new TaskCompletionSource<T>();
            dq.TryEnqueue(async () =>
            {
                try
                {
                    var result = await func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return await tcs.Task;
        }

        return await func();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  1. PICK FILES (OPEN)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens the native Windows File Open dialog for selecting one or more files.
    /// </summary>
    public static async Task<IReadOnlyList<string>> PickFilesAsync(
        string title = "Select Files",
        string filter = "Supported Files\0*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.webp;*.heic;*.raw;*.gif;*.svg;*.pdf;*.docx;*.pptx;*.xlsx;*.zip;*.axvault\0All Files (*.*)\0*.*\0",
        bool allowMultiple = true)
    {
        return await RunOnUiThreadAsync(async () =>
        {
            var hwnd = GetActiveWindowHandle();

            // Tier 1: Modern WinRT FileOpenPicker initialized with HWND
            try
            {
                var picker = new FileOpenPicker();
                if (hwnd != IntPtr.Zero)
                {
                    InitializeWithWindow.Initialize(picker, hwnd);
                }

                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

                var extensions = ExtractExtensionsFromFilter(filter);
                if (extensions.Count > 0)
                {
                    foreach (var ext in extensions)
                    {
                        picker.FileTypeFilter.Add(ext);
                    }
                }
                else
                {
                    picker.FileTypeFilter.Add("*");
                }

                if (allowMultiple)
                {
                    var storageFiles = await picker.PickMultipleFilesAsync();
                    if (storageFiles != null && storageFiles.Count > 0)
                    {
                        return (IReadOnlyList<string>)storageFiles.Select(f => f.Path).ToList();
                    }
                }
                else
                {
                    var storageFile = await picker.PickSingleFileAsync();
                    if (storageFile != null)
                    {
                        return (IReadOnlyList<string>)new[] { storageFile.Path };
                    }
                }

                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeFilePickerHelper] WinRT FileOpenPicker error: {ex.Message}");
            }

            // Tier 2 Fallback: Modern IFileOpenDialog on UI thread
            try
            {
                var comRes = ShowComFileOpenDialog(title, filter, allowMultiple, hwnd);
                if (comRes != null && comRes.Count > 0) return comRes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeFilePickerHelper] IFileOpenDialog error: {ex.Message}");
            }

            // Tier 3 Fallback: Win32 comdlg32 GetOpenFileName
            try
            {
                return ShowLegacyOpenFileDialog(title, filter, allowMultiple, hwnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeFilePickerHelper] GetOpenFileName error: {ex.Message}");
            }

            return Array.Empty<string>();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  2. PICK FOLDER
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens the native Windows Folder Picker dialog for selecting a directory.
    /// </summary>
    public static async Task<string?> PickFolderAsync(string title = "Select Folder")
    {
        return await RunOnUiThreadAsync(async () =>
        {
            var hwnd = GetActiveWindowHandle();

            // Tier 1: Modern WinRT FolderPicker initialized with HWND
            try
            {
                var picker = new FolderPicker();
                if (hwnd != IntPtr.Zero)
                {
                    InitializeWithWindow.Initialize(picker, hwnd);
                }

                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    return folder.Path;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeFilePickerHelper] WinRT FolderPicker error: {ex.Message}");
            }

            // Tier 2 Fallback: Modern IFileOpenDialog with FOS_PICKFOLDERS on UI thread
            try
            {
                var comPath = ShowComFolderDialog(title, hwnd);
                if (!string.IsNullOrEmpty(comPath)) return comPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeFilePickerHelper] IFileOpenDialog folder picker error: {ex.Message}");
            }

            // Tier 3 Fallback: SHBrowseForFolderW
            try
            {
                return ShowLegacyBrowseForFolder(title, hwnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeFilePickerHelper] SHBrowseForFolder error: {ex.Message}");
            }

            return null;
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  3. PICK SAVE FILE
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens the native Windows Save File dialog for choosing a save destination.
    /// </summary>
    public static async Task<string?> PickSaveFileAsync(
        string title = "Save File As",
        string defaultExt = "pdf",
        string filter = "PDF Document (*.pdf)\0*.pdf\0All Files (*.*)\0*.*\0",
        string suggestedFileName = "Document")
    {
        return await RunOnUiThreadAsync(async () =>
        {
            var hwnd = GetActiveWindowHandle();
            string cleanExt = "." + defaultExt.TrimStart('.');

            // Tier 1: Modern WinRT FileSavePicker initialized with HWND
            try
            {
                var picker = new FileSavePicker();
                if (hwnd != IntPtr.Zero)
                {
                    InitializeWithWindow.Initialize(picker, hwnd);
                }

                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.SuggestedFileName = suggestedFileName;
                picker.DefaultFileExtension = cleanExt;

                var choices = ExtractChoiceFromFilter(filter, cleanExt);
                foreach (var kvp in choices)
                {
                    picker.FileTypeChoices.Add(kvp.Key, kvp.Value);
                }

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    return file.Path;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeFilePickerHelper] WinRT FileSavePicker error: {ex.Message}");
            }

            // Tier 2 Fallback: Modern IFileSaveDialog on UI thread
            try
            {
                var comPath = ShowComSaveFileDialog(title, defaultExt.TrimStart('.'), filter, suggestedFileName, hwnd);
                if (!string.IsNullOrEmpty(comPath)) return comPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeFilePickerHelper] IFileSaveDialog error: {ex.Message}");
            }

            // Tier 3 Fallback: comdlg32 GetSaveFileName
            try
            {
                return ShowLegacySaveFileDialog(title, defaultExt.TrimStart('.'), filter, suggestedFileName, hwnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeFilePickerHelper] GetSaveFileName error: {ex.Message}");
            }

            return null;
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FILTER PARSERS FOR WINRT PICKERS
    // ══════════════════════════════════════════════════════════════════════════

    private static List<string> ExtractExtensionsFromFilter(string filter)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(filter)) return result;

        char delimiter = filter.Contains('\0') ? '\0' : '|';
        var tokens = filter.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < tokens.Length; i += 2)
        {
            var specs = tokens[i].Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var spec in specs)
            {
                var clean = spec.Trim();
                if (clean == "*.*" || clean == "*")
                {
                    if (!result.Contains("*")) result.Add("*");
                }
                else if (clean.StartsWith("*."))
                {
                    var ext = clean[1..];
                    if (!result.Contains(ext)) result.Add(ext);
                }
                else if (!clean.StartsWith("."))
                {
                    var ext = "." + clean;
                    if (!result.Contains(ext)) result.Add(ext);
                }
                else
                {
                    if (!result.Contains(clean)) result.Add(clean);
                }
            }
        }

        var distinct = result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        
        // WinRT does not allow '*' mixed with specific extensions.
        // If we have specific extensions, we should remove '*' to enforce the specific filter.
        if (distinct.Count > 1 && distinct.Contains("*"))
        {
            distinct.Remove("*");
        }

        return distinct;
    }

    private static Dictionary<string, List<string>> ExtractChoiceFromFilter(string filter, string defaultExt)
    {
        var choices = new Dictionary<string, List<string>>();
        if (string.IsNullOrWhiteSpace(filter))
        {
            choices.Add("Document", new List<string> { defaultExt });
            return choices;
        }

        char delimiter = filter.Contains('\0') ? '\0' : '|';
        var tokens = filter.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            string label = tokens[i].Trim();
            var specs = tokens[i + 1].Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var exts = new List<string>();

            foreach (var spec in specs)
            {
                var clean = spec.Trim();
                if (clean == "*.*" || clean == "*") continue; // FileSavePicker does NOT support * or .*
                else if (clean.StartsWith("*.")) exts.Add(clean[1..]);
                else if (!clean.StartsWith(".")) exts.Add("." + clean);
                else exts.Add(clean);
            }

            if (exts.Count > 0 && !choices.ContainsKey(label))
            {
                choices.Add(label, exts);
            }
        }

        if (choices.Count == 0)
        {
            choices.Add("Document", new List<string> { defaultExt });
        }

        return choices;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  COM DIALOG FALLBACKS (UI THREAD)
    // ══════════════════════════════════════════════════════════════════════════

    private static List<string> ShowComFileOpenDialog(string title, string filter, bool allowMultiple, IntPtr hwnd)
    {
        var results = new List<string>();
        var dialog = (IFileOpenDialog)new FileOpenDialogClass();
        try
        {
            dialog.SetTitle(title);
            var options = FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM | FILEOPENDIALOGOPTIONS.FOS_FILEMUSTEXIST;
            if (allowMultiple) options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;
            dialog.SetOptions(options);

            var filterSpecs = ParseFilterString(filter);
            if (filterSpecs.Length > 0)
            {
                dialog.SetFileTypes((uint)filterSpecs.Length, filterSpecs);
                dialog.SetFileTypeIndex(1);
            }

            int hr = dialog.Show(hwnd);
            if (hr == 0) // S_OK
            {
                if (allowMultiple)
                {
                    dialog.GetResults(out var itemArray);
                    if (itemArray != null)
                    {
                        try
                        {
                            itemArray.GetCount(out uint count);
                            for (uint i = 0; i < count; i++)
                            {
                                itemArray.GetItemAt(i, out var item);
                                if (item != null)
                                {
                                    try
                                    {
                                        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path);
                                        if (!string.IsNullOrEmpty(path)) results.Add(path);
                                    }
                                    finally { Marshal.ReleaseComObject(item); }
                                }
                            }
                        }
                        finally { Marshal.ReleaseComObject(itemArray); }
                    }
                }
                else
                {
                    dialog.GetResult(out var item);
                    if (item != null)
                    {
                        try
                        {
                            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path);
                            if (!string.IsNullOrEmpty(path)) results.Add(path);
                        }
                        finally { Marshal.ReleaseComObject(item); }
                    }
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
        return results;
    }

    private static string? ShowComFolderDialog(string title, IntPtr hwnd)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogClass();
        try
        {
            dialog.SetTitle(title);
            dialog.SetOptions(
                FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS |
                FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM |
                FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST);

            int hr = dialog.Show(hwnd);
            if (hr == 0) // S_OK
            {
                dialog.GetResult(out var item);
                if (item != null)
                {
                    try
                    {
                        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path);
                        if (!string.IsNullOrEmpty(path)) return path;
                    }
                    finally { Marshal.ReleaseComObject(item); }
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
        return null;
    }

    private static string? ShowComSaveFileDialog(string title, string defaultExt, string filter, string suggestedFileName, IntPtr hwnd)
    {
        var dialog = (IFileSaveDialog)new FileSaveDialogClass();
        try
        {
            dialog.SetTitle(title);
            dialog.SetDefaultExtension(defaultExt);
            dialog.SetFileName(suggestedFileName);
            dialog.SetOptions(
                FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM |
                FILEOPENDIALOGOPTIONS.FOS_OVERWRITEPROMPT |
                FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST);

            var filterSpecs = ParseFilterString(filter);
            if (filterSpecs.Length > 0)
            {
                dialog.SetFileTypes((uint)filterSpecs.Length, filterSpecs);
                dialog.SetFileTypeIndex(1);
            }

            int hr = dialog.Show(hwnd);
            if (hr == 0) // S_OK
            {
                dialog.GetResult(out var item);
                if (item != null)
                {
                    try
                    {
                        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path);
                        if (!string.IsNullOrEmpty(path)) return path;
                    }
                    finally { Marshal.ReleaseComObject(item); }
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
        return null;
    }

    private static COMDLG_FILTERSPEC[] ParseFilterString(string filter)
    {
        var specs = new List<COMDLG_FILTERSPEC>();
        if (string.IsNullOrWhiteSpace(filter)) return specs.ToArray();

        char delimiter = filter.Contains('\0') ? '\0' : '|';
        var tokens = filter.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            specs.Add(new COMDLG_FILTERSPEC
            {
                pszName = tokens[i].Trim(),
                pszSpec = tokens[i + 1].Trim()
            });
        }

        if (specs.Count == 0 && tokens.Length > 0)
        {
            specs.Add(new COMDLG_FILTERSPEC
            {
                pszName = "Supported Files",
                pszSpec = tokens[0].Trim()
            });
        }

        return specs.ToArray();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LEGACY WIN32 COMDLG32 / SHELL32 FALLBACKS
    // ══════════════════════════════════════════════════════════════════════════

    private static List<string> ShowLegacyOpenFileDialog(string title, string filter, bool allowMultiple, IntPtr hwnd)
    {
        var results = new List<string>();
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = hwnd;

        string cleanFilter = filter.Replace('|', '\0').TrimEnd('\0') + "\0\0";
        ofn.lpstrFilter = cleanFilter;
        ofn.nFilterIndex = 1;
        // Use StringBuilder so Win32 can write the selected path back into the buffer
        var fileBuffer = new StringBuilder(65536);
        ofn.lpstrFile = fileBuffer;
        ofn.nMaxFile = fileBuffer.Capacity;
        ofn.lpstrTitle = title;
        ofn.Flags = 0x00080000 /*OFN_EXPLORER*/ | 0x00001000 /*OFN_FILEMUSTEXIST*/ | 0x00000800 /*OFN_PATHMUSTEXIST*/;
        if (allowMultiple) ofn.Flags |= 0x00000200 /*OFN_ALLOWMULTISELECT*/;

        if (GetOpenFileName(ref ofn))
        {
            // In multi-select mode, Win32 returns: "directory\0file1\0file2\0\0"
            // In single-select mode: "fullpath\0\0"
            string raw = fileBuffer.ToString();
            var parts = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                results.Add(parts[0]);
            }
            else if (parts.Length > 1)
            {
                string dir = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    results.Add(Path.Combine(dir, parts[i]));
                }
            }
        }
        return results;
    }

    private static string? ShowLegacySaveFileDialog(string title, string defaultExt, string filter, string suggestedFileName, IntPtr hwnd)
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = hwnd;

        string cleanFilter = filter.Replace('|', '\0').TrimEnd('\0') + "\0\0";
        ofn.lpstrFilter = cleanFilter;
        ofn.nFilterIndex = 1;

        // Use StringBuilder so Win32 can write the chosen save path back into the buffer
        var fileBuffer = new StringBuilder(65536);
        if (!string.IsNullOrEmpty(suggestedFileName))
        {
            fileBuffer.Append(suggestedFileName);
        }
        ofn.lpstrFile = fileBuffer;
        ofn.nMaxFile = fileBuffer.Capacity;
        ofn.lpstrTitle = title;
        ofn.lpstrDefExt = defaultExt;
        ofn.Flags = 0x00080000 /*OFN_EXPLORER*/ | 0x00000002 /*OFN_OVERWRITEPROMPT*/ | 0x00000004 /*OFN_HIDEREADONLY*/ | 0x00000800 /*OFN_PATHMUSTEXIST*/;

        if (GetSaveFileName(ref ofn))
        {
            string raw = fileBuffer.ToString();
            var parts = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                return parts[0];
            }
        }
        return null;
    }

    private static string? ShowLegacyBrowseForFolder(string title, IntPtr hwnd)
    {
        var bi = new BROWSEINFO
        {
            hwndOwner = hwnd,
            lpszTitle = title,
            ulFlags = 0x00000040 /* BIF_NEWDIALOGSTYLE */ | 0x00000001 /* BIF_RETURNONLYFSDIRS */
        };

        IntPtr pidl = SHBrowseForFolder(ref bi);
        if (pidl != IntPtr.Zero)
        {
            try
            {
                var pathBuf = new char[260];
                if (SHGetPathFromIDList(pidl, pathBuf))
                {
                    string path = new string(pathBuf).TrimEnd('\0');
                    if (!string.IsNullOrEmpty(path)) return path;
                }
            }
            finally
            {
                CoTaskMemFree(pidl);
            }
        }
        return null;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetSaveFileName([In, Out] ref OPENFILENAME ofn);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, [Out] char[] pszPath);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        // StringBuilder is required so Win32 can write the selected path back into the buffer.
        // An immutable string would silently fail — the result would always be empty.
        public StringBuilder lpstrFile;
        public int nMaxFile;
        public StringBuilder? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public string pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    // ── COM Interop Definitions ───────────────────────────────────────────────

    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    [ClassInterface(ClassInterfaceType.None)]
    private class FileOpenDialogClass { }

    [ComImport]
    [Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    [ClassInterface(ClassInterfaceType.None)]
    private class FileSaveDialogClass { }

    [ComImport]
    [Guid("d57c7279-a4a6-476e-aecc-da5302951547")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(FILEOPENDIALOGOPTIONS fos);
        void GetOptions(out FILEOPENDIALOGOPTIONS pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid([In] ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        void GetResults(out IShellItemArray ppenum);
        void GetSelectedItems(out IShellItemArray ppsai);
    }

    [ComImport]
    [Guid("84bccd23-21de-4cd0-8023-bd143de9f146")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileSaveDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(FILEOPENDIALOGOPTIONS fos);
        void GetOptions(out FILEOPENDIALOGOPTIONS pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid([In] ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        void SetSaveAsItem(IShellItem psi);
        void SetProperties(IntPtr pStore);
        void SetCollectedProperties(IntPtr pList, int fAppendDefault);
        void GetProperties(out IntPtr ppStore);
        void ApplyProperties(IShellItem psi, IntPtr pStore, IntPtr hwnd, IntPtr pSink);
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport]
    [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler(IntPtr pbc, [In] ref Guid rbhid, [In] ref Guid riid, out IntPtr ppvOut);
        void GetPropertyStore(int flags, [In] ref Guid riid, out IntPtr ppv);
        void GetPropertyDescriptionList([In] ref Guid keyType, [In] ref Guid riid, out IntPtr ppv);
        void GetAttributes(int AttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
        void GetCount(out uint pdwNumItems);
        void GetItemAt(uint dwIndex, out IShellItem ppsi);
        void EnumItems(out IntPtr ppenumShellItems);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pszSpec;
    }

    [Flags]
    private enum FILEOPENDIALOGOPTIONS : uint
    {
        FOS_OVERWRITEPROMPT = 0x2,
        FOS_STRICTFILETYPES = 0x4,
        FOS_NOCHANGEDIR = 0x8,
        FOS_PICKFOLDERS = 0x20,
        FOS_FORCEFILESYSTEM = 0x40,
        FOS_ALLNONSTORAGEITEMS = 0x80,
        FOS_NOVALIDATE = 0x100,
        FOS_ALLOWMULTISELECT = 0x200,
        FOS_PATHMUSTEXIST = 0x800,
        FOS_FILEMUSTEXIST = 0x1000,
        FOS_CREATEPROMPT = 0x2000,
        FOS_SHAREAWARE = 0x4000,
        FOS_NOREADONLYRETURN = 0x8000,
        FOS_NOTESTFILECREATE = 0x10000,
        FOS_HIDEMRUPLACES = 0x20000,
        FOS_HIDEPINNEDPLACES = 0x40000,
        FOS_NODEREFERENCELINKS = 0x100000,
        FOS_OKBUTTONNEEDSINTERACTION = 0x200000,
        FOS_DONTADDTORECENT = 0x2000000,
        FOS_FORCESHOWHIDDEN = 0x10000000,
        FOS_DEFAULTNOMINIMODE = 0x20000000,
        FOS_FORCEPREVIEWPANEON = 0x40000000,
        FOS_SUPPORTSTREAMABLEITEMS = 0x80000000
    }

    private enum SIGDN : uint
    {
        SIGDN_NORMALDISPLAY = 0,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001,
        SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
        SIGDN_PARENTRELATIVEEDITING = 0x80031001,
        SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
        SIGDN_FILESYSPATH = 0x80058000,
        SIGDN_URL = 0x80068000,
        SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
        SIGDN_PARENTRELATIVE = 0x80080001
    }
}
