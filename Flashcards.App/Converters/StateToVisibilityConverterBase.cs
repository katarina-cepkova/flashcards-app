using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Flashcards.App.Models;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Base for converters that show an element only when the current AppState
    /// is one of the states listed (comma-separated) in the converter parameter.
    /// Derived classes decide what "not shown" means (Collapsed vs Hidden).
    /// </summary>
    public abstract class StateToVisibilityConverterBase : IValueConverter
    {
        protected abstract Visibility HiddenVisibility { get; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not AppState currentState || parameter is not string allowedStatesString)
                return HiddenVisibility;

            string[] allowedStates = allowedStatesString.Split(',');
            bool isAllowed = allowedStates.Any(s => s.Trim() == currentState.ToString());  // enum.ToString() -> textual name

            return isAllowed ? Visibility.Visible : HiddenVisibility;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}