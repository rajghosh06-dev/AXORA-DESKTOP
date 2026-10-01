using System.Reflection;
using Microsoft.UI.Xaml.Controls;
namespace Axora.Studio.Views;
public sealed partial class AboutPage : Page
{
    public string Identity => "Assembly: Axora.Studio • Executable: Axora.Studio.exe";
    public string BuildVersion => "Build: " + (typeof(AboutPage).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AboutPage).Assembly.GetName().Version?.ToString() ?? "Unavailable");
    public AboutPage() => InitializeComponent();
}
