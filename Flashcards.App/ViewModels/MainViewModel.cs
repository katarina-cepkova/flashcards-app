using Flashcards.App.Commands;
using Flashcards.App.Models;
using Flashcards.App.Services;
using Flashcards.App.Dialogs;
using Flashcards.Core.Entities;
using Flashcards.Core.Repositories;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
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
        private readonly ITopicRepository _topicRepository;
        private readonly IFlashcardRepository _flashcardRepository;
        private FlashcardManager _flashcardManager;

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

        private AppState _currentState = AppState.ClosedSet;

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

        /// <summary>
        /// All topics currently known to the app, loaded once (on first Create/Open) and kept in
        /// sync locally on create/delete afterward — avoids a DB round-trip on every keystroke
        /// when checking for name collisions in IsTopicNameValid.
        /// </summary>
        public ObservableCollection<Topic> AvailableTopics { get; } = new();

        private bool _topicsLoaded;

        /// <summary>Loads AvailableTopics from the repository once; subsequent calls are a no-op.</summary>
        private async Task EnsureTopicsLoadedAsync()
        {
            if (_topicsLoaded) return;

            IReadOnlyList<Topic> topics = await _topicRepository.GetAllAsync();
            foreach (Topic topic in topics)
                AvailableTopics.Add(topic);
            _topicsLoaded = true;
        }

        private Topic? _topic;

        /// <summary>The name of the currently open (or being created/renamed) flashcard set.</summary>
        public string TopicName
        {
            get => _topic?.Name ?? "";
            set
            {
                if (_topic is not null)
                {
                    _topic.Name = value;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RemainingCharactersText));
                    OnPropertyChanged(nameof(RemainingCharactersColor));
                    OnPropertyChanged(nameof(RemainingCharactersCount));
                    OnPropertyChanged(nameof(IsTopicNameValid));
                    OnPropertyChanged(nameof(TopicNameValidationMessage));
                }
            }
        }

        /// <summary>
        /// True when TopicName is non-empty and doesn't collide (case-insensitively, after
        /// trimming) with any other topic already loaded into AvailableTopics — checked entirely
        /// in memory, no DB round-trip per keystroke. The topic's own Id is excluded from the
        /// collision check, so renaming a topic back to its own current name (or leaving it
        /// unchanged) doesn't falsely flag as a collision. AvailableTopics entries are always
        /// already-trimmed (ConfirmTopicNameAsync trims before saving), so only TopicName itself
        /// needs trimming here.
        /// </summary>
        public bool IsTopicNameValid =>
            !string.IsNullOrWhiteSpace(TopicName) &&
            !AvailableTopics.Any(t => t.Id != _topic?.Id && string.Equals(t.Name, TopicName.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Explains why TopicName is currently invalid — empty, or colliding with an existing topic
        /// — for display under TopicNameTextBox. Empty string when the name is valid (IsTopicNameValid).
        /// </summary>
        public string TopicNameValidationMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TopicName))
                    return (string)_resources["CreateSet_InvalidSetTopicMessage"];

                bool collides = AvailableTopics.Any(t => t.Id != _topic?.Id && string.Equals(t.Name, TopicName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (collides)
                    return ((string)_resources["CreateSet_AlreadyExistingTopicMessage"]).Replace("@", TopicName.Trim());

                return "";
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
                IsDirty = true;
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

        /// <summary>
        /// Replaces _flashcardManager with a fresh instance wrapping the given cards, re-wiring its
        /// PropertyChanged forwarding (unsubscribing the old instance first, so it can be garbage
        /// collected cleanly) and re-raising every property whose value depends on it — used whenever
        /// a set is newly created (empty cards), opened (cards loaded from the repository), or saved
        /// (cards refreshed with real Ids from the repository).
        /// </summary>
        private void ReplaceFlashcardManager(IEnumerable<Flashcard> cards)
        {
            if (_flashcardManager is not null)
                _flashcardManager.PropertyChanged -= OnFlashcardManagerPropertyChanged;

            _flashcardManager = new FlashcardManager(cards);
            _flashcardManager.PropertyChanged += OnFlashcardManagerPropertyChanged;

            OnPropertyChanged(nameof(Cards));
            OnPropertyChanged(nameof(CurrentFlashcard));
            OnPropertyChanged(nameof(DisplayedText));
            OnPropertyChanged(nameof(CurrentCardIndex));
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

        #region Save

        private bool _isDirty;

        /// <summary>True when the currently open set has unsaved changes — controls SaveButton's enabled state.</summary>
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value) return;
                _isDirty = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Persists the currently open set's cards (inserts, updates, and purges tombstones) via
        /// SaveChangesAsync, then rebuilds _flashcardManager from the refreshed list it returns —
        /// new cards now have real Ids, deleted ones are gone — and clears IsDirty.
        /// </summary>
        private async Task SaveFlashcardsAsync()
        {
            IReadOnlyList<Flashcard> savedCards = await _flashcardRepository.SaveChangesAsync(_flashcardManager.Cards);
            ReplaceFlashcardManager(savedCards);
            IsDirty = false;
        }

        /// <summary>Saves the currently open set's cards. Disabled unless there are unsaved changes.</summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// If the currently open set has unsaved changes, asks the user whether to save them before
        /// proceeding (e.g. before creating/opening another set). Returns false if the user cancels
        /// (caller should abort whatever it was about to do), true otherwise.
        /// </summary>
        private async Task<bool> TrySaveOpenSetIfNeededAsync()
        {
            if (!IsDirty)
                return true;

            string message = ((string)_resources["UnsavedChangesMessage"]).Replace("@", _topic?.Name ?? "");
            string caption = (string)_resources["UnsavedChangesTitle"];

            MessageBoxResult result = MessageBox.Show(message, caption, MessageBoxButton.YesNoCancel);
            Debug.WriteLine((string)_resources["UnsavedChangesMessage"]);
            if (result == MessageBoxResult.Cancel)
                return false;

            if (result == MessageBoxResult.Yes)
                await SaveFlashcardsAsync();

            return true;
        }

        #endregion

        #region Create set

        /// <summary>
        /// Saves the currently open set (if any), then starts a new in-memory draft Topic and switches
        /// to CreatingSet — TopicNameTextBox becomes visible/editable while the rest of the card-editing
        /// UI stays disabled (gated on OpenedSetEdit, which this state isn't). _flashcardManager is
        /// cleared immediately (rather than waiting for ConfirmTopicNameAsync) so the previous set's
        /// cards don't linger and confuse the user while naming the new one. The draft topic isn't
        /// persisted until ConfirmTopicNameAsync runs.
        /// </summary>
        private async Task EnterCreatingSetAsync()
        {
            if (!await TrySaveOpenSetIfNeededAsync())
                return;

            await EnsureTopicsLoadedAsync();

            _topic = new Topic { Name = "" };
            OnPropertyChanged(nameof(TopicName));
            OnPropertyChanged(nameof(IsTopicNameValid));

            ReplaceFlashcardManager(Array.Empty<Flashcard>());

            CurrentState = AppState.CreatingSet;
        }

        /// <summary>Starts creating a new flashcard set, saving the currently open one first if needed.</summary>
        public ICommand CreateSetCommand { get; }

        /// <summary>
        /// Persists changes to _topic's name: creates it (AddAsync) if this is a new draft from
        /// EnterCreatingSetAsync, or renames it (RenameAsync) if the user is editing an already-
        /// saved topic's name while a set is open. Trims whitespace before saving either way, since
        /// live validation (IsTopicNameValid) intentionally doesn't trim mid-typing.
        /// </summary>
        private async Task ConfirmTopicNameAsync()
        {
            if (_topic is null)
                throw new InvalidOperationException("Cannot confirm set changes: no topic exists.");

            _topic.Name = _topic.Name.Trim();
            OnPropertyChanged(nameof(TopicName));

            if (_topic.Id is not long topicId)
            {
                // New topic — not yet saved.
                long newId = await _topicRepository.AddAsync(_topic);
                _topic.Id = newId;
                AvailableTopics.Add(_topic);

                CurrentState = AppState.OpenedSetEdit;
            }
            else
            {
                // Existing topic — renaming.
                await _topicRepository.RenameAsync(topicId, _topic.Name);
            }
        }

        /// <summary>Confirms the current topic name — creates a new topic or renames the open one, depending on context.</summary>
        public ICommand ConfirmTopicNameCommand { get; }

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
            if (_topic is null)
                throw new InvalidOperationException("Cannot add a flashcard: no topic is open.");

            // A topic only gets edited (and cards added to it) after it's been saved to
            // the database at least once, so Id should always be set by this point.
            if (_topic.Id is not long topicId)
                throw new InvalidOperationException("Cannot add a flashcard: the current topic has not been saved yet.");

            Flashcard card = Flashcard.CreateDefault(topicId, _lastUsedColorArgb);
            _flashcardManager.AddCard(card);
            IsDirty = true;
        }

        /// <summary>Marks the currently selected flashcard as deleted (tombstone, purged on save).</summary>
        private void DeleteFlashcard()
        {
            _flashcardManager.DeleteCurrentFlashcard();
            IsDirty = true;
        }

        /// <summary>Marks the currently selected flashcard as deleted.</summary>
        public ICommand DeleteFlashcardCommand { get; }

        #endregion

        #region Color

        private int _lastUsedColorArgb;

        /// <summary>
        /// Opens the system color picker and applies the chosen color to CurrentFlashcard. The null
        /// check is redundant with ColorCommand's canExecute (which already disables the button when
        /// CurrentFlashcard is null) — kept as a defensive guard against other ways Execute could be
        /// triggered (e.g. a future KeyBinding) that might bypass canExecute.
        /// </summary>
        private void ChooseColor()
        {
            if (CurrentFlashcard is null) return;

            if (ColorPickerDialog.PickColor(CurrentFlashcard.ColorArgb) is int newColor)
            {
                CurrentFlashcard.ColorArgb = newColor;
                _lastUsedColorArgb = newColor;
                OnPropertyChanged(nameof(CurrentFlashcard));
                IsDirty = true;
            }
        }

        /// <summary>Opens the system color picker to change the current flashcard's background color.</summary>
        public ICommand EditFlashcardColorCommand { get; }

        #endregion

        /// <summary>Constructs the view model and initializes all commands.</summary>
        public MainViewModel(ILocalizationService localizationService, ITopicRepository topicRepository, IFlashcardRepository flashcardRepository)
        {
            _localizationService = localizationService;
            _topicRepository = topicRepository;
            _flashcardRepository = flashcardRepository;

            _lastUsedColorArgb = ArgbColorConverter.ToArgb(
                ((SolidColorBrush)_resources["DefaultFlashcardBackgroundBrush"]).Color
            );  // fallback default

            _flashcardManager = new FlashcardManager(Array.Empty<Flashcard>());
            _flashcardManager.PropertyChanged += OnFlashcardManagerPropertyChanged;
            AvailableTopics.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(IsTopicNameValid));
                OnPropertyChanged(nameof(TopicNameValidationMessage));
            };

            // commands
            NextCommand = new RelayCommand(() => _flashcardManager.MoveToNext(), () => _flashcardManager.CanMoveToNext());
            PreviousCommand = new RelayCommand(() => _flashcardManager.MoveToPrevious(), () => _flashcardManager.CanMoveToPrevious());
            FlipCommand = new RelayCommand(() => IsFront = !IsFront, CanFlip);
            CycleStateCommand = new RelayCommand(CycleState);
            CreateFlashcardCommand = new RelayCommand(AddFlashcard);
            DeleteFlashcardCommand = new RelayCommand(DeleteFlashcard);
            EditFlashcardColorCommand = new RelayCommand(ChooseColor, () => CurrentFlashcard is not null);

            CreateSetCommand = new AsyncRelayCommand(EnterCreatingSetAsync);
            ConfirmTopicNameCommand = new AsyncRelayCommand(ConfirmTopicNameAsync, () => IsTopicNameValid);
            SaveCommand = new AsyncRelayCommand(SaveFlashcardsAsync, () => IsDirty);
        }
    }
}