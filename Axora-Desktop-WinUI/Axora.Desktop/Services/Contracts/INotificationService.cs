using System;

namespace Axora.Desktop.Services.Contracts;

public enum NotificationSeverity
{
    Informational,
    Success,
    Warning,
    Error
}

public sealed class NotificationEventArgs : EventArgs
{
    public string Message { get; }
    public NotificationSeverity Severity { get; }
    public string? Title { get; }
    public TimeSpan? Duration { get; }

    public NotificationEventArgs(string message, NotificationSeverity severity, string? title, TimeSpan? duration)
    {
        Message = message;
        Severity = severity;
        Title = title;
        Duration = duration;
    }
}

public interface INotificationService
{
    void Show(string message, NotificationSeverity severity = NotificationSeverity.Informational, string? title = null, TimeSpan? duration = null);
    void ShowSuccess(string message, string? title = null, TimeSpan? duration = null);
    void ShowWarning(string message, string? title = null, TimeSpan? duration = null);
    void ShowError(string message, string? title = null, TimeSpan? duration = null);
    void Dismiss();

    event EventHandler<NotificationEventArgs>? NotificationRequested;
    event EventHandler? DismissRequested;
}
