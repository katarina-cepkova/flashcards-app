using Flashcards.App.Common;
using Flashcards.Core.Entities;
using System.Collections.ObjectModel;

namespace Flashcards.App.Services
{
    /// <summary>
    /// Manages the currently open set's flashcards: which one is selected, and
    /// navigation between them that skips over cards marked IsDeleted (a "tombstone"
    /// left in place until the set is saved, at which point deleted cards are purged
    /// for real and Cards shrinks to match).
    /// </summary>
    internal class FlashcardManager : ObservableObject
    {
        /// <summary>The cards belonging to the currently open set, including deleted tombstones until the set is saved.</summary>
        public ObservableCollection<Flashcard> Cards { get; }

        private int _physicalIndex = -1;

        /// <summary>The physical index into Cards of the currently selected card, or -1 if none is selected.</summary>
        public int PhysicalIndex
        {
            get => _physicalIndex;
            private set
            {
                if (_physicalIndex == value) return;

                _physicalIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentFlashcard));
                OnPropertyChanged(nameof(LogicalIndex));
            }
        }

        /// <summary>
        /// The position of CurrentFlashcard among only the active (non-deleted) cards —
        /// e.g. 2 means CurrentFlashcard is the 3rd active card, regardless of how many
        /// deleted tombstones sit before it in Cards. Used by CardNavigationScrollBar,
        /// whose range (0 to ActiveFlashcardCount - 1) only ever counts active cards.
        /// </summary>
        public int LogicalIndex => CalculateLogicalIndex(PhysicalIndex);

        private int CalculateLogicalIndex(int physicalIndex)
        {
            if (physicalIndex < 0)
                return -1;

            int logicalIndex = -1;
            for (int i = 0; i <= physicalIndex; i++)
            {
                if (!Cards[i].IsDeleted)
                    logicalIndex++;
            }
            return logicalIndex;
        }

        /// <summary>The card at PhysicalIndex, or null if PhysicalIndex is -1 (nothing selected).</summary>
        public Flashcard? CurrentFlashcard => PhysicalIndex >= 0 ? Cards[PhysicalIndex] : null;

        private int _activeFlashcardCount;

        /// <summary>
        /// Number of cards in Cards not marked IsDeleted — the count a user would actually consider "in the set", since
        /// deleted cards remain physically present (as tombstones) until the set is saved.
        /// </summary>
        public int ActiveFlashcardCount
        {
            get => _activeFlashcardCount;
            set
            {
                if (value < 0 || value > Cards.Count)
                    return;
                _activeFlashcardCount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Creates the manager for the given set of cards, selecting the first one (if any).
        /// </summary>
        public FlashcardManager(IEnumerable<Flashcard> cards)
        {
            Cards = new ObservableCollection<Flashcard>(cards);
            if (Cards.Count > 0)
                PhysicalIndex = 0;
            // Cards passed in are always freshly loaded (from the database, or newly created
            // in memory) — IsDeleted is guaranteed false on all of them at this point, since
            // deleted cards are purged for real on save and never persisted as tombstones.
            // ActiveFlashcardCount can therefore start as Cards.Count directly, without
            // filtering, since IsDeleted=true can only arise from a delete during the current
            // editing session, after construction.
            ActiveFlashcardCount = Cards.Count;
        }

        /// <summary>Selects the given card by reference, if it's found in Cards.</summary>
        public void SelectCard(Flashcard card)
        {
            for (int i = 0; i < Cards.Count; i++)
            {
                if (ReferenceEquals(Cards[i], card))
                {
                    PhysicalIndex = i;
                    return;
                }
            }
        }

        /// <summary>
        /// Translates a logical position among active (non-deleted) cards — e.g. "the 3rd
        /// active card" — into its physical index in Cards. Used by SelectIndex, since
        /// CardNavigationScrollBar's positions (0 to ActiveFlashcardCount - 1) count only
        /// active cards, while Cards itself may have deleted cards interspersed as tombstones.
        /// </summary>
        private int? FindPhysicalIndexOfActiveCard(int activeIndex)
        {
            int activeCount = -1;
            for (int i = 0; i < Cards.Count; i++)
            {
                if (Cards[i].IsDeleted)
                    continue;

                activeCount++;
                if (activeCount == activeIndex)
                    return i;
            }
            return null;
        }


        /// <summary>
        /// Selects the card at the given logical (active-card) position — e.g. activeIndex=2 selects
        /// the 3rd non-deleted card — translating it to a physical index via FindPhysicalIndexOfActiveCard.
        /// Does nothing if activeIndex doesn't correspond to any active card.
        /// </summary>
        public void SelectIndex(int activeIndex)
        {
            // changed logical index -> physical index
            if (FindPhysicalIndexOfActiveCard(activeIndex) is int physicalIndex)
                PhysicalIndex = physicalIndex;
        }


        /// <summary>
        /// Finds the index of the nearest non-deleted card in the given direction from
        /// `fromIndex`, or null if there isn't one. Shared by CanMoveToNext/MoveToNext and
        /// their Previous counterparts, so the "skip deleted cards" logic exists in one place.
        /// </summary>
        private int? FindNextActiveIndex(int fromIndex, int step)
        {
            int i = fromIndex + step;
            while (i >= 0 && i < Cards.Count)
            {
                if (!Cards[i].IsDeleted)
                    return i;
                i += step;
            }
            return null;
        }

        /// <summary>True if there's a non-deleted card after the current one.</summary>
        public bool CanMoveToNext()
        {
            int? nextIndex = FindNextActiveIndex(PhysicalIndex, +1);
            return nextIndex != null;

        }

        /// <summary>Moves to the nearest non-deleted card after the current one, if any.</summary>
        public void MoveToNext()
        {
            int? nextIndex = FindNextActiveIndex(PhysicalIndex, +1);
            if (nextIndex != null)
                PhysicalIndex = nextIndex.Value;
        }

        /// <summary>True if there's a non-deleted card before the current one.</summary>
        public bool CanMoveToPrevious()
        {
            int? previousIndex = FindNextActiveIndex(PhysicalIndex, -1);
            return previousIndex != null;
        }

        /// <summary>Moves to the nearest non-deleted card before the current one, if any.</summary>
        public void MoveToPrevious()
        {
            int? previousIndex = FindNextActiveIndex(PhysicalIndex, -1);
            if (previousIndex != null)
                PhysicalIndex = previousIndex.Value;
        }

        /// <summary>Appends a new card to the end of Cards and selects it as the current card.</summary>
        public void AddCard(Flashcard card)
        {
            Cards.Add(card);
            ActiveFlashcardCount++;
            // making the added card the CurrentFlashcard
            PhysicalIndex = Cards.Count - 1;

        }

        /// <summary>
        /// Marks the current card as deleted (tombstone, not physically removed) and moves selection
        /// to the nearest remaining active card — first searching forward, then backward if none
        /// follows. Does nothing if no card is currently selected.
        /// </summary>
        public void DeleteCurrentFlashcard()
        {
            if (CurrentFlashcard is null) return;
            CurrentFlashcard.IsDeleted = true;
            ActiveFlashcardCount--;
            
            PhysicalIndex = FindNextActiveIndex(PhysicalIndex, +1) ?? FindNextActiveIndex(PhysicalIndex, -1) ?? -1;
        }
    }
}