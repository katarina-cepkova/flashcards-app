using Flashcards.App.Commands;
using Flashcards.App.Models;
using Flashcards.App.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;


namespace Flashcards.App.ViewModels
{
    /// <summary>
    /// View model for the main application window.
    /// </summary>
    class MainViewModel : ViewModelBase
    {
        private ResourceDictionary _resources = Application.Current.Resources;
        private readonly ILocalizationService _localizationService;
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
            }
        }

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

        private string _topicName = "testing";

        /// <summary>The name of the currently open (or being created/renamed) flashcard set.</summary>
        public string TopicName
        {
            get => _topicName;
            set
            {
                _topicName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingCharactersText));
                OnPropertyChanged(nameof(RemainingCharactersColor));
                OnPropertyChanged(nameof(RemainingCharactersCount));
            }
        }

        /// <summary>Maximum allowed length of a topic name.</summary>
        public int TopicMaxLength => 50;  // binding cannot have a static constant, use int instead

        /// <summary>How many more characters can still be typed into the topic name.</summary>
        public int RemainingCharactersCount => TopicMaxLength - TopicName.Length;

        /// <summary>Text representation of <see cref="RemainingCharactersCount"/>, for display.</summary>
        public string RemainingCharactersText => RemainingCharactersCount.ToString();

        private int CriticalRemainingCharacterCount => 5;

        /// <summary>
        /// Remaining-character threshold at or below which the remaining-characters
        /// label should be shown to warn the user.
        /// </summary>
        public int RemainingCharactersWarningThreshold => 10;

        /// <summary>
        /// Text color for the remaining-characters label, escalating from the default
        /// color to a warning color and finally a critical color as the limit approaches.
        /// </summary>
        public Brush RemainingCharactersColor
        {
            get
            {
                if (RemainingCharactersCount <= CriticalRemainingCharacterCount)
                    return (Brush)_resources["CriticalBrush"];
                else if (RemainingCharactersCount <= RemainingCharactersWarningThreshold)
                    return (Brush)_resources["AlmostCriticalBrush"];
                return (Brush)_resources["TextOnDarkBrush"];
            }
        }


        public MainViewModel(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }
    }
}