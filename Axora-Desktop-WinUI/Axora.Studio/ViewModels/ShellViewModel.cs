using Axora.Studio.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Studio.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    public IReadOnlyList<StudioRouteEntry> Routes => StudioRoutes.All;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentLabel))]
    private StudioRoute selectedRoute = StudioRoute.Home;
    public string CurrentLabel => StudioRoutes.Resolve(SelectedRoute).Label;
    partial void OnSelectedRouteChanging(StudioRoute value) => _ = StudioRoutes.Resolve(value);
    public void Navigate(StudioRoute route)
    {
        _ = StudioRoutes.Resolve(route);
        SelectedRoute = route;
    }
}
