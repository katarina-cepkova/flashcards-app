using ColorCode;
using ColorCode.Wpf;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;
using Markdig.Syntax;
using System.Text;
using System.Windows.Documents;

namespace Flashcards.App.Services
{
    /// <summary>
    /// Replaces Markdig.Wpf's default CodeBlockRenderer for fenced code blocks, applying
    /// ColorCode syntax highlighting based on the block's language identifier (its Info string,
    /// e.g. "csharp"). Tags the resulting Paragraph so MarkdownToFlowDocumentConverter can find
    /// it afterward to apply the card-matching background, without needing to guess via Style
    /// inspection like the quote-block handling does.
    /// </summary>
    internal class CodeBlockHighlightRenderer : WpfObjectRenderer<CodeBlock>
    {
        /// <summary>Marker used to identify this Paragraph as a code block afterward.</summary>
        public const string CodeBlockTag = "MarkdownCodeBlock";

        // One formatter per theme, built once and reused across every code block rendered by
        // this instance — StyleDictionary.DefaultLight/DefaultDark supply ColorCode's full
        // token color set (keywords, strings, comments, etc.), unlike the parameterless
        // constructor, which only colors keywords.
        private readonly RichTextBoxFormatter _lightFormatter = new(ColorCode.Styling.StyleDictionary.DefaultDark);
        private readonly RichTextBoxFormatter _darkFormatter = new(ColorCode.Styling.StyleDictionary.DefaultLight);

        /// <summary>Set before each render by the converter, which knows the card's resolved text color.</summary>
        public bool UseDarkTheme { get; set; }

        /// <summary>
        /// Renders a code block (fenced or plain) as a single Paragraph. If the block has a known
        /// language identifier (e.g. ```csharp), its text is syntax-highlighted via ColorCode using
        /// the current theme (UseDarkTheme); otherwise the text is written plain — no language given,
        /// or the given language isn't one ColorCode recognizes.
        /// </summary>
        protected override void Write(WpfRenderer renderer, CodeBlock obj)
        {
            // Tag marks this WPF Paragraph as a code block, so MarkdownToFlowDocumentConverter
            // can find it afterward (to set its background) without guessing via Style inspection.
            Paragraph paragraph = new Paragraph
            {
                Tag = CodeBlockTag,
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };

            string codeText = ExtractText(obj);

            // A fenced block (```csharp) is parsed as FencedCodeBlock, with Info holding the
            // language string. A plain indented code block has no language at all.
            string? languageId = (obj as FencedCodeBlock)?.Info;
            RichTextBoxFormatter formatter = UseDarkTheme ? _lightFormatter : _darkFormatter;

            if (string.IsNullOrWhiteSpace(languageId))
            {
                // No language specified — nothing to highlight against.
                paragraph.Inlines.Add(new Run(codeText));
            }
            else
            {
                ILanguage? language = Languages.FindById(languageId);
                if (language is not null)
                    // Recognized language — apply syntax highlighting.
                    formatter.FormatInlines(codeText, language, paragraph.Inlines);
                else
                    // Language string given but not recognized by ColorCode — show plain text
                    // rather than silently dropping the highlighting attempt.
                    paragraph.Inlines.Add(new Run(codeText));
            }

            renderer.WriteBlock(paragraph);
        }

        /// <summary>
        /// Joins a code block's lines into a single string. CodeBlock.Lines holds only the content
        /// between the fences (Markdig strips the ``` fence markers themselves during parsing) as
        /// StringSlice references into the original markdown text, not copied strings. A newline is
        /// inserted between lines (not after each one), so the result has no trailing blank line.
        /// </summary>
        private static string ExtractText(CodeBlock obj)
        {
            var sb = new StringBuilder();
            var lines = obj.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                // Slice is a lightweight reference (start/end index) into the original markdown
                // text, not a copied string — ToString() here is what actually materializes it.
                sb.Append(lines.Lines[i].Slice.ToString());

                if (i != lines.Count - 1)
                    sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}