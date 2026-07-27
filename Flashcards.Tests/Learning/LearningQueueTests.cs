using Flashcards.Core.Entities;
using Flashcards.Core.Learning;

namespace Flashcards.Tests.Learning
{
    public class LearningQueueTests
    {
        private class FixedRequeuePolicy : IRequeuePolicy
        {
            private readonly uint _stepsAhead;

            public FixedRequeuePolicy() { }
            public FixedRequeuePolicy(uint stepsAhead)
            {
                _stepsAhead = stepsAhead;
            }

            public bool ShouldRemove(LearningSessionCard card) => true;

            public uint StepsAhead(LearningSessionCard card) => _stepsAhead;
        }

        private static LearningQueue CreateQueue(int cardCount, IRequeuePolicy? policy = null)
        {
            List<Flashcard> flashcards = new List<Flashcard>();
            for (int i = 1; i <= cardCount; i++)
            {
                flashcards.Add(new Flashcard { Id = i, TopicId = 1, FrontText = $"Card {i}" });
            }
            return new LearningQueue(flashcards, policy ?? new FixedRequeuePolicy());
        }


        [Fact]
        public void RemoveCurrentAndAdvance_OnlyCardInQueue_LeavesQueueFinished()
        {
            // arrange
            LearningQueue queue = CreateQueue(1, new FixedRequeuePolicy());
            // act
            queue.RemoveCurrentAndAdvance();
            // assert
            Assert.True(queue.IsFinished);
        }

        [Fact]
        public void RemoveCurrentAndAdvance_EmptyQueue_LeavesQueueFinished()
        {
            // arrange
            LearningQueue queue = CreateQueue(0, new FixedRequeuePolicy());
            // act
            queue.RemoveCurrentAndAdvance();
            // assert
            Assert.True(queue.IsFinished);
        }

        [Fact]
        public void RemoveCurrentAndAdvance_OnSecondToLastCard_MovesCurrentToLastRemainingCard()
        {
            // arrange
            LearningQueue queue = CreateQueue(2, new FixedRequeuePolicy());
            // act
            queue.RemoveCurrentAndAdvance();
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(2, queue.Current.Flashcard.Id);
        }

        [Fact]
        public void RemoveCurrentAndAdvance_OnLastCard_MovesCurrentToFirstCard()
        {
            // arrange
            LearningQueue queue = CreateQueue(2, new FixedRequeuePolicy());
            queue.Advance();

            // act
            queue.RemoveCurrentAndAdvance();
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(1, queue.Current.Flashcard.Id);
        }

        [Fact]
        public void RemoveCurrentAndAdvance_OnMiddleCard_MovesCurrentToLastCard()
        {
            // arrange
            LearningQueue queue = CreateQueue(3, new FixedRequeuePolicy());
            queue.Advance();

            // act
            queue.RemoveCurrentAndAdvance();
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(3, queue.Current.Flashcard.Id);
        }

        [Fact]
        public void Advance_OnlyCardInQueue_RewrapsToTheFirstCard()
        {
            // arrange
            LearningQueue queue = CreateQueue(1, new FixedRequeuePolicy());
            // act
            queue.Advance();
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(1, queue.Current.Flashcard.Id);
        }

        [Fact]
        public void Advance_EmptyQueue_LeavesQueueFinished()
        {
            // arrange
            LearningQueue queue = CreateQueue(0, new FixedRequeuePolicy());
            // act
            queue.Advance();
            // assert
            Assert.True(queue.IsFinished);
        }

        [Fact]
        public void Advance_OnSecondToLastCard_MovesCurrentToLastRemainingCard()
        {
            // arrange
            LearningQueue queue = CreateQueue(2, new FixedRequeuePolicy());
            // act
            queue.Advance();
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(2, queue.Current.Flashcard.Id);
        }

        [Fact]
        public void Advance_OnLastCard_RewrapsToTheFirstCard()
        {
            // arrange
            LearningQueue queue = CreateQueue(2, new FixedRequeuePolicy());
            queue.Advance();

            // act
            queue.Advance();
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(1, queue.Current.Flashcard.Id);
        }

