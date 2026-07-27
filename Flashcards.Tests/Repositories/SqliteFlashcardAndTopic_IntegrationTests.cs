using Flashcards.Core.Entities;
using Flashcards.Core.Repositories;
using Flashcards.Data.Database;
using Flashcards.Data.Repositories;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Tests.Repositories
{
    public class SqliteFlashcardAndTopic_IntegrationTests :IAsyncLifetime
    {
        // Guid.NewGuid() -> generates a random 128-bit Globally Unique Identifier,
        // used here to avoid filename collisions between test runs
        private readonly string _connectionString =
            $"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared;Foreign Keys=True";

        private SqliteConnection _keeperConnection = null!;
        private SqliteTopicRepository _topicRepository = null!;
        private SqliteFlashcardRepository _flashcardRepository = null!;

        public async Task InitializeAsync()
        {
            // keeps in-memory DB alive, otherwise each test would open a new, empty one
            _keeperConnection = new SqliteConnection(_connectionString);
            await _keeperConnection.OpenAsync();

            DatabaseInitializer dbInit = new DatabaseInitializer(_connectionString);
            dbInit.EnsureInitialized();

            _topicRepository = new SqliteTopicRepository(_connectionString);
            _flashcardRepository = new SqliteFlashcardRepository(_connectionString);
        }

        public async Task DisposeAsync()
        {
            await _keeperConnection.DisposeAsync();
        }

        [Fact]
        public async Task DeleteAsync_ValidIdWithOneFlashcard_CascadeDelete()
        {
            // arrange
            string topicName = ".NET";
            Topic topic = new Topic { Name = topicName, CreatedAt = DateTime.UtcNow };
            long topicId = await _topicRepository.AddAsync(topic);
            topic.Id = topicId;

            Flashcard flashcard = new Flashcard()
            {
                TopicId = topicId,
                FrontText = "What is the capital of France?",
                BackText = "Paris",
                FrontSoundPath = null,
                BackSoundPath = null,
                CorrectAnswersCount = 0,
                IncorrectAnswersCount = 0,
                ColorArgb = 0x7FB3E5FC, // pale blue, alpha = 0x7F
                CreatedAt = DateTime.UtcNow,
                LastReviewedAt = null,
                NextReviewAt = null
            };

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            flashcard.Id = await _flashcardRepository.AddAsync(transaction, flashcard);
            await transaction.CommitAsync();

            // act
            await _topicRepository.DeleteAsync(topicId);

            // assert
            Assert.Empty(await _flashcardRepository.GetByTopicIdAsync(topicId));
        }


        [Fact]
        public async Task MergeAsync_ValidTopics_ReassignsFlashcardsAndDeletesSource()
        {
            // arrange
            Topic sourceTopic = new Topic { Name = "C#", CreatedAt = DateTime.UtcNow };
            long sourceTopicId = await _topicRepository.AddAsync(sourceTopic);
            sourceTopic.Id = sourceTopicId;

            Topic targetTopic = new Topic { Name = "Programming", CreatedAt = DateTime.UtcNow };
            long targetTopicId = await _topicRepository.AddAsync(targetTopic);
            targetTopic.Id = targetTopicId;

            Flashcard sourceCard1 = new Flashcard
            {
                TopicId = sourceTopicId,
                FrontText = "What keyword marks a class as not inheritable?",
                BackText = "sealed",
                FrontSoundPath = null,
                BackSoundPath = null,
                CorrectAnswersCount = 0,
                IncorrectAnswersCount = 0,
                ColorArgb = 0x7FB3E5FC,
                CreatedAt = DateTime.UtcNow,
                LastReviewedAt = null,
                NextReviewAt = null
            };

            Flashcard sourceCard2 = new Flashcard
            {
                TopicId = sourceTopicId,
                FrontText = "What does IEquatable<T> provide?",
                BackText = "A strongly-typed Equals method",
                FrontSoundPath = null,
                BackSoundPath = null,
                CorrectAnswersCount = 0,
                IncorrectAnswersCount = 0,
                ColorArgb = 0x7FB3E5FC,
                CreatedAt = DateTime.UtcNow,
                LastReviewedAt = null,
                NextReviewAt = null
            };

            Flashcard targetCard = new Flashcard
            {
                TopicId = targetTopicId,
                FrontText = "What does DRY stand for?",
                BackText = "Don't Repeat Yourself",
                FrontSoundPath = null,
                BackSoundPath = null,
                CorrectAnswersCount = 0,
                IncorrectAnswersCount = 0,
                ColorArgb = 0x7FB3E5FC,
                CreatedAt = DateTime.UtcNow,
                LastReviewedAt = null,
                NextReviewAt = null
            };

            IReadOnlyList<Flashcard> saved = await _flashcardRepository.SaveChangesAsync(
                new List<Flashcard> { sourceCard1, sourceCard2, targetCard });

            // act
            await _topicRepository.MergeAsync(sourceTopicId, targetTopicId);

            // assert
            IReadOnlyList<Flashcard> targetCards = await _flashcardRepository.GetByTopicIdAsync(targetTopicId);
            Assert.Equal(3, targetCards.Count);
            Assert.Equal(
                saved.Select(c => c.Id),
                targetCards.Select(c => c.Id)
            );
            Assert.Null(await _topicRepository.GetByIdAsync(sourceTopicId));
        }


        [Fact]
        public async Task MergeAsync_InvalidTargetId_ThrowsEntityNotFoundException()
        {
            // arrange
            Topic sourceTopic = new Topic { Name = "C#", CreatedAt = DateTime.UtcNow };
            long sourceTopicId = await _topicRepository.AddAsync(sourceTopic);

            long invalidTargetId = sourceTopicId + 1000;

            Flashcard sourceCard = new Flashcard  // we need flashcard so that the key violation is thrown
            {
                TopicId = sourceTopicId,
                FrontText = "What keyword marks a class as not inheritable?",
                BackText = "sealed",
                FrontSoundPath = null,
                BackSoundPath = null,
                CorrectAnswersCount = 0,
                IncorrectAnswersCount = 0,
                ColorArgb = 0x7FB3E5FC,
                CreatedAt = DateTime.UtcNow,
                LastReviewedAt = null,
                NextReviewAt = null
            };
            await _flashcardRepository.SaveChangesAsync(new List<Flashcard> { sourceCard });

            // act & assert
            await Assert.ThrowsAsync<EntityNotFoundException>(
                () => _topicRepository.MergeAsync(sourceTopicId, invalidTargetId));
        }

    }
}
