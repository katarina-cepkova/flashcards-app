using Flashcards.Core.Entities;
using Flashcards.Core.Repositories;
using Flashcards.Data.Database;
using Flashcards.Data.Repositories;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Transactions;

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

        public async ValueTask InitializeAsync()
        {
            // keeps in-memory DB alive, otherwise each test would open a new, empty one
            _keeperConnection = new SqliteConnection(_connectionString);
            await _keeperConnection.OpenAsync();

            DatabaseInitializer dbInit = new DatabaseInitializer(_connectionString);
            dbInit.EnsureInitialized();

            _topicRepository = new SqliteTopicRepository(_connectionString);
            _flashcardRepository = new SqliteFlashcardRepository(_connectionString);
        }

        public async ValueTask DisposeAsync()
        {
            await _keeperConnection.DisposeAsync();
        }


        #region helper methods
        private static Flashcard CreateTestFlashcard(long topicId, string front = "Front", string back = "Back")
        {
            return new Flashcard
            {
                TopicId = topicId,
                FrontText = front,
                BackText = back,
                FrontSoundPath = null,
                BackSoundPath = null,
                CorrectAnswersCount = 0,
                IncorrectAnswersCount = 0,
                ColorArgb = 0x7FB3E5FC,
                CreatedAt = DateTime.UtcNow,
                LastReviewedAt = null,
                NextReviewAt = null
            };
        }

        private async Task<long> AddTopicAsync(string name = "C#")
        {
            Topic topic = new Topic { Name = name, CreatedAt = DateTime.UtcNow };
            return await _topicRepository.AddAsync(topic);
        }

        private async Task AddFlashcardAsync(Flashcard flashcard)
        {
            using SqliteTransaction transaction = (SqliteTransaction)await _keeperConnection.BeginTransactionAsync();
            long id = await _flashcardRepository.AddAsync(transaction, flashcard);
            await transaction.CommitAsync();
            flashcard.Id = id;
        }

        private async Task DeleteFlashcardAsync(long id)
        {
            using SqliteTransaction transaction = (SqliteTransaction)await _keeperConnection.BeginTransactionAsync();
            await _flashcardRepository.DeleteAsync(transaction, id);
            await transaction.CommitAsync();
        }

        private async Task UpdateFlashcardAsync(Flashcard flashcard)
        {
            using SqliteTransaction transaction = (SqliteTransaction)await _keeperConnection.BeginTransactionAsync();
            await _flashcardRepository.UpdateAsync(transaction, flashcard);
            await transaction.CommitAsync();
        }
        #endregion



        #region topic repository methods
        [Fact]
        public async Task DeleteAsync_ValidIdWithOneFlashcard_CascadeDelete()
        {
            // arrange
            long topicId = await AddTopicAsync("C#");
            Flashcard flashcard = CreateTestFlashcard(topicId);
            await AddFlashcardAsync(flashcard);

            // act
            await _topicRepository.DeleteAsync(topicId);

            // assert
            Assert.Empty(await _flashcardRepository.GetByTopicIdAsync(topicId));  // card deleted, topic persisted
        }


        [Fact]
        public async Task MergeAsync_ValidTopics_ReassignsFlashcardsAndDeletesSource()
        {
            // arrange
            long sourceTopicId = await AddTopicAsync("C#");
            long targetTopicId = await AddTopicAsync("Programming");


            Flashcard sourceCard1 = CreateTestFlashcard(
                sourceTopicId,
                "What keyword marks a class as not inheritable?",
                "sealed");

            Flashcard sourceCard2 = CreateTestFlashcard(
                sourceTopicId,
                "What does IEquatable<T> provide?",
                "A strongly-typed Equals method");

            Flashcard targetCard = CreateTestFlashcard(
                targetTopicId,
                "What does DRY stand for?",
                "Don't Repeat Yourself");
                

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
        public async Task MergeAsync_InvalidTargetId_ThrowsEntityNotFoundExceptionAndRollsBack()
        {
            // arrange
            long sourceTopicId = await AddTopicAsync("C#");
            long invalidTargetId = sourceTopicId + 1000;

            Flashcard sourceCard = CreateTestFlashcard(
                sourceTopicId,
                "What keyword marks a class as not inheritable?",
                "sealed");

            await _flashcardRepository.SaveChangesAsync(new List<Flashcard> { sourceCard });

            // act & assert
            await Assert.ThrowsAsync<EntityNotFoundException>(
                () => _topicRepository.MergeAsync(sourceTopicId, invalidTargetId));
            Assert.Equal(sourceTopicId, sourceCard.TopicId);
        }
        #endregion


        #region flashcard repository methods
        #region AddAsync
        [Fact]
        public async Task AddAsync_FlashcardWithExistingTopicId_Succeeds()
        {
            // arrange
            long topicId = await AddTopicAsync("C#");
            Flashcard flashcard = CreateTestFlashcard(
                topicId,
                "What keyword marks a class as not inheritable?",
                "sealed");

            // act
            await AddFlashcardAsync(flashcard);

            // assert
            Assert.NotNull(flashcard.Id);
        }


        [Fact]
        public async Task AddAsync_FlashcardWithNonExistingTopicId_Succeeds()
        {
            // arrange
            Flashcard flashcard = CreateTestFlashcard(
                2,
                "What keyword marks a class as not inheritable?",
                "sealed");

            // act & assert
            await Assert.ThrowsAsync < SqliteException > (() => AddFlashcardAsync(flashcard));
            Assert.Null(flashcard.Id);
            Assert.Null(await _topicRepository.GetByIdAsync(2));  // the insertion will not create the topic
        }

        [Fact]
        public async Task AddAsync_ClientSetId_IsIgnoredAndOverwrittenByDatabase()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard flashcard = CreateTestFlashcard(topicId);
            
            // act
            flashcard.Id = 12345;  // trying to enforce own id
            await AddFlashcardAsync(flashcard);

            // assert
            Assert.NotEqual(12345, flashcard.Id);
        }


        #endregion

        #region DeleteAsync
        [Fact]
        public async Task DeleteAsync_ExistingFlashcard_RemovesFromDatabaseAndKeepsTopic()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard flashcard = CreateTestFlashcard(topicId);
            await AddFlashcardAsync(flashcard);
            long id = flashcard.Id!.Value;

            // act
            await DeleteFlashcardAsync(id);  // null-forgiving operator: addFlashcard assigns the id to flashcard

            // assert
            Assert.Null(await _flashcardRepository.GetByIdAsync(id));
            Assert.NotNull(await _topicRepository.GetByIdAsync(topicId));
        }


        [Fact]
        public async Task DeleteAsync_InvalidId_ThrowsEntityNotFoundException()
        {
            // act & assert
            await Assert.ThrowsAsync<EntityNotFoundException>(
                () => DeleteFlashcardAsync(999));
        }



        #endregion

        #region GetByIdAsync
        [Fact]
        public async Task GetByIdAsync_ValidId_ReturnsInsertedFlashcard()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard flashcard = CreateTestFlashcard(topicId, "What keyword marks a class as not inheritable?", "sealed");
            await AddFlashcardAsync(flashcard);

            // act
            Flashcard? retrieved = await _flashcardRepository.GetByIdAsync(flashcard.Id!.Value);
            
            // assert
            Assert.NotNull(retrieved);
            Assert.Equal(flashcard.TopicId, retrieved.TopicId);
            Assert.Equal(flashcard.FrontText, retrieved.FrontText);
            Assert.Equal(flashcard.BackText, retrieved.BackText);
            Assert.Equal(flashcard.FrontSoundPath, retrieved.FrontSoundPath);
            Assert.Equal(flashcard.BackSoundPath, retrieved.BackSoundPath);
            Assert.Equal(flashcard.CorrectAnswersCount, retrieved.CorrectAnswersCount);
            Assert.Equal(flashcard.IncorrectAnswersCount, retrieved.IncorrectAnswersCount);
            Assert.Equal(flashcard.ColorArgb, retrieved.ColorArgb);
            Assert.Equal(flashcard.CreatedAt, retrieved.CreatedAt);
            Assert.Equal(flashcard.LastReviewedAt, retrieved.LastReviewedAt);
            Assert.Equal(flashcard.NextReviewAt, retrieved.NextReviewAt);
        }

        [Fact]
        public async Task GetByIdAsync_InvalidId_ReturnsNull()
        {
            Assert.Null(await _flashcardRepository.GetByIdAsync(999));
        }

        #endregion


        #region GetByTopicIdAsync
        [Fact]
        public async Task GetByTopicIdAsync_InvalidTopicId_ReturnsEmpty()
        {
            Assert.Empty(await _flashcardRepository.GetByTopicIdAsync(999));
        }

        [Fact]
        public async Task GetByTopicIdAsync_TopicWithNoFlashcards_ReturnsEmpty()
        {
            // arrange
            long topicId = await AddTopicAsync();

            // act & assert
            Assert.Empty(await _flashcardRepository.GetByTopicIdAsync(topicId));
        }

        [Fact]
        public async Task GetByTopicIdAsync_TopicWithMultipleFlashcards_ReturnsAllOrderedById()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard first = CreateTestFlashcard(topicId, "Q1", "A1");
            Flashcard second = CreateTestFlashcard(topicId, "Q2", "A2");
            Flashcard third = CreateTestFlashcard(topicId, "Q3", "A3");

            await AddFlashcardAsync(first);
            await AddFlashcardAsync(second);
            await AddFlashcardAsync(third);

            // act
            IReadOnlyList<Flashcard> result = await _flashcardRepository.GetByTopicIdAsync(topicId);

            // assert
            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { first.Id, second.Id, third.Id }, result.Select(f => f.Id));
        }
        #endregion


        #region UpdateAsync

        [Fact]
        public async Task UpdateAsync_ExistingFlashcard_PersistsChanges()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard flashcard = CreateTestFlashcard(topicId, "Old front", "Old back");
            await AddFlashcardAsync(flashcard);

            // act
            flashcard.FrontText = "New front";
            flashcard.CorrectAnswersCount = 5;
            await UpdateFlashcardAsync(flashcard);

            // assert
            Flashcard? updated = await _flashcardRepository.GetByIdAsync(flashcard.Id!.Value);
            Assert.Equal("New front", updated!.FrontText);
            Assert.Equal(5, updated.CorrectAnswersCount);
        }

        [Fact]
        public async Task UpdateAsync_NonExistingFlashcard_ThrowsEntityNotFoundException()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard flashcard = CreateTestFlashcard(topicId);
            
            // act & assert
            flashcard.Id = 999;
            await Assert.ThrowsAsync<EntityNotFoundException>(() => UpdateFlashcardAsync(flashcard));
        }


        [Fact]
        public async Task UpdateAsync_InvalidTopicId_ThrowsSqliteException()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard flashcard = CreateTestFlashcard(topicId);
            await AddFlashcardAsync(flashcard);

            // act & assert
            flashcard.TopicId = 999;
            await Assert.ThrowsAsync<SqliteException>(() => UpdateFlashcardAsync(flashcard));
        }

        #endregion


        #region SaveChangesAsync
        [Fact]
        public async Task SaveChangesAsync_OnlyNewFlashcards_AssignsIdsAndPersistsAll()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard first = CreateTestFlashcard(topicId, "Q1", "A1");
            Flashcard second = CreateTestFlashcard(topicId, "Q2", "A2");

            // act
            IReadOnlyList<Flashcard> saved = await _flashcardRepository.SaveChangesAsync(
                new List<Flashcard> { first, second });

            // assert
            Assert.All(saved, f => Assert.NotNull(f.Id));
            Assert.Equal(2, (await _flashcardRepository.GetByTopicIdAsync(topicId)).Count);
        }

        [Fact]
        public async Task SaveChangesAsync_OnlyExistingFlashcards_UpdatesFields()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard flashcard = CreateTestFlashcard(topicId, "Old", "Old");
            await AddFlashcardAsync(flashcard);
            
            // act
            flashcard.FrontText = "Updated";
            await _flashcardRepository.SaveChangesAsync(new List<Flashcard> { flashcard });

            // assert
            Flashcard? updated = await _flashcardRepository.GetByIdAsync(flashcard.Id!.Value);
            Assert.Equal("Updated", updated!.FrontText);
        }

        [Fact]
        public async Task SaveChangesAsync_MixOfNewAndExisting_PersistsBoth()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard existing = CreateTestFlashcard(topicId, "Existing", "Existing");
            await AddFlashcardAsync(existing);

            // act
            existing.BackText = "Existing updated";
            Flashcard brandNew = CreateTestFlashcard(topicId, "New", "New");

            IReadOnlyList<Flashcard> saved = await _flashcardRepository.SaveChangesAsync(
                new List<Flashcard> { existing, brandNew });

            // assert
            Assert.Equal(1, saved[0].Id);
            Assert.Equal(2, saved[1].Id);

            IReadOnlyList<Flashcard> allInTopic = await _flashcardRepository.GetByTopicIdAsync(topicId);
            Assert.Equal(saved, allInTopic);
        }

        [Fact]
        public async Task SaveChangesAsync_ExistingCardWithInvalidId_ThrowsEntityNotFoundException()
        {
            // arrange
            long topicId = await AddTopicAsync();
            Flashcard flashcard = CreateTestFlashcard(topicId);
            
            // act & assert
            flashcard.Id = 999;
            await Assert.ThrowsAsync<EntityNotFoundException>(
                () => _flashcardRepository.SaveChangesAsync(new List<Flashcard> { flashcard }));
        }

        [Fact]
        public async Task SaveChangesAsync_NewCardWithInvalidTopicId_ThrowsSqliteException()
        {
            // arrange
            Flashcard flashcard = CreateTestFlashcard(999);

            // act & assert
            await Assert.ThrowsAsync<SqliteException>(
                () => _flashcardRepository.SaveChangesAsync(new List<Flashcard> { flashcard }));
        }


        #endregion
        #endregion
    }
}
