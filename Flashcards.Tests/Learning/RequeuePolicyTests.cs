using Flashcards.Core.Entities;
using Flashcards.Core.Learning;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Tests.Learning
{
    public class RequeuePolicyTests
    {
        private static RequeuePolicy CreateDefaultPolicy(
            uint minCorrectAnswers = 2, uint maxCorrectAnswers = 5,
            double minCorrectRatio = 0.5,
            uint baseSteps = 5, uint minSteps = 1, uint maxSteps = 10,
            double correctWeight = 0.5, double incorrectWeight = 0.5)
        {
            return new RequeuePolicy(
                minCorrectAnswers, maxCorrectAnswers,
                minCorrectRatio,
                baseSteps, minSteps, maxSteps,
                correctWeight, incorrectWeight);
        }

        private static Flashcard CreateDummyFlashcard()
        {
            return new Flashcard
            {
                TopicId = 1,
                FrontText = "irrelevant",
                BackText = "irrelevant",
                FrontSoundPath = null,
                BackSoundPath = null,
                CorrectAnswersCount = 0,
                IncorrectAnswersCount = 0,
                ColorArgb = 0,
                CreatedAt = DateTime.UtcNow,
                LastReviewedAt = null,
                NextReviewAt = null
            };
        }

        #region Ctor tests
        [Fact]
        public void Ctor_MinCorrectAnswersZero_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateDefaultPolicy(minCorrectAnswers: 0));
        }

        [Fact]
        public void Ctor_MaxCorrectAnswersLessThanMin_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateDefaultPolicy(minCorrectAnswers: 5, maxCorrectAnswers: 3));
        }

        [Theory]
        [InlineData(-0.1)]
        [InlineData(1.1)]
        public void Ctor_MinCorrectRatioOutOfRange_ThrowsArgumentOutOfRangeException(double invalidRatio)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateDefaultPolicy(minCorrectRatio: invalidRatio));
        }

        [Fact]
        public void Ctor_MinStepsZero_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateDefaultPolicy(minSteps: 0));
        }

        [Fact]
        public void Ctor_MaxStepsLessThanMinSteps_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateDefaultPolicy(minSteps: 5, maxSteps: 3));
        }

        [Fact]
        public void Ctor_BaseStepsZero_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateDefaultPolicy(baseSteps: 0));
        }

        [Fact]
        public void Ctor_NegativeCorrectWeight_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateDefaultPolicy(correctWeight: -0.1));
        }

        [Fact]
        public void Ctor_NegativeIncorrectWeight_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateDefaultPolicy(incorrectWeight: -0.1));
        }

        [Fact]
        public void Ctor_ValidArguments_DoesNotThrow()
        {
            var exception = Record.Exception(() => CreateDefaultPolicy());
            Assert.Null(exception);
        }

        #endregion



        private static LearningSessionCard CreateCard(int correct, int miss)
        {
            return new LearningSessionCard
            {
                Flashcard = CreateDummyFlashcard(),
                SessionCorrectCount = correct,
                SessionMissCount = miss
            };
        }

        private static RequeuePolicy CreateDummyRequeuePolicy()
        {
            return CreateDefaultPolicy(
                minCorrectAnswers: 2, maxCorrectAnswers: 5,
                minCorrectRatio: 0.5,
                baseSteps: 5, minSteps: 1, maxSteps: 10,
                correctWeight: 0.5, incorrectWeight: 0.5);
        }


        #region ShouldRemove

        [Fact]
        public void ShouldRemove_NoAnswersYet_ReturnsFalse()
        {
            // arrange
            RequeuePolicy policy = CreateDummyRequeuePolicy();
            // act & assert
            Assert.False(policy.ShouldRemove(CreateCard(0, 0)));
        }

        [Fact]
        public void ShouldRemove_RatioMetButBelowMinCorrectAnswers_ReturnsFalse()
        {
            // ratio = 1/1 = 1.0 >= 0.5
            // but correctCount = 1 < minCorrectAnswers = 2
            
            // arrange
            RequeuePolicy policy = CreateDummyRequeuePolicy();
            // act & assert
            Assert.False(policy.ShouldRemove(CreateCard(1, 0)));
        }

        [Fact]
        public void ShouldRemove_RatioAndMinCorrectAnswersMet_ReturnsTrue()
        {
            // ratio = 2/2 = 1.0 >= 0.5, correctCount = 2 >= minCorrectAnswers = 2
            // arrange
            RequeuePolicy policy = CreateDummyRequeuePolicy();
            // act & assert
            Assert.True(policy.ShouldRemove(CreateCard(2, 0)));
        }

        [Fact]
        public void ShouldRemove_RatioBelowThreshold_CorrectAnswersBelowMaximum_ReturnsFalse()
        {
            // ratio = 2/5 = 0.4 < 0.5, correctCount = 2 <= maxCorrectAnswers = 5
            // arrange
            RequeuePolicy policy = CreateDummyRequeuePolicy();
            // act & assert
            Assert.False(policy.ShouldRemove(CreateCard(2, 3)));
        }

        [Fact]
        public void ShouldRemove_MaxCorrectAnswersReached_ReturnsTrueRegardlessOfRatio()
        {
            // ratio = 5/15 = 1/3 < 0.5
            // but correctCount = 5 >= maxCorrectAnswers = 5
            // arrange
            RequeuePolicy policy = CreateDummyRequeuePolicy();
            // act & assert
            Assert.True(policy.ShouldRemove(CreateCard(5, 10)));
        }


        [Fact]
        public void ShouldRemove_CorrectCountLessThanMaxCorrectAnswers_RatioAboveMin_ReturnsTrue()
        {
            // correctCount = 3 >= minCorrectAnswers = 2
            // but ratio = 3/6 = 0.5 < 0.9
            // arrange
            RequeuePolicy policy = CreateDummyRequeuePolicy();
            // act & assert
            Assert.True(policy.ShouldRemove(CreateCard(3, 3)));
        }

        #endregion


        #region StepsAhead
        [Fact]
        public void StepsAhead_NoAnswers_Throws()
        {
            RequeuePolicy policy = CreateDummyRequeuePolicy();
            Assert.Throws<InvalidOperationException>(
                () => policy.StepsAhead(CreateCard(0, 0), wasLastAnswerCorrect: true));  // throws regardless of the bool value
        }


        [Fact]
        public void StepsAhead_RawStepsBelowMinSteps_ClampsToMinSteps()
        {
            // correctnessRatio = 0.0
            // recentAdjustments = -5 * 0.5 = -2.5
            // rawSteps = (5*0 - 2.5) <= minSteps = 1
            
            // arrange
            RequeuePolicy policy = CreateDummyRequeuePolicy();
            // act
            uint result = policy.StepsAhead(CreateCard(0, 1), wasLastAnswerCorrect: false);
            // assert
            Assert.Equal(1u, result); // minSteps
        }

        [Fact]
        public void StepsAhead_RawStepsAboveMaxSteps_ClampsToMaxSteps()
        {
            // correctnessRatio = 1.0
            // recentAdjustments = 10 * 0.5 = 5
            // rawSteps = 10 * 1.0 + 5 = 15 >= maxSteps = 6

            // arrange
            RequeuePolicy policy = CreateDefaultPolicy(
                minCorrectAnswers: 2, maxCorrectAnswers: 20,
                minCorrectRatio: 0.5,
                baseSteps: 10, minSteps: 1, maxSteps: 6,
                correctWeight: 0.5, incorrectWeight: 0.5);
            // act
            uint result = policy.StepsAhead(CreateCard(10, 0), wasLastAnswerCorrect: true);
            // assert
            Assert.Equal(6u, result); // maxSteps
        }

        [Fact]
        public void StepsAhead_RawStepsWithinBounds_ReturnsRoundedValue()
        {
            // correctnessRatio = 0.5
            // recentAdjustments = 10 * 0.2 = 2
            // rawSteps = 10 * 0.5 + 2 = 7 <= maxSteps = 10
            // minSteps <= rawSteps <= maxSteps

            // arrange
            RequeuePolicy policy = new RequeuePolicy(
                minCorrectAnswers: 2, maxCorrectAnswers: 20,
                minCorrectRatio: 0.5,
                baseSteps: 10, minSteps: 1, maxSteps: 10,
                correctWeight: 0.2, incorrectWeight: 0.3);
            // act
            uint result = policy.StepsAhead(CreateCard(5, 5), wasLastAnswerCorrect: true);
            // assert
            Assert.Equal(7u, result);  // rawSteps
        }

        #endregion
    }
}
