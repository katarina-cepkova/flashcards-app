using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Documents;
using Flashcards.App.Services;

namespace Flashcards.App.Converters
{
    public class MarkdownToFlowDocumentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string ?? string.Empty;
            FlowDocument document =  Markdig.Wpf.Markdown.ToFlowDocument(text, MarkdownPipelineProvider.Pipeline);
            return document;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException("Preview is read-only.");
    }
}
