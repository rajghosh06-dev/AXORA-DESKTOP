using System.ComponentModel;

namespace Axora.Desktop.Services.Contracts;

public interface IAppSettingsService : INotifyPropertyChanged
{
    int ThemeIndex { get; set; }
    string AccentColor { get; set; }
    bool IsTelemetryEnabled { get; set; }
    bool AutoStartP2pEngine { get; set; }
    bool BackgroundQuickDropListen { get; set; }
    string P2pPort { get; set; }
    string DownloadDirectory { get; set; }

    // FEAT-6: Argon2id advanced cryptography settings
    int Argon2MemoryMb { get; set; }
    int Argon2Iterations { get; set; }

    void Save();
    void ResetToDefaults();
}
