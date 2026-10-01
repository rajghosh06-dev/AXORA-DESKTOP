using Axora.Studio.Models;
using Axora.Studio.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Axora.Studio.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StudioSettingsService _settings;
    public IReadOnlyList<StudioTheme> Themes { get; } = Array.AsReadOnly(Enum.GetValues<StudioTheme>());
    public event Action<StudioTheme>? ThemeSaved;
    [ObservableProperty] private StudioTheme selectedTheme;
    [ObservableProperty] private string status;
    [ObservableProperty] private bool canEdit = true;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool canSave;
    public SettingsViewModel(StudioSettingsService settings)
    {
        _settings = settings;
        selectedTheme = settings.Current.Theme;
        canSave = settings.CanSave;
        status = settings.Warning ?? "Changes are saved only when you choose Save.";
    }
    partial void OnSelectedThemeChanged(StudioTheme value)
        => Status = CanSave ? "Theme selection changed. Choose Save to persist and apply it."
            : _settings.Warning ?? "Settings are read-only; nothing has been saved or applied.";

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        // Capture the actual draft being saved; UI disables editing while executing.
        var theme = SelectedTheme;
        CanEdit = false;
        Status = "Saving…";
        try
        {
            var result = await _settings.SaveAsync(theme);
            CanSave = _settings.CanSave;
            Status = result.Message;
            if (result.Published)
            {
                try { ThemeSaved?.Invoke(theme); }
                catch (Exception ex)
                { Status = $"Settings saved, but applying the theme failed ({ex.GetType().Name}). Restart Studio to apply it."; }
            }
        }
        catch (Exception ex)
        {
            Status = $"Settings operation failed ({ex.GetType().Name}); no successful save is claimed.";
        }
        finally { CanEdit = true; }
    }
}
