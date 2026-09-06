using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Flashcards.Core.Entities;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Shows a front/back side label only when a flashcard is selected AND the
    /// requested side (via ConverterParameter, "Front" or "Back") is currently active.
    /// </summary>
    internal class FrontBackVisibleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is not [bool isFront, Flashcard currentFlashcard])
                return Visibility.Hidden;

            if (currentFlashcard is null)
                return Visibility.Hidden;

            bool isRequestedSide = parameter as string == "Front" ? isFront : !isFront;
            return isRequestedSide ? Visibility.Visible : Visibility.Hidden;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}