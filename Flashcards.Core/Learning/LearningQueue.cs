using Flashcards.Core.Entities;

namespace Flashcards.Core.Learning
{
    /// <summary>
    /// Holds the flashcards of an in-progress learning session and controls
    /// their order, allowing incorrectly answered cards to be requeued a few
    /// positions ahead instead of only at the end.
    /// </summary>
    public class LearningQueue
    {
        /// <summary>Decides when a card leaves the queue and how far ahead it's requeued after an answer.</summary>
        private readonly IRequeuePolicy _policy;

        /// <summary>The cards still in the queue, in their current (possibly reordered) sequence.</summary>
        private readonly LinkedList<LearningSessionCard> _cards = new LinkedList<LearningSessionCard>();

        /// <summary>The node holding the card currently being reviewed, or null once the queue is finished.</summary>
        private LinkedListNode<LearningSessionCard>? _current;

        /// <summary>Number of cards removed from the queue so far (RequeuePolicy decided they're learned).</summary>
        public int LearnedCount { get; private set; }

        /// <summary>Total number of cards this queue started with, before any were removed.</summary>
        public int TotalCount { get; private set; }

        /// <summary>
        /// Creates a learning session queue over the given flashcards, in the order provided.
        /// </summary>
        /// <param name="flashcards">
        /// The flashcards to review in this session. Must be non-empty — callers are
        /// responsible for ensuring a topic has at least one flashcard before starting
        /// a learning session.
        /// </param>
        /// <param name="policy">The policy deciding when a card leaves the queue and how far ahead it is requeued.</param>
        public LearningQueue(List<Flashcard> flashcards, IRequeuePolicy policy)
        {
            _policy = policy;
            // initialising linked list from flashcard list
            foreach (Flashcard f in flashcards)
            {
                LearningSessionCard card = new LearningSessionCard() { Flashcard = f };
                _cards.AddLast(card);
            }
            _current = _cards.First;
            TotalCount = _cards.Count;
            LearnedCount = 0;
        }


        /// <summary>The card currently being reviewed, or <c>null</c> if the session is finished.</summary>
        public LearningSessionCard? Current => _current?.Value;

        /// <summary>True once there is no current card left to review.</summary>
        public bool IsFinished => _current is null;


        /// <summary>
        /// Records a correct answer for the current card, then either removes it from
        /// the queue (if <see cref="IRequeuePolicy.ShouldRemove"/> says it's been learned)
        /// or simply advances to the next card.
        /// </summary>
        public void MarkCorrect()
        {
            if (_current is null)
                return;

            Current!.SessionCorrectCount++;  // null-forgiving operator: _current is not null -> Current is not null
            if (_policy.ShouldRemove(Current))
                RemoveCurrentAndAdvance();
            else
                RequeueAhead(_policy.StepsAhead(Current, true));
        }


        /// <summary>
        /// Records an incorrect answer for the current card and requeues it a number
        /// of positions ahead, as determined by <see cref="IRequeuePolicy.StepsAhead"/>.
        /// </summary>
        public void MarkIncorrect()
        {
            if (_current is null)
                return;

            Current!.SessionMissCount++;  // null-forgiving operator: _current is not null -> Current is not null
            RequeueAhead(_policy.StepsAhead(Current, false));
        }


        /// <summary>
        /// Removes the current card from the queue entirely and advances to what was the next card, wrapping around to the
        /// front if it was the last one.
        /// </summary>
        /// <remarks>
        /// Does nothing if the queue is already finished (<see cref="IsFinished"/>). If the removed card was the only one
        /// in the queue, the queue becomes finished.
        /// </remarks>
        public void RemoveCurrentAndAdvance()
        {
            if (_current is null)
                return;
        
            LinkedListNode<LearningSessionCard> originalCurrent = _current;  // storing reference for later deletion

            bool isOnlyCard = _current.Previous is null && _current.Next is null;
            if (isOnlyCard)  // removing reference - card will be deleted, nowhere to advance
                _current = null;
            else
                Advance();
            _cards.Remove(originalCurrent);
            LearnedCount++;
        
        }


