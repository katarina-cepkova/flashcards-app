using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Documents;
using Markdig.Syntax;
using Markdig;
using Flashcards.App.Services;
using Block = System.Windows.Documents.Block;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Renders markdown text into a FlowDocument, then post-processes it: dims quote block (Section) foreground/border
    /// to a lower-alpha version of the resolved text color instead of Markdig.Wpf's hardcoded gray, and tints code
    /// block backgrounds to match the card's own color (syntax highlighting itself is applied earlier, during
    /// rendering, by CodeBlockHighlightRenderer) — so both quotes and code blocks stay visually tied to whatever color
    /// the current card has.
    /// </summary>
    public class MarkdownToFlowDocumentConverter : IMultiValueConverter
    {
        /// <summary>Alpha applied to the text color for quote blocks (0-255). Lower = more faded.</summary>
        private const byte QuoteAlpha = 200;

        /// <summary>Alpha applied to the card's own background color for code blocks. Higher = more opaque/visible tint.</summary>
        private const byte CodeBackgroundAlpha = 40;

        /// <summary>
        /// Parses values[0] (raw markdown text) into a FlowDocument, using values[1] (the
        /// card's resolved text color) to dim quote foregrounds and values[2] (the card's raw
        /// background color) to decide the code-highlighting theme and tint code block
        /// backgrounds — so the whole preview stays visually tied to the current card's color.
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {

            string text = values[0] as string ?? string.Empty;
            Brush textBrush = values[1] as Brush ?? Brushes.Black;

            Brush cardBackgroundBrush = values[2] as Brush ?? Brushes.White;
            Color cardBackgroundColor = (cardBackgroundBrush as SolidColorBrush)?.Color ?? Colors.White;

            bool cardIsDark = ArgbColorConverter.CalculateLuminance(
                ArgbColorConverter.ToArgb(cardBackgroundColor)
            ) <= 128;

            // Built manually (instead of Markdig.Wpf.Markdown.ToFlowDocument's one-line helper) so
            // CodeBlockHighlightRenderer can be swapped in for the default CodeBlockRenderer before
            // rendering
            var document = new FlowDocument();
            var renderer = new Markdig.Renderers.WpfRenderer(document);

            var codeRenderer = new CodeBlockHighlightRenderer { UseDarkTheme = cardIsDark };
            renderer.ObjectRenderers.RemoveAll(r => r is Markdig.Renderers.Wpf.CodeBlockRenderer);
            renderer.ObjectRenderers.Add(codeRenderer);

            MarkdownDocument parsedDocument = Markdown.Parse(text, MarkdownPipelineProvider.Pipeline);
            renderer.Render(parsedDocument);

            if (textBrush is SolidColorBrush solidBrush)
                ApplyQuoteForeground(document, solidBrush.Color);

            ApplyCodeBackground(document, cardIsDark);
            return document;
        }

        /// <summary>
        /// Walks every block in the document looking for quote Sections (identified by their
        /// Markdig-assigned Style, since there's no simpler type/tag to check) and sets their
        /// Foreground to textColor with QuoteAlpha instead of the library's default gray.
        /// </summary>
        private void ApplyQuoteForeground(FlowDocument document, Color textColor)
        {
            Color dimmedColor = Color.FromArgb(QuoteAlpha, textColor.R, textColor.G, textColor.B);
            Brush dimmedBrush = new SolidColorBrush(dimmedColor);

            foreach (Block block in document.Blocks)
                ApplyQuoteForegroundRecursive(block, dimmedBrush);
        }

        /// <summary>
        /// Recurses into Section children only (Blocks/Lists/Tables aren't walked — quotes
        /// nested inside those are rare enough for flashcard content not to be worth the extra
        /// traversal code) so a quote nested inside another quote still gets dimmed.
        /// </summary>
        private void ApplyQuoteForegroundRecursive(Block block, Brush dimmedBrush)
        {
            if (block is Section section)
            {
                // Identifies a quote Section heuristically, since Markdig.Wpf gives it no dedicated marker
                // (unlike code blocks, which CodeBlockHighlightRenderer tags itself via paragraph.Tag) —
                // instead we check whether its Style sets Foreground, which is specific to Markdig.Wpf's
                // QuoteBlockStyleKey style and unlikely to appear on any other block type. Setters can also
                // contain Triggers etc., so OfType<Setter> filters down to just the property setters first.
                if (section.Style?.Setters.OfType<Setter>().Any(s => s.Property == TextElement.ForegroundProperty) == true)
                {
                    section.Foreground = dimmedBrush;
                    section.BorderBrush = dimmedBrush;
                }

                foreach (Block child in section.Blocks)
                    ApplyQuoteForegroundRecursive(child, dimmedBrush);
            }
        }

        /// <summary>
        /// Walks the document looking for Paragraphs tagged by CodeBlockHighlightRenderer as code blocks, and gives
        /// them a translucent white (light card) or black (dark card) overlay. WPF composites this against the card's
        /// own background at render time, so the code block reads as visually distinct while nudging its effective
        /// shade toward what the chosen ColorCode theme (light/dark formatter) expects for contrast.
        /// </summary>
        private void ApplyCodeBackground(FlowDocument document, bool cardIsDark)
        {
            // lighter color for dark background, darker for light
            // - so even white and black backgrounds have distinct colors for code blocks

            Color overlay = cardIsDark ? Colors.White : Colors.Black;
            Color translucentCodeBackground = Color.FromArgb(CodeBackgroundAlpha, overlay.R, overlay.G, overlay.B);
            Brush backgroundBrush = new SolidColorBrush(translucentCodeBackground);

            foreach (Block block in document.Blocks)
                ApplyCodeBackgroundRecursive(block, backgroundBrush);
        }

        /// <summary>
        /// Recurses into Section children looking for the tagged code-block Paragraph. Code
        /// blocks are always rendered as a direct Paragraph (never nested inside another
        /// Section themselves), so once one is found there's nothing further to recurse into.
        /// </summary>
        private void ApplyCodeBackgroundRecursive(Block block, Brush backgroundBrush)
        {
            if (block is Paragraph paragraph && Equals(paragraph.Tag, CodeBlockHighlightRenderer.CodeBlockTag))
            {
                paragraph.Background = backgroundBrush;
            }
            else if (block is Section section)
            {
                foreach (Block child in section.Blocks)
                    ApplyCodeBackgroundRecursive(child, backgroundBrush);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException("Preview is read-only.");
    }
}