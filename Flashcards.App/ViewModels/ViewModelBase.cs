using Flashcards.App.Common;

namespace Flashcards.App.ViewModels
{
    /// <summary>
    /// Base class for view models. Currently adds nothing beyond
    /// <see cref="ObservableObject"/>, but exists as an explicit extension
    /// point for view-model-specific behavior.
    /// </summary>
    internal abstract class ViewModelBase : ObservableObject
    {
    }
}