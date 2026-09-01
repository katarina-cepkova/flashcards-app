using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using Flashcards.App.Models;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Visible when the bound message is non-empty AND CurrentState matches one of the
    /// comma-separated AppState names in ConverterParameter — so a validation message never
    /// shows in states where the field it refers to isn't even on screen (e.g. ClosedSet).
    /// </summary>
    public class MessageAndStateToVisibleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 
                || values[0] is not string message 
                || values[1] is not AppState currentState
                || parameter is not string allowedStates)
                return Visibility.Hidden;

            bool hasMessage = !string.IsNullOrEmpty(message);
            bool stateMatches = AppStateConverterHelpers.MatchesAnyState(allowedStates, currentState);

            return (hasMessage && stateMatches) ? Visibility.Visible : Visibility.Hidden;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}