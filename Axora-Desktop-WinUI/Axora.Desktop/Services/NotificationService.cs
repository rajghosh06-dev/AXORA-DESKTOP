using System;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Thread-safe in-app notification service. Decouples ViewModels and background services
/// from WinUI controls by publishing events subscribed to by ShellView's global InfoBar host.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private static readonly TimeSpan DefaultInfoDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan DefaultSuccessDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan DefaultWarningDuration = TimeSpan.FromSeconds(7);

    public event EventHandler<NotificationEventArgs>? NotificationRequested;
    public event EventHandler? DismissRequested;

    public void Show(string message, NotificationSeverity severity = NotificationSeverity.Informational, string? title = null, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var effectiveDuration = duration ?? severity switch
        {
            NotificationSeverity.Informational => DefaultInfoDuration,
            NotificationSeverity.Success => DefaultSuccessDuration,
            NotificationSeverity.Warning => DefaultWarningDuration,
            NotificationSeverity.Error => null, // Errors stay visible until dismissed
            _ => DefaultInfoDuration
        };

        NotificationRequested?.Invoke(this, new NotificationEventArgs(message, severity, title, effectiveDuration));
    }

    public void ShowSuccess(string message, string? title = null, TimeSpan? duration = null)
    {
        Show(message, NotificationSeverity.Success, title ?? "Success", duration);
    }

    public void ShowWarning(string message, string? title = null, TimeSpan? duration = null)
    {
        Show(message, NotificationSeverity.Warning, title ?? "Notice", duration);
    }

    public void ShowError(string message, string? title = null, TimeSpan? duration = null)
    {
        Show(message, NotificationSeverity.Error, title ?? "Error", duration);
    }

    public void Dismiss()
    {
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }
}
