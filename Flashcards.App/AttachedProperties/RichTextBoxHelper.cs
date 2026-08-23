using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xaml;

namespace Flashcards.App.AttachedProperties
{
    /// <summary>
    /// Provides an attached property that bridges <see cref="RichTextBox.Document"/> (a <see cref="System.Windows.Documents.FlowDocument"/>,
    /// not directly bindable) with a plain <see cref="string"/> property on a ViewModel, so rich text content
    /// can participate in ordinary MVVM binding.
    /// </summary>
    //
    // This class doesn't create a normal object with properties — it "attaches" new properties
    // onto existing WPF elements (here, RichTextBox) that don't natively have them. Each attached
    // property below follows the same four-part shape:
    //
    // 1. `public static readonly DependencyProperty X` field — registers the property with
    //    WPF under a string name (e.g. "IsBold") and a default value. This is what makes the
    //    property recognizable to {Binding}, Style/Setter, and Trigger — a plain C# field or
    //    auto-property would not work with any of those.
    //
    // 2. RegisterAttached(...) parameters, in order:
    //      - name: the string key WPF uses internally, and what XAML refers to (e.g. "IsBold")
    //      - typeof(bool)/typeof(string): the type of value the property holds
    //      - typeof(RichTextBoxHelper): the class that owns/registers this property (not the
    //        class it gets attached to — RichTextBox never needs to know this property exists)
    //      - PropertyMetadata(...): the default value, and optionally a callback to run whenever
    //        the value changes (see DocumentTextProperty below for an example)
    //
    // 3. A public static `GetX(DependencyObject obj)` method — required by WPF's naming convention
    //    so the XAML parser and binding system can read the value. Internally just calls
    //    obj.GetValue(XProperty), where obj is the specific element (e.g. one particular
    //    RichTextBox) whose attached value is being read.
    //
    // 4. A `SetX(DependencyObject obj, T value)` method — writes the value via obj.SetValue(...).
    //    Public where external XAML/bindings need to set it (DocumentText, since it's two-way);
    //    private where only this class's own event handlers should set it (IsBold/IsItalic/
    //    IsUnderline, which are derived read-only reflections of the selection's formatting).
    //
    // Values set via SetValue are stored per-instance (each RichTextBox on screen has its own
    // independent DocumentText/IsBold/etc., even though the property is "defined" once here).
    //
    internal static class RichTextBoxHelper
    {
        // Guard flag preventing an infinite loop between the Model→UI and UI→Model directions:
        // when OnRichTextBoxTextChanged writes back into DocumentText, that write would normally
        // trigger OnDocumentTextChanged again, which would reload the document, which could
        // re-trigger TextChanged, and so on.
        private static bool _isUpdating;

        // A second, per-instance flag needed because the TextChanged handler must be attached
        // exactly once per RichTextBox — not once per DocumentText value change.
        // DependencyProperty itself has no "attach once" mechanism, so we track it manually.
        private static readonly DependencyProperty IsHandlerAttachedProperty =
            DependencyProperty.RegisterAttached(
                "IsHandlerAttached",
                typeof(bool),
                typeof(RichTextBoxHelper),
                new PropertyMetadata(false));

        /// <summary>
        /// Identifies the DocumentText attached property. Bind this to a string property (e.g. Flashcard.Front/Back)
        /// on a <see cref="RichTextBox"/> to keep its formatted content synchronized with the ViewModel,
        /// serialized as XAML.
        /// </summary>
        public static readonly DependencyProperty DocumentText =
            DependencyProperty.RegisterAttached(
                "DocumentText",
                typeof(string),
                typeof(RichTextBoxHelper),
                new FrameworkPropertyMetadata(default(string), OnDocumentTextChanged) { BindsTwoWayByDefault = true });

        /// <summary>Identifies the read-only IsBold attached property, reflecting whether the
        /// current selection/caret position is bold, so a Bold toggle button can display it.</summary>
        public static readonly DependencyProperty IsBold =
            DependencyProperty.RegisterAttached("IsBold", typeof(bool), typeof(RichTextBoxHelper), new PropertyMetadata(false));

