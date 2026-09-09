using Flashcards.App.Dialogs;
using Flashcards.Core.Entities;

using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Controls.Primitives;


namespace Flashcards.App.ViewModels
{
    partial class MainViewModel
    {
        #region Flashcards and navigation

        /// <summary>The flashcards belonging to the currently open set.</summary>
        public ObservableCollection<Flashcard> Cards => _flashcardManager.Cards;

        /// <summary>The flashcard currently shown/edited/studied.</summary>
        public Flashcard? CurrentFlashcard => CurrentState == Models.AppState.LearningSession
            ? _learningQueue?.Current?.Flashcard : _flashcardManager.CurrentFlashcard;

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
    }
}
