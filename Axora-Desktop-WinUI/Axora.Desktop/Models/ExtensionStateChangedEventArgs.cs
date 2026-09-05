using System;

namespace Axora.Desktop.Models;

/// <summary>
/// Event arguments fired when an extension transitions between lifecycle states.
/// </summary>
public sealed class ExtensionStateChangedEventArgs : EventArgs
{
    public string ExtensionId { get; }
    public ExtensionStatus OldStatus { get; }
    public ExtensionStatus NewStatus { get; }
    public ExtensionModel Extension { get; }

    public ExtensionStateChangedEventArgs(string extensionId, ExtensionStatus oldStatus, ExtensionStatus newStatus, ExtensionModel extension)
    {
        ExtensionId = extensionId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Extension = extension;
    }
}