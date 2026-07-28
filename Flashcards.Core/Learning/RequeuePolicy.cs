namespace Flashcards.Core.Learning
{
    /// <summary>
    /// Requeue policy that removes a card once it has been answered correctly enough times,
    /// and otherwise decides how far ahead to requeue it based on the card's overall
    /// correctness ratio for the session, adjusted up or down depending on whether the
    /// most recent answer was correct.
    /// </summary>
    internal class RequeuePolicy : IRequeuePolicy
    {
        private readonly uint _minCorrectAnswers;
        private readonly uint _maxCorrectAnswers;
        private readonly double _minCorrectRatio;

        private readonly uint _baseSteps;
        private readonly uint _minSteps;
        private readonly uint _maxSteps;

        private readonly double _correctWeight;
        private readonly double _incorrectWeight;

        /// <summary>
        /// Creates a requeue policy with the given thresholds and step parameters.
        /// </summary>
        /// <param name="minCorrectAnswers">
        /// Minimum number of correct answers required, together with <paramref name="minCorrectRatio"/>,
        /// before a card can be removed from the queue. Must be greater than zero.
        /// </param>
        /// <param name="maxCorrectAnswers">
        /// Number of correct answers at which a card is always removed, regardless of its
        /// correctness ratio. Must be at least <paramref name="minCorrectAnswers"/>.
        /// </param>
        /// <param name="minCorrectRatio">
        /// Minimum correct-to-total-answers ratio, together with <paramref name="minCorrectAnswers"/>,
        /// required for a card to be removed. Must be between 0 and 1 inclusive.
        /// </param>
        /// <param name="baseSteps">
        /// The number of steps ahead a card is requeued when its correctness ratio is 1.0,
        /// before the recent-answer adjustment is applied. Must be greater than zero.
        /// </param>
        /// <param name="minSteps">
        /// The lower bound on the number of steps ahead a card can be requeued. Must be
        /// greater than zero.
        /// </param>
        /// <param name="maxSteps">
        /// The upper bound on the number of steps ahead a card can be requeued. Must be
        /// at least <paramref name="minSteps"/>.
        /// </param>
        /// <param name="correctWeight">
        /// Weight applied to <paramref name="baseSteps"/> to boost the step count when the
        /// most recent answer was correct. Must be positive.
        /// </param>
        /// <param name="incorrectWeight">
        /// Weight applied to <paramref name="baseSteps"/> to reduce the step count when the
        /// most recent answer was incorrect. Must be positive.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if any parameter violates the constraints described above.
        /// </exception>
        public RequeuePolicy(
            uint minCorrectAnswers, uint maxCorrectAnswers,
            double minCorrectRatio,
            uint baseSteps, uint minSteps, uint maxSteps,
            double correctWeight, double incorrectWeight)
        {
            if (minCorrectAnswers == 0)
                throw new ArgumentOutOfRangeException(nameof(minCorrectAnswers));

            if (maxCorrectAnswers < minCorrectAnswers)
                throw new ArgumentOutOfRangeException(nameof(maxCorrectAnswers));

            if (minCorrectRatio < 0 || minCorrectRatio > 1)
                throw new ArgumentOutOfRangeException(nameof(minCorrectRatio));

            if (minSteps == 0)
                throw new ArgumentOutOfRangeException(nameof(minSteps));

            if (maxSteps < minSteps)
                throw new ArgumentOutOfRangeException(nameof(maxSteps));

            if (baseSteps == 0)
                throw new ArgumentOutOfRangeException(nameof(baseSteps));

            if (correctWeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(correctWeight));

            if (incorrectWeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(incorrectWeight));

            _minCorrectAnswers = minCorrectAnswers;
            _maxCorrectAnswers = maxCorrectAnswers;
            _minCorrectRatio = minCorrectRatio;
            _baseSteps = baseSteps;
            _minSteps = minSteps;
            _maxSteps = maxSteps;
            _correctWeight = correctWeight;
            _incorrectWeight = incorrectWeight;
        }

        /// <summary>
        /// Decides whether a card has been learned well enough this session to leave the
        /// queue: either its correctness ratio and correct-answer count both meet their
        /// thresholds, or it has reached <c>maxCorrectAnswers</c> regardless of ratio.
        /// </summary>
        /// <param name="card">The card to evaluate, with its session correct/miss counts so far.</param>
        /// <returns>
        /// <c>true</c> if the card should be removed from the queue; <c>false</c> if it has not
        /// yet been answered at all, or does not meet the removal criteria.
        /// </returns>
        public bool ShouldRemove(LearningSessionCard card)
        {
            int totalAnswers = card.SessionCorrectCount + card.SessionMissCount;

            if (totalAnswers == 0)
                return false;

            double correctnessRatio = (double)card.SessionCorrectCount / totalAnswers;

            return
                (correctnessRatio >= _minCorrectRatio && card.SessionCorrectCount >= _minCorrectAnswers)
                || card.SessionCorrectCount >= _maxCorrectAnswers;
        }

        /// <summary>
        /// Computes how many positions ahead a card not yet removed from the queue should
        /// be requeued, based on its overall correctness ratio for the session, adjusted
        /// upward if the most recent answer was correct or downward if it was incorrect.
        /// </summary>
        /// <param name="card">The card to schedule, with its session correct/miss counts so far.</param>
        /// <param name="wasLastAnswerCorrect">Whether the answer that triggered this call was correct.</param>
        /// <returns>
        /// The number of steps ahead to requeue the card, clamped to the
        /// <c>[minSteps, maxSteps]</c> range configured on this policy.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the card has not been answered yet this session (no correct or miss
        /// count recorded), since a correctness ratio cannot be computed.
        /// </exception>
        public uint StepsAhead(LearningSessionCard card, bool wasLastAnswerCorrect)
        {
            int totalAnswers = card.SessionCorrectCount + card.SessionMissCount;
            if (totalAnswers == 0)
                throw new InvalidOperationException();

            double correctnessRatio = (double)card.SessionCorrectCount / totalAnswers;

            double recentAdjustment = wasLastAnswerCorrect
                ? _baseSteps * _correctWeight
                : -(_baseSteps * _incorrectWeight);

            double rawSteps = (_baseSteps * correctnessRatio) + recentAdjustment;

            if (rawSteps <= _minSteps)
                return _minSteps;

            if (rawSteps >= _maxSteps)
                return _maxSteps;

            return (uint)Math.Round(rawSteps);
        }
    }
}