        /// <summary>Identifies the read-only IsItalic attached property, reflecting whether the
        /// current selection/caret position is italic, so an Italic toggle button can display it.</summary>
        public static readonly DependencyProperty IsItalic =
            DependencyProperty.RegisterAttached("IsItalic", typeof(bool), typeof(RichTextBoxHelper), new PropertyMetadata(false));

        /// <summary>Identifies the read-only IsUnderline attached property, reflecting whether the
        /// current selection/caret position is underlined, so an Underline toggle button can display it.</summary>
        public static readonly DependencyProperty IsUnderline =
            DependencyProperty.RegisterAttached("IsUnderline", typeof(bool), typeof(RichTextBoxHelper), new PropertyMetadata(false));

        /// <summary>Identifies the read-only IsStrikethrough attached property, reflecting whether the
        /// current selection/caret position is struck through, so a Strikethrough toggle button can display it.</summary>
        public static readonly DependencyProperty IsStrikethrough =
            DependencyProperty.RegisterAttached("IsStrikethrough", typeof(bool), typeof(RichTextBoxHelper), new PropertyMetadata(false));

        /// <summary>
        /// Custom command for toggling strikethrough, since WPF has no built-in
        /// EditingCommands.ToggleStrikethrough (unlike Bold/Italic/Underline).
        /// </summary>
        public static readonly RoutedUICommand ToggleStrikethrough =
            new RoutedUICommand("Toggle Strikethrough", "ToggleStrikethrough", typeof(RichTextBoxHelper));

        /// <summary>
        /// Gets the current value of the DocumentText attached property for the given object.
        /// Required by the WPF attached-property naming convention (Get + property name) so the XAML
        /// parser can resolve bindings; not called directly elsewhere in this class.
        /// </summary>
        public static string GetDocumentText(DependencyObject obj) => (string)obj.GetValue(DocumentText);

        /// <summary>
        /// Sets the value of the DocumentText attached property for the given object.
        /// </summary>
        public static void SetDocumentText(DependencyObject obj, string value) => obj.SetValue(DocumentText, value);


        /// <summary>Gets the current value of the IsBold attached property, for XAML bindings to read.</summary>
        public static bool GetIsBold(DependencyObject obj) => (bool)obj.GetValue(IsBold);

        /// <summary>
        /// Sets the IsBold attached property. Private — only OnRichTextBoxSelectionChanged should set
        /// this, since it's a derived reflection of the selection's formatting, not something callers
        /// should assign directly.
        /// </summary> 
        private static void SetIsBold(DependencyObject obj, bool value) => obj.SetValue(IsBold, value);

        /// <summary>Gets the current value of the IsItalic attached property, for XAML bindings to read.</summary>
        public static bool GetIsItalic(DependencyObject obj) => (bool)obj.GetValue(IsItalic);

        /// <summary>Sets the IsItalic attached property. Private for the same reason as SetIsBold.</summary>
        private static void SetIsItalic(DependencyObject obj, bool value) => obj.SetValue(IsItalic, value);

        /// <summary>Gets the current value of the IsUnderline attached property, for XAML bindings to read.</summary>
        public static bool GetIsUnderline(DependencyObject obj) => (bool)obj.GetValue(IsUnderline);

        /// <summary>Sets the IsUnderline attached property. Private for the same reason as SetIsBold.</summary>
        private static void SetIsUnderline(DependencyObject obj, bool value) => obj.SetValue(IsUnderline, value);

        /// <summary>Gets the current value of the IsStrikethrough attached property, for XAML bindings to read.</summary>
        public static bool GetIsStrikethrough(DependencyObject obj) => (bool)obj.GetValue(IsStrikethrough);

        /// <summary>Sets the IsStrikethrough attached property. Private for the same reason as SetIsBold.</summary>
        private static void SetIsStrikethrough(DependencyObject obj, bool value) => obj.SetValue(IsStrikethrough, value);
        
        
        /// <summary>
        /// Clears the RichTextBox's undo/redo history. Call this explicitly whenever the
        /// displayed content changes to something unrelated to what was there before (switching
        /// cards, flipping to the other side, opening a different set), so Undo can't reach back
        /// into content that's no longer showing.
        /// </summary>
        /// <remarks>
        /// Not done automatically inside OnDocumentTextChanged, because that method also runs as
        /// a side effect of formatting commands (e.g. Bold) — and WPF throws if IsUndoEnabled is
        /// touched while such a command has its own internal change block open. Calling this
        /// explicitly, only from places that know a genuine content switch is happening, avoids
        /// that entirely.
        /// </remarks>
        public static void ClearUndoHistory(RichTextBox richTextBox)
        {
            // Turning IsUndoEnabled off and back on discards whatever undo/redo history had
            // built up — there's no direct "clear" method on RichTextBox, so this off/on toggle
            // is the standard WPF workaround.
            richTextBox.IsUndoEnabled = false;
            richTextBox.IsUndoEnabled = true;
        }

