using Flashcards.App.Commands;
using Flashcards.App.Models;
using Flashcards.App.Services;
using Flashcards.Core.Entities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
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
        private readonly ResourceDictionary _resources = Application.Current.Resources;
        private readonly ILocalizationService _localizationService;
        private readonly FlashcardManager _flashcardManager;

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

        private AppState _currentState = AppState.OpenedSetEdit;

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

        /// <summary>The flashcards belonging to the currently open set.</summary>
        public ObservableCollection<Flashcard> Cards => _flashcardManager.Cards;

        /// <summary>The flashcard currently shown/edited.</summary>
        public Flashcard? CurrentFlashcard => _flashcardManager.CurrentFlashcard;

        private bool _isFront = true;

        /// <summary>Whether the front (true) or back (false) side of the flashcard is currently shown.</summary>
        public bool IsFront
        {
            get => _isFront;
            set
            {
                if (_isFront == value) return;
                _isFront = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayedText));
            }
        }

        /// <summary>
        /// The text of the currently visible side of <see cref="CurrentFlashcard"/>. Bound
        /// two-way from the single shared RichTextBox via RichTextBoxHelper.
        /// </summary>
        public string DisplayedText
        {
            get
            {
                if (CurrentFlashcard is null)
                    return "";
                return IsFront ? CurrentFlashcard.FrontText : CurrentFlashcard.BackText; 
            }

            set
            {
                if (CurrentFlashcard is null) return;

                if (IsFront)
                    CurrentFlashcard.FrontText = value;
                else
                    CurrentFlashcard.BackText = value;
            }
        }
        private static bool CanFlip() => Keyboard.FocusedElement is not TextBoxBase;

        public ICommand FlipCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }

        /// <summary>
        /// Wraps plain text in the minimal WPF flow-content XAML that TextRange.Load
        /// expects (DataFormats.Xaml) — a bare string has no root element and throws
        /// XamlParseException. Only needed for constructing test data; real content
        /// is already in this format once saved through RichTextBoxHelper.
        /// </summary>
        private static string PlainTextAsFlowXaml(string text) =>
            $"<Section xml:space=\"preserve\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            $"<Paragraph><Run>{System.Security.SecurityElement.Escape(text)}</Run></Paragraph></Section>";

        /// <summary>
        /// Forwards FlashcardManager's own PropertyChanged notifications so bindings
        /// on this view model (e.g. Cards, CurrentFlashcard) stay in sync. CurrentFlashcard
        /// changes also need to re-raise DisplayedText.
        /// </summary>
        private void OnFlashcardManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);

            if (e.PropertyName == nameof(FlashcardManager.CurrentFlashcard))
                OnPropertyChanged(nameof(DisplayedText));
        }

        public MainViewModel(ILocalizationService localizationService)
        {
            _localizationService = localizationService;

            // TEMPORARY test data, remove once loading from repository is wired up
            _flashcardManager = new FlashcardManager(new[]
            {
                new Flashcard { TopicId = 1, FrontText = PlainTextAsFlowXaml("Front 1"), BackText = PlainTextAsFlowXaml("Back 1") },
                new Flashcard { TopicId = 1, FrontText = PlainTextAsFlowXaml("Front 2"), BackText = PlainTextAsFlowXaml("Back 2") },
                new Flashcard { TopicId = 1, FrontText = PlainTextAsFlowXaml("Front 3"), BackText = PlainTextAsFlowXaml("Back 3") },
                new Flashcard { TopicId = 1, FrontText = PlainTextAsFlowXaml("Front 4"), BackText = PlainTextAsFlowXaml("Back 4") },
                new Flashcard { TopicId = 1, FrontText = PlainTextAsFlowXaml("Front 5"), BackText = PlainTextAsFlowXaml("Back 5") },
            });
            _flashcardManager.PropertyChanged += OnFlashcardManagerPropertyChanged;

            // commands
            NextCommand = new RelayCommand(_flashcardManager.MoveToNext, _flashcardManager.CanMoveToNext);
            PreviousCommand = new RelayCommand(_flashcardManager.MoveToPrevious, _flashcardManager.CanMoveToPrevious);
            FlipCommand = new RelayCommand(() => IsFront = !IsFront, CanFlip);
        }
    }
}