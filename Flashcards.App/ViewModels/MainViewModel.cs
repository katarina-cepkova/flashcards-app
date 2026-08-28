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
            }
        }

        #endregion

        #region App state

        private AppState _currentState = AppState.OpenedSetEdit;

        /// <summary>The overall UI state of the app, controlling which panels and controls are visible.</summary>
        public AppState CurrentState
        {
            get => _currentState;
            set
            {
                _currentState = value;
                Debug.WriteLine(_currentState);
                OnPropertyChanged();
            }
        }

        #endregion

        #region Topic
        private Topic? _topic;

        /// <summary>The name of the currently open (or being created/renamed) flashcard set.</summary>
        public string TopicName
        {
            get => _topic?.Name ?? "";
            set
            {
                if (_topic is not null)
                {
                    // TODO: db check - valid topic name
                    _topic.Name = value;
                
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RemainingCharactersText));
                    OnPropertyChanged(nameof(RemainingCharactersColor));
                    OnPropertyChanged(nameof(RemainingCharactersCount));

                }
            }
        }

        /// <summary>Maximum allowed length of a topic name.</summary>
        public int TopicMaxLength => 50;  // binding cannot have a static constant, use int instead

        /// <summary>How many more characters can still be typed into the topic name.</summary>
        public int RemainingCharactersCount => TopicMaxLength - TopicName.Length;

        /// <summary>Text representation of <see cref="RemainingCharactersCount"/>, for display.</summary>
        public string RemainingCharactersText => RemainingCharactersCount.ToString();

        /// <summary>Remaining-character threshold at or below which the label switches to the critical color.</summary>
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

        #endregion

        #region Flashcards and navigation

        /// <summary>The flashcards belonging to the currently open set.</summary>
        public ObservableCollection<Flashcard> Cards => _flashcardManager.Cards;

        /// <summary>The flashcard currently shown/edited.</summary>
        public Flashcard? CurrentFlashcard => _flashcardManager.CurrentFlashcard;

        /// <summary>
        /// The highest valid position on CardNavigationScrollBar — one less than the number of active (non-deleted)
        /// cards, since deleted cards remain physically in Cards as tombstones and shouldn't count toward the visible
        /// range. Clamped to 0 so an empty set (all cards deleted) never produces a negative Maximum.
        /// </summary>
        public int MaxCardIndex => Math.Max(0, _flashcardManager.ActiveFlashcardCount - 1);

        /// <summary>
        /// The index of the currently shown card within Cards. Setting it jumps directly
        /// to that card (via FlashcardManager.SelectIndex), independent of the one-step-
        /// at-a-time NextCommand/PreviousCommand — used by CardNavigationScrollBar's thumb,
        /// which can be dragged to an arbitrary position, not just moved one step.
        /// </summary>
        public int CurrentCardIndex
        {
            get => _flashcardManager.LogicalIndex;
            set => _flashcardManager.SelectIndex(value);
        }

        /// <summary>Moves to the next active flashcard, if one exists.</summary>
        public ICommand NextCommand { get; }

        /// <summary>Moves to the previous active flashcard, if one exists.</summary>
        public ICommand PreviousCommand { get; }

        #endregion

        #region Flip and display

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
        /// The text of the currently visible side of <see cref="CurrentFlashcard"/>.
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

        /// <summary>Flipping is blocked while a TextBox has focus, so Space can be typed normally.</summary>
        private static bool CanFlip() => Keyboard.FocusedElement is not TextBoxBase;

        /// <summary>Flips the currently displayed flashcard between its front and back side.</summary>
        public ICommand FlipCommand { get; }

        #endregion

        /// <summary>
        /// Forwards FlashcardManager's own PropertyChanged notifications so bindings on this view model (e.g. Cards,
        /// CurrentFlashcard) stay in sync. CurrentFlashcard changes also need to re-raise DisplayedText, LogicalIndex
        /// changes need to re-raise CurrentCardIndex, and ActiveFlashcardCount changes need to re-raise MaxCardIndex.
        /// </summary>
        private void OnFlashcardManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);

            if (e.PropertyName == nameof(FlashcardManager.CurrentFlashcard))
                OnPropertyChanged(nameof(DisplayedText));

            if (e.PropertyName == nameof(FlashcardManager.LogicalIndex))
                OnPropertyChanged(nameof(CurrentCardIndex));

            if (e.PropertyName == nameof(FlashcardManager.ActiveFlashcardCount))
                OnPropertyChanged(nameof(MaxCardIndex));
        }

        #region Manual UI testing helper (temporary)

        /// <summary>Manual UI testing helper: cycles CurrentState through every AppState value. Remove before submission.</summary>
        public ICommand CycleStateCommand { get; }

        /// <summary>All defined AppState values, in declaration order, used by CycleState to wrap around.</summary>
        private static readonly AppState[] AllStates =
            (AppState[])Enum.GetValues(typeof(AppState));

        /// <summary>Cycles CurrentState through every AppState value in order, wrapping around.</summary>
        private void CycleState()
        {
            int currentIndex = Array.IndexOf(AllStates, CurrentState);
            int nextIndex = (currentIndex + 1) % AllStates.Length;
            CurrentState = AllStates[nextIndex];
        }

        #endregion

        #region Modify flashcard set

        /// <summary>Adds a new default flashcard to the currently open topic.</summary>
        public ICommand CreateFlashcardCommand { get; }

        /// <summary>
        /// Creates a new default-colored flashcard under the current topic and adds it via FlashcardManager.
        /// AddFlashcardButton is only enabled while a set is open, so _topic should never be null here —
        /// this is a defensive guard against a UI/state bug, not an expected path.
        /// </summary>
        private void AddFlashcard()
        {
            // AddFlashcardButton is only enabled while a set is open, so _topic should
            // never be null here — this is a defensive guard against a UI/state bug,
            // not an expected path.
            if (_topic is null)
                throw new InvalidOperationException("Cannot add a flashcard: no topic is open.");

            // A topic only gets edited (and cards added to it) after it's been saved to
            // the database at least once, so Id should always be set by this point.
            if (_topic.Id is not long topicId)
                throw new InvalidOperationException("Cannot add a flashcard: the current topic has not been saved yet.");

            Flashcard card = Flashcard.CreateDefault(topicId, 0);
            _flashcardManager.AddCard(card);
        }

        /// <summary>Marks the currently selected flashcard as deleted.</summary>
        public ICommand DeleteFlashcardCommand { get; }

        #endregion

        /// <summary>
        /// Constructs the view model with temporary hardcoded topic and flashcard data (to be replaced
        /// once repository-backed topic loading is wired up), and initializes all commands.
        /// </summary>
        public MainViewModel(ILocalizationService localizationService)
        {
            _localizationService = localizationService;

            // TEMPORARY test data, remove once loading from repository is wired up
            _flashcardManager = new FlashcardManager(new[]
            {
                new Flashcard { TopicId = 1, FrontText = "Front 1", BackText = "Back 1" },
                new Flashcard { TopicId = 1, FrontText = "Front 2", BackText = "Back 2" },
                new Flashcard { TopicId = 1, FrontText = "Front 3", BackText = "Back 3" },
                new Flashcard { TopicId = 1, FrontText = "Front 4", BackText = "Back 4" },
                new Flashcard { TopicId = 1, FrontText = "Front 5", BackText = "Back 5" },
            });
            _flashcardManager.PropertyChanged += OnFlashcardManagerPropertyChanged;
            _topic = new Topic() {
                Id = 1,
                Name = "Topic",
                CreatedAt = DateTime.Now,
            };
            // commands
            NextCommand = new RelayCommand(_flashcardManager.MoveToNext, _flashcardManager.CanMoveToNext);
            PreviousCommand = new RelayCommand(_flashcardManager.MoveToPrevious, _flashcardManager.CanMoveToPrevious);
            FlipCommand = new RelayCommand(() => IsFront = !IsFront, CanFlip);
            CycleStateCommand = new RelayCommand(CycleState);
            CreateFlashcardCommand = new RelayCommand(AddFlashcard);
            DeleteFlashcardCommand = new RelayCommand(_flashcardManager.DeleteCurrentFlashcard);

        }
    }
}