using Flashcards.App.Commands;
using Flashcards.App.Services;
using Flashcards.App.Models;
using Flashcards.Core.Entities;
using Flashcards.Core.Repositories;

using System.Windows;
using System.Windows.Media;

namespace Flashcards.App.ViewModels
{
    /// <summary>
    /// View model for the main application window.
    /// </summary>
    partial class MainViewModel : ViewModelBase
    {
        private readonly ResourceDictionary _resources = Application.Current.Resources;
        private readonly ILocalizationService _localizationService;
        private readonly ITopicRepository _topicRepository;
        private readonly IFlashcardRepository _flashcardRepository;
        private FlashcardManager _flashcardManager;

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
            NextCommand = new RelayCommand(() => _flashcardManager.MoveToNext(), _flashcardManager.CanMoveToNext);
            PreviousCommand = new RelayCommand(() => _flashcardManager.MoveToPrevious(), _flashcardManager.CanMoveToPrevious);
            FlipCommand = new RelayCommand(() => IsFront = !IsFront, CanFlip);
            // editing
            CreateFlashcardCommand = new RelayCommand(AddFlashcard);
            DeleteFlashcardCommand = new RelayCommand(DeleteFlashcard, () => CurrentFlashcard is not null);
            EditFlashcardColorCommand = new RelayCommand(ChooseColor, () => CurrentFlashcard is not null);
            // set navigation
            CreateSetCommand = new AsyncRelayCommand(EnterCreatingSetAsync);
            EnterSelectingSetCommand = new AsyncRelayCommand(EnterSelectingSetAsync);
            OpenSelectedSetCommand = new AsyncRelayCommand(OpenSelectedSetAsync, () => SelectedTopicToOpen is not null);
            DeleteOrLeaveSetCommand = new AsyncRelayCommand(DeleteOrLeaveSetAsync);
            ConfirmTopicNameCommand = new AsyncRelayCommand(ConfirmTopicNameAsync, () => IsTopicNameValid);
            SaveCommand = new AsyncRelayCommand(SaveFlashcardsAsync, () => IsDirty);
            // learning session
            RestartLearningSessionCommand = new AsyncRelayCommand(RestartLearningSessionAsync, () => _learningQueue is not null);
            ToggleLearningSessionCommand = new AsyncRelayCommand(ToggleLearningSessionAsync, () => CurrentFlashcard is not null);
            MarkCorrectCommand = new AsyncRelayCommand(MarkCorrectAsync, CanExecuteMarkCommand);
            MarkIncorrectCommand = new RelayCommand(MarkIncorrect, CanExecuteMarkCommand);
        }
    }
}