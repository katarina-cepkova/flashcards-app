using Flashcards.App.Models;
using Flashcards.Core.Entities;
using Flashcards.Core.Learning;
using System.Windows;
using System.Windows.Input;

namespace Flashcards.App.ViewModels
{
    internal partial class MainViewModel
    {
        /// <summary>
        /// The active/paused learning session's card queue for the currently open topic, or null if none has started
        /// yet for this topic. Survives leaving LearningSession (see LeaveLearningSessionAsync) so a later
        /// EnterLearningSessionAsync can offer to resume it.
        /// </summary>
        private LearningQueue? _learningQueue;

        /// <summary>
        /// True once at least one card in the current session has been marked correct/incorrect.
        /// </summary>
        private bool _learningSessionInProgress;

        /// <summary>Shared source of randomness for Shuffle, so each call doesn't construct a new Random.</summary>
        private static readonly Random _random = new();

        /// <summary>Number of cards fully learned (removed from the queue) so far this session — used for a progress bar. 0 if no session has started yet.</summary>
        public int LearnedCardCount => _learningQueue?.LearnedCount ?? 0;

        /// <summary>Total cards this learning session started with — used as a progress bar's Maximum. 0 if no session has started yet.</summary>
        public int TotalLearningCardCount => _learningQueue?.TotalCount ?? 0;

        /// <summary>
        /// Builds a fresh LearningQueue from the currently open set's active flashcards, in
        /// shuffled order, discarding any previous session's queue/progress ordering (not the
        /// persisted CorrectAnswersCount/IncorrectAnswersCount on the cards themselves).
        /// </summary>
        private void StartNewLearningQueue()
        {
            IRequeuePolicy policy = new RequeuePolicy(
                minCorrectAnswers: 2, maxCorrectAnswers: 5, minCorrectRatio: 0.7,
                baseSteps: 5, minSteps: 3, maxSteps: 10,
                correctWeight: 1.5, incorrectWeight: 0.3);


            List<Flashcard> activeCards;
            // no tombstones
            if (_flashcardManager.ActiveFlashcardCount == _flashcardManager.Cards.Count)
                activeCards = _flashcardManager.Cards.ToList();
            // filters tombstones if user chose not to save changes
            else
                activeCards = _flashcardManager.Cards.Where(c => !c.IsDeleted).ToList();

            Shuffle(activeCards);
            _learningSessionInProgress = false;  // fresh queue, nothing marked yet
            _learningQueue = new LearningQueue(activeCards, policy);
            OnPropertyChanged(nameof(LearnedCardCount));
            OnPropertyChanged(nameof(TotalLearningCardCount));
        }

        /// <summary>
        /// Shuffles a list in place using the Fisher-Yates algorithm: walks the list from the end to the start, and at
        /// each position swaps the current element with a randomly chosen element from the "unshuffled" portion still
        /// ahead of it (positions 0 to i inclusive). Each pass shrinks the unshuffled portion by one, so every element
        /// ends up moved exactly once into a uniformly random final position — unlike naive approaches (e.g. sorting by
        /// a random key), this is provably unbiased: every possible ordering is equally likely.
        /// </summary>
        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }


        #region Restart session
        /// <summary>
        /// Discards the current learning queue's ordering and any unsaved in-session answer count changes, reloading
        /// the set's cards fresh from the database before building a new shuffled queue — a full reset, not just a
        /// reshuffle.
        /// </summary>
        private async Task RestartLearningSessionAsync()
        {
            if (_learningQueue is null)
                throw new InvalidOperationException("Cannot restart learning session that has not started yet.");

            if (_topic?.Id is not long topicId)
                throw new InvalidOperationException("Cannot restart: no saved topic is open.");

            ReplaceFlashcardManager(await _flashcardRepository.GetByTopicIdAsync(topicId));
            IsDirty = false;

            StartNewLearningQueue();
            OnPropertyChanged(nameof(CurrentFlashcard));
            OnPropertyChanged(nameof(DisplayedText));
        }

