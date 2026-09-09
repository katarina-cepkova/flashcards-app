using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using Flashcards.App.Services;

namespace Flashcards.Tests.App.Markdown
{
    internal static class Helpers
    {
        public static TextBox CreateTextBox(string text, int selectionStart, int selectionLength = 0)
        {
            var textBox = new TextBox { Text = text };
            textBox.SelectionStart = selectionStart;
            textBox.SelectionLength = selectionLength;
            return textBox;
        }
    }
    
}
