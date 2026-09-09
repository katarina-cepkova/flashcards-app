using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Flashcards.App.Models;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Shows the remaining-characters label only when the topic name field is
    /// focused, currently editable, and the remaining character count has
    /// dropped to or below a threshold (given as ConverterParameter).
    /// </summary>
    public class RemainingCharactersLabelVisibilityConverter : IMultiValueConverter
    {
        // values[0] = IsFocused (bool), values[1] = CurrentState (AppState),
        // values[2] = RemainingCharactersCount (int), values[3] = threshold
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length != 4)
                return Visibility.Collapsed;

            if (values[0] is not bool isFocused
                || values[1] is not AppState currentState
                || values[2] is not int remainingCount
                || values[3] is not int threshold)
                return Visibility.Hidden;

            bool isEditable = currentState == AppState.OpenedSetEdit;
            bool isNearLimit = remainingCount <= threshold;

            return (isFocused && isEditable && isNearLimit) ? Visibility.Visible : Visibility.Hidden;
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}