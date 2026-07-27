using Flashcards.Core.Entities;
using Flashcards.Core.Repositories;
using Flashcards.Data.Database;
using Microsoft.Data.Sqlite;

namespace Flashcards.Data.Repositories
{
    internal class SqliteTopicRepository : ITopicRepository
    {
        private readonly string _connectionString;


        public SqliteTopicRepository(string connectionString)
        {
            _connectionString = connectionString;
        }


        /// <summary>
        /// Returns all topics ordered by Id.
        /// </summary>
        public async Task<IReadOnlyList<Topic>> GetAllAsync()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Topics ORDER BY Id;";

            List<Topic> topics = new List<Topic>();
            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                topics.Add(new Topic
                {
                    Id = reader.GetInt64(reader.GetOrdinal("Id")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    CreatedAt = reader.GetDateTime("CreatedAt")  // our extension: parses the TEXT column with InvariantCulture
                }
                );
            }
            return topics;
        }


        /// <summary>
        /// Returns the topic with the given id, or null if none exists.
        /// </summary>
        public async Task<Topic?> GetByIdAsync(long id)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Topics WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            // Id is the primary key, so at most one row can match — if, not while
            if (await reader.ReadAsync())
            {
                return new Topic
                {
                    Id = reader.GetInt64(reader.GetOrdinal("Id")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    CreatedAt = reader.GetDateTime("CreatedAt")  // our extension: parses the TEXT column with InvariantCulture
                };

            }
            return null;
        }


        /// <summary>
        /// Returns the topic with the given name, or null if none exists.
        /// </summary>
        public async Task<Topic?> GetByNameAsync(string name)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Topics WHERE Name=$name;";
            command.Parameters.AddWithValue("$name", name);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Topic
                {
                    Id = reader.GetInt64(reader.GetOrdinal("Id")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    CreatedAt = reader.GetDateTime("CreatedAt")  // our extension: parses the TEXT column with InvariantCulture
                };

            }
            return null;
        }


        /// <summary>
        /// Inserts a new topic and returns its assigned id.
        /// </summary>
        /// <exception cref="DuplicateEntityException">A topic with the same name already exists.</exception>
        public async Task<long> AddAsync(Topic topic)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Topics (Name, CreatedAt) VALUES ($name, $createdAt);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$name", topic.Name);
            command.AddDateTime("$createdAt", topic.CreatedAt);
            try
            {
                object? topicId = await command.ExecuteScalarAsync();
                return (long)topicId!;  // the id is assigned by the database = will not be null
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE violation on Name
            {
                // wrapping SqliteException into a common domain language
                throw new DuplicateEntityException($"Topic with name '{topic.Name}' already exists.");
            }
        }


        /// <summary>
        /// Reassigns all flashcards from the source topic to the target topic, then deletes the source topic.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Source or target topic does not exist.</exception>
        public async Task MergeAsync(long sourceTopicId, long targetTopicId)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using SqliteCommand checkSourceCommand = connection.CreateCommand();
            checkSourceCommand.CommandText = "SELECT 1 FROM Topics WHERE Id=$source;";
            checkSourceCommand.Parameters.AddWithValue("$source", sourceTopicId);
            object? sourceExists = await checkSourceCommand.ExecuteScalarAsync();
            if (sourceExists is null)
            {
                throw new EntityNotFoundException($"Source topic {sourceTopicId} does not exist.");
            }

            // BeginTransactionAsync is declared on the common DbConnection base class and
            // returns DbTransaction, not SqliteTransaction directly — a known limitation
            // shared across ADO.NET providers (see dotnet/SqlClient#3248 for the same issue
            // with SqlConnection). The cast is needed to assign the transaction to our commands.
            using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            try
            {
                SqliteCommand updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = "UPDATE Flashcards SET TopicId=$target WHERE TopicId=$source;";
                updateCommand.Parameters.AddWithValue("$target", targetTopicId);
                updateCommand.Parameters.AddWithValue("$source", sourceTopicId);

                try
                {
                    await updateCommand.ExecuteNonQueryAsync();
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // FK violation: target doesn't exist
                {
                    // wrapping SqliteException into a common domain language
                    throw new EntityNotFoundException($"Target topic {targetTopicId} does not exist.");
                }

                using SqliteCommand deleteCommand = connection.CreateCommand();
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM Topics WHERE Id=$source;";
                deleteCommand.Parameters.AddWithValue("$source", sourceTopicId);
                await deleteCommand.ExecuteNonQueryAsync();

                // nothing is actually persisted until Commit — if anything above threw,
                // we never reach this line and the whole operation rolls back instead
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        /// <summary>
        /// Renames the topic with the given id.
        /// </summary>
        /// <exception cref="EntityNotFoundException">No topic with the given id exists.</exception>
        /// <exception cref="DuplicateEntityException">A topic with <paramref name="newName"/> already exists.</exception>
        public async Task RenameAsync(long id, string newName)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE Topics SET Name=$name WHERE Id=$id;";
            command.Parameters.AddWithValue("$name", newName);
            command.Parameters.AddWithValue("$id", id);

            try
            {
                int rowsAffected = await command.ExecuteNonQueryAsync();
                EnsureRowAffected(rowsAffected, id);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE violation on Name
            {
                // wrapping SqliteException into a common domain language
                throw new DuplicateEntityException($"Topic with name '{newName}' already exists.");
            }
        }


        /// <summary>
        /// Deletes the topic with the given id. Flashcards under it are cascade-deleted by the database.
        /// </summary>
        /// <exception cref="EntityNotFoundException">No topic with the given id exists.</exception>
        public async Task DeleteAsync(long id)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Topics WHERE Id=$id;"; // cascades to Flashcards via ON DELETE CASCADE
            command.Parameters.AddWithValue("$id", id);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            EnsureRowAffected(rowsAffected, id);
        }


        /// <summary>
        /// Throws <see cref="EntityNotFoundException"/> if no row was affected by the last command.
        /// </summary>
        private static void EnsureRowAffected(int rowsAffected, long id)
        {
            if (rowsAffected == 0)
            {
                throw new EntityNotFoundException($"Topic {id} does not exist.");
            }
        }
    }
}