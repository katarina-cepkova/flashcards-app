using Flashcards.Core.Entities;
using Flashcards.Core.Learning;

namespace Flashcards.App.ViewModels
{
    partial class MainViewModel
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

    }
}
