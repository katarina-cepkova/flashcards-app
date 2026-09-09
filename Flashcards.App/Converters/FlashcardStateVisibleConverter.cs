using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Flashcards.App.Models;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Shows an element only when a flashcard is selected AND the current AppState
    /// is one of the states listed (comma-separated) in the converter parameter.
    /// </summary>
    public class FlashcardStateVisibleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is not [var currentFlashcard, AppState currentState]
            || currentFlashcard is null
            || parameter is not string allowedStates)
                return Visibility.Collapsed;

            bool stateMatches = AppStateConverterHelpers.MatchesAnyState(allowedStates, currentState);

            return stateMatches ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}