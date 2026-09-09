using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Converts null to Visibility.Hidden and any non-null value to Visibility.Visible.
    /// </summary>
    internal class NullToVisibleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is null ? Visibility.Hidden : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}