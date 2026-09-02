using System;

namespace Axora.Desktop.Services.Contracts;

public interface ITrayService
{
    void Initialize(IntPtr hwnd);
    void ShowNotification(string title, string message);
    void Remove();
}
