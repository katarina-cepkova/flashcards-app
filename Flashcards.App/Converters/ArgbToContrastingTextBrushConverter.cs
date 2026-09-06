using Flashcards.App.Services;
using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Converts a Flashcard's packed ARGB background color into a white or black text brush,
    /// whichever contrasts better — computed dynamically from luminance rather than stored,
    /// so it always matches the current background even after the color changes.
    /// </summary>
    internal class ArgbToContrastingTextBrushConverter : IValueConverter
    {
        /// <summary>Below this luminance the background counts as dark and gets white text; at or above, black text.</summary>
        private const double LuminanceThreshold = 128;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int argb = (int)value;
            double luminance = ArgbColorConverter.CalculateLuminance(argb);
            string resourceKey = luminance < LuminanceThreshold
                ? "TextOnDarkBrush"
                : "TextOnLightBrush";
            return (Brush)Application.Current.Resources[resourceKey];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("ArgbToContrastingTextBrushConverter only supports one-way binding.");
        }
    }
}