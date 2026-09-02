using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Axora.Desktop.Models;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; } = App.GetService<DashboardViewModel>();

    public DashboardPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is QuickDropItem item)
        {
            ViewModel.ShowInExplorerCommand.Execute(item);
        }
    }
}
