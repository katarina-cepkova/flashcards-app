using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Visible when the bound TextBox is both focused and not read-only — used for controls
    /// (like a confirm button) that should only appear while the user is actively able to
    /// edit that specific field, derived directly from IsReadOnly rather than duplicating
    /// the state logic that already computes it.
    /// </summary>
    public class FocusAndNotReadOnlyToVisibleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || values[0] is not bool isFocused || values[1] is not bool isReadOnly)
                return Visibility.Collapsed;

            return (isFocused && !isReadOnly) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
