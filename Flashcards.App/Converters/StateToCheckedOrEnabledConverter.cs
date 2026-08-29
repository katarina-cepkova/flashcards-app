using Flashcards.App.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace Flashcards.App.Converters
{
    class StateToCheckedOrEnabledConverter : IValueConverter
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
