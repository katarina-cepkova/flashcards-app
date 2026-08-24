using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Text;
using System.Windows.Controls;

namespace Flashcards.App.Services
{
    /// <summary>
    /// Handles inserting/removing markdown emphasis markers (bold, italic, strikethrough)
    /// around the current selection or cursor position in a plain TextBox.
    /// </summary>
    public static class MarkdownEditingService
    {
        /// <summary>
        /// Toggles the given markdown marker (e.g. "**" for bold) on the current selection
        /// or caret position. If the caret/selection is already inside a matching emphasis
        /// span, the markers are removed; otherwise they're inserted around the selection
        /// (or the whole word under the caret, if nothing is selected).
        /// </summary>
        public static void ToggleEmphasis(TextBox textBox, string marker)
        {
            string text = textBox.Text;
            int selectionStart = textBox.SelectionStart;
            int selectionEnd = selectionStart + textBox.SelectionLength;

            MarkdownDocument document = Markdown.Parse(text, MarkdownPipelineProvider.Pipeline);
            var enclosing = FindEnclosingEmphasis(document, selectionStart, selectionEnd, marker);

            if (enclosing is null)
                InsertMarkers(textBox, selectionStart, selectionStart + textBox.SelectionLength, marker);
            
            else
                RemoveMarkers(textBox, enclosing.Value, marker);
            
        }

        /// <summary>
        /// Wraps the given range in the text with the given marker (e.g. "**"), inserting
        /// an opening and closing pair. If nothing is selected (start == end), the caret's
        /// surrounding word is used instead, so clicking a formatting button with the caret
        /// resting inside a word formats that whole word rather than inserting an empty pair
        /// of markers at the caret.
        /// </summary>
        private static void InsertMarkers(TextBox textBox, int start, int end, string marker)
        {
            string text = textBox.Text;

            // Insert at `end` first — inserting at `start` first would shift every index
            // after it, and `end` would no longer point at the right place.
            string updated = text.Insert(end, marker).Insert(start, marker);
            textBox.Text = updated;

            // Select the whole word/range that was just wrapped, shifted right by
            // marker.Length since the opening marker now sits before it. This also
            // covers the caret-only case: even if nothing was selected before, the
            // whole word ends up selected after formatting.
            textBox.SelectionStart = start + marker.Length;
            textBox.SelectionLength = end - start;
        }

        /// <summary>
        /// Removes a marker pair (e.g. "**") from around the given range. The range is
        /// expected to include the markers themselves (as returned by FindEnclosingEmphasis),
        /// not just the text between them.
        /// </summary>
        private static void RemoveMarkers(TextBox textBox, (int start, int end) selection, string marker)
        {
            string text = textBox.Text;
            int markerLength = marker.Length;

            // Remove the closing marker first — removing the opening one first would
            // shift `selection.end`, since everything after it moves left.
            string updated = text.Remove(selection.end - markerLength, markerLength)
                                 .Remove(selection.start, markerLength);
            textBox.Text = updated;

            // Restore the selection over the original word, now marker-free.
            textBox.SelectionStart = selection.start;
            textBox.SelectionLength = Math.Max(0, selection.end - selection.start - (2 * markerLength));
        }


        /// <summary>
        /// Walks the parsed AST looking for an EmphasisInline node (matched by delimiter
        /// character and count) whose span contains the given position — this is what gives
        /// us correct nesting handling for free, instead of writing a custom tokenizer.
        /// </summary>
        /*
        Markdig parses text into a tree, not a flat list. For example:
        
          This is **bold *and italic* word**.
        
        becomes roughly:
        
        MarkdownDocument
          └── ParagraphBlock
              ├── LiteralInline "This is "
              └── EmphasisInline (bold, DelimiterCount=2)
                  ├── LiteralInline "bold "
                  ├── EmphasisInline (italic, DelimiterCount=1)
                  │   └── LiteralInline "and italic"
                  └── LiteralInline " word"
        
        Descendants<EmphasisInline>() walks this tree recursively (at any nesting
        depth) and returns every EmphasisInline node — here, both the bold node and
        the nested italic node — as one flat sequence, so we don't have to walk the
        tree by hand.
        
        Note: an EmphasisInline's own children are the *plain text* it wraps (its
        markers are not stored as content) — the "**"/"*" characters only exist as
        coordinates in emphasis.Span, pointing back into the original raw string.
        That's exactly what we need here, since we're about to edit that raw string,
        not the parsed tree.
        */
        private static (int start, int end)? FindEnclosingEmphasis(MarkdownDocument document, int start, int end, string marker)
        {
            char delimiterChar = marker[0];
            int delimiterLength = marker.Length; // 1 for italic, 2 for bold or strikethrough

            IEnumerable<EmphasisInline> emphases = document.Descendants<EmphasisInline>();

            foreach (EmphasisInline emphasis in emphases)
            {
                // skip nodes with different delimiter (e.g. italic when looking for bold)
                if (emphasis.DelimiterChar != delimiterChar || emphasis.DelimiterCount != delimiterLength)
                    continue;

                // emphasis.Span covers the whole segment including its markers (e.g. both
                // "**" pairs around "bold"), which is why span.End + 1 is used as the
                // exclusive end below
                int spanStart = emphasis.Span.Start;
                int spanEnd = emphasis.Span.End + 1;

                // The entire selection must fit inside this emphasis span — not just its
                // start — otherwise a selection that only partially overlaps existing
                // markers (e.g. starts in plain text, ends inside a bold word) would be
                // wrongly identified as "already fully formatted".
                if (spanStart <= start && end <= spanEnd)
                    return (spanStart, spanEnd);
            }
            return null;
        }


        /// <summary>
        /// Clears the TextBox's undo/redo history. Call whenever the displayed content changes
        /// to something unrelated to what was there before (switching cards, flipping side,
        /// opening a different set), so Undo can't reach back into content that's no longer showing.
        /// </summary>
        public static void ClearUndoHistory(TextBox textBox)
        {
            textBox.IsUndoEnabled = false;
            textBox.IsUndoEnabled = true;
        }
    }
}
