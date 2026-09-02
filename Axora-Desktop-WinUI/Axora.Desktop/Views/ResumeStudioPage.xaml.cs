using System;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Axora.Desktop.Models;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

public sealed partial class ResumeStudioPage : Page
{
    public ResumeStudioViewModel ViewModel { get; private set; } = null!;

    // Prevents re-entrancy when we programmatically set IsChecked
    private bool _suppressEvents = false;

    public ResumeStudioPage()
    {
        try
        {
            ViewModel = App.GetService<ResumeStudioViewModel>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ResumeStudioPage] CRITICAL: ViewModel initialization failed: {ex}");
        }

        InitializeComponent();
        DataContext = this;

        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
        Loaded += ResumeStudioPage_Loaded;
    }

    private DispatcherTimer? _autoSaveTimer;

    private void TriggerAutoSave()
    {
        _autoSaveTimer?.Stop();
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _autoSaveTimer.Tick += async (_, _) =>
        {
            _autoSaveTimer.Stop();
            try
            {
                if (ViewModel != null) await ViewModel.SaveToLibraryAsync();
            }
            catch { }
        };
        _autoSaveTimer.Start();
    }

    private void ResumeStudioPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Hook auto-save on any change to Document or Header
        if (ViewModel?.Document != null)
        {
            ViewModel.Document.PropertyChanged += (_, _) => TriggerAutoSave();
            ViewModel.Document.Header.PropertyChanged += (_, _) => TriggerAutoSave();
        }

        if (TxtResumeTitle != null)
        {
            TxtResumeTitle.LostFocus += async (_, _) =>
            {
                try
                {
                    if (ViewModel != null) await ViewModel.SaveToLibraryAsync();
                }
                catch { }
            };
        }

        // Defer one layout pass so all RadioButton templates are fully applied
        // before we set IsChecked — otherwise the first CheckState VSM transition is missed.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            ApplyTargetSelection();
            ApplyTabSelection();
            ApplyExportFormatSelection();   // sync export format radio from ViewModel
        });
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResumeStudioViewModel.SelectedTargetLength))
            ApplyTargetSelection();
        else if (e.PropertyName == nameof(ResumeStudioViewModel.ActiveRightTabIndex))
            ApplyTabSelection();
        else if (e.PropertyName == nameof(ResumeStudioViewModel.SelectedExportFormatIndex))
            ApplyExportFormatSelection();
    }

    // ── Target Length Dropdown (1-Page / 2-Page / 3-Page / 4+ CV) ────────────

    private void TargetLengthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || TargetLengthCombo == null) return;
        string val = TargetLengthCombo.SelectedIndex switch
        {
            0 => "1",
            1 => "2",
            2 => "3",
            3 => "4",
            _ => "2"
        };
        ViewModel.SetTargetLength(val);
    }

    private void ApplyTargetSelection()
    {
        _suppressEvents = true;
        try
        {
            var t = ViewModel.SelectedTargetLength;
            if (TargetLengthCombo != null)
            {
                TargetLengthCombo.SelectedIndex = t switch
                {
                    PageTargetLength.OnePage => 0,
                    PageTargetLength.TwoPages => 1,
                    PageTargetLength.ThreePages => 2,
                    PageTargetLength.FourPlusPages => 3,
                    _ => 1
                };
            }
        }
        finally { _suppressEvents = false; }
    }

    // ── Tabs: Content / Style / ATS ──────────────────────────────────────────

    private void TabEditor_Click(object sender, RoutedEventArgs e)     { if (!_suppressEvents) SwitchTab(0); }
    private void TabFormatting_Click(object sender, RoutedEventArgs e) { if (!_suppressEvents) SwitchTab(1); }
    private void TabAts_Click(object sender, RoutedEventArgs e)        { if (!_suppressEvents) SwitchTab(2); }

    private void SwitchTab(int tabIndex)
    {
        ViewModel.ActiveRightTabIndex = tabIndex;

        if (TabEditorScrollViewer != null)
            TabEditorScrollViewer.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (TabStyleScrollViewer != null)
            TabStyleScrollViewer.Visibility  = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        if (TabAtsScrollViewer != null)
            TabAtsScrollViewer.Visibility    = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

        ApplyTabSelection();
    }

    private void ApplyTabSelection()
    {
        _suppressEvents = true;
        try
        {
            int tab = ViewModel.ActiveRightTabIndex;
            if (TabBtnContent != null) TabBtnContent.IsChecked = (tab == 0);
            if (TabBtnStyle   != null) TabBtnStyle.IsChecked   = (tab == 1);
            if (TabBtnAts     != null) TabBtnAts.IsChecked     = (tab == 2);
        }
        finally { _suppressEvents = false; }
    }

    // ── Export Format RadioButton Sync ────────────────────────────────────────
    // Called from Loaded and from ViewModel_PropertyChanged so the radio buttons
    // always reflect the ViewModel's SelectedExportFormatIndex state.
    private void ApplyExportFormatSelection()
    {
        _suppressEvents = true;
        try
        {
            bool isPdf = ViewModel.SelectedExportFormatIndex == 0;
            if (ExportRadioPdf != null) ExportRadioPdf.IsChecked = isPdf;
            if (ExportRadioTxt != null) ExportRadioTxt.IsChecked = !isPdf;
        }
        finally { _suppressEvents = false; }
    }

    // ── Presets ───────────────────────────────────────────────────────────────
    private void PresetRishit_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadPreset("rishit-ghosh");
        ApplyTargetSelection();
        SwitchTab(0);
        ViewModel.RefreshPreview();          // force preview re-render after document swap
    }

    private void PresetSoftware_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadPreset("software-engineer");
        ApplyTargetSelection();
        SwitchTab(0);
        ViewModel.RefreshPreview();          // force preview re-render after document swap
    }

    // ── Undo / Redo / Refresh ─────────────────────────────────────────────────
    private void Undo_Click(object sender, RoutedEventArgs e)           => ViewModel.UndoCommand.Execute(null);
    private void Redo_Click(object sender, RoutedEventArgs e)           => ViewModel.RedoCommand.Execute(null);
    private void RefreshPreview_Click(object sender, RoutedEventArgs e) => ViewModel.RefreshPreviewCommand.Execute(null);

    // ── Navigation ────────────────────────────────────────────────────────────
    private async void BackToDashboard_Click(object sender, RoutedEventArgs e)
    {
        // Auto-save to library so the resume shows up in the dashboard tile list
        try { await ViewModel.SaveToLibraryAsync(); } catch { }

        if (Frame != null)
        {
            Frame.Navigate(typeof(ResumeStudioDashboardPage), null,
                new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
        }
    }

    // ── Export & Save ───────────────────────────────────────────────────────────
    private async void SaveJson_Click(object sender, RoutedEventArgs e)
    {
        try { await ViewModel.ExportJsonAsync(); }
        catch (Exception ex) { await ShowErrorDialog("Save JSON Failed", ex.Message); }
    }

    private async void ExportMain_Click(Microsoft.UI.Xaml.Controls.SplitButton sender, Microsoft.UI.Xaml.Controls.SplitButtonClickEventArgs args)
    {
        try { await ViewModel.ExportSmartAsync(); }
        catch (Exception ex) { await ShowErrorDialog("Export Failed", ex.Message); }
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedExportFormatIndex = 0;
        try { await ViewModel.ExportPdfAsync(); }
        catch (Exception ex) { await ShowErrorDialog("Export PDF Failed", ex.Message); }
    }

    private async void ExportTxt_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedExportFormatIndex = 1;
        try { await ViewModel.ExportPlainTextAsync(); }
        catch (Exception ex) { await ShowErrorDialog("Export Text Failed", ex.Message); }
    }

    private async void SaveToLibrary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.SaveToLibraryAsync();
            // Brief visual confirmation
            var dlg = new ContentDialog
            {
                Title = "Saved",
                Content = "Resume saved to your Resume Studio library.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            _ = dlg.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Save Failed", ex.Message);
        }
    }

    private void ExportFormatPdf_Checked(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) ViewModel.SelectedExportFormatIndex = 0;
    }

    private void ExportFormatTxt_Checked(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) ViewModel.SelectedExportFormatIndex = 1;
    }

    private async System.Threading.Tasks.Task ShowErrorDialog(string title, string message)
    {
        try
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = $"{message}\n\nPlease check that you have write permission to the selected path.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dlg.ShowAsync();
        }
        catch { /* dialog itself failed — absorb */ }
    }

    // ── Keyboard Shortcuts ────────────────────────────────────────────────────
    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrl && e.Key == VirtualKey.Z && ViewModel.CanUndo)
        {
            ViewModel.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && e.Key == VirtualKey.Y && ViewModel.CanRedo)
        {
            ViewModel.RedoCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ── Formatting Toolbar ────────────────────────────────────────────────────
    private void FormatBold_Click(object sender, RoutedEventArgs e)
        => ApplyWrapFormatting(SummaryTextBox, "**", "**");

    private void FormatItalic_Click(object sender, RoutedEventArgs e)
        => ApplyWrapFormatting(SummaryTextBox, "_", "_");

    private void FormatUnderline_Click(object sender, RoutedEventArgs e)
        => ApplyWrapFormatting(SummaryTextBox, "__", "__");

    private void FormatLink_Click(object sender, RoutedEventArgs e)
    {
        if (SummaryTextBox == null) return;
        int start  = SummaryTextBox.SelectionStart;
        int length = SummaryTextBox.SelectionLength;
        string txt = SummaryTextBox.Text ?? "";
        string lbl = length > 0 ? txt.Substring(start, length) : "link text";
        string ins = $"[{lbl}](https://)";
        SummaryTextBox.Text = txt.Remove(start, length).Insert(start, ins);
        SummaryTextBox.SelectionStart  = start + lbl.Length + 3;
        SummaryTextBox.SelectionLength = 8;
        ViewModel.PushUndoSnapshot();
    }

    private void ApplyWrapFormatting(TextBox? box, string pre, string suf)
    {
        if (box == null) return;
        int start  = box.SelectionStart;
        int length = box.SelectionLength;
        string txt = box.Text ?? "";
        if (length > 0)
        {
            string sel = txt.Substring(start, length);
            box.Text = txt.Remove(start, length).Insert(start, pre + sel + suf);
            box.SelectionStart  = start + pre.Length;
            box.SelectionLength = length;
        }
        else
        {
            box.Text = txt.Insert(start, pre + suf);
            box.SelectionStart = start + pre.Length;
        }
        ViewModel.PushUndoSnapshot();
    }
}
