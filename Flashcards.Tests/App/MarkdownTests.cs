using System.Windows.Controls;
using Flashcards.App.Services;

namespace Flashcards.Tests.App
{
    public class MarkdownTests
    {
        private static TextBox CreateTextBox(string text, int selectionStart, int selectionLength = 0)
        {
            var textBox = new TextBox { Text = text };
            textBox.SelectionStart = selectionStart;
            textBox.SelectionLength = selectionLength;
            return textBox;
        }

        // ---------- Caret only (no selection) — relies on ExpandToWordBoundaries ----------

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretInsideWord_FormatsPlainWord(string marker)
        {
            // Arrange — caret resting inside "ipsum", nothing selected.
            var textBox = CreateTextBox("Lorem ipsum dolor sit amet.", 9);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert — the whole word gets wrapped, and the selection lands on the
            // word itself (not the markers), shifted right by the opening marker.
            Assert.Equal($"Lorem {marker}ipsum{marker} dolor sit amet.", textBox.Text);
            Assert.Equal(6 + marker.Length, textBox.SelectionStart);
            Assert.Equal(5, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretBeforeWord_FormatsPlainWord(string marker)
        {
            // Arrange — caret sitting right before the word starts, still no selection.
            var textBox = CreateTextBox("Lorem ipsum dolor sit amet.", 6);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert — same result as caret-inside-word: word boundary expansion
            // doesn't depend on exactly where within the word the caret sits.
            Assert.Equal($"Lorem {marker}ipsum{marker} dolor sit amet.", textBox.Text);
            Assert.Equal(6 + marker.Length, textBox.SelectionStart);
            Assert.Equal(5, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretAfterWord_FormatsPlainWord(string marker)
        {
            // Arrange — caret sitting right after the word ends.
            var textBox = CreateTextBox("Lorem ipsum dolor sit amet.", 11);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert
            Assert.Equal($"Lorem {marker}ipsum{marker} dolor sit amet.", textBox.Text);
            Assert.Equal(6 + marker.Length, textBox.SelectionStart);
            Assert.Equal(5, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretBeforePunctuation_FormatsPlainWord(string marker)
        {
            // Arrange — caret inside "amet", the word directly preceding the
            // sentence's trailing period.
            var textBox = CreateTextBox("Lorem ipsum dolor sit amet.", 26);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert — the trailing period is correctly excluded from the word,
            // since IsPunctuation stops expansion right at it.
            Assert.Equal($"Lorem ipsum dolor sit {marker}amet{marker}.", textBox.Text);
            Assert.Equal(22 + marker.Length, textBox.SelectionStart);
            Assert.Equal(4, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretAfterPunctuation_InsertsMarkerPair(string marker)
        {
            // Arrange — caret right after the trailing period, with nothing but
            // end-of-text after it — no word to expand into on either side.
            var textBox = CreateTextBox("Lorem ipsum dolor sit amet.", 27);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert — an empty marker pair is inserted right at the caret,
            // ready for the user to start typing between the two halves.
            Assert.Equal($"Lorem ipsum dolor sit amet.{marker}{marker}", textBox.Text);
            Assert.Equal(27 + marker.Length, textBox.SelectionStart);
            Assert.Equal(0, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretBetweenPunctuation_InsertsMarkerPair(string marker)
        {
            // Arrange — caret sitting between two commas, with no word character
            // touching it on either side.
            var textBox = CreateTextBox("Lorem ipsum,, dolor sit amet.", 12);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert — same empty-pair behavior as after a single punctuation mark.
            Assert.Equal($"Lorem ipsum,{marker}{marker}, dolor sit amet.", textBox.Text);
            Assert.Equal(12 + marker.Length, textBox.SelectionStart);
            Assert.Equal(0, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretInsideFormatted_DeletesInnerMatchingMarker(string marker)
        {
            // Arrange — word already wrapped in the marker being toggled, caret inside it.
            var textBox = CreateTextBox($"Lorem {marker}ipsum{marker} dolor sit amet.", 8);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert — markers removed, selection lands on the now-bare word.
            Assert.Equal($"Lorem ipsum dolor sit amet.", textBox.Text);
            Assert.Equal(6, textBox.SelectionStart);
            Assert.Equal(5, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        public void ToggleEmphasis_CaretInsideCombinedFormatting_DeletesMatchingMarker(string marker)
        {
            // Arrange — a length-3 run (e.g. "***") combining this marker with the
            // other one sharing its character. Toggling should peel off only this
            // marker's length, leaving the other marker type intact around the word.
            string combinedMarker = new string(marker[0], 3);
            var textBox = CreateTextBox($"Lorem {combinedMarker}ipsum{combinedMarker} dolor sit amet.", 11);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert — only `marker`'s length is peeled off each side; the
            // remaining run length (3 - marker.Length) represents the other marker.
            string resultingMarker = new string(marker[0], 3 - marker.Length);
            Assert.Equal($"Lorem {resultingMarker}ipsum{resultingMarker} dolor sit amet.", textBox.Text);
            Assert.Equal(6 + resultingMarker.Length, textBox.SelectionStart);
            Assert.Equal(5, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("__", "**")]
        [InlineData("_", "*")]
        public void ToggleEmphasis_CaretInsideWord_RemovesEquivalentMarkers(string existingMarker, string proposedMarker)
        {
            // Arrange — word hand-written with the underscore form of the marker
            // the button only ever generates the asterisk form.
            var textBox = CreateTextBox($"Lorem {existingMarker}ipsum{existingMarker} dolor sit amet.", 10);

            // Act — toggling the asterisk-form marker must still recognize the
            // underscore-form as equivalent (see AreEquivalentDelimiters).
            MarkdownEditingService.ToggleEmphasis(textBox, proposedMarker);

            // Assert
            Assert.Equal("Lorem ipsum dolor sit amet.", textBox.Text);
            Assert.Equal(6, textBox.SelectionStart);
            Assert.Equal(5, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**", "*")]
        [InlineData("**", "_")]
        [InlineData("**", "~~")]
        [InlineData("__", "*")]
        [InlineData("__", "_")]
        [InlineData("__", "~~")]
        [InlineData("*", "**")]
        [InlineData("*", "__")]
        [InlineData("*", "~~")]
        [InlineData("_", "**")]
        [InlineData("_", "__")]
        [InlineData("_", "~~")]
        [InlineData("~~", "*")]
        [InlineData("~~", "_")]
        [InlineData("~~", "**")]
        [InlineData("~~", "__")]
        public void ToggleEmphasis_CaretInsideWord_NonmatchingInnerMarker_AddsMarker(string existingMarker, string proposedMarker)
        {
            // Arrange — word already wrapped in one marker; caret sees this
            // nearest layer, and it doesn't match what's being toggled.
            var textBox = CreateTextBox($"Lorem {existingMarker}ipsum{existingMarker} dolor sit amet.", 10);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, proposedMarker);

            // Assert — the nearest (existing) layer is left untouched, and a new
            // matching pair is added directly around the word, inside it.
            Assert.Equal($"Lorem {existingMarker}{proposedMarker}ipsum{proposedMarker}{existingMarker} dolor sit amet.", textBox.Text);
            Assert.True(textBox.SelectionStart <= 11);
            Assert.Equal(5, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**", "*")]
        [InlineData("**", "~~")]
        [InlineData("__", "*")]
        [InlineData("__", "~~")]
        [InlineData("*", "**")]
        [InlineData("*", "~~")]
        [InlineData("_", "**")]
        [InlineData("_", "~~")]
        [InlineData("~~", "*")]
        [InlineData("~~", "**")]
        public void ToggleEmphasis_CaretInsideWord_MatchingOuter_RemovesMarker(string existingInnerMarker, string proposedMarker)
        {
            // Arrange — two layers, with `proposedMarker` as the OUTER one this
            // time (existingInnerMarker sits closer to the word). FindEnclosing
            // continues to check other layers when the first doesn't match
            var textBox = CreateTextBox($"Lorem {proposedMarker}{existingInnerMarker}ipsum{existingInnerMarker}{proposedMarker} dolor sit amet.", 10);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, proposedMarker);

            // Assert — outer marker removed, inner one left in place.
            Assert.Equal($"Lorem {existingInnerMarker}ipsum{existingInnerMarker} dolor sit amet.", textBox.Text);

            // not calculating caret position - already tested elsewhere
            Assert.Equal(5, textBox.SelectionLength);
        }

        // ---------- Boundary positions ----------

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretAtStartOfText_FormatsFirstWord(string marker)
        {
            // Arrange — caret at index 0, the ExpandToWordBoundaries `start > 0`
            // guard is what's under test here.
            var textBox = CreateTextBox("Lorem ipsum.", 0);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert
            Assert.Equal($"{marker}Lorem{marker} ipsum.", textBox.Text);
            Assert.Equal(marker.Length, textBox.SelectionStart);
            Assert.Equal(5, textBox.SelectionLength);
        }

        [StaTheory]
        [InlineData("**")]
        [InlineData("__")]
        [InlineData("*")]
        [InlineData("_")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretAtEndOfText_FormatsLastWord(string marker)
        {
            // Arrange — caret at text.Length exactly, with nothing after it at all
            // (not even punctuation) — the `end < text.Length` guard is under test.
            var textBox = CreateTextBox("Lorem ipsum", 11);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert
            Assert.Equal($"Lorem {marker}ipsum{marker}", textBox.Text);
            Assert.Equal(6 + marker.Length, textBox.SelectionStart);
            Assert.Equal(5, textBox.SelectionLength);
        }

        // ---------- Run length 4+ has no standard markdown meaning — must not crash or
        // misidentify itself as a match; falls back to adding a new marker around it ----------

        [StaTheory]
        [InlineData("**")]
        [InlineData("*")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretInsideFourCharacterRun_AddsMarkerInsteadOfMatching(string marker)
        {
            // Arrange — a 4-character run of the same delimiter. Even-length and
            // not an exact match for `marker`, so FindEnclosing must not treat it
            // as a valid combined marker the way it does for length-3 runs.
            string run = new string(marker[0], 4);
            var textBox = CreateTextBox($"Lorem {run}ipsum{run} dolor sit amet.", 6 + run.Length + 2);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert — falls back to inserting a new pair inside the existing run,
            // rather than crashing or stripping characters off an invalid length.
            Assert.Equal($"Lorem {run}{marker}ipsum{marker}{run} dolor sit amet.", textBox.Text);
        }

        // ---------- Unpaired/unclosed marker elsewhere in the text — must not confuse
        // matching for an unrelated word ----------

        [StaTheory]
        [InlineData("**")]
        [InlineData("*")]
        [InlineData("~~")]
        public void ToggleEmphasis_CaretOnPlainWord_UnrelatedUnclosedMarkerElsewhere_StillWrapsNormally(string marker)
        {
            // Arrange — the stray "*" before "dolor" is never closed; it sits well
            // outside "ipsum"'s word boundaries, so it must not interfere with
            // formatting the unrelated word "ipsum".
            var textBox = CreateTextBox("Lorem ipsum *dolor sit amet.", 8);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, marker);

            // Assert
            Assert.Equal($"Lorem {marker}ipsum{marker} *dolor sit amet.", textBox.Text);
        }

        // ---------- Explicit selection — reaches layers caret alone cannot ----------

        [StaFact]
        public void Selection_SpanningOuterLayer_RemovesOuterMarker_UnlikeCaretAlone()
        {
            // Arrange — nested markers where the OUTER "~~" layer is selected
            // explicitly, excluding the markers themselves from the selection
            // (only "*ipsum*" is selected, not "~~*ipsum*~~"). ExpandToWordBoundaries
            // could never produce a range this wide on its own — it always stops at
            // the nearest layer — so this is the one behavior caret-only toggling
            // structurally cannot reach.
            var text = "Lorem ~~*ipsum*~~ dolor sit amet.";
            int start = text.IndexOf("*ipsum*", StringComparison.Ordinal);
            int end = start + "*ipsum*".Length;
            var textBox = CreateTextBox(text, start, end - start);

            // Act
            MarkdownEditingService.ToggleEmphasis(textBox, "~~");

            // Assert — outer "~~" removed, inner "*ipsum*" left untouched.
            Assert.Equal("Lorem *ipsum* dolor sit amet.", textBox.Text);
        }
    }
}