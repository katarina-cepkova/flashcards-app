namespace Flashcards.Core.Learning
{
    namespace Flashcards.Core.Learning
    {
        /// <summary>
        /// Strategy for deciding, after each answer, how a <see cref="LearningQueue"/> should
        /// treat a card: whether it has been learned well enough to leave the queue, and if not,
        /// how far ahead it should be requeued.
        /// </summary>
        public interface IRequeuePolicy
        {
            /// <summary>
            /// Decides whether a card has been answered well enough this session to be removed
            /// from the queue entirely.
            /// </summary>
            /// <param name="card">The card to evaluate, with its session correct/miss counts so far.</param>
            /// <returns><c>true</c> if the card should be removed from the queue; otherwise <c>false</c>.</returns>
            public bool ShouldRemove(LearningSessionCard card);

            /// <summary>
            /// Computes how many positions ahead a card that was not removed by <see cref="ShouldRemove"/>
            /// should be requeued.
            /// </summary>
            /// <param name="card">The card to schedule, with its session correct/miss counts so far.</param>
            /// <param name="wasLastAnswerCorrect">Whether the answer that triggered this call was correct.</param>
            /// <returns>The number of steps ahead to requeue the card.</returns>
            public uint StepsAhead(LearningSessionCard card, bool wasLastAnswerCorrect);
        }
    }
}
