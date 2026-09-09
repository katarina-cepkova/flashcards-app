using Flashcards.App.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.App.Converters
{
    internal static class AppStateConverterHelpers
    {
        public static bool MatchesAnyState(string commaSeparatedStates, AppState currentState)
        {
            var states = commaSeparatedStates.Split(',', StringSplitOptions.TrimEntries);
            return states.Any(s => s.Trim() == currentState.ToString());
        }
    }
}
