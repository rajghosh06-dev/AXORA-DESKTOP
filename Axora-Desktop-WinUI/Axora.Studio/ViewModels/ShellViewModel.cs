using Axora.Studio.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Studio.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private StudioRoute _selectedRoute = StudioRoute.Home;
    private bool _navigationPending;
    public Func<StudioRoute, Task<bool>>? DepartureGuard { get; set; }
    public IReadOnlyList<StudioRouteEntry> Routes => StudioRoutes.All;
    public StudioRoute SelectedRoute
    {
        get => _selectedRoute;
        set
        {
            _ = StudioRoutes.Resolve(value);
            if (DepartureGuard is not null) throw new InvalidOperationException("Use guarded NavigateAsync.");
            Commit(value);
        }
    }
    public string CurrentLabel => StudioRoutes.Resolve(SelectedRoute).Label;
    private void Commit(StudioRoute route)
    { if (SetProperty(ref _selectedRoute, route, nameof(SelectedRoute))) OnPropertyChanged(nameof(CurrentLabel)); }
    public void Navigate(StudioRoute route)
    {
        _ = StudioRoutes.Resolve(route);
        SelectedRoute = route;
    }
    public async Task<bool> NavigateAsync(StudioRoute route)
    {
        _ = StudioRoutes.Resolve(route);
        if (_navigationPending) return false;
        if (route == SelectedRoute) return true;
        _navigationPending = true;
        try { if (DepartureGuard is not null && !await DepartureGuard(route)) return false; Commit(route); return true; }
        finally { _navigationPending = false; }
    }
}
