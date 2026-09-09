using Flashcards.App.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Tests.App.Markdown
{
    public class HeadingTests
    {
        // ---------- GetHeadingLevel ----------

        [StaTheory]
        [InlineData("Heading", 0)]
        [InlineData("# Heading", 1)]
        [InlineData("## Heading", 2)]
        [InlineData("### Heading", 3)]
        [InlineData("#### Heading", 4)]
        [InlineData("##### Heading", 5)]
        [InlineData("###### Heading", 6)]
        public void GetHeadingLevel_ValidHeading_ReturnsCorrectLevel(string line, int expectedLevel)
        {
            // Arrange
            var textBox = Helpers.CreateTextBox(line, 0);

            // Act
            int level = MarkdownEditingService.GetHeadingLevel(textBox);

            // Assert
            Assert.Equal(expectedLevel, level);
        }


        [StaFact]
        public void GetHeadingLevel_HashesWithoutTrailingSpace_ReturnsZero()
        {
            // Arrange — "###word" is not a valid heading per CommonMark, no space after '#'.
            var textBox = Helpers.CreateTextBox("###word", 0);

            // Act
            int level = MarkdownEditingService.GetHeadingLevel(textBox);

            // Assert
            Assert.Equal(0, level);
        }

        [StaFact]
        public void GetHeadingLevel_MoreThanSixHashes_ReturnsZero()
        {
            // Arrange — CommonMark caps ATX headings at 6 '#'; a 7th makes it plain text.
            var textBox = Helpers.CreateTextBox("####### Heading", 0);

            // Act
            int level = MarkdownEditingService.GetHeadingLevel(textBox);

            // Assert
            Assert.Equal(0, level);
        }

        [StaFact]
        public void GetHeadingLevel_OnlyHashesNoContent_ReturnsZero()
        {
            // Arrange — a line that's ONLY '#' characters, nothing after them at all
            // (not even a trailing space) — position could land exactly at text.Length.
            var textBox = Helpers.CreateTextBox("###", 0);

            // Act
            int level = MarkdownEditingService.GetHeadingLevel(textBox);

            // Assert
            Assert.Equal(0, level);
        }

        [StaFact]
        public void GetHeadingLevel_UsesLineContainingCaret_NotFirstLine()
        {
            // Arrange — caret on the second line, which is a heading; first line is not.
            string text = "Plain text\n## Second line heading";
            var textBox = Helpers.CreateTextBox(text, 16);

            // Act
            int level = MarkdownEditingService.GetHeadingLevel(textBox);

            // Assert
            Assert.Equal(2, level);
        }

        // ---------- IncreaseHeadingLevel ----------

        [StaTheory]
        [InlineData(0, 6)]
        [InlineData(6, 5)]
        [InlineData(5, 4)]
        [InlineData(4, 3)]
        [InlineData(3, 2)]
        [InlineData(2, 1)]
        public void IncreaseHeadingLevel_FromExistingHeading(int startLevel, int expectedLevel)
        {
            // Arrange
            string delimiter = startLevel == 0 ? "" : " ";
            string line = new string('#', startLevel) + $"{delimiter}Heading";
            var textBox = Helpers.CreateTextBox(line, 0);

            // Act
            MarkdownEditingService.IncreaseHeadingLevel(textBox);

            // Assert
            string expected = new string('#', expectedLevel) + " Heading";
            Assert.Equal(expected, textBox.Text);
        }

        [StaFact]
        public void IncreaseHeadingLevel_AlreadyAtH1_IsNoOp()
        {
            // Arrange
            var textBox = Helpers.CreateTextBox("# Heading", 0);

            // Act
            MarkdownEditingService.IncreaseHeadingLevel(textBox);

            // Assert — defensive guard, text unchanged.
            Assert.Equal("# Heading", textBox.Text);
        }

        [StaFact]
        public void IncreaseHeadingLevel_InvalidHashRunWithoutSpace_InsertsWithoutConsumingIt()
        {
            // Arrange — "#####abc" has no space, so GetHeadingLevel treats it as plain
            // text (level 0); the existing '#' run must be left untouched, not eaten by
            // the new marker.
            var textBox = Helpers.CreateTextBox("#####abc", 0);

            // Act
            MarkdownEditingService.IncreaseHeadingLevel(textBox);

            // Assert
            Assert.Equal("###### #####abc", textBox.Text);
        }

        // ---------- DecreaseHeadingLevel ----------

        [StaTheory]
        [InlineData(1, 2)]
        [InlineData(2, 3)]
        [InlineData(3, 4)]
        [InlineData(4, 5)]
        [InlineData(5, 6)]
        [InlineData(6, 0)]
        public void DecreaseHeadingLevel_FromExistingHeading(int startLevel, int expectedLevel)
        {
            // Arrange
            string line = new string('#', startLevel) + " Heading";
            var textBox = Helpers.CreateTextBox(line, 0);

            // Act
            MarkdownEditingService.DecreaseHeadingLevel(textBox);

            // Assert
            string delimiter = expectedLevel == 0 ? "" : " ";
            string expected = new string('#', expectedLevel) + $"{delimiter}Heading";
            Assert.Equal(expected, textBox.Text);
        }


        [StaFact]
        public void DecreaseHeadingLevel_AlreadyPlainText_IsNoOp()
        {
            // Arrange
            var textBox = Helpers.CreateTextBox("Heading", 0);

            // Act
            MarkdownEditingService.DecreaseHeadingLevel(textBox);

            // Assert — defensive guard, text unchanged.
            Assert.Equal("Heading", textBox.Text);
        }

        // ---------- Hashtags inside heading content are not affected ----------

        [StaFact]
        public void IncreaseHeadingLevel_HashtagInContent_LeavesItUntouched()
        {
            // Arrange — the leading '#' run detection stops at the first space, so a
            // '#' character later in the line (part of the heading's own content) must
            // never be touched.
            var textBox = Helpers.CreateTextBox("## Heading #hashtag", 0);

            // Act
            MarkdownEditingService.IncreaseHeadingLevel(textBox);

            // Assert
            Assert.Equal("# Heading #hashtag", textBox.Text);
        }
    }
}
