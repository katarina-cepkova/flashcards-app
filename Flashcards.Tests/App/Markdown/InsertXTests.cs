using Flashcards.App.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Tests.App.Markdown
{
    public class InsertXTests
    {
        #region InsertQuote

        [StaFact]
        public void InsertQuote_CaretOnSingleLineWithoutNewline_QuotesThatLine()
        {
            // Arrange — caret inside "ipsum", single-line text, nothing selected.
            var textBox = Helpers.CreateTextBox("Lorem ipsum dolor.", 8);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert
            Assert.Equal("> Lorem ipsum dolor.", textBox.Text);
            Assert.Equal(0, textBox.SelectionStart);
            Assert.Equal("> Lorem ipsum dolor.".Length, textBox.SelectionLength);
        }

        [StaFact]
        public void InsertQuote_CaretOnSingleLineWithNewline_QuotesThatLine()
        {
            // Arrange — caret inside "ipsum", single-line text, nothing selected.
            var textBox = Helpers.CreateTextBox("Lorem ipsum dolor.\r\n", 8); // TextBox inserts \r\n when user presses Enter/Shift+Enter

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert
            Assert.Equal("> Lorem ipsum dolor.\r\n", textBox.Text);
            Assert.Equal(0, textBox.SelectionStart);
            Assert.Equal("> Lorem ipsum dolor.".Length, textBox.SelectionLength);
        }

        [StaFact]
        public void InsertQuote_SelectionMidWord_QuotesWholeLine()
        {
            // Arrange — selection covers only "ipsum", not the whole line — the whole
            // line should still get quoted, not just the selected word.
            var textBox = Helpers.CreateTextBox("Lorem ipsum dolor.", 6, 5);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert
            Assert.Equal("> Lorem ipsum dolor.", textBox.Text);
        }

        [StaFact]
        public void InsertQuote_SelectionSpanningMultipleLines_QuotesEachTouchedLine()
        {
            // Arrange — selection starts on line 1 and ends on line 2 of a 3-line text
            // line 3 must be left untouched.
            string text = "first\nsecond\nthird";
            var textBox = Helpers.CreateTextBox(text, 2, 8);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert — third line unaffected, \n replaced for \r\n
            Assert.Equal("> first\r\n> second\nthird", textBox.Text);
        }

        [StaFact]
        public void InsertQuote_SelectionEndingExactlyAtNewline_DoesNotPullInNextLine()
        {
            string text = "first\r\nsecond";
            int start = 0;
            int end = 5; // selection includes the newline character itself
            var textBox = Helpers.CreateTextBox(text, start, end - start);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert — only the first line is quoted.
            Assert.Equal("> first\r\nsecond", textBox.Text);
        }

        [StaFact]
        public void InsertQuote_CaretOnFirstLineOfMultilineText_HandlesStartOfTextCorrectly()
        {
            // Arrange — caret on the very first line, testing the LastIndexOf('\n', ...)
            // guard for when there's no preceding newline at all.
            var textBox = Helpers.CreateTextBox("first\nsecond\nthird", 2);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert
            Assert.Equal("> first\nsecond\nthird", textBox.Text);
        }

        [StaFact]
        public void InsertQuote_CaretOnLastLineWithNoTrailingNewline_HandlesEndOfTextCorrectly()
        {
            // Arrange — caret on the last line, which has no trailing newline character —
            // tests the IndexOf('\n', ...) == -1 fallback to text.Length.
            var textBox = Helpers.CreateTextBox("first\nsecond\nthird", 13);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert — only the last line is quoted; earlier lines untouched.
            Assert.Equal("first\nsecond\n> third", textBox.Text);
        }

        [StaFact]
        public void InsertQuote_ResultingSelection_CoversWholeQuotedBlock()
        {
            // Arrange
            string text = "first\nsecond\nthird";
            var textBox = Helpers.CreateTextBox(text, 0, 10);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert — selection covers exactly the quoted block, not the original
            // selection's relative position (see InsertQuote's doc comment).
            string quotedBlock = "> first\r\n> second";
            Assert.Equal(0, textBox.SelectionStart);
            Assert.Equal(quotedBlock.Length, textBox.SelectionLength);
        }

        [StaFact]
        public void InsertQuote_CaretAtVeryStartOfText_HandlesZeroPositionCorrectly()
        {
            // Arrange — caret at index 0 exactly, testing Math.Max(0, selectionStart - 1)
            // guard directly (selectionStart - 1 would be -1 without it).
            var textBox = Helpers.CreateTextBox("first\nsecond", 0);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert
            Assert.Equal("> first\nsecond", textBox.Text);
        }

        [StaFact]
        public void InsertQuote_SelectionSpanningBlankLine_QuotesEmptyLineToo()
        {
            // Arrange — selection spans a blank line between two non-empty lines.
            string text = "first\n\nthird";
            var textBox = Helpers.CreateTextBox(text, 0, text.Length);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert — the blank line gets "> " (marker with no content after it), not
            // skipped or left unquoted.
            Assert.Equal("> first\r\n> \r\n> third", textBox.Text);
        }

        [StaFact]
        public void InsertQuote_OnAlreadyQuotedLine_AddsNestedQuoteMarker()
        {
            // Arrange — line is already a blockquote; InsertQuote never checks for this,
            // since "> > text" is valid nested-blockquote markdown, not a mistake to guard against.
            var textBox = Helpers.CreateTextBox("> first", 3);

            // Act
            MarkdownEditingService.InsertQuote(textBox);

            // Assert
            Assert.Equal("> > first", textBox.Text);
        }
        #endregion

        #region InsertLink

        [StaFact]
        public void InsertLink_NoSelection_InsertsEmptyTemplateWithCaretInsideBrackets()
        {
            // Arrange — caret with nothing selected.
            var textBox = Helpers.CreateTextBox("Lorem ipsum.", 6);

            // Act
            MarkdownEditingService.InsertLink(textBox);

            // Assert — empty "[]()" template inserted, caret lands inside the "[]"
            // since there's no text yet to serve as the link text.
            Assert.Equal("Lorem []()ipsum.", textBox.Text);
            Assert.Equal(7, textBox.SelectionStart);
            Assert.Equal(0, textBox.SelectionLength);
        }

        [StaFact]
        public void InsertLink_WithSelection_WrapsSelectedTextAsLinkText()
        {
            // Arrange — "ipsum" selected.
            var textBox = Helpers.CreateTextBox("Lorem ipsum dolor.", 6, 5);

            // Act
            MarkdownEditingService.InsertLink(textBox);

            // Assert — selected text becomes the link text, caret lands inside the
            // empty "()" ready to type the URL.
            Assert.Equal("Lorem [ipsum]() dolor.", textBox.Text);
            Assert.Equal(14, textBox.SelectionStart); // right after "[ipsum]("
            Assert.Equal(0, textBox.SelectionLength);
        }

        #endregion

        #region InsertCodeBlock
        [StaFact]
        public void InsertCodeBlock_NoSelection_InsertsEmptyBlock()
        {
            // Arrange — caret with nothing selected.
            var textBox = Helpers.CreateTextBox("Lorem ipsum.", 6);

            // Act
            MarkdownEditingService.InsertCodeBlock(textBox);

            // Assert
            Assert.Equal("Lorem \r\n```\r\n\r\n```ipsum.", textBox.Text);
            Assert.Equal(15, textBox.SelectionStart);
            Assert.Equal(0, textBox.SelectionLength);
        }

        [StaFact]
        public void InsertCodeBlock_WithSelection_WrapsSelectedTextInBlock()
        {
            // Arrange — "ipsum" selected.
            var textBox = Helpers.CreateTextBox("Lorem ipsum dolor.", 6, 5);

            // Act
            MarkdownEditingService.InsertCodeBlock(textBox);

            // Assert — selected text sits between the fences unchanged, and stays selected.
            Assert.Equal("Lorem \r\n```\r\nipsum\r\n``` dolor.", textBox.Text);
            Assert.Equal(6 + "\r\n```\r\n".Length, textBox.SelectionStart);
            Assert.Equal(5, textBox.SelectionLength);
        }

        [StaFact]
        public void InsertCodeBlock_WithMultilineSelection_LeavesInternalLineBreaksUntouched()
        {
            // Arrange — a selection that already spans two lines.
            string selected = "first\nsecond";
            var textBox = Helpers.CreateTextBox($"Lorem {selected} dolor.", 6, selected.Length);

            // Act
            MarkdownEditingService.InsertCodeBlock(textBox);

            // Assert — unlike InsertQuote, no per-line prefixing happens; the selection's
            // own \n is preserved exactly as-is inside the fences.
            Assert.Equal($"Lorem \r\n```\r\n{selected}\r\n``` dolor.", textBox.Text);
        }


        #endregion
    }
}
