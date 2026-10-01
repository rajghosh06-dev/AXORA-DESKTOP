using Axora.Studio.Views;
using Microsoft.UI.Xaml.Controls;

namespace Axora.Studio.Models;

public enum StudioRoute { Home, Settings, About }
public sealed record StudioRouteEntry(StudioRoute Route, string Label, Type PageType, Symbol Icon);

public static class StudioRoutes
{
    public static IReadOnlyList<StudioRouteEntry> All { get; } = Array.AsReadOnly(new[]
    {
        new StudioRouteEntry(StudioRoute.Home, "Home", typeof(HomePage), Symbol.Home),
        new StudioRouteEntry(StudioRoute.Settings, "Settings", typeof(SettingsPage), Symbol.Setting),
        new StudioRouteEntry(StudioRoute.About, "About", typeof(AboutPage), Symbol.Help)
    });
    public static StudioRouteEntry Resolve(StudioRoute route) => All.SingleOrDefault(item => item.Route == route)
        ?? throw new ArgumentOutOfRangeException(nameof(route), "Unknown Studio route.");
}
