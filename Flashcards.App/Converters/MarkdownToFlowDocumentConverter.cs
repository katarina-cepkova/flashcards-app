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
    internal class MarkdownToFlowDocumentConverter : IMultiValueConverter
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
            {
                ApplyQuoteForeground(document, solidBrush.Color);
                ApplyHeadingForeground(document, solidBrush.Color);
            }
            ApplyCodeBackground(document, cardIsDark);
            ApplyInlineCodeBackground(document, cardIsDark);
            return document;
        }

        /// <summary>
        /// Walks the document looking for heading Paragraphs and overrides their Foreground to match
        /// the resolved text color, so headings aren't always black regardless of the card's
        /// background.
        /// </summary>
        private static void ApplyHeadingForeground(FlowDocument document, Color textColor)
        {
            Brush textBrush = new SolidColorBrush(textColor);
            ForEachParagraph(document, paragraph =>
            {
                // Markdig.Wpf hardcodes headings' Foreground directly on the element (no Style to
                // inspect, unlike quotes/code) — and from debugging, H5/H6 headings otherwise render
                // identically to body text (same FontSize, no TextDecorations), so this exact
                // hardcoded black value is the only marker that works across all heading levels.
                if (paragraph.Foreground is SolidColorBrush brush && brush.Color == Colors.Black)
                    paragraph.Foreground = textBrush;
            });
        }

        /// <summary>
        /// Walks every block in the document looking for quote Sections (identified by their
        /// Markdig-assigned Style, since there's no simpler type/tag to check) and sets their
        /// Foreground to textColor with QuoteAlpha instead of the library's default gray.
        /// </summary>
        private static void ApplyQuoteForeground(FlowDocument document, Color textColor)
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
        private static void ApplyQuoteForegroundRecursive(Block block, Brush dimmedBrush)
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
        /// Walks every Paragraph in the document (recursing into Sections), invoking the given action
        /// on each one — shared traversal logic for ApplyCodeBackground (tagged fenced-code Paragraphs)
        /// and ApplyInlineCodeBackground (Runs with CodeStyleKey inside any Paragraph).
        /// </summary>
        private static void ForEachParagraph(FlowDocument document, Action<Paragraph> action)
        {
            foreach (Block block in document.Blocks)
                ForEachParagraphRecursive(block, action);
        }

        /// <summary>Recurses into Section children looking for Paragraphs; invokes action on each one found.</summary>
        private static void ForEachParagraphRecursive(Block block, Action<Paragraph> action)
        {
            if (block is Paragraph paragraph)
                action(paragraph);
            else if (block is Section section)
            {
                foreach (Block childBlock in section.Blocks)
                    ForEachParagraphRecursive(childBlock, action);
            }
            else if (block is List list)
            {
                foreach (ListItem item in list.ListItems)
                    foreach (Block childBlock in item.Blocks)
                        ForEachParagraphRecursive(childBlock, action);
            }
        }

        /// <summary>
        /// Builds the translucent white/black overlay brush used for code backgrounds, contrasting with the card's own
        /// color.
        /// </summary>
        private static Brush CreateOverlayBrush(bool cardIsDark)
        {
            // lighter color for dark background, darker for light
            // - so even white and black backgrounds have distinct colors for code blocks
            Color overlay = cardIsDark ? Colors.White : Colors.Black;
            Color translucentOverlay = Color.FromArgb(CodeBackgroundAlpha, overlay.R, overlay.G, overlay.B);
            return new SolidColorBrush(translucentOverlay);
        }

        /// <summary>
        /// Walks the document looking for Paragraphs tagged by CodeBlockHighlightRenderer as code blocks, and gives
        /// them a translucent white (dark card) or black (light card) overlay. WPF composites this against the card's
        /// own background at render time, so the code block reads as visually distinct.
        /// </summary>
        private static void ApplyCodeBackground(FlowDocument document, bool cardIsDark)
        { 
            Brush backgroundBrush = CreateOverlayBrush(cardIsDark);
            ForEachParagraph(document, paragraph =>
            {
                if (Equals(paragraph.Tag, CodeBlockHighlightRenderer.CodeBlockTag))
                    paragraph.Background = backgroundBrush;
            });
            
        }

        /// <summary>
        /// Walks the document looking for inline code Runs and gives them the same translucent overlay as
        /// ApplyCodeBackground uses for fenced code blocks, so inline code reads as visually distinct too.
        /// </summary>
        private static void ApplyInlineCodeBackground(FlowDocument document, bool cardIsDark)
        {
            Brush backgroundBrush = CreateOverlayBrush(cardIsDark);
            ForEachParagraph(document, paragraph =>
            {
                foreach (Inline inline in paragraph.Inlines)
                    ApplyInlineCodeBackgroundToInline(inline, backgroundBrush);
            });
        }

        /// <summary>
        /// Recurses into Span-derived inlines (Bold, Italic, Strikethrough, etc.) looking for Runs —
        /// inline code inside e.g. ~~strikethrough~~ text is nested inside that Span, not a direct
        /// child of the Paragraph's Inlines.
        /// </summary>
        private static void ApplyInlineCodeBackgroundToInline(Inline inline, Brush backgroundBrush)
        {
            if (inline is Run run)
            {
                // Inline code Runs have no tag of their own (unlike fenced code block Paragraphs,
                // which CodeBlockHighlightRenderer tags itself) — so instead this looks for any Run
                // whose Style sets Background, which is specific to Markdig.Wpf's CodeStyleKey style
                // and unlikely to appear on any other inline text
                if (run.Style?.Setters.OfType<Setter>().Any(s => s.Property == TextElement.BackgroundProperty) == true)
                    run.Background = backgroundBrush;
            }
            else if (inline is Span span)
            {
                foreach (Inline child in span.Inlines)
                    ApplyInlineCodeBackgroundToInline(child, backgroundBrush);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException("Preview is read-only.");
    }
}