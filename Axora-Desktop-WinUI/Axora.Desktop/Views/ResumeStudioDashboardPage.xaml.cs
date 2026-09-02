using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Axora.Desktop.Helpers;
using Axora.Desktop.Models;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

/// <summary>
/// Dashboard landing page for Resume Studio — lists every JSON resume the user has saved
/// in Documents\Axora\Resumes. Clicking New Resume / Edit opens ResumeStudioPage.
/// </summary>
public sealed partial class ResumeStudioDashboardPage : Page
{
    private static string ResumeFolder => ResumeStorageHelper.ResumeFolder;

    public ResumeStudioDashboardPage()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ResumeStudioDashboardPage] InitializeComponent failed: {ex}");

            try
            {
                if (EmptyState != null) EmptyState.Visibility = Visibility.Visible;
                if (TilesStack != null) TilesStack.Visibility = Visibility.Collapsed;
            }
            catch { }

            Loaded += OnPageLoaded;
            return;
        }

        Loaded += OnPageLoaded;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DispatcherQueue.TryEnqueue(() =>
        {
            ResumeStorageHelper.EnsureDirectory();
            RefreshTiles();
        });
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        ResumeStorageHelper.EnsureDirectory();
        RefreshTiles();
    }

    // ── Tile refresh ─────────────────────────────────────────────────────────
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshTiles(SearchBox?.Text);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshTiles(SearchBox?.Text);
    }

    private void RefreshTiles(string? searchQuery = null)
    {
        if (TxtCount is null || EmptyState is null || TilesStack is null) return;

        string[] files = Array.Empty<string>();

        try
        {
            if (Directory.Exists(ResumeFolder))
            {
                files = Directory.GetFiles(ResumeFolder, "*.json")
                                 .OrderByDescending(File.GetLastWriteTime)
                                 .ToArray();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ResumeStudioDashboardPage] Directory scan failed: {ex}");
        }

        TxtCount.Text = files.Length.ToString();

        if (files.Length == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            TilesStack.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        TilesStack.Visibility = Visibility.Visible;

        TilesStack.Children.Clear();
        foreach (var path in files)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    var fn = Path.GetFileNameWithoutExtension(path);
                    if (!fn.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                    {
                        var content = File.ReadAllText(path);
                        if (!content.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                }

                TilesStack.Children.Add(BuildTile(path));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ResumeStudioDashboardPage] BuildTile failed for {path}: {ex}");
            }
        }
    }

    // ── Relative Time Helper ──────────────────────────────────────────────────
    private static string FormatRelativeTime(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalSeconds < 60) return "Edited just now";
        if (span.TotalMinutes < 60) return $"Edited {(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24 && dt.Date == DateTime.Today) return $"Edited today at {dt:HH:mm}";
        if (dt.Date == DateTime.Today.AddDays(-1)) return $"Edited yesterday at {dt:HH:mm}";
        return $"Edited {dt:MMM d, yyyy \u2022 HH:mm}";
    }

    // ── Tile card builder ────────────────────────────────────────────────────
    private UIElement BuildTile(string filePath)
    {
        string name = "Untitled Resume";
        string roleTitle = "No title specified";
        string pageBadge = "2-Page ATS";

        try
        {
            var json = File.ReadAllText(filePath);
            var doc = JsonSerializer.Deserialize<ResumeDocument>(json);
            if (doc != null)
            {
                if (!string.IsNullOrWhiteSpace(doc.ResumeTitle))
                    name = doc.ResumeTitle;
                else if (!string.IsNullOrWhiteSpace(doc.Header.FullName))
                    name = doc.Header.FullName;

                if (!string.IsNullOrWhiteSpace(doc.Header.ProfessionalTitle))
                    roleTitle = doc.Header.ProfessionalTitle;

                pageBadge = doc.Formatting.TargetLength switch
                {
                    PageTargetLength.OnePage => "1-Page ATS",
                    PageTargetLength.TwoPages => "2-Page ATS",
                    PageTargetLength.ThreePages => "3-Page ATS",
                    PageTargetLength.FourPlusPages => "4+ CV",
                    _ => "2-Page ATS"
                };
            }
        }
        catch { /* corrupt JSON — show defaults */ }

        string lastModified = "Unknown";
        try { lastModified = FormatRelativeTime(File.GetLastWriteTime(filePath)); }
        catch { }

        var capturedPath = filePath;
        var capturedName = name;

        // Card container
        var card = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            Background = TryGetResource<Brush>("LayerFillColorDefaultBrush"),
            BorderBrush = TryGetResource<Brush>("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left info stack
        var infoStack = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameBadgeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        nameBadgeRow.Children.Add(new FontIcon
        {
            Glyph = "\uE8A5",
            FontSize = 14,
            Foreground = TryGetResource<Brush>("AccentTextFillColorPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        nameBadgeRow.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = TryGetResource<Brush>("TextFillColorPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var badge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 215, 0)),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = pageBadge,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 180, 0))
        };
        nameBadgeRow.Children.Add(badge);
        infoStack.Children.Add(nameBadgeRow);

        if (!string.IsNullOrWhiteSpace(roleTitle))
        {
            infoStack.Children.Add(new TextBlock
            {
                Text = roleTitle,
                FontSize = 12,
                Foreground = TryGetResource<Brush>("TextFillColorSecondaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        infoStack.Children.Add(new TextBlock
        {
            Text = lastModified,
            FontSize = 11,
            Foreground = TryGetResource<Brush>("TextFillColorTertiaryBrush")
        });

        Grid.SetColumn(infoStack, 0);
        root.Children.Add(infoStack);

        // Right actions
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Edit Button
        var editBtn = new Button
        {
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(255, 91, 125, 232)),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE70F", FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(255, 17, 17, 17)) },
                    new TextBlock
                    {
                        Text = "Edit Resume",
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 17, 17, 17))
                    }
                }
            }
        };
        editBtn.Click += (_, _) => OpenEditor(capturedPath);

        // Rename Button
        var renameBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE8AC", FontSize = 11 },
            Padding = new Thickness(8, 6, 8, 6)
        };
        ToolTipService.SetToolTip(renameBtn, "Rename this resume");
        renameBtn.Click += async (_, _) =>
        {
            try
            {
                var inputTb = new TextBox
                {
                    Text = capturedName,
                    PlaceholderText = "Enter resume title...",
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var dlg = new ContentDialog
                {
                    Title = "Rename Resume",
                    Content = inputTb,
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };

                if (await dlg.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputTb.Text))
                {
                    await ResumeStorageHelper.RenameResumeAsync(capturedPath, inputTb.Text.Trim());
                    RefreshTiles(SearchBox?.Text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResumeStudioDashboardPage] Rename failed: {ex}");
            }
        };

        // Duplicate Button
        var dupBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE8C8", FontSize = 11 },
            Padding = new Thickness(8, 6, 8, 6)
        };
        ToolTipService.SetToolTip(dupBtn, "Duplicate this resume");
        dupBtn.Click += async (_, _) =>
        {
            try
            {
                var json = await File.ReadAllTextAsync(capturedPath);
                var doc = JsonSerializer.Deserialize<ResumeDocument>(json);
                if (doc != null)
                {
                    var copyBase = $"{capturedName} (Copy)";
                    doc.ResumeTitle = ResumeStorageHelper.GenerateUniqueResumeTitle(copyBase);
                    var safeName = string.Concat(doc.ResumeTitle.Split(Path.GetInvalidFileNameChars()));
                    var guidShort = Guid.NewGuid().ToString("N")[..8];
                    var newPath = Path.Combine(ResumeFolder, $"{safeName}_{guidShort}.json");
                    var opt = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(newPath, JsonSerializer.Serialize(doc, opt));
                    RefreshTiles(SearchBox?.Text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResumeStudioDashboardPage] Duplicate failed: {ex}");
            }
        };

        // Delete Button
        var delBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE74D", FontSize = 11 },
            Padding = new Thickness(8, 6, 8, 6)
        };
        ToolTipService.SetToolTip(delBtn, "Delete this resume permanently");
        delBtn.Click += async (_, _) =>
        {
            try
            {
                var dlg = new ContentDialog
                {
                    Title = "Delete Resume?",
                    Content = $"This will permanently delete \"{capturedName}\".",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    XamlRoot = XamlRoot
                };
                if (await dlg.ShowAsync() == ContentDialogResult.Primary)
                {
                    try { File.Delete(capturedPath); } catch { }
                    RefreshTiles(SearchBox?.Text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ResumeStudioDashboardPage] Delete dialog failed: {ex}");
            }
        };

        actions.Children.Add(editBtn);
        actions.Children.Add(renameBtn);
        actions.Children.Add(dupBtn);
        actions.Children.Add(delBtn);
        Grid.SetColumn(actions, 1);
        root.Children.Add(actions);

        card.Child = root;
        return card;
    }

    // ── Quick Start Template Handlers ─────────────────────────────────────────
    private async void TemplateBlank_Click(object sender, RoutedEventArgs e)
    {
        await CreateNewResumeFromPreset("blank");
    }

    private async void TemplateRishit_Click(object sender, RoutedEventArgs e)
    {
        await CreateNewResumeFromPreset("rishit-ghosh");
    }

    private async void TemplateSoftware_Click(object sender, RoutedEventArgs e)
    {
        await CreateNewResumeFromPreset("software-engineer");
    }

    private async void TemplateExecutive_Click(object sender, RoutedEventArgs e)
    {
        await CreateNewResumeFromPreset("executive-leadership");
    }

    private async void TemplateAcademic_Click(object sender, RoutedEventArgs e)
    {
        await CreateNewResumeFromPreset("academic-research");
    }

    private async Task CreateNewResumeFromPreset(string preset)
    {
        try
        {
            var vm = App.GetService<ViewModels.ResumeStudioViewModel>();
            vm.LoadPreset(preset);
            vm.ActiveFilePath = null;
            await vm.SaveToLibraryAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ResumeStudioDashboardPage] CreateFromPreset failed: {ex}");
        }

        if (this.Frame != null)
        {
            this.Frame.Navigate(typeof(Views.ResumeStudioPage), null,
                new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static T? TryGetResource<T>(string key) where T : class
    {
        try
        {
            if (Application.Current.Resources.TryGetValue(key, out var res) && res is T typed)
                return typed;
        }
        catch { }
        return default;
    }

    // ── Navigation ────────────────────────────────────────────────────────────
    private void NewResume_Click(object sender, RoutedEventArgs e)
    {
        NavigateToEditor(null);
    }

    private void OpenEditor(string filePath)
    {
        NavigateToEditor(filePath);
    }

    private async void NavigateToEditor(string? filePath)
    {
        try
        {
            var vm = App.GetService<ViewModels.ResumeStudioViewModel>();
            if (filePath != null && File.Exists(filePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    vm.RestoreFromJson(json);
                    vm.ActiveFilePath = filePath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ResumeStudioDashboardPage] Load resume failed: {ex}");
                    vm.LoadPreset("blank");
                    vm.ActiveFilePath = null;
                    await vm.SaveToLibraryAsync();
                }
            }
            else
            {
                vm.LoadPreset("blank");
                vm.ActiveFilePath = null;
                await vm.SaveToLibraryAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ResumeStudioDashboardPage] GetService/Load failed: {ex}");
        }

        try
        {
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(Views.ResumeStudioPage), null,
                    new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
            }
            else
            {
                var shell = App.MainAppWindow?.ShellRoot
                    ?? (App.MainAppWindow?.Content as Grid)?.Children.OfType<ShellView>().FirstOrDefault();

                if (shell != null)
                {
                    shell.NavigateTo("ResumeStudioEditor");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ResumeStudioDashboardPage] NavigateToEditor failed: {ex}");
        }
    }
}
