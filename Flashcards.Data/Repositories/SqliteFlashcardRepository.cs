using Flashcards.Core.Entities;
using Flashcards.Core.Repositories;
using Flashcards.Data.Database;
using Microsoft.Data.Sqlite;

namespace Flashcards.Data.Repositories
{
    public class SqliteFlashcardRepository : IFlashcardRepository
    {
        private readonly string _connectionString;


        public SqliteFlashcardRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Binds all column parameters shared by the insert and update commands from a flashcard's fields.
        /// </summary>
        private static void LoadFlashcardDataIntoCommand(Flashcard flashcard, SqliteCommand command)
        {
            command.Parameters.AddWithValue("$topicId", flashcard.TopicId);
            command.Parameters.AddWithValue("$frontText", flashcard.FrontText);
            command.Parameters.AddWithValue("$backText", flashcard.BackText);

            command.AddNullableString("$frontSoundPath", flashcard.FrontSoundPath);
            command.AddNullableString("$backSoundPath", flashcard.BackSoundPath);

            command.Parameters.AddWithValue("$correctAnswersCount", flashcard.CorrectAnswersCount);
            command.Parameters.AddWithValue("$incorrectAnswersCount", flashcard.IncorrectAnswersCount);
            command.Parameters.AddWithValue("$colorArgb", flashcard.ColorArgb);
            command.AddDateTime("$createdAt", flashcard.CreatedAt);
            command.AddNullableDateTime("$lastReviewedAt", flashcard.LastReviewedAt);
            command.AddNullableDateTime("$nextReviewAt", flashcard.NextReviewAt);
        }

        /// <summary>
        /// Inserts a new flashcard and assigns the identifier returned by the database to <paramref name="flashcard"/>.
        /// </summary>
        public async Task<long> AddAsync(SqliteTransaction transaction, Flashcard flashcard)
        {
            SqliteConnection connection = transaction.Connection!;  // null-forgiving operator: caller initialises connection and transaction
                                                                    // no using - the caller is responsible for disposal
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = """
                INSERT INTO Flashcards 
                (TopicId, FrontText, BackText, 
                FrontSoundPath, BackSoundPath, 
                CorrectAnswersCount, IncorrectAnswersCount,
                ColorArgb, CreatedAt, LastReviewedAt, NextReviewAt) 
                
                VALUES ($topicId, $frontText, $backText, 
                $frontSoundPath, $backSoundPath, 
                $correctAnswersCount, $incorrectAnswersCount,
                $colorArgb, $createdAt, $lastReviewedAt, $nextReviewAt);

                SELECT last_insert_rowid();
                """;
            LoadFlashcardDataIntoCommand(flashcard, command);

            object? flashcardId = await command.ExecuteScalarAsync();
            return (long)flashcardId!;  // the id is assigned by the database = will not be null
        }

        /// <summary>
        /// Inserts each flashcard in <paramref name="flashcards"/> sequentially within the same transaction,
        /// assigning each its newly created identifier.
        /// </summary>
        private async Task AddAllAsync(SqliteTransaction transaction, IReadOnlyCollection<Flashcard> flashcards)
        {
            foreach (Flashcard flashcard in flashcards)
            {
                long id = await AddAsync(transaction, flashcard);
                flashcard.Id = id;
            }
        }

        /// <summary>
        /// Deletes the flashcard with the given id.
        /// </summary>
        /// <exception cref="EntityNotFoundException">No flashcard with the given id exists.</exception>
        public async Task DeleteAsync(SqliteTransaction transaction, long id)
        {
            SqliteConnection connection = transaction.Connection!;  // null-forgiving operator: caller initialises connection and transaction
                                                                    // no using - the caller is responsible for disposal
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = "DELETE FROM Flashcards WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", id);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            RepositoryHelpers.EnsureRowsAffected(rowsAffected, id);
        }

        /// <summary>
        /// Deletes each flashcard identified in <paramref name="ids"/> sequentially within the same transaction.
        /// </summary>
        private async Task DeleteAllAsync(SqliteTransaction transaction, IReadOnlyCollection<long> ids)
        {
            foreach (long id in ids)
            {
                await DeleteAsync(transaction, id);
            }
        }

