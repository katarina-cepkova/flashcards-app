using System.Windows.Input;
using Flashcards.App.Commands;
using Flashcards.App.Services;


namespace Flashcards.App.ViewModels
{
    /// <summary>
    /// View model for the main application window.
    /// </summary>
    class MainViewModel : ViewModelBase
    {
        private readonly ILocalizationService _localizationService;
        private string _currentLanguage = "en-GB";  // default language at the app's start
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            { 
                if (_currentLanguage == value)
                    return;
                _currentLanguage = value;
                OnPropertyChanged();  // notify all subscribing UI elements
                _localizationService.SetLanguage(value);  // change the language of the app
            }
        }


        public MainViewModel(ILocalizationService localizationService)
        {
            _localizationService = localizationService;

        }

        
    }
}
