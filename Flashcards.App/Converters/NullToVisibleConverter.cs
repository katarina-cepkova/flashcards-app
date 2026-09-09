using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Flashcards.App.Models;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Converts null to Visibility.Hidden and any non-null value to Visibility.Visible.
    /// </summary>
    public class NullToVisibleConverter : IValueConverter
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