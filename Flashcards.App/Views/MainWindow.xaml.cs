using Flashcards.App.Services;
using Flashcards.App.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Flashcards.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Constructs the window, wires up its ViewModel, and subscribes to the handlers
        /// that keep focus behavior and undo history consistent across the app.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // DataContext is assigned after InitializeComponent so every binding in the XAML
            // resolves against a fully constructed MainViewModel — nothing in the constructor
            // runs before this window's controls exist to bind to.
            var viewModel = new MainViewModel(new LocalizationService());
            DataContext = viewModel;

            // Undo history is tied to the RichTextBox instance, not to which card/side is
            // displayed, so it has to be cleared manually whenever the displayed content
            // switches to a different card or side — otherwise Undo could reach back into
            // content that's no longer showing.
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            PreviewMouseDown += MainWindow_PreviewMouseDown;
        }

        /// <summary>
        /// Clicking anywhere outside a TextBox moves focus to MainGrid instead of leaving it
        /// wherever it last was — e.g. so a click on empty space doesn't leave focus sitting
        /// inside the RichTextBox, which would otherwise keep intercepting keys like Space
        /// (see FlipCommand's CanFlip guard) meant for the rest of the window.
        /// </summary>
        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not TextBoxBase)
            {
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), MainGrid);
                Keyboard.Focus(MainGrid);
            }
        }

        /// <summary>
        /// Runs whenever any property on MainViewModel changes; only DisplayedText is relevant
        /// here, since that's the only change that means "the RichTextBox is about to show
        /// different content" (switching cards or flipping side).
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.DisplayedText))
                MarkdownEditingService.ClearUndoHistory(EditTextBox);
        }



        private void BoldButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.ToggleEmphasis(EditTextBox, "**");
        }

        private void ItalicButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.ToggleEmphasis(EditTextBox, "*");
        }


        private void StrikethroughButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.ToggleEmphasis(EditTextBox, "~~");
        }

        private void InlineCodeButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.ToggleEmphasis(EditTextBox, "`");
        }

        private void EditTextBox_OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            BoldButton.IsChecked = MarkdownEditingService.IsMarkerActive(textBox, "**");
            ItalicButton.IsChecked = MarkdownEditingService.IsMarkerActive(textBox, "*");
            StrikethroughButton.IsChecked = MarkdownEditingService.IsMarkerActive(textBox, "~~");
            InlineCodeButton.IsChecked = MarkdownEditingService.IsMarkerActive(textBox, "`");
        }

        private void QuoteButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.InsertQuote(EditTextBox);
        }

        private void LinkButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.InsertLink(EditTextBox);
        }

        private void EditTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // scrollbar Maximum/ViewportSize recalculation
        }

        private void EditTextBox_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // scrollbar Maximum/ViewportSize recalculation
        }

        private void EditTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.B when Keyboard.Modifiers == ModifierKeys.Control:
                    MarkdownEditingService.ToggleEmphasis(EditTextBox, "**");
                    e.Handled = true; // stop propagation to other components
                    break;
                case Key.I when Keyboard.Modifiers == ModifierKeys.Control:
                    MarkdownEditingService.ToggleEmphasis(EditTextBox, "*");
                    e.Handled = true;
                    break;
                case Key.E when Keyboard.Modifiers == ModifierKeys.Control:
                    MarkdownEditingService.ToggleEmphasis(EditTextBox, "`");
                    e.Handled = true;
                    break;
                // keyboard modifiers represented as enum - bitwise OR
                case Key.X when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                    MarkdownEditingService.ToggleEmphasis(EditTextBox, "~~");
                    e.Handled = true;
                    break;
            }
        }

        private void EditPreviewScrollBar_OnScroll(object sender, ScrollEventArgs e)
        {
            // scroll sync na EditTextBox a PreviewViewer
        }
    }
}