        /// <summary>
        /// Moves to the next card in the queue without changing its contents or order, wrapping around to the front if the
        /// current card was the last one.
        /// </summary>
        /// <remarks>
        /// Does nothing if the queue is already finished (<see cref="IsFinished"/>). Since cards only leave the queue via
        /// <see cref="RemoveCurrentAndAdvance"/>, repeated calls will keep cycling through the remaining cards rather than
        /// ever finishing the queue on their own.
        /// </remarks>
        public void Advance() 
        {
            if (_current is null)
                return;

            _current = _current.Next ?? _cards.First;  // wrap around when reaching the end
                                                       // _current is not null -> _cards.First exists
        }


        /// <summary>
        /// Removes the current card from its position and reinserts it further ahead
        /// in the queue, then advances to what was the next card before the move.
        /// </summary>
        /// <param name="stepsAhead">
        /// How many cards to skip over when reinserting. If this is at least the number
        /// of other cards in the queue, the card is left at its original position instead
        /// (the maximum meaningful distance is one full pass through the remaining cards).
        /// </param>
        /// <remarks>
        /// Does nothing if the queue is already finished (<see cref="IsFinished"/>).
        /// </remarks>
        public void RequeueAhead(uint stepsAhead)
        {
            if (_current is null)
                return; // nothing to requeue

            // keeping the reference to the card being moved before advancing _current
            LinkedListNode<LearningSessionCard> cardToMove = _current;
            _current = _current.Next ?? _cards.First;  // wrap around when reaching the end

            int remainingCardsCount = _cards.Count - 1;
            if (remainingCardsCount <= 0)  // no other card to requeue behind — leave it where it is
                return;

            // rescheduling
            if (stepsAhead >= remainingCardsCount || stepsAhead == 0)  // requested distance covers the whole queue or 0 steps — leave it where it is
                return;

            // card behind which cardToMove will be reinserted; captured before cardToMove removal
            LinkedListNode<LearningSessionCard>? insertionPoint = cardToMove.Next ?? _cards.First;
            // deleting the card from the original position before requeuing
            _cards.Remove(cardToMove);  // .Previous, .Next are set to null

            // insertionPoint already accounts for 1 step, so walk the remaining (stepsAhead - 1)
            uint remainingSteps = stepsAhead -1;  // steps = num of "edges" between the nodes that we travel
            while (remainingSteps > 0)          
            {
                remainingSteps--;
                if (insertionPoint?.Next is null)
                    insertionPoint = _cards.First; // wrap around at the end
                else
                    insertionPoint = insertionPoint.Next;
            }

            // insertion at the new position
            if (insertionPoint is null)
                _cards.AddLast(cardToMove.Value);
            else
                _cards.AddAfter(insertionPoint, cardToMove.Value);
        }

        /// <summary>
        /// Returns the flashcard IDs currently in the queue, in physical order starting from the front of the queue
        /// (independent of <see cref="Current"/> ). Intended for diagnostics and testing.
        /// </summary>
        /// <remarks>
        /// Cards in the queue are expected to already be persisted and have a valid, non-zero <c>Flashcard.Id</c> — true
        /// for any <see cref="LearningQueue"/> used in its intended context, during an actual learning session. If a card's
        /// <c>Id</c> is null (e.g. some test setups with unsaved cards), it is represented as <c>0</c> in the returned
        /// sequence, which serves as a visible marker of a card without a real ID.
        /// </remarks>
        internal IEnumerable<long> GetQueueOrder()
        {
            if (_current is null)
                yield break;

            LinkedListNode<LearningSessionCard> node = _cards.First!;  // _current is not null -> _cards.First exists

            for (int i = 0; i < _cards.Count; i++)
            {
                yield return node.Value.Flashcard.Id ?? 0;

                if (i < _cards.Count - 1)
                    node = node.Next ?? _cards.First!;
            }

        }
    }
}