        /// <summary>
        /// Returns the flashcard with the given id, or null if none exists.
        /// </summary>
        public async Task<Flashcard?> GetByIdAsync(long id)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Flashcards WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            // Id is the primary key, so at most one row can match — if, not while
            if (await reader.ReadAsync())
            {
                FlashcardOrdinals ordinals = FlashcardOrdinals.FromReader(reader);
                return reader.ToFlashcard(ordinals);
            }
            return null;
        }

        /// <summary>
        /// Returns all flashcards belonging to the given topic, ordered by Id.
        /// </summary>
        public async Task<IReadOnlyList<Flashcard>> GetByTopicIdAsync(long topicId)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Flashcards WHERE TopicId = $topicId ORDER BY Id;";
            command.Parameters.AddWithValue("$topicId", topicId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            List<Flashcard> flashcards = new List<Flashcard>();

            FlashcardOrdinals ordinals = FlashcardOrdinals.FromReader(reader);
            while (await reader.ReadAsync())
            {
                flashcards.Add(reader.ToFlashcard(ordinals));
            }
            return flashcards;
        }


        /// <summary>
        /// Persists a full set of in-progress changes to a topic's flashcards in a single atomic transaction,
        /// then returns the surviving flashcards (with identifiers assigned to any newly added ones).
        /// </summary>
        /// <remarks>
        /// Cards are split by <see cref="Flashcard.Id"/>/<see cref="Flashcard.IsDeleted"/> into inserts, updates,
        /// and deletes, then applied sequentially on a single shared transaction — SQLite connections cannot
        /// execute commands concurrently, so parallelizing these calls is not an option. A card with no Id that
        /// is also marked deleted was created and removed within the same session before ever reaching the
        /// database, so it is silently skipped rather than routed to any of the three operations.
        /// </remarks>
        public async Task<IReadOnlyList<Flashcard>> SaveChangesAsync(IReadOnlyList<Flashcard> flashcards)
        {
            List<Flashcard> newCards = new List<Flashcard>();
            List<Flashcard> updatedCards = new List<Flashcard>();
            List<long> idsForDeletion = new List<long>();

            foreach (Flashcard flashcard in flashcards)
            {
                if (flashcard.Id is long id)
                {
                    if (flashcard.IsDeleted)
                        idsForDeletion.Add(id);
                    else
                        updatedCards.Add(flashcard);
                }
                else if (!flashcard.IsDeleted)
                    newCards.Add(flashcard);
                // null Id and IsDeleted = card deleted before being saved in db, silent NOP
            }

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // BeginTransactionAsync is declared on the common DbConnection base class and
            // returns DbTransaction, not SqliteTransaction directly — a known limitation
            // shared across ADO.NET providers (see dotnet/SqlClient#3248 for the same issue
            // with SqlConnection). The cast is needed to assign the transaction to our commands.
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            try
            {
                await AddAllAsync(transaction, newCards);
                await UpdateAllAsync(transaction, updatedCards);
                await DeleteAllAsync(transaction, idsForDeletion);
                await transaction.CommitAsync();
                return flashcards.Where(f => !f.IsDeleted).ToList();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        /// <summary>
        /// Persists changes to an existing flashcard's content, formatting, color, and review state.
        /// </summary>
        /// <remarks>
        /// The caller is responsible for ensuring <paramref name="flashcard"/> .<see cref="Flashcard.Id"/> is not null
        /// before calling this method — it is not validated here.
        /// </remarks>
        /// <exception cref="EntityNotFoundException">No flashcard with <see cref="Flashcard.Id"/> exists.</exception>
        public async Task UpdateAsync(SqliteTransaction transaction, Flashcard flashcard)
        {
            SqliteConnection connection = transaction.Connection!;  // null-forgiving operator: caller initialises connection and transaction
                                                                    // no using - the caller is responsible for disposal
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = """
                
                UPDATE Flashcards SET

                TopicId = $topicId,
                FrontText = $frontText, 
                BackText = $backText,

                FrontSoundPath = $frontSoundPath, 
                BackSoundPath = $backSoundPath,

                CorrectAnswersCount = $correctAnswersCount, 
                IncorrectAnswersCount = $incorrectAnswersCount,

                ColorArgb = $colorArgb, 
                CreatedAt = $createdAt, 
                LastReviewedAt = $lastReviewedAt, 
                NextReviewAt = $nextReviewAt 

                WHERE Id = $id;
                """;
            long id = (long)flashcard.Id!;
            command.Parameters.AddWithValue("$id", id);  // caller is responsible fot nullability check
            LoadFlashcardDataIntoCommand(flashcard, command);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            RepositoryHelpers.EnsureRowsAffected(rowsAffected, id);
        }


        /// <summary>
        /// Updates each flashcard in <paramref name="flashcards"/> sequentially within the same transaction.
        /// </summary>
        private async Task UpdateAllAsync(SqliteTransaction transaction, IReadOnlyCollection<Flashcard> flashcards)
        {
            foreach (Flashcard flashcard in flashcards)
            {
                await UpdateAsync(transaction, flashcard);
            }
        }
    }
}