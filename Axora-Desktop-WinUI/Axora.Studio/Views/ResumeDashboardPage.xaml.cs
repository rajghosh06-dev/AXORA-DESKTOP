using Axora.Studio.Models;
using Axora.Studio.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Axora.Studio.Views;
public sealed partial class ResumeDashboardPage : Page
{
    public ResumeViewModel ViewModel { get; }
    public ResumeDashboardPage(ResumeViewModel viewModel)
    {
        ViewModel = viewModel; InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
    private async void OnOpen(object sender, RoutedEventArgs args)
    { if (sender is FrameworkElement { DataContext: ResumeEntry entry }) await ViewModel.OpenCommand.ExecuteAsync(entry); }
    private async void OnReviewRecovery(object sender, RoutedEventArgs args)
    { if (sender is FrameworkElement { DataContext: ResumeEntry entry }) await ViewModel.ReviewRecoveryCommand.ExecuteAsync(entry); }
    private async void OnRecover(object sender, RoutedEventArgs args)
    { if (sender is FrameworkElement { DataContext: ResumeRecoveryEntry entry }) await ViewModel.RecoverCommand.ExecuteAsync(entry); }
}