        /// <summary>
        /// Model → UI direction: fires whenever the bound Front/Back string changes
        /// (either from the ViewModel, or as a side-effect of our own write-back below).
        /// Loads the incoming string into the RichTextBox's FlowDocument as XAML-formatted content.
        /// </summary>
        private static void OnDocumentTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (_isUpdating)
                return;

            RichTextBox richTextBox = (RichTextBox)d;
            bool isHandlerAttached = (bool)richTextBox.GetValue(IsHandlerAttachedProperty);

            if (!isHandlerAttached)
            {
                richTextBox.TextChanged += OnRichTextBoxTextChanged;
                richTextBox.SelectionChanged += OnRichTextBoxSelectionChanged;

                // ToggleStrikethrough has no built-in WPF handling (unlike Bold/Italic/Underline via
                // EditingCommands), so we register our own CommandBinding directly on this RichTextBox
                // to tell it what to do when that command is invoked.
                richTextBox.CommandBindings.Add(new CommandBinding(
                    ToggleStrikethrough, OnToggleStrikethroughExecuted, OnToggleStrikethroughCanExecute));

                richTextBox.SetValue(IsHandlerAttachedProperty, true);
            }

            // e.NewValue = the incoming string
            string newText = (string)e.NewValue ?? string.Empty;

            if (string.IsNullOrEmpty(newText))
            {
                richTextBox.Document.Blocks.Clear();
                return;
            }

            try
            {
                // TextRange = a span of content inside a FlowDocument
                TextRange range = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd); // entire content

                // TextRange.Load expects a Stream, not a string directly, so we convert the string
                // to raw bytes (UTF8) and wrap them in a MemoryStream, which Stream-based APIs can read from.
                using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(newText)); // releases resources at function's end

