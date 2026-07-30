using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Flashcards.App.Models;

namespace Flashcards.App.Converters
{

    /// <summary>Hides the element and collapses its layout space when not in an allowed state.</summary>
    public class StateToCollapsedConverter : StateToVisibilityConverterBase
    {
        protected override Visibility HiddenVisibility => Visibility.Collapsed;
    }
}