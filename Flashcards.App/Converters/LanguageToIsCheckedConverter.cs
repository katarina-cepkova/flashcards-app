using System.Globalization;
using System.Windows.Data;

namespace Flashcards.App.Converters
{
    /// <summary>
    /// Converts between the currently selected language (a culture code string)
    /// and whether a specific RadioButton (identified by its ConverterParameter)
    /// should appear checked.
    /// </summary>
    internal class LanguageToIsCheckedConverter : IValueConverter
    {
        // value = SelectedLanguage ("en-GB"), parameter = ConverterParameter from XAML
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        // value = bool from RadioButton (true when clicked), parameter = RadioButton's ConverterParameter
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // if RadioButton is checked, return its language code
            // else - do not rewrite SelectedLanguage
            return (value is bool isChecked && isChecked) ? parameter : Binding.DoNothing;
        }
    }
}