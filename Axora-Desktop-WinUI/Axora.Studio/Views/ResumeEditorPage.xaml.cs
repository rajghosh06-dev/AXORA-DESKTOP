using System.Windows.Input;
using Axora.Studio.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Axora.Studio.Views;

public sealed partial class ResumeEditorPage : Page
{
    public ResumeViewModel ViewModel { get; }
    public ResumeEditorPage(ResumeViewModel viewModel)
    {
        ViewModel = viewModel; InitializeComponent();
        Loaded += (_, _) => { ViewModel.EditorStructureChanged += OnStructureChanged; BuildSurface(); };
        Unloaded += (_, _) => ViewModel.EditorStructureChanged -= OnStructureChanged;
    }
    private void OnStructureChanged(object? sender, EventArgs args) => BuildSurface();
    private void BuildSurface()
    {
        EditorSurface.Children.Clear();
        if (!ViewModel.HasDocument)
        {
            EditorSurface.Children.Add(new TextBlock { Text = "Create or open a Resume from the dashboard.", TextWrapping = TextWrapping.Wrap });
            return;
        }
        foreach (var group in ViewModel.Groups)
        {
            var content = new StackPanel { Spacing = 14, Padding = new Thickness(16) };
            content.Children.Add(new TextBlock { Text = group.Description, TextWrapping = TextWrapping.Wrap, Opacity = .75 });
            if (group.UpCommand is not null)
            {
                var order = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                order.Children.Add(ActionButton("Move section up", group.UpCommand)); order.Children.Add(ActionButton("Move section down", group.DownCommand));
                content.Children.Add(order);
            }
            foreach (var field in group.Fields) content.Children.Add(FieldControl(field));
            foreach (var item in group.Items)
            {
                var fields = new StackPanel { Spacing = 12, Padding = new Thickness(16) };
                fields.Children.Add(new TextBlock { Text = "Entry " + item.Number, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                foreach (var field in item.Fields) fields.Children.Add(FieldControl(field));
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                actions.Children.Add(ActionButton("Move up", item.UpCommand)); actions.Children.Add(ActionButton("Move down", item.DownCommand)); actions.Children.Add(ActionButton("Remove entry", item.RemoveCommand));
                fields.Children.Add(actions);
                content.Children.Add(new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(64, 128, 128, 128)), Child = fields });
            }
            if (group.AddCommand is not null)
            {
                var button = ActionButton("Add " + group.Title + " entry", group.AddCommand);
                AutomationProperties.SetAutomationId(button, "Resume.Add." + group.Title);
                content.Children.Add(button);
            }
            var expander = new Expander { Header = group.Title, Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsExpanded = group.Title == "Profile" };
            AutomationProperties.SetAutomationId(expander, "Resume.Section." + group.Title);
            EditorSurface.Children.Add(expander);
        }
        if (ViewModel.RecoveryEntries.Count != 0)
        {
            var recovery = new StackPanel { Spacing = 8, Padding = new Thickness(16) };
            recovery.Children.Add(new TextBlock { Text = "Recovery publishes a verified prior revision as a new saved revision.", TextWrapping = TextWrapping.Wrap });
            foreach (var entry in ViewModel.RecoveryEntries)
                recovery.Children.Add(new Button { Content = entry.Label, Command = ViewModel.RecoverCommand, CommandParameter = entry });
            EditorSurface.Children.Add(new Expander { Header = "Saved revisions", Content = recovery, HorizontalAlignment = HorizontalAlignment.Stretch });
        }
    }
    private static Button ActionButton(string label, ICommand? command) => new() { Content = label, Command = command, HorizontalAlignment = HorizontalAlignment.Left };
    private static FrameworkElement FieldControl(ResumeEditorField field)
    {
        var panel = new StackPanel { Spacing = 4 };
        FrameworkElement input;
        if (field.ValueType == typeof(bool))
        {
            var toggle = new ToggleSwitch { Header = field.Label };
            toggle.SetBinding(ToggleSwitch.IsOnProperty, Bind(field, nameof(field.Boolean), BindingMode.TwoWay)); input = toggle;
        }
        else if (field.ValueType.IsEnum)
        {
            var choice = new ComboBox { Header = field.Label, ItemsSource = field.Choices, HorizontalAlignment = HorizontalAlignment.Stretch };
            choice.SetBinding(ComboBox.SelectedIndexProperty, Bind(field, nameof(field.Selected), BindingMode.TwoWay)); input = choice;
        }
        else
        {
            var text = new TextBox { Header = field.Label, MaxLength = field.MaxLength, AcceptsReturn = field.IsNarrative,
                TextWrapping = field.IsNarrative ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = field.IsNarrative ? 100 : 0 };
            text.SetBinding(TextBox.TextProperty, Bind(field, nameof(field.Text), BindingMode.TwoWay)); input = text;
        }
        AutomationProperties.SetAutomationId(input, "Resume.Field." + field.Path);
        AutomationProperties.SetName(input, field.Label);
        panel.Children.Add(input);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        error.SetBinding(TextBlock.TextProperty, Bind(field, nameof(field.Error), BindingMode.OneWay));
        panel.Children.Add(error);
        return panel;
    }
    private static Binding Bind(object source, string property, BindingMode mode) => new() { Source = source, Path = new PropertyPath(property), Mode = mode, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
}
