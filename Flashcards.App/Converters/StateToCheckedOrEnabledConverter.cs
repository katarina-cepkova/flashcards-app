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
            if (value is not AppState currentState || parameter is not string allowedStatesString)
                return false;

            string[] allowedStates = allowedStatesString.Split(',');
            bool isAllowed = allowedStates.Any(s => s.Trim() == currentState.ToString());  // enum.ToString() -> textual name

            return isAllowed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
