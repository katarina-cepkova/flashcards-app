using Flashcards.App.Models;

namespace Flashcards.App.ViewModels
{
    internal partial class MainViewModel
    {
        #region Language

        private string _currentLanguage = "en-GB";  // default language at the app's start

        /// <summary>The culture code of the app's currently active language, e.g. "en-GB".</summary>
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage == value)
                    return;
                _currentLanguage = value;
                OnPropertyChanged();
                _localizationService.SetLanguage(value);  // change the language of the app

                OnPropertyChanged(nameof(TopicNameValidationMessage));
            }
        }

        #endregion

        #region App state

        private AppState _currentState = AppState.ClosedSet;

        /// <summary>The overall UI state of the app, controlling which panels and controls are visible.</summary>
        public AppState CurrentState
        {
            get => _currentState;
            set
            {
                _currentState = value;
                OnPropertyChanged();
            }
        }

        #endregion

    }
}
