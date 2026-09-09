using Flashcards.App.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace Flashcards.App.Converters
{
    public class StateToColumnWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is AppState.OpenedSetEdit ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
