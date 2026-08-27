using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using Flashcards.Core.Entities;
using Flashcards.App.Common;

namespace Flashcards.App.Services
{
    internal class FlashcardManager : ObservableObject
    {
        public ObservableCollection<Flashcard> Cards { get; }
        private int _index = -1;
        public int Index
        {
            get => _index;
            private set
            {
                if (_index == value) return;

                _index = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentFlashcard));
            }
        }

        public Flashcard? CurrentFlashcard => Index >= 0 ? Cards[Index] : null;

        public FlashcardManager(IEnumerable<Flashcard> cards)
        {
            Cards = new ObservableCollection<Flashcard>(cards);
            if (Cards.Count > 0)
                Index = 0;
        }

        public void SelectCard(Flashcard card)
        {
            for (int i = 0; i < Cards.Count; i++)
            {
                if (ReferenceEquals(Cards[i], card))
                {
                    Index = i;
                    return;
                }
            }
        }

        /// <summary>
        /// Jumps directly to the card at the given index — used by the scrollbar-based
        /// navigation, which needs to set an arbitrary position, not just step by one.
        /// </summary>
        public void SelectIndex(int index)
        {
            if (index < 0 || index >= Cards.Count) return;
            Index = index;
        }

        public bool CanMoveToNext() => Index >= 0 && Index < Cards.Count - 1;
        public void MoveToNext()
        {
            if (!CanMoveToNext()) return;
            Index++;
        }

        public bool CanMoveToPrevious() => Index > 0;
        public void MoveToPrevious()
        {
            if (!CanMoveToPrevious()) return;
            Index--;
        }
    }
}
