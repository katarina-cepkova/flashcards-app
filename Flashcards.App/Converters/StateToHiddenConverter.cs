using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Flashcards.App.Models;

namespace Flashcards.App.Converters
{

    /// <summary>Hides the element but keeps its layout space reserved when not in an allowed state.</summary>
    public class StateToHiddenConverter : StateToVisibilityConverterBase
    {
        protected override Visibility HiddenVisibility => Visibility.Hidden;
    }
}