        /// <summary>
        /// Restarts the learning session from scratch: reloads the set's cards from the database (discarding unsaved
        /// answer-count changes from this session) and builds a freshly shuffled queue.
        /// </summary>
        public ICommand RestartLearningSessionCommand { get; }

        #endregion

        #region Toggle session
        /// <summary>
        /// Starts (or resumes) a learning session over the currently open set's active flashcards. Any pending
        /// edit-mode changes are saved automatically first (no prompt). If a session is already in progress and
        /// unfinished, asks whether to resume it or start a fresh one; an untouched queue is resumed silently, with
        /// no prompt. Requires at least one active card — the button should already be disabled otherwise, but this
        /// guards defensively.
        /// </summary>
        private async Task EnterLearningSessionAsync()
        {
            if (_flashcardManager.ActiveFlashcardCount == 0)
                throw new InvalidOperationException("Cannot start a learning session: no flashcards in this set.");

            // Any pending edits are saved automatically here (no prompt) — entering a learning session
            // is a natural save point, and this keeps LeaveLearningSessionAsync's later prompt honestly
            // about session progress only, not unrelated edit-mode changes.
            if (IsDirty)
                await SaveFlashcardsAsync();

            // resuming
            if (_learningQueue is not null && !_learningQueue.IsFinished)
            {
                if (_learningSessionInProgress)
                {
                    MessageBoxResult result = MessageBox.Show(
                    (string)_resources["ResumeLearningSession_Message"],
                    (string)_resources["ResumeLearningSession_Caption"],
                    MessageBoxButton.YesNo);

                    if (result == MessageBoxResult.No)
                        await RestartLearningSessionAsync(); // discard progress + reload from DB, not just reshuffle
                }
                else
                {
                    // queue exists, nothing marked yet — just resume silently, no need to ask
                }
            }
            else
                StartNewLearningQueue(); // no existing queue at all — nothing to discard, just build fresh

            CurrentState = AppState.LearningSession;
            OnPropertyChanged(nameof(CurrentFlashcard));
            OnPropertyChanged(nameof(DisplayedText));
        }

