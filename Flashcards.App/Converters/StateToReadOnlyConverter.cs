using Flashcards.App.Models;
using System.Globalization;
using System.Windows.Data;

namespace Flashcards.App.Converters
{
    internal class StateToReadOnlyConverter : IValueConverter
    {
        /// <summary>
        /// Returns false (editable) when CurrentState matches any of the comma-separated AppState
        /// names in ConverterParameter, true (read-only) otherwise — e.g. ConverterParameter=
        /// "OpenedSetEdit,CreatingSet" makes the TextBox editable in both those states.
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not AppState currentState || parameter is not string editableStates)
                return true;

            return AppStateConverterHelpers.MatchesAnyState(editableStates, currentState);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
