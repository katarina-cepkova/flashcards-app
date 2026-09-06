using System.Windows;

namespace Flashcards.App.Converters
{

    /// <summary>Hides the element and collapses its layout space when not in an allowed state.</summary>
    internal class StateToCollapsedConverter : StateToVisibilityConverterBase
    {
        protected override Visibility HiddenVisibility => Visibility.Collapsed;
    }
}