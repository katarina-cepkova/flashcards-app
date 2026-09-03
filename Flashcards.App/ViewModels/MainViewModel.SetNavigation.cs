using Flashcards.App.Models;
using Flashcards.App.Services;
using Flashcards.Core.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Flashcards.App.ViewModels
{
    partial class MainViewModel
    {
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

        /// <summary>
        /// Sets _topic to newTopic (null for "nothing open", or a fresh/selected Topic) and _flashcardManager to wrap
        /// the given cards (empty by default), re-raising every property that depends on either. Shared by every
        /// transition that starts a set navigation move (close, create, select, open).
        /// </summary>
        private void SetOpenTopic(Topic? newTopic = null, IEnumerable<Flashcard>? cards = null)
        {
            _topic = newTopic;
            OnPropertyChanged(nameof(TopicName));
            OnPropertyChanged(nameof(IsTopicNameValid));
            OnPropertyChanged(nameof(TopicNameValidationMessage));
            ReplaceFlashcardManager(cards ?? Array.Empty<Flashcard>());

            _learningQueue = null;
            _learningSessionInProgress = false;

            // IsDirty and CurrentState are deliberately NOT touched here — callers need different
            // values for each (e.g. CreatingSet keeps a non-null draft topic, SelectingSet needs a
            // different target state than ClosedSet), and IsDirty is usually already false by the
            // time this runs (see CloseTopic/EnterSelectingSetAsync below).
        }



        /// <summary>
        /// Clears the currently open/draft topic and its flashcards, returning the app to ClosedSet —
        /// shared by discarding an in-progress draft (CreatingSet) and finishing a real deletion
        /// (DeleteOrLeaveSetAsync), since both end up in the same empty state.
        /// </summary>
        private void CloseTopic()
        {
            SetOpenTopic();
            IsDirty = false;  // reached from CreatingSet (never set IsDirty) or after a delete
            CurrentState = AppState.ClosedSet;
        }

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
        /// Persists the currently open set's cards (inserts, updates, and discards tombstones) via
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

            string message = ((string)_resources["UnsavedChanges_Message"]).Replace("@", _topic?.Name ?? "");
            string caption = (string)_resources["UnsavedChanges_Caption"];

            MessageBoxResult result = MessageBox.Show(message, caption, MessageBoxButton.YesNoCancel);
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
                return;  // user cancelled — nothing here has run yet, so nothing to undo

            await EnsureTopicsLoadedAsync();
            SetOpenTopic(new Topic { Name = "" });
            CurrentState = AppState.CreatingSet;
            // No IsDirty reset needed: TrySaveOpenSetIfNeededAsync above already guarantees it's false.
        }

        /// <summary>Starts creating a new flashcard set, saving the currently open one first if needed.</summary>
        public ICommand CreateSetCommand { get; }

        /// <summary>
        /// Raised after a topic name is successfully created or renamed, so the view can move focus away from
        /// TopicNameTextBox as feedback that the action happened.
        /// </summary>
        public event EventHandler? TopicNameConfirmed;

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
                // IsDirty is left untouched: it only tracks card edits, and renaming doesn't touch cards.
            }
            TopicNameConfirmed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Confirms the current topic name — creates a new topic or renames the open one, depending on context.</summary>
        public ICommand ConfirmTopicNameCommand { get; }

        #endregion

        #region Select set

        /// <summary>
        /// Saves the currently open set (if any), then loads/refreshes the topic list and switches to
        /// SelectingSet — the topic-selection table becomes visible while the rest of the card-editing
        /// UI stays disabled.
        /// </summary>
        private async Task EnterSelectingSetAsync()
        {
            if (!await TrySaveOpenSetIfNeededAsync())
                return;  // user cancelled

            await RefreshTopicsAsync();

            if (TopicsForSelection.Count == 0)
            {
                // Nothing to pick from — bail out before switching state, so the selection screen
                // never shows up empty and confusing. The app stays exactly where it was.
                MessageBox.Show(
                    (string)_resources["NoExistingSets_Message"],
                    (string)_resources["NoExistingSets_Caption"],
                    MessageBoxButton.OK
                );
                return;
            }

            SetOpenTopic();
            CurrentState = AppState.SelectingSet;
            // No IsDirty reset needed here either: TrySaveOpenSetIfNeededAsync above already guarantees it.
        }

        /// <summary>Opens the topic-selection list, saving the currently open set first if needed.</summary>
        public ICommand EnterSelectingSetCommand { get; }

        private TopicListItem? _selectedTopicToOpen;

        /// <summary>The currently highlighted row in the topic-selection list, if any.</summary>
        public TopicListItem? SelectedTopicToOpen
        {
            get => _selectedTopicToOpen;
            set
            {
                _selectedTopicToOpen = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Loads the selected topic's flashcards and switches to OpenedSetView — used by both the
        /// "Open" button and double-clicking a row in the selection list.
        /// </summary>
        private async Task OpenSelectedSetAsync()
        {
            if (SelectedTopicToOpen is not TopicListItem topicItem)
                throw new InvalidOperationException("Cannot open: no topic selected.");

            // No TrySaveOpenSetIfNeededAsync/SetOpenTopic call needed: EnterSelectingSetAsync
            // already cleared any previously open set (and confirmed saving it) on the way into
            // SelectingSet, so there's nothing left to lose here.
            IReadOnlyList<Flashcard> cards = await _flashcardRepository.GetByTopicIdAsync(topicItem.Topic.Id!.Value);
            SetOpenTopic(topicItem.Topic, cards);

            CurrentState = AppState.OpenedSetView;
        }

        /// <summary>Opens the currently selected topic in the selection list.</summary>
        public ICommand OpenSelectedSetCommand { get; }

        #endregion


        #region Delete set

        /// <summary>
        /// Deletes, discards, or cancels — depending on context. In OpenedSetView/OpenedSetEdit,
        /// deletes the saved topic after confirmation. In CreatingSet, discards the in-progress draft
        /// with no confirmation. In SelectingSet, cancels the current selection. All paths return to
        /// ClosedSet.
        /// </summary>
        private async Task DeleteOrLeaveSetAsync()
        {
            if (CurrentState == AppState.SelectingSet)
            // Cancelling a selection — nothing was ever opened (EnterSelectingSetAsync already
            // cleared _topic/_flashcardManager), so there's nothing to reset here beyond the
            // selection itself and the state.
            {
                SelectedTopicToOpen = null;
                CurrentState = AppState.ClosedSet;
                return;
            }

            // Discarding a draft — nothing persisted yet, no confirmation needed.
            if (CurrentState == AppState.CreatingSet)
            {
                CloseTopic();
                return;
            }
            if (_topic?.Id is not long topicId)
                throw new InvalidOperationException("Cannot delete: no saved topic is open.");

            // Persisted set — confirming with the user, since this is the one irreversible path.
            string message = ((string)_resources["DeleteSet_Message"]).Replace("@", _topic.Name);
            string caption = (string)_resources["DeleteSet_Caption"];

            MessageBoxResult result = MessageBox.Show(message, caption, MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
                return;

            // Deleting — the topic's flashcards cascade-delete with it in the database, so only the
            // in-memory AvailableTopics list needs an explicit removal to match.
            await _topicRepository.DeleteAsync(topicId);
            AvailableTopics.Remove(_topic);

            CloseTopic();
        }

        /// <summary>
        /// Deletes the currently open flashcard set (with confirmation), or discards the in-progress
        /// draft if still naming a new one. Enabled whenever a set is open or being created.
        /// </summary>
        public ICommand DeleteOrLeaveSetCommand { get; }



        #endregion
    }
}