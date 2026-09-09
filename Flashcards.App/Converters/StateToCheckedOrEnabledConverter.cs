using Flashcards.App.Models;
using System.Globalization;
using System.Windows.Data;

namespace Flashcards.App.Converters
{
    internal class StateToCheckedOrEnabledConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not AppState currentState || parameter is not string allowedStates)
                return false;

            return AppStateConverterHelpers.MatchesAnyState(allowedStates, currentState);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