        /// <summary>
        /// Leaves the learning session, prompting to save if there are unsaved changes (e.g. answer
        /// counts updated by MarkCorrect/MarkIncorrect this session). Choosing not to save reloads the
        /// set's cards from the database, discarding any in-memory changes since the last save.
        /// _learningQueue itself is left untouched either way, so a later EnterLearningSessionAsync can
        /// still offer to resume it.
        /// </summary>
        private async Task LeaveLearningSessionAsync()
        {
            if (IsDirty)
            {
                MessageBoxResult result = MessageBox.Show(
                    (string)_resources["LeaveLearningSession_Message"],
                    (string)_resources["LeaveLearningSession_Caption"],
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                    await SaveFlashcardsAsync();
                else
                {
                    if (_topic?.Id is not long topicId)
                        throw new InvalidOperationException("Cannot reload cards: no saved topic is open.");

                    // _learningQueue was built from references to the same Flashcard instances
                    // _flashcardManager holds, so MarkCorrect/MarkIncorrect's CorrectAnswersCount++/
                    // IncorrectAnswersCount++ mutated those objects in place. A no-op here would leave the
                    // increments sitting in memory — reloading from the DB replaces those objects
                    ReplaceFlashcardManager(await _flashcardRepository.GetByTopicIdAsync(topicId));
                    IsDirty = false;  // reload discarded the in-memory changes IsDirty was tracking
                }
            }

            CurrentState = AppState.OpenedSetView;
            OnPropertyChanged(nameof(CurrentFlashcard));
            OnPropertyChanged(nameof(DisplayedText));
        }

        /// <summary>
        /// Handles the LearningSessionToggle's Click: enters/resumes the session if not currently in one, or leaves it
        /// (with a save prompt if needed) if currently in one. Reads CurrentState directly rather than a
        /// CommandParameter from the ToggleButton.
        /// </summary>
        private async Task ToggleLearningSessionAsync()
        {
            if (CurrentState == AppState.LearningSession)
                await LeaveLearningSessionAsync();
            else
                await EnterLearningSessionAsync();
        }

        /// <summary>Toggles the learning session on/off, based on the ToggleButton's new checked state.</summary>
        public ICommand ToggleLearningSessionCommand { get; }

        #endregion

        #region Learning

        /// <summary>
        /// Flips the current card back to front and re-raises the bindings that depend on it, so the next card in the
        /// queue starts unrevealed.
        /// </summary>
        private void ResetNextCard()
        {
            // flipping to front so user does not see answer first
            IsFront = true;
            OnPropertyChanged(nameof(CurrentFlashcard));
            OnPropertyChanged(nameof(DisplayedText));
        }

        /// <summary>
        /// Records a correct answer: increments the counter on the current card, advances the LearningQueue (which may
        /// remove the card entirely if RequeuePolicy says it's learned, or requeue it further ahead), and resets to the
        /// next card via ResetNextCard. Ends the session automatically if the queue is now finished.
        /// </summary>
        private async Task MarkCorrectAsync()
        {
            // nothing left to learn
            if (CurrentFlashcard is null || _learningQueue is null) return;
            _learningSessionInProgress = true;

            // increment the correct answer count on the card -> future persisting
            CurrentFlashcard.CorrectAnswersCount++;
            IsDirty = true;

            _learningQueue.MarkCorrect();
            OnPropertyChanged(nameof(LearnedCardCount));
            ResetNextCard();

            // last card marked correct = queue is finished
            if (_learningQueue.IsFinished)
                await FinishLearningSessionAsync();
        }

        /// <summary>Marks the current card as answered correctly.</summary>
        public ICommand MarkCorrectCommand { get; }

        /// <summary>
        /// Records an incorrect answer: increments the counter on the current card, requeues it (via LearningQueue) a
        /// number of positions ahead based on RequeuePolicy, and resets to the next card via ResetNextCard.
        /// </summary>
        private void MarkIncorrect()
        {
            // nothing left to learn
            if (CurrentFlashcard is null || _learningQueue is null) return;
            _learningSessionInProgress = true;
            // increment the incorrect answer count on the card -> future persisting
            CurrentFlashcard.IncorrectAnswersCount++;
            IsDirty = true;

            _learningQueue.MarkIncorrect();
            ResetNextCard();
        }

        /// <summary>Marks the current card as answered incorrectly.</summary>
        public ICommand MarkIncorrectCommand { get; }

        /// <summary>
        /// Saves the session's answer counts and returns to OpenedSetView once the learning queue is
        /// finished (every card met the requeue policy's removal criteria).
        /// </summary>
        private async Task FinishLearningSessionAsync()
        {
            await SaveFlashcardsAsync();

            // Brief pause so the user actually sees the progress bar reach 100% before the view
            // switches away — without this, completing the last card and leaving LearningSession
            // happen in the same instant, and the filled bar is never visible.
            await Task.Delay(800);

            CurrentState = AppState.OpenedSetView;
            OnPropertyChanged(nameof(CurrentFlashcard));
            OnPropertyChanged(nameof(DisplayedText));
        }

        /// <summary>
        /// True when there's a card to mark, an active learning queue, and CurrentState is
        /// LearningSession — shared canExecute condition for MarkCorrectCommand/MarkIncorrectCommand,
        /// so a stray Left/Right keypress (or stale command state) can't mark a card outside an active
        /// session, even though _learningQueue itself can still exist while paused in edit mode.
        /// </summary>
        private bool CanExecuteMarkCommand()
        {
            return CurrentFlashcard is not null
                && _learningQueue is not null
                && _currentState == AppState.LearningSession;
        }

        #endregion

    }
}