        [Fact]
        public void Advance_OnMiddleCard_MovesCurrentToLastCard()
        {
            // arrange
            LearningQueue queue = CreateQueue(3, new FixedRequeuePolicy());
            queue.Advance();

            // act
            queue.Advance();
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(3, queue.Current.Flashcard.Id);
        }


        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void RequeueAhead_SingleCardInQueue_ResultIsSameRegardlessOfSteps(uint stepsAhead)
        {
            // arrange
            LearningQueue queue = CreateQueue(1, new FixedRequeuePolicy(stepsAhead));
            // act
            queue.RequeueAhead(stepsAhead);
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(1, queue.Current.Flashcard.Id);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void RequeueAhead_NoCardInQueue_ReturnsRegardlessOfSteps(uint stepsAhead)
        {
            // arrange
            LearningQueue queue = CreateQueue(0, new FixedRequeuePolicy(stepsAhead));
            // act
            queue.RequeueAhead(stepsAhead);
            // assert
            Assert.True(queue.IsFinished);
            Assert.Null(queue.Current);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void RequeueAhead_FirstOfTwoCards_StaysInPlace(uint stepsAhead)
        {
            // arrange
            LearningQueue queue = CreateQueue(2, new FixedRequeuePolicy(stepsAhead));
            // act
            queue.RequeueAhead(stepsAhead);
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(2, queue.Current.Flashcard.Id);

            List<long> expectedIds = new List<long>() { 1, 2 };
            IEnumerable<long> actualIds = queue.GetQueueOrder();
            Assert.Equal(expectedIds, actualIds);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void RequeueAhead_LastOfTwoCards_StaysInPlace(uint stepsAhead)
        {
            // arrange
            LearningQueue queue = CreateQueue(2, new FixedRequeuePolicy(stepsAhead));
            queue.Advance();

            // act
            queue.RequeueAhead(stepsAhead);
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(1, queue.Current.Flashcard.Id);

            List<long> expectedIds = new List<long>() { 1, 2 };
            IEnumerable<long> actualIds = queue.GetQueueOrder();
            Assert.Equal(expectedIds, actualIds);
        }

        [Fact]
        public void RequeueAhead_RealMove_StaysWithinBounds()
        {
            // arrange
            uint stepsAhead = 2;
            LearningQueue queue = CreateQueue(5, new FixedRequeuePolicy(stepsAhead));
            queue.Advance();

            // act
            queue.RequeueAhead(stepsAhead);
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(3, queue.Current.Flashcard.Id);

            List<long> expectedIds = new List<long>() { 1, 3, 4, 2, 5 };
            IEnumerable<long> actualIds = queue.GetQueueOrder();
            Assert.Equal(expectedIds, actualIds);
        }


        [Fact]
        public void RequeueAhead_RealMove_MovesToTheEnd()
        {
            // arrange
            uint stepsAhead = 3;
            LearningQueue queue = CreateQueue(5, new FixedRequeuePolicy(stepsAhead));
            queue.Advance();

            // act
            queue.RequeueAhead(stepsAhead);
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(3, queue.Current.Flashcard.Id);

            List<long> expectedIds = new List<long>() { 1, 3, 4, 5, 2 };
            IEnumerable<long> actualIds = queue.GetQueueOrder();
            Assert.Equal(expectedIds, actualIds);
        }

        [Fact]
        public void RequeueAhead_RealMove_WrapsAroundEndToFront()
        {
            // arrange
            uint stepsAhead = 2;
            LearningQueue queue = CreateQueue(5, new FixedRequeuePolicy(stepsAhead));
            queue.Advance();
            queue.Advance();
            queue.Advance();

            // act
            queue.RequeueAhead(stepsAhead);
            // assert
            Assert.False(queue.IsFinished);
            Assert.NotNull(queue.Current);
            Assert.Equal(5, queue.Current.Flashcard.Id);

            List<long> expectedIds = new List<long>() { 1, 4, 2, 3, 5 };
            IEnumerable<long> actualIds = queue.GetQueueOrder();
            Assert.Equal(expectedIds, actualIds);
        }
    }
}