                // DataFormats.Xaml tells WPF to interpret the stream as WPF's own XAML-based rich text representation
                range.Load(stream, DataFormats.Xaml);
            }
            catch (Exception ex) when (ex is XamlParseException or IOException)
            {
                // Corrupted formatting data or a stream I/O failure — fall back to plain text
                // so the user's content is not silently lost, even though formatting information is.
                richTextBox.Document.Blocks.Clear();
                richTextBox.Document.Blocks.Add(new Paragraph(new Run(newText)));
            }
        }

        /// <summary>
        /// UI → Model direction: fires on every content change the user makes while typing or formatting.
        /// Serializes the RichTextBox's current FlowDocument content back into the DocumentText attached
        /// property (and, through binding, into the ViewModel).
        /// </summary>
        private static void OnRichTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            _isUpdating = true;
            RichTextBox richTextBox = (RichTextBox)sender;

            try
            {
                TextRange range = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);

                // Empty stream — Save will write the serialized content into it, we don't provide bytes upfront.
                using MemoryStream stream = new MemoryStream();
                range.Save(stream, DataFormats.Xaml);

                // stream.ToArray() reads out everything Save just wrote into the stream, as a raw byte array.
                string serializedText = Encoding.UTF8.GetString(stream.ToArray());

                SetDocumentText(richTextBox, serializedText);

                // Formatting commands (Bold, Strikethrough, ...) change the document without moving
                // the caret, so SelectionChanged never fires for them — only TextChanged does.
                // Re-checking formatting here too is what makes toggle buttons update immediately
                // after being clicked, instead of only after the next caret move.
                UpdateFormattingState(richTextBox);

            }
            catch (IOException)
            {
                // Save failed — the model simply keeps its last successfully saved value.
                // Nothing to overwrite it with here, so we just skip this update rather than crash.
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Checks whether the given selection's TextDecorations include the specified kind
        /// (e.g. Underline, Strikethrough). Shared by the SelectionChanged detection logic
        /// and by ToggleStrikethrough's own before/after check.
        /// </summary>
        private static bool HasTextDecoration(TextSelection selection, TextDecorationLocation location)
        {
            object decorations = selection.GetPropertyValue(Inline.TextDecorationsProperty);
            return decorations is TextDecorationCollection collection
                && collection.Any(d => d.Location == location);
        }


        /// <summary>
        /// Reads Bold/Italic/Underline/Strikethrough formatting at the current selection and
        /// updates the corresponding attached properties, so the toolbar's toggle buttons
        /// reflect it. Called from both OnRichTextBoxSelectionChanged (caret moved) and
        /// OnRichTextBoxTextChanged (formatting changed without the caret moving, e.g. clicking
        /// Bold) — selection alone can't detect the second case, since it never fires then.
        /// </summary>
        private static void UpdateFormattingState(RichTextBox richTextBox)
        {
            TextSelection selection = richTextBox.Selection;

            // GetPropertyValue inspects every run of text within the selection; if they don't all
            // agree on a property (e.g. selection spans both bold and non-bold text), it returns
            // DependencyProperty.UnsetValue, which the pattern matches below simply treat as "not set".
            //
            // FontWeight/FontStyle are declared on TextElement — the common base class for every
            // element that can appear in a FlowDocument (Run, Paragraph, Span, ...) — since font
            // properties can apply at any of those levels, not just to a run of inline text.
            object fontWeight = selection.GetPropertyValue(TextElement.FontWeightProperty);
            bool isBold = fontWeight is FontWeight weight && weight == FontWeights.Bold;
            SetIsBold(richTextBox, isBold);

            object fontStyle = selection.GetPropertyValue(TextElement.FontStyleProperty);
            bool isItalic = fontStyle is FontStyle style && style == FontStyles.Italic;
            SetIsItalic(richTextBox, isItalic);

            // TextDecorations is declared on Inline (not TextElement) — decorations like underline
            // only make sense on inline runs of text, not on block-level elements like Paragraph.
            // Unlike FontWeight (one value), TextDecorations is a collection, since multiple
            // decorations (e.g. underline AND strikethrough) can be present on the same text at
            // once — so checking "is this exactly equal to an underline collection" would be wrong;
            // instead we check whether an underline is present among possibly several decorations.
            // (Location identifies which kind of decoration that entry is — Underline, Strikethrough,
            // OverLine, or Baseline).
            bool isUnderline = HasTextDecoration(selection, TextDecorationLocation.Underline);
            SetIsUnderline(richTextBox, isUnderline);

            bool isStriked = HasTextDecoration(selection, TextDecorationLocation.Strikethrough);
            SetIsStrikethrough(richTextBox, isStriked);
        }

        /// <summary>
        /// Fires whenever the caret moves or the selection changes (clicking, arrow keys).
        /// Delegates to UpdateFormattingState, since moving the caret changes what "current
        /// formatting" means even if nothing about the document itself changed.
        /// </summary>
        private static void OnRichTextBoxSelectionChanged(object sender, RoutedEventArgs e)
        {
            RichTextBox richTextBox = (RichTextBox)sender;
            UpdateFormattingState(richTextBox);
        }

        /// <summary>
        /// Runs when ToggleStrikethrough is invoked. Flips strikethrough on the current selection:
        /// removes it if already present, applies it otherwise. ApplyPropertyValue on an empty
        /// selection (just a caret) still works — it sets the formatting that newly typed text
        /// will use, the same way clicking Bold with nothing selected does.
        /// </summary>
        private static void OnToggleStrikethroughExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            RichTextBox richTextBox = (RichTextBox)sender;
            TextSelection selection = richTextBox.Selection;

            bool isCurrentlyStrikethrough = HasTextDecoration(selection, TextDecorationLocation.Strikethrough);            
            selection.ApplyPropertyValue(
                Inline.TextDecorationsProperty,
                isCurrentlyStrikethrough ? null : TextDecorations.Strikethrough);
        }

        /// <summary>
        /// Custom commands are enabled by default unless told otherwise — always allow it here,
        /// same as Bold/Italic/Underline.
        /// </summary>
        private static void OnToggleStrikethroughCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
    }
}