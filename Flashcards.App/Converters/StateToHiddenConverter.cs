using System.Windows;

namespace Flashcards.App.Converters
{

    /// <summary>Hides the element but keeps its layout space reserved when not in an allowed state.</summary>
    internal class StateToHiddenConverter : StateToVisibilityConverterBase
    {
        protected override Visibility HiddenVisibility => Visibility.Hidden;
    }
}