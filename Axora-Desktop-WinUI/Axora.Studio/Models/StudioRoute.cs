using Axora.Studio.Views;
using Microsoft.UI.Xaml.Controls;

namespace Axora.Studio.Models;

public enum StudioRoute { Home = 0, Settings = 1, About = 2, Flashcards = 3 }
public sealed record StudioRouteEntry(StudioRoute Route, string Label, Type PageType, Symbol Icon);

public static class StudioRoutes
{
    public static IReadOnlyList<StudioRouteEntry> All { get; } = Array.AsReadOnly(new[]
    {
        new StudioRouteEntry(StudioRoute.Home, "Home", typeof(HomePage), Symbol.Home),
        new StudioRouteEntry(StudioRoute.Flashcards, "Flashcards", typeof(FlashcardsPage), Symbol.Library),
        new StudioRouteEntry(StudioRoute.Settings, "Settings", typeof(SettingsPage), Symbol.Setting),
        new StudioRouteEntry(StudioRoute.About, "About", typeof(AboutPage), Symbol.Help)
    });
    public static StudioRouteEntry Resolve(StudioRoute route) => All.SingleOrDefault(item => item.Route == route)
        ?? throw new ArgumentOutOfRangeException(nameof(route), "Unknown Studio route.");
}
