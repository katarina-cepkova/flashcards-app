using Flashcards.App.Models;

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
