using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xaml;

namespace Flashcards.App.AttachedProperties
{
    /// <summary>
    /// Provides an attached property that bridges <see cref="RichTextBox.Document"/> (a <see cref="System.Windows.Documents.FlowDocument"/>,
    /// not directly bindable) with a plain <see cref="string"/> property on a ViewModel, so rich text content
    /// can participate in ordinary MVVM binding.
    /// </summary>
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
        public static readonly DependencyProperty DocumentTextProperty =
            DependencyProperty.RegisterAttached(
                "DocumentText",
                typeof(string),
                typeof(RichTextBoxHelper),
                new FrameworkPropertyMetadata(default(string), OnDocumentTextChanged)
                { BindsTwoWayByDefault = true });

        /// <summary>
        /// Gets the current value of the DocumentText attached property for the given object.
        /// Required by the WPF attached-property naming convention (Get + property name) so the XAML
        /// parser can resolve bindings; not called directly elsewhere in this class.
        /// </summary>
        public static string GetDocumentText(DependencyObject obj) => (string)obj.GetValue(DocumentTextProperty);

        /// <summary>
        /// Sets the value of the DocumentText attached property for the given object.
        /// </summary>
        public static void SetDocumentText(DependencyObject obj, string value) => obj.SetValue(DocumentTextProperty, value);


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
    }
}