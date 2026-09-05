using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Controls;

/// <summary>
/// Reusable UI control for displaying dependency readiness, missing states, update notices, and repair alerts.
/// Provides direct navigation to DownloadManager with the target dependency selected.
/// </summary>
public sealed partial class DependencyStatusControl : UserControl
{
    public static readonly DependencyProperty DependencyIdProperty =
        DependencyProperty.Register(
            nameof(DependencyId),
            typeof(string),
            typeof(DependencyStatusControl),
            new PropertyMetadata(string.Empty, OnDependencyIdChanged));

    public static readonly DependencyProperty IsOptionalProperty =
        DependencyProperty.Register(
            nameof(IsOptional),
            typeof(bool),
            typeof(DependencyStatusControl),
            new PropertyMetadata(true, OnVisualStateChanged));

    public static readonly DependencyProperty FeatureNameProperty =
        DependencyProperty.Register(
            nameof(FeatureName),
            typeof(string),
            typeof(DependencyStatusControl),
            new PropertyMetadata(string.Empty, OnVisualStateChanged));

    public static readonly DependencyProperty FallbackMessageProperty =
        DependencyProperty.Register(
            nameof(FallbackMessage),
            typeof(string),
            typeof(DependencyStatusControl),
            new PropertyMetadata(string.Empty, OnVisualStateChanged));

    public string DependencyId
    {
        get => (string)GetValue(DependencyIdProperty);
        set => SetValue(DependencyIdProperty, value);
    }

    public bool IsOptional
    {
        get => (bool)GetValue(IsOptionalProperty);
        set => SetValue(IsOptionalProperty, value);
    }

    public string FeatureName
    {
        get => (string)GetValue(FeatureNameProperty);
        set => SetValue(FeatureNameProperty, value);
    }

    public string FallbackMessage
    {
        get => (string)GetValue(FallbackMessageProperty);
        set => SetValue(FallbackMessageProperty, value);
    }

    private IDependencyManager? _dependencyManager;

    public DependencyStatusControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _dependencyManager = App.TryGetService<IDependencyManager>();
        if (_dependencyManager != null)
        {
            _dependencyManager.DependencyStatusChanged += OnDependencyStatusChanged;
        }
        UpdateStatusDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_dependencyManager != null)
        {
            _dependencyManager.DependencyStatusChanged -= OnDependencyStatusChanged;
        }
    }

    private static void OnDependencyIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DependencyStatusControl control)
        {
            control.UpdateStatusDisplay();
        }
    }

    private static void OnVisualStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DependencyStatusControl control)
        {
            control.UpdateStatusDisplay();
        }
    }

    private void OnDependencyStatusChanged(object? sender, ExtensionStateChangedEventArgs e)
    {
        if (e.ExtensionId.Equals(DependencyId, StringComparison.OrdinalIgnoreCase))
        {
            DispatcherQueue.TryEnqueue(UpdateStatusDisplay);
        }
    }

    public void UpdateStatusDisplay()
    {
        StatusInfoBar ??= FindName("StatusInfoBar") as InfoBar;
        OpenDownloadManagerButton ??= FindName("OpenDownloadManagerButton") as Button;
        if (StatusInfoBar == null) return;

        if (string.IsNullOrWhiteSpace(DependencyId))
        {
            StatusInfoBar.IsOpen = false;
            return;
        }

        _dependencyManager ??= App.TryGetService<IDependencyManager>();
        var ext = _dependencyManager?.GetExtension(DependencyId);

        var name = ext?.DisplayName ?? DependencyId;

        if (ext == null || ext.Status == ExtensionStatus.NotInstalled)
        {
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = IsOptional ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
            StatusInfoBar.Title = $"{name} is not installed";
            StatusInfoBar.Message = IsOptional
                ? (!string.IsNullOrWhiteSpace(FallbackMessage) ? FallbackMessage : "Native WIC processing engine active as fallback.")
                : $"This operation requires {name} to be installed.";
            if (OpenDownloadManagerButton != null) OpenDownloadManagerButton.Visibility = Visibility.Visible;
        }
        else if (ext.Status is ExtensionStatus.RepairRequired or ExtensionStatus.Corrupted)
        {
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = $"{name} requires repair";
            StatusInfoBar.Message = ext.ErrorMessage ?? "Extension files are missing or damaged.";
            if (OpenDownloadManagerButton != null) OpenDownloadManagerButton.Visibility = Visibility.Visible;
        }
        else if (ext.Status == ExtensionStatus.UpdateAvailable)
        {
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = InfoBarSeverity.Informational;
            StatusInfoBar.Title = $"{name} update available (v{ext.LatestVersion})";
            StatusInfoBar.Message = "A newer version is available. All updates require explicit user approval.";
            if (OpenDownloadManagerButton != null) OpenDownloadManagerButton.Visibility = Visibility.Visible;
        }
        else if (ext.Status == ExtensionStatus.Installing)
        {
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = InfoBarSeverity.Informational;
            StatusInfoBar.Title = $"{name} is installing…";
            StatusInfoBar.Message = ext.StatusMessage ?? "Please wait while files are configured.";
            if (OpenDownloadManagerButton != null) OpenDownloadManagerButton.Visibility = Visibility.Collapsed;
        }
        else if (ext.Status == ExtensionStatus.Installed)
        {
            // For installed optional dependency: show success confirmation
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Title = $"{name} Ready (v{ext.InstalledVersion})";
            StatusInfoBar.Message = "High-performance acceleration and optimization engine active.";
            if (OpenDownloadManagerButton != null) OpenDownloadManagerButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity = InfoBarSeverity.Warning;
            StatusInfoBar.Title = $"{name} status: {ext.Status}";
            StatusInfoBar.Message = ext.ErrorMessage ?? "Check Download Manager for details.";
            if (OpenDownloadManagerButton != null) OpenDownloadManagerButton.Visibility = Visibility.Visible;
        }
    }

    private void OpenDownloadManagerButton_Click(object sender, RoutedEventArgs e)
    {
        App.MainAppWindow?.ShellRoot.NavigateTo("DownloadManager", DependencyId);
    }
}