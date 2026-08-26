using System.Windows.Controls;

namespace Flashcards.App.Services
{
    /// <summary>
    /// Handles inserting/removing markdown emphasis markers (bold, italic, strikethrough) around the current
    /// selection or cursor position in a plain TextBox.
    /// Positioning is done by scanning the raw text directly rather than relying on Markdig's AST —
    /// EmphasisInline.Span is unreliable in this Markdig version (confirmed via direct testing: it always returns
    /// 0,0), so parsing the document can't tell us where markers actually sit in the source text.
    /// </summary>
    public static class MarkdownEditingService
    {
        /// <summary>
        /// Reports whether the caret/selection currently sits inside a marker pair matching
        /// `marker` — i.e. what ToggleEmphasis would remove if clicked right now. Used to drive
        /// toggle button active state (IsChecked), so it mirrors ToggleEmphasis's own start/end
        /// resolution (word-boundary expansion when nothing is selected) rather than introducing
        /// a second, possibly inconsistent notion of "formatted".
        /// </summary>
        public static bool IsMarkerActive(TextBox textBox, string marker)
        {
            string text = textBox.Text;
            int start = textBox.SelectionStart;
            int end = start + textBox.SelectionLength;

            if (start == end)
                (start, end) = ExpandToWordBoundaries(text, start);

            return FindEnclosing(text, start, end, marker) is not null;
        }


        /// <summary>
        /// Toggles the given markdown marker (e.g. "**" for bold) on the current selection or caret position. If the
        /// caret/selection is already inside a matching marker pair, the markers are removed; otherwise they're
        /// inserted around the selection (or the whole word under the caret, if nothing is selected).
        /// </summary>
        public static void ToggleEmphasis(TextBox textBox, string marker)
        {
            string text = textBox.Text;
            int selectionStart = textBox.SelectionStart;
            int selectionEnd = selectionStart + textBox.SelectionLength;

            // No selection — expand to the boundaries of the word the caret is
            // resting inside, so the whole word gets wrapped, not an empty pair
            // of markers dropped right at the caret.
            if (selectionStart == selectionEnd)
                (selectionStart, selectionEnd) = ExpandToWordBoundaries(textBox.Text, selectionStart);

            var enclosing = FindEnclosing(text, selectionStart, selectionEnd, marker);

            if (enclosing is null)
                InsertMarkers(textBox, selectionStart, selectionEnd, marker);

            else
                RemoveMarkers(textBox, enclosing.Value, marker);

        }

        /// <summary>
        /// Wraps the given range in the text with the given marker (e.g. "**"), inserting an opening and closing pair.
        /// </summary>
        private static void InsertMarkers(TextBox textBox, int start, int end, string marker)
        {
            // Replace the whole range in a single SelectedText assignment (rather than two
            // separate inserts) — this is what makes WPF record the entire toggle as ONE
            // undoable edit instead of two, so a single Ctrl+Z undoes the whole operation.
            // SelectedText (instead of replacing the whole Text property) is what makes WPF
            // reliably record this as an undoable edit at all, the way a real keystroke would.
            string original = textBox.Text.Substring(start, end - start);
            textBox.Select(start, end - start);
            textBox.SelectedText = $"{marker}{original}{marker}";

            textBox.SelectionStart = start + marker.Length;
            textBox.SelectionLength = end - start;
        }

        /// <summary>
        /// Removes a marker pair (e.g. "**") from around the given range. The range is
        /// expected to include the markers themselves (as returned by FindEnclosing),
        /// not just the text between them.
        /// </summary>
        private static void RemoveMarkers(TextBox textBox, (int start, int end) selection, string marker)
        {
            int markerLength = marker.Length;

            // Same single-assignment approach as InsertMarkers — replace the whole range
            // (markers included) with just the inner content in one SelectedText write,
            // so removal is recorded as one undoable edit too.
            string inner = textBox.Text.Substring(selection.start + markerLength, selection.end - selection.start - 2 * markerLength);
            textBox.Select(selection.start, selection.end - selection.start);
            textBox.SelectedText = inner;

            // The raw content now sits where the markers used to be, shifted by the
            // removed opening marker's length.
            int rawStart = selection.start;
            int rawEnd = rawStart + inner.Length;
            (rawStart, rawEnd) = StripEdgeMarkers(textBox.Text, rawStart, rawEnd);

            // Restore the selection over the raw word
            textBox.SelectionStart = rawStart;
            textBox.SelectionLength = rawEnd - rawStart;
        }

        /// <summary>
        /// Repeatedly strips one character of matching delimiter markers from each edge of the given range — e.g. for
        /// "*italic*", peels the surrounding "*" to leave just "italic". Only used to determine where to place the
        /// selection after RemoveMarkers, not to modify any text, so it doesn't need to reason about run lengths the
        /// way FindEnclosing does: RemoveMarkers only ever produces text we generated ourselves, where matching edges
        /// are always the same delimiter character, so comparing one character at a time is enough. Stops as soon as
        /// the edges no longer match or aren't delimiter characters.
        /// Used because RemoveMarkers' resulting selection covers whatever text remains after removing one layer —
        /// which may itself still be wrapped in further layers of markers (e.g. after removing "~~" from
        /// "~~*italic*~~", the remaining "*italic*" still has "*" at its edges) — so the caller can select just the raw
        /// word underneath.
        /// </summary>
        private static (int Start, int End) StripEdgeMarkers(string text, int start, int end)
        {
            while (end - start >= 2 && text[start] == text[end - 1] && IsDelimiterChar(text[start])) {
                start++;
                end--;
            }
            return (start, end);
        }


        /// <summary>
        /// Finds the start/end indices of the word surrounding the given caret position, treating whitespace and
        /// punctuation (which includes markdown delimiter characters like "*"/"~"/"_") as word boundaries. As a side
        /// effect, this means expansion naturally stops at the nearest layer of existing markers rather than reaching
        /// through them — which is what makes nested emphasis "the user's problem" to resolve via an explicit
        /// selection, rather than something this code guesses at.
        /// </summary>
        private static (int Start, int End) ExpandToWordBoundaries(string text, int position)
        {
            int start = position;
            while (start > 0 && !char.IsWhiteSpace(text[start - 1])
                && !char.IsPunctuation(text[start - 1])
                && !IsDelimiterChar(text[start - 1]))
                start--;

            int end = position;
            while (end < text.Length && !char.IsWhiteSpace(text[end])
                && !char.IsPunctuation(text[end])
                && !IsDelimiterChar(text[end]))
                end++;

            return (start, end);
        }
        private static bool IsDelimiterChar(char c) => c == '*' || c == '~' || c == '_' || c == '`';

        /// <summary>
        /// CommonMark treats "*" and "_" (and their doubled forms "**"/"__") as
        /// interchangeable emphasis delimiters — both represent the same semantic
        /// meaning (italic for single, bold for double). This lets FindEnclosingMarker
        /// recognize italic/bold the user typed by hand with "_" even though our own
        /// buttons only ever generate "*".
        /// </summary>
        private static bool AreEquivalentDelimiters(char a, char b) =>
            a == b || (a is '*' or '_' && b is '*' or '_');


        /// <summary>
        /// Scans backward from `position`, grouping consecutive runs of the same delimiter
        /// character (e.g. "**", "~~", "*") into ordered tokens — innermost (closest to the
        /// word) first. Each token records its character, length, and absolute position in
        /// the text, so a match can be removed directly without re-scanning.
        /// </summary>
        private static List<(char Delimiter, int Length, int Start, int End)> TokenizeMarkersLeft(string text, int position)
        {
            var tokens = new List<(char delimiter, int length, int start, int end)>();
            int i = position;

            while (i > 0 && IsDelimiterChar(text[i - 1]))
            {
                char delimiter = text[i - 1];
                int end = i;
                int start = i - 1;
                while (start > 0 && text[start - 1] == delimiter)
                    start--;

                tokens.Add((delimiter, end - start, start, end));
                i = start;
            }

            return tokens;
        }

        /// <summary>
        /// Same as TokenizeMarkersLeft, but scanning forward from `position` — used for the
        /// closing side of a range.
        /// </summary>
        private static List<(char Delimiter, int Length, int Start, int End)> TokenizeMarkersRight(string text, int position)
        {
            var tokens = new List<(char, int, int, int)>();
            int i = position;

            while (i < text.Length && IsDelimiterChar(text[i]))
            {
                char delimiter = text[i];
                int start = i;
                int end = i + 1;
                while (end < text.Length && text[end] == delimiter)
                    end++;

                tokens.Add((delimiter, end - start, start, end));
                i = end;
            }

            return tokens;
        }

        /// <summary>
        /// Looks for the given marker anywhere in the block of delimiter characters
        /// surrounding the given range, matching it against the delimiter at the same
        /// depth on the opposite side (e.g. for "**~~*word*~~**", looking for "~~" finds
        /// it at depth 1 on both sides, even though "*" sits closer to the word). Depth
        /// must match on both sides — a marker found at depth 1 on the left is only a
        /// valid pair if the same marker sits at depth 1 on the right, not depth 0 or 2.
        ///
        /// This replaces relying on Markdig's AST for positioning — EmphasisInline.Span
        /// is unreliable in this Markdig version (confirmed via direct testing, it always
        /// returns 0,0) — with a direct scan of the raw text instead.
        /// </summary>
        private static (int Start, int End)? FindEnclosing(string text, int start, int end, string marker)
        {
            var leftTokens = TokenizeMarkersLeft(text, start);
            var rightTokens = TokenizeMarkersRight(text, end);
            int markerLength = marker.Length;


            int depth = Math.Min(leftTokens.Count, rightTokens.Count);

            for (int i = 0; i < depth; i++)
            {
                var left = leftTokens[i];
                var right = rightTokens[i];

                if (left.Delimiter == right.Delimiter && left.Length == right.Length && AreEquivalentDelimiters(left.Delimiter, marker[0]))
                {
                    int length = left.Length;
                    // Our markers are only ever 1 or 2 characters long, so the only run
                    // length that can represent "two markers sharing an edge" (e.g. "***"
                    // = "**" + "*") is 3 — always odd. An odd-length run at least as long
                    // as the requested marker is treated as containing it: peeling
                    // `markerLength` characters off the edge nearest the word leaves the
                    // other marker type intact on the outside (e.g. "***bold***" toggling
                    // italic leaves the bold "**" in place).
                    bool isOddLength = length % 2 != 0;
                    if ((isOddLength && markerLength <= length))
                    {
                        return (left.Start + (length - markerLength), right.End - (length - markerLength));
                    }
                    // An even-length run only matches if it's exactly the marker we're
                    // looking for — e.g. "**" is bold, not "bold containing italic", so
                    // looking for "*" against a "**" run must NOT match here (it falls
                    // through to the `else` below, and InsertMarkers adds "*" around it).
                    else if (!isOddLength && markerLength == length)
                    {
                        return (left.Start, right.End);
                    }
                    // else: this layer exists and is the right delimiter character, but its
                    // length can't represent the requested marker (e.g. length 1 when looking
                    // for length 2) — not a match at THIS depth, but an outer layer still might
                    // match, so keep looking rather than giving up entirely.

                }
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

        /// <summary>
        /// Finds the start index of the line containing the given position — the character
        /// right after the nearest preceding newline, or 0 if the position is on the first
        /// line.
        /// </summary>
        private static int FindParagraphStart(string text, int start)
        {
            return text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
        }

        /// <summary>
        /// Inserts a "> " blockquote prefix on every line touched by the current selection (or just the caret's line,
        /// if nothing is selected) — including lines only partially covered by the selection, e.g. a selection starting
        /// mid-line still quotes that whole line. Always inserts — doesn't check whether a line is already a blockquote
        /// (nested quotes are valid markdown syntax). Uses a single SelectedText write covering the whole affected
        /// range, so the entire operation is recorded as one undoable edit. Leaves the whole quoted block selected
        /// afterward, rather than trying to preserve the original selection's exact position across the now-longer
        /// lines.
        /// </summary>
        public static void InsertQuote(TextBox textBox)
        {
            string text = textBox.Text;
            int selectionStart = textBox.SelectionStart;
            int selectionEnd = selectionStart + textBox.SelectionLength;

            // finding the range of affected lines
            // using \n instead of \r\n for finding to catch all newlines
            int rangeStart = FindParagraphStart(text, selectionStart);
            int newlineIndex = text.IndexOf('\n', selectionEnd);
            int rangeEnd;
            if (newlineIndex == -1)
                rangeEnd = text.Length;
            else
                rangeEnd = (newlineIndex > 0 && text[newlineIndex - 1] == '\r') ? newlineIndex - 1 : newlineIndex;

            IEnumerable<string> lines = text.Substring(rangeStart, rangeEnd - rangeStart)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            string quoted = string.Join("\r\n", lines.Select(line => "> " + line));
            textBox.Select(rangeStart, rangeEnd - rangeStart);
            textBox.SelectedText = quoted;
            textBox.SelectionStart = rangeStart;
            textBox.SelectionLength = quoted.Length;

        }


        /// <summary>
        /// Inserts a markdown link. With a selection, the selected text becomes the link
        /// text (wrapped in "[...]"), followed by an empty "()" for the URL, with the caret
        /// landing inside the parentheses ready to type the URL. With no selection, an empty
        /// "[]()" template is inserted, with the caret landing inside the "[]" instead, since
        /// there's no text yet to link from.
        /// </summary>
        public static void InsertLink(TextBox textBox)
        {
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;

            // only caret
            if (selectionLength == 0)
            {
                textBox.Select(selectionStart, 0);
                textBox.SelectedText = "[]()";
                textBox.SelectionStart = selectionStart + 1; // cursor inside the []
                textBox.SelectionLength = 0;
            }
            else
            {
                string selectedText = textBox.SelectedText;
                textBox.Select(selectionStart, selectionLength);
                textBox.SelectedText = $"[{selectedText}]()";
                textBox.SelectionStart = selectionStart + selectedText.Length + 3; // cursor inside the ()
                textBox.SelectionLength = 0;
            }
        }

        /// <summary>
        /// Inserts a fenced code block. With no selection, an empty block is inserted at the
        /// caret, with the caret left on the blank line between the fences, ready to type.
        /// With a selection, the selected text (which may already span multiple lines) is
        /// wrapped inside the fences as-is — unlike InsertQuote, there's no per-line prefixing
        /// here, so the selection's own line breaks are left untouched. Always inserts —
        /// doesn't check whether the range is already inside a code block. Writes via a single
        /// SelectedText assignment so the whole operation is recorded as one undoable edit.
        /// </summary>
        public static void InsertCodeBlock(TextBox textBox)
        {
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;

            // only caret
            if (selectionLength == 0)
            {
                textBox.Select(selectionStart, 0);
                textBox.SelectedText = "\r\n```\r\n\r\n```";
                // The +1 here is not a miscount — "\r\n```\r\n".Length lands the caret
                // between the two \r\n pairs, but WPF renders that exact position as the
                // END of the previous line rather than the start of the blank line between
                // the fences. The extra +1 nudges the caret past the leading \r of the second
                // pair, which WPF does render as sitting on the blank line.
                textBox.SelectionStart = selectionStart + "\r\n```\r\n".Length + 1;
                textBox.SelectionLength = 0;
            }
            else
            {
                string selectedText = textBox.SelectedText;
                textBox.Select(selectionStart, selectionLength);
                textBox.SelectedText = $"\r\n```\r\n{selectedText}\r\n```";
                textBox.SelectionStart = selectionStart + "\r\n```\r\n".Length;
                textBox.SelectionLength = selectionLength;
            }
        }


        /// <summary>
        /// Returns the heading level of the line the caret/selection is on: 0 for plain text (including a line with
        /// more than 6 leading '#' characters, or '#' characters with no space after them), 1-6 for H1-H6.
        /// </summary>
        public static int GetHeadingLevel(TextBox textBox)
        {
            int selectionStart = textBox.SelectionStart;
            string text = textBox.Text;
            int paragraphStart = FindParagraphStart(text, selectionStart);

            // CommonMark defines ATX headings as 1-6 '#' characters — a 7th '#' means the
            // line is no longer a valid heading at all, just plain text starting with '#'.
            int headingLevel = 0;
            int position = paragraphStart;
            while (position < text.Length && text[position] == '#' && headingLevel < 6)
            {
                headingLevel++;
                position++;
            }

            // CommonMark requires a space after the '#' run for it to count as a heading
            // at all — e.g. "###word" is plain text, not H3. No '#' at all means headingLevel
            // == 0 -> "no space to check".
            bool hasTrailingSpace = position < text.Length && text[position] == ' ';

            return hasTrailingSpace ? headingLevel : 0;
        }

        /// <summary>
        /// Sets the heading level of the line the caret/selection is on to exactly
        /// `targetLevel` (0 for plain text, 1-6 for H1-H6), replacing whatever heading
        /// marker (if any) is currently there.
        /// </summary>
        private static void SetHeadingLevel(TextBox textBox, int targetLevel)
        {
            if (targetLevel is < 0 or > 6)
                throw new ArgumentOutOfRangeException(nameof(targetLevel), 
                    targetLevel, "Heading level must be between 0 and 6.");

            string text = textBox.Text;
            int selectionStart = textBox.SelectionStart;
            int lineStart = FindParagraphStart(text, selectionStart);

            // Only treat the leading '#' run as an existing marker to replace if it's a
            // VALID heading (has a trailing space) — an invalid run like "#####abc" (no
            // space) is left untouched as plain text content, and the new marker is
            // inserted before it instead of eating into what the user actually typed.
            int currentLevel = GetHeadingLevel(textBox);
            int charCountToReplace = 0;

            if (currentLevel > 0)
            {
                int position = lineStart;
                while (text[position] == '#')
                {
                    charCountToReplace++;
                    position++;
                }
                // the trailing space, guaranteed present since currentLevel > 0
                charCountToReplace++;
            }

            string newMarker = targetLevel == 0 ? "" : new string('#', targetLevel) + ' ';

            textBox.Select(lineStart, charCountToReplace);
            textBox.SelectedText = newMarker;

            textBox.SelectionStart = lineStart + newMarker.Length;
            textBox.SelectionLength = 0;
        }

        /// <summary>
        /// Increases heading SIZE (Upsize button) — fewer '#' means a BIGGER heading in
        /// markdown (H1 is the largest, H6 the smallest), so "increasing size" means
        /// DECREASING the '#' count, moving toward H1. From plain text (level 0), jumps
        /// straight to H6 (the smallest). No-ops if already at H1, as a defensive guard 
        /// independent of the button's IsEnabled state (e.g. in case of a rapid click
        /// before IsEnabled updates).
        /// </summary>
        public static void IncreaseHeadingLevel(TextBox textBox)
        {
            int headingLevel = GetHeadingLevel(textBox);
            // Already maximum — this is a defensive guard, not a case expected to actually
            // happen: EditTextBox_OnSelectionChanged is expected to disable the button once
            // headingLevel == 1, so in practice this method should never be called with it.
            if (headingLevel == 1)
                return;
            else if (headingLevel == 0)
                SetHeadingLevel(textBox, 6);
            else
                SetHeadingLevel(textBox, headingLevel - 1);
        }

        /// <summary>
        /// Decreases heading SIZE (Downsize button) — more '#' means a SMALLER heading, so "decreasing size" means
        /// INCREASING the '#' count, moving toward H6, and then past it back to plain text. No-ops if already at plain
        /// text, as a defensive guard independent of the button's IsEnabled state (e.g. in case of a rapid click before
        /// IsEnabled updates).
        /// </summary>
        public static void DecreaseHeadingLevel(TextBox textBox)
        {
            int headingLevel = GetHeadingLevel(textBox);
            // Already minimum — this is a defensive guard, not a case expected to actually
            // happen: EditTextBox_OnSelectionChanged is expected to disable the button once
            // headingLevel == 0, so in practice this method should never be called with it.
            if (headingLevel == 0)
                return;
            else if (headingLevel == 6)
                SetHeadingLevel(textBox, 0); // switching to plain text
            else
                SetHeadingLevel(textBox, headingLevel + 1);
        }

    }
}
