using Flashcards.App.Services;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Converts a Flashcard's packed ARGB int (ColorArgb) into a SolidColorBrush for binding
    /// directly to Background. One-way only — color changes go through ColorPickerDialog, not
    /// through the user editing a Brush in the UI.
    /// </summary>
    internal class ArgbToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int argb = value is not null ? (int)value : 0;
            Color color = ArgbColorConverter.FromArgb(argb);
            return new SolidColorBrush(color);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
