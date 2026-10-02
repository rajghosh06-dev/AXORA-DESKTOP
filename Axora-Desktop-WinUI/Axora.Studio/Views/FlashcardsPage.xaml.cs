using Axora.Studio.Models;
using Axora.Studio.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;

namespace Axora.Studio.Views;

public sealed partial class FlashcardsPage : Page
{
    public FlashcardsViewModel ViewModel { get; }
    public FlashcardsPage(FlashcardsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += (_, _) => { UpdateLayoutState(); CardButton.Focus(FocusState.Programmatic); };
        SizeChanged += (_, _) => UpdateLayoutState();
    }
    private void UpdateLayoutState() => VisualStateManager.GoToState(this, ActualWidth >= 720 ? "Wide" : "Narrow", false);
    private void Rate_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse<CardDifficulty>(tag, out var rating))
            ViewModel.RateCard(rating);
    }
    public static bool ShouldHandleStudyKey(VirtualKey key, bool handled, bool editingOrSelection,
        bool buttonFocus, bool modified)
    {
        if (handled || editingOrSelection || modified) return false;
        if (buttonFocus && key is VirtualKey.Space or VirtualKey.Enter) return false;
        return key is VirtualKey.Space or VirtualKey.Enter or VirtualKey.Left or VirtualKey.A or VirtualKey.Right or VirtualKey.D
            or VirtualKey.Number1 or VirtualKey.Number2 or VirtualKey.Number3
            or VirtualKey.NumberPad1 or VirtualKey.NumberPad2 or VirtualKey.NumberPad3;
    }
    protected override void OnKeyDown(KeyRoutedEventArgs args)
    {
        base.OnKeyDown(args);
        bool editing = false, button = false;
        for (DependencyObject? node = args.OriginalSource as DependencyObject; node is not null && node != this;
             node = VisualTreeHelper.GetParent(node))
        {
            editing |= node is TextBox or RichEditBox or PasswordBox or Selector or SelectorItem;
            button |= node is ButtonBase;
        }
        bool modified = new[] { VirtualKey.Control, VirtualKey.Menu, VirtualKey.Shift }.Any(key =>
            (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0);
        if (!ShouldHandleStudyKey(args.Key, args.Handled, editing, button, modified) || !ViewModel.HasCard) return;
        switch (args.Key)
        {
            case VirtualKey.Space: case VirtualKey.Enter: ViewModel.FlipCard(); break;
            case VirtualKey.Left: case VirtualKey.A: ViewModel.PreviousCard(); break;
            case VirtualKey.Right: case VirtualKey.D: ViewModel.NextCard(); break;
            case VirtualKey.Number1: case VirtualKey.NumberPad1: ViewModel.RateCard(CardDifficulty.Easy); break;
            case VirtualKey.Number2: case VirtualKey.NumberPad2: ViewModel.RateCard(CardDifficulty.Medium); break;
            case VirtualKey.Number3: case VirtualKey.NumberPad3: ViewModel.RateCard(CardDifficulty.Hard); break;
        }
        args.Handled = true;
    }
}
