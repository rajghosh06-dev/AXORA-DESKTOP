using System;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Models;

namespace Axora.Desktop.Models;

/// <summary>
/// UI wrapper for a ConversionJob in the Universal Converter queue.
/// Exposes human-readable labels, icons, visual states, and command preconditions.
/// </summary>
public sealed partial class ConversionJobUiModel : ObservableObject, IDisposable
{
    public ConversionJob Job { get; }

    public string JobId => Job.JobId;
    public string FileName => Job.SourceFileName;
    public string SourceFilePath => Job.SourceFilePath;
    public string FormattedSize => Job.FormattedSourceSize;

    public string SourceExtensionDisplay => !string.IsNullOrWhiteSpace(Job.SourceExtension)
        ? Job.SourceExtension.TrimStart('.').ToUpperInvariant()
        : "?";

    public string TargetExtensionDisplay => !string.IsNullOrWhiteSpace(Job.TargetExtension)
        ? Job.TargetExtension.TrimStart('.').ToUpperInvariant()
        : "?";

    public string TransformationDisplay => $"{SourceExtensionDisplay}  →  {TargetExtensionDisplay}";

    public string OutputFilePath => Job.OutputFilePath;

    public string OutputFileName => !string.IsNullOrWhiteSpace(Job.OutputFilePath)
        ? Path.GetFileName(Job.OutputFilePath)
        : string.Empty;

    public string FormattedOutputSize => Job.FormattedOutputSize;

    public double Progress => Job.ProgressPercentage;

    public bool IsIndeterminateProgress =>
        (Job.State == ConversionJobState.Validating || Job.State == ConversionJobState.Running) &&
        Job.ProgressPercentage <= 0.0;

    public string HumanStatus => Job.State switch
    {
        ConversionJobState.Pending => "Pending",
        ConversionJobState.Validating => "Preparing…",
        ConversionJobState.Queued => "Queued",
        ConversionJobState.Running => "Converting…",
        ConversionJobState.Cancelling => "Cancelling…",
        ConversionJobState.Cancelled => "Cancelled",
        ConversionJobState.Succeeded => "Completed",
        ConversionJobState.Failed => "Failed",
        ConversionJobState.Skipped => "Skipped",
        _ => Job.Status.ToString()
    };

    public string IconGlyph => Job.SourceExtension.ToLowerInvariant() switch
    {
        ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".tiff" or ".tif" => "\uEB9F", // Photo / Image
        ".pdf" => "\uEA90", // PDF Document
        ".md" or ".markdown" => "\uE8A5", // Document
        ".txt" => "\uE8C4", // Text Page
        ".html" or ".htm" => "\uE774", // Web / Globe
        _ => "\uE8A5" // Default File
    };

    public bool CanCancel => Job.State == ConversionJobState.Queued || Job.State == ConversionJobState.Running;
    public bool CanRetry => Job.State == ConversionJobState.Failed;
    public bool CanOpen => Job.State == ConversionJobState.Succeeded && !string.IsNullOrWhiteSpace(Job.OutputFilePath);
    public bool IsRunning => Job.State == ConversionJobState.Running;
    public bool IsSuccess => Job.State == ConversionJobState.Succeeded;
    public string StatusText => HumanStatus;
    public bool HasError => Job.State == ConversionJobState.Failed && !string.IsNullOrWhiteSpace(Job.ErrorMessage);
    public string? ErrorMessage => Job.ErrorMessage;
    public string? DiagnosticDetails => Job.DiagnosticDetails;
    public bool HasDiagnostics => !string.IsNullOrWhiteSpace(Job.DiagnosticDetails);

    [ObservableProperty]
    private bool _isErrorExpanded;

    public ConversionJobUiModel(ConversionJob job)
    {
        Job = job ?? throw new ArgumentNullException(nameof(job));
        Job.PropertyChanged += OnJobPropertyChanged;
    }

    private void OnJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var dq = App.MainAppWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dq != null && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(DispatcherQueuePriority.Normal, RaiseAllPropertiesChanged);
        }
        else
        {
            RaiseAllPropertiesChanged();
        }
    }

    private void RaiseAllPropertiesChanged()
    {
        OnPropertyChanged(nameof(HumanStatus));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(IsIndeterminateProgress));
        OnPropertyChanged(nameof(OutputFilePath));
        OnPropertyChanged(nameof(OutputFileName));
        OnPropertyChanged(nameof(FormattedOutputSize));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(CanOpen));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(DiagnosticDetails));
        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(TargetExtensionDisplay));
        OnPropertyChanged(nameof(TransformationDisplay));
    }

    public void Refresh()
    {
        OnJobPropertyChanged(this, new PropertyChangedEventArgs(string.Empty));
    }

    public void Dispose()
    {
        Job.PropertyChanged -= OnJobPropertyChanged;
    }
}
