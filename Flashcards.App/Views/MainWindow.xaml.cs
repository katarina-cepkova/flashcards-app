using Flashcards.App.Services;
using Flashcards.App.ViewModels;
using Flashcards.App.Models;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Flashcards.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>PreviewViewer's internal ScrollViewer, found once at load time.</summary>
        private ScrollViewer? _previewScrollViewer;

        /// <summary>
        /// Guards SyncScrollPosition and PreviewViewer_OnScrollChanged against re-entrancy:
        /// each of the two methods programmatically scrolls the other side, which would
        /// otherwise raise that side's own ScrollChanged event and call back into the first
        /// method, looping indefinitely. While set, both methods return immediately instead
        /// of running their body.
        /// </summary>
        private bool _isSyncingScroll;

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

            // Undo history is tied to the TextBox instance, not to which card/side is
            // displayed, so it has to be cleared manually whenever the displayed content
            // switches to a different card or side — otherwise Undo could reach back into
            // content that's no longer showing.
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            PreviewMouseDown += MainWindow_PreviewMouseDown;
        }

        /// <summary>
        /// Finds PreviewViewer's internal ScrollViewer (only available once the control has
        /// been rendered, hence waiting for Loaded rather than doing this in the constructor),
        /// establishes the initial EditPreviewScrollBar range, and subscribes to both
        /// EditTextBox's and PreviewViewer's ScrollChanged events so any scrolling on either
        /// side — mouse wheel, keyboard, or dragging the shared scrollbar — keeps all three in sync.
        /// </summary>
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _previewScrollViewer = FindScrollViewer(PreviewViewer);

            RecalculateEditPreviewScrollBarRange();
            EditTextBox.AddHandler(ScrollViewer.ScrollChangedEvent,
                new ScrollChangedEventHandler(EditTextBox_OnScrollChanged));

            if (_previewScrollViewer is not null)
                _previewScrollViewer.ScrollChanged += PreviewViewer_OnScrollChanged;
        }

        /// <summary>
        /// Clicking anywhere outside a TextBox moves focus to MainGrid instead of leaving it wherever it last was —
        /// e.g. so a click on empty space doesn't leave focus sitting inside EditTextBox, which would otherwise keep
        /// intercepting keys like Space (see FlipCommand's CanFlip guard) meant for the rest of the window.
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
        /// Moves to the next/previous flashcard on mouse wheel input anywhere in the window
        /// that hasn't already been consumed by a child element (see FlashcardEditor_OnMouseWheel,
        /// which stops this from firing while the wheel is over the flashcard content itself).
        /// Scrolling up (positive delta) goes to the previous card, scrolling down goes to the next.
        /// </summary>
        private void MainGrid_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;
            ICommand command = e.Delta > 0 ? viewModel.PreviousCommand : viewModel.NextCommand;

            if (command.CanExecute(null))
                command.Execute(null);

            e.Handled = true;
        }

        /// <summary>
        /// Stops scroll-to-navigate (MainGrid_OnMouseWheel) from firing when the wheel is
        /// used anywhere over the flashcard content area — EditTextBox/PreviewViewer handle
        /// their own scrolling, but don't mark the event Handled themselves when there's
        /// nothing to scroll, so it would otherwise bubble up and unintentionally trigger
        /// card navigation.
        /// </summary>
        private void FlashcardEditor_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// Runs whenever any property on MainViewModel changes; only DisplayedText is relevant
        /// here, since that's the only change that means "EditTextBox is about to show
        /// different content" (switching cards or flipping side).
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.DisplayedText))
                MarkdownEditingService.ClearUndoHistory(EditTextBox);
        }

        /// <summary>Toggles "**" (bold) on the current selection/caret position.</summary>
        private void BoldButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.ToggleEmphasis(EditTextBox, "**");
        }

        /// <summary>Toggles "*" (italic) on the current selection/caret position.</summary>
        private void ItalicButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.ToggleEmphasis(EditTextBox, "*");
        }

        /// <summary>Toggles "~~" (strikethrough) on the current selection/caret position.</summary>
        private void StrikethroughButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.ToggleEmphasis(EditTextBox, "~~");
        }

        /// <summary>Toggles "`" (inline code) on the current selection/caret position.</summary>
        private void InlineCodeButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.ToggleEmphasis(EditTextBox, "`");
        }

        /// <summary>Increases the heading size (toward H1) of the current line.</summary>
        private void UpsizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.IncreaseHeadingLevel(EditTextBox);
        }

        /// <summary>Decreases the heading size (toward H6, then plain text) of the current line.</summary>
        private void DownsizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.DecreaseHeadingLevel(EditTextBox);
        }

        /// <summary>
        /// Refreshes the formatting toolbar's active state (checked/unchecked, enabled/disabled)
        /// to reflect the marker/heading level at the current caret position or selection.
        /// </summary>
        private void EditTextBox_OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            BoldButton.IsChecked = MarkdownEditingService.IsMarkerActive(textBox, "**");
            ItalicButton.IsChecked = MarkdownEditingService.IsMarkerActive(textBox, "*");
            StrikethroughButton.IsChecked = MarkdownEditingService.IsMarkerActive(textBox, "~~");
            InlineCodeButton.IsChecked = MarkdownEditingService.IsMarkerActive(textBox, "`");

            int headingLevel = MarkdownEditingService.GetHeadingLevel(textBox);
            UpsizeButton.IsEnabled = headingLevel != 1;
            DownsizeButton.IsEnabled = headingLevel != 0;
        }

        /// <summary>Adds a "> " blockquote prefix to every line touched by the current selection.</summary>
        private void QuoteButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.InsertQuote(EditTextBox);
        }

        /// <summary>Inserts a markdown link template, wrapping the selection as the link text if any.</summary>
        private void LinkButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.InsertLink(EditTextBox);
        }

        /// <summary>Inserts a fenced code block, wrapping the selection's content if any.</summary>
        private void CodeBlockButton_OnClick(object sender, RoutedEventArgs e)
        {
            MarkdownEditingService.InsertCodeBlock(EditTextBox);
        }

        /// <summary>Keeps EditPreviewScrollBar's range accurate as the text content changes.</summary>
        private void EditTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateEditPreviewScrollBarRange();
        }

        /// <summary>Keeps EditPreviewScrollBar's range accurate as the window/control is resized.</summary>
        private void EditTextBox_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RecalculateEditPreviewScrollBarRange();
        }

        /// <summary>
        /// Keyboard shortcuts for the formatting toolbar's toggle buttons: Ctrl+B (bold),
        /// Ctrl+I (italic), Ctrl+E (inline code), Ctrl+Shift+X (strikethrough). Runs as a
        /// Preview (tunneling) handler so it can intercept the key before EditTextBox's own
        /// input handling processes it.
        /// </summary>
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

        /// <summary>
        /// Recursively searches the visual tree under `parent` for a ScrollViewer.
        /// FlowDocumentScrollViewer wraps one internally (as part of its default
        /// ControlTemplate), but doesn't expose it as a public property, so this is
        /// how we get a direct reference to it.
        /// </summary>
        private static ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv)
                    return sv;

                var found = FindScrollViewer(child);
                if (found is not null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Recalculates EditPreviewScrollBar's Maximum/ViewportSize based on EditTextBox's
        /// current content size — called whenever the text or the window size changes,
        /// since both affect how much (if anything) there is to scroll.
        /// </summary>
        private void RecalculateEditPreviewScrollBarRange()
        {
            double contentHeight = EditTextBox.ExtentHeight;
            double visibleHeight = EditTextBox.ViewportHeight;
            // The difference is exactly how much extra content exists beyond what's
            // currently visible — i.e. how far there is to scroll.
            double scrollableHeight = Math.Max(0, contentHeight - visibleHeight);
            EditPreviewScrollBar.Maximum = scrollableHeight;
            // ViewportSize controls the thumb's visual size relative to the whole
            // track — set to the visible height
            EditPreviewScrollBar.ViewportSize = visibleHeight;
        }

        /// <summary>
        /// Scrolls EditTextBox to the given offset, updates EditPreviewScrollBar's thumb to
        /// match, and scrolls PreviewViewer proportionally. 
        /// </summary>
        /// <remarks>
        /// EditTextBox and PreviewViewer almost never have the same total content height 
        /// (plain text vs. formatted headings/code blocks), so the offset can't be reused 
        /// directly for the preview side — instead it's expressed as a ratio (0.0 to 1.0) 
        /// of EditTextBox's own scrollable range, and that same ratio is applied to 
        /// PreviewViewer's own range. Shared by EditPreviewScrollBar_OnScroll (scrollbar 
        /// dragged/clicked by the user) and EditTextBox_OnScrollChanged (EditTextBox scrolled 
        /// some other way, e.g. mouse wheel or keyboard), since both need to end up in the 
        /// same synchronized state.
        /// </remarks>
        private void SyncEditToPreview(double editVerticalOffset)
        {
            if (_isSyncingScroll) return;
            _isSyncingScroll = true;

            // editVerticalOffset is already the value we need for the edit side — apply it
            // directly to both EditTextBox and the scrollbar's thumb before computing
            // anything for the preview side.
            EditTextBox.ScrollToVerticalOffset(editVerticalOffset);
            EditPreviewScrollBar.Value = editVerticalOffset;

            if (_previewScrollViewer is null)
            {
                _isSyncingScroll = false;
                return;
            }

            // The preview side's offset isn't known directly — express editVerticalOffset
            // as a ratio of EditTextBox's own scrollable range first, then apply that ratio
            // to PreviewViewer's (different) scrollable range.
            double editScrollableHeight = Math.Max(1, // 1 to prevent dividing by zero
                EditTextBox.ExtentHeight - EditTextBox.ViewportHeight);
            double ratio = editVerticalOffset / editScrollableHeight;

            double previewScrollableHeight = Math.Max(0,
                _previewScrollViewer.ExtentHeight - _previewScrollViewer.ViewportHeight);
            _previewScrollViewer.ScrollToVerticalOffset(ratio * previewScrollableHeight);

            _isSyncingScroll = false;
        }

        /// <summary>Forwards EditTextBox's own scroll position (however it changed) into SyncEditToPreview.</summary>
        private void EditTextBox_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            SyncEditToPreview(e.VerticalOffset);
        }

        /// <summary>Forwards EditPreviewScrollBar's new thumb position into SyncEditToPreview.</summary>
        private void EditPreviewScrollBar_OnScroll(object sender, ScrollEventArgs e)
        {
            SyncEditToPreview(e.NewValue);
        }

        /// <summary>
        /// Mirror of SyncEditToPreview, in the opposite direction: expresses PreviewViewer's offset as a ratio of
        /// its own scrollable range, applies that ratio to EditTextBox's scrollable range, and updates
        /// EditPreviewScrollBar's thumb to match. Guarded by the same _isSyncingScroll flag as SyncEditToPreview, since
        /// the two methods each programmatically scroll the side the other one listens to.
        /// </summary>
        private void SyncPreviewToEdit(double previewVerticalOffset)
        {
            if (_isSyncingScroll || _previewScrollViewer is null) return;
            _isSyncingScroll = true;

            // Unlike SyncEditToPreview, the edit-side offset isn't known directly here —
            // express previewVerticalOffset as a ratio of PreviewViewer's own scrollable
            // range first, then apply that ratio to EditTextBox's (different) scrollable
            // range to get editOffset.
            double previewScrollableHeight = Math.Max(1, _previewScrollViewer.ExtentHeight - _previewScrollViewer.ViewportHeight);
            double ratio = previewVerticalOffset / previewScrollableHeight;

            double editScrollableHeight = Math.Max(0, EditTextBox.ExtentHeight - EditTextBox.ViewportHeight);
            double editOffset = ratio * editScrollableHeight;

            // Now that editOffset is known, apply it to both EditTextBox and the
            // scrollbar's thumb — same as SyncEditToPreview's first two lines.
            EditTextBox.ScrollToVerticalOffset(editOffset);
            EditPreviewScrollBar.Value = editOffset;

            _isSyncingScroll = false;
        }

        /// <summary>Forwards PreviewViewer's own scroll position (however it changed) into SyncPreviewToEdit.</summary>
        private void PreviewViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            SyncPreviewToEdit(e.VerticalOffset);
        }

        /// <summary>
        /// Routes CardNavigationScrollBar interactions to the matching card-navigation
        /// action: clicking an arrow button (SmallIncrement/SmallDecrement) or the track
        /// itself (LargeIncrement/LargeDecrement) steps one card via NextCommand/
        /// PreviousCommand; dragging or clicking the thumb (ThumbTrack/ThumbPosition) jumps
        /// straight to the dragged-to index via CurrentCardIndex.
        /// </summary>
        private void CardNavigationScrollBar_OnScroll(object sender, ScrollEventArgs e)
        {
            MainViewModel viewModel = (MainViewModel)DataContext;

            switch (e.ScrollEventType)
            {
                case ScrollEventType.SmallIncrement: // arrow button click
                case ScrollEventType.LargeIncrement: // clicking on the track itself
                    if (viewModel.NextCommand.CanExecute(null))
                        viewModel.NextCommand.Execute(null);
                    break;

                case ScrollEventType.SmallDecrement: // arrow button click
                case ScrollEventType.LargeDecrement: // clicking on the track itself
                    if (viewModel.PreviousCommand.CanExecute(null))
                        viewModel.PreviousCommand.Execute(null);
                    break;

                case ScrollEventType.ThumbTrack:
                case ScrollEventType.ThumbPosition:
                    // Direct drag/click on the thumb — jump straight to the dragged-to index.
                    viewModel.CurrentCardIndex = (int)Math.Round(e.NewValue);
                    break;
            }
        }

        private void EditButton_OnClick(object sender, RoutedEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;
            if (viewModel.CurrentState == AppState.OpenedSetView)
                viewModel.CurrentState = AppState.OpenedSetEdit;
            else if (viewModel.CurrentState == AppState.OpenedSetEdit)
                viewModel.CurrentState = AppState.OpenedSetView;
        }
    }
}