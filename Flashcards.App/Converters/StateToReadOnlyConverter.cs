using Flashcards.App.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace Flashcards.App.Converters
{
    class StateToReadOnlyConverter :IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is AppState currentState && currentState != AppState.OpenedSetEdit;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
