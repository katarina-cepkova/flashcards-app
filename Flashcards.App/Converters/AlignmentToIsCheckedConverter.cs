using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Converts a TextAlignment to bool for a RadioButton's IsChecked, comparing it against
    /// the alignment named in ConverterParameter (e.g. "Center").
    /// </summary>
    public class AlignmentToIsCheckedConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not TextAlignment currentAlignment || parameter is not string targetAlignment)
                return false;
            return currentAlignment.ToString() == targetAlignment;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
