using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Axora.Desktop.Models;
using Axora.Desktop.ViewModels;
using Windows.System;

namespace Axora.Desktop.Views;

public sealed partial class FlashcardsPage : Page
{
    public FlashcardsViewModel ViewModel { get; } = App.GetService<FlashcardsViewModel>();

    public FlashcardsPage()
    {
        InitializeComponent();
        DataContext = this;
        ViewModel.PropertyChanged += (_, _) => UpdateCardDisplay();
        Loaded += (_, _) =>
        {
            UpdateCardDisplay();
            // FEAT-4: Focus page root so keyboard accelerators fire without clicking first
            this.Focus(FocusState.Programmatic);
        };
    }

    private void DeckList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // The TwoWay binding updates ViewModel.ActiveDeck, but does NOT call SelectDeck()
        // which resets CurrentCardIndex, IsCardFlipped, and calls UpdateCurrentCard().
        // We must call SelectDeck() explicitly to synchronize the full deck state.
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is Axora.Desktop.Models.FlashcardDeck selectedDeck)
        {
            ViewModel.SelectDeckCommand.Execute(selectedDeck);
        }
        UpdateCardDisplay();
    }

    private void Card_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.FlipCardCommand.Execute(null);
        UpdateCardDisplay();
    }

    private void Rate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string difficulty)
        {
            ViewModel.RateCardCommand.Execute(difficulty);
            UpdateCardDisplay();
        }
    }

    // FEAT-4: Keyboard accelerators for flashcard study session
    // Space/Enter = Flip, ← / A = Previous, → / D = Next, 1 = Easy, 2 = Medium, 3 = Hard
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case VirtualKey.Space:
            case VirtualKey.Enter:
                ViewModel.FlipCardCommand.Execute(null);
                UpdateCardDisplay();
                e.Handled = true;
                break;

            case VirtualKey.Left:
            case VirtualKey.A:
                ViewModel.PreviousCardCommand.Execute(null);
                UpdateCardDisplay();
                e.Handled = true;
                break;

            case VirtualKey.Right:
            case VirtualKey.D:
                ViewModel.NextCardCommand.Execute(null);
                UpdateCardDisplay();
                e.Handled = true;
                break;

            case VirtualKey.Number1:
            case VirtualKey.NumberPad1:
                ViewModel.RateCardCommand.Execute("Easy");
                UpdateCardDisplay();
                e.Handled = true;
                break;

            case VirtualKey.Number2:
            case VirtualKey.NumberPad2:
                ViewModel.RateCardCommand.Execute("Medium");
                UpdateCardDisplay();
                e.Handled = true;
                break;

            case VirtualKey.Number3:
            case VirtualKey.NumberPad3:
                ViewModel.RateCardCommand.Execute("Hard");
                UpdateCardDisplay();
                e.Handled = true;
                break;
        }
    }

    private void UpdateCardDisplay()
    {
        CardLabelText ??= FindName("CardLabelText") as TextBlock;
        CardBodyText ??= FindName("CardBodyText") as TextBlock;

        if (ViewModel.CurrentCard is null)
        {
            if (CardLabelText != null) CardLabelText.Text = "No Card";
            if (CardBodyText != null) CardBodyText.Text = "Select or create a deck to begin study.";
            return;
        }

        if (ViewModel.IsCardFlipped)
        {
            if (CardLabelText != null) CardLabelText.Text = "Answer / Solution";
            if (CardBodyText != null) CardBodyText.Text = ViewModel.CurrentCard.Back;
        }
        else
        {
            if (CardLabelText != null) CardLabelText.Text = "Question / Concept";
            if (CardBodyText != null) CardBodyText.Text = ViewModel.CurrentCard.Front;
        }
    }
}
