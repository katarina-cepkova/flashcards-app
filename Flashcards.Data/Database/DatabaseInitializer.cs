using Microsoft.Data.Sqlite;

namespace Flashcards.Data.Database
{
    /// <summary>
    /// Creates the SQLite database file and its schema on startup, if they don't already exist.
    /// </summary>
    public class DatabaseInitializer
    {

        private readonly string _connectionString;

        /// <summary>
        /// Creates an initializer that will operate against the database identified by <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string to initialize.</param>
        public DatabaseInitializer(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Creates the <c>Topics</c> and <c>Flashcards</c> tables if they don't already exist, and creates
        /// the underlying database file itself as a side effect of opening the connection.
        /// </summary>
        public void EnsureInitialized()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);  // will close after finishing the function
            connection.Open();  // creates flashcards.db database if it doesn't exist

            SqliteCommand command = connection.CreateCommand();
            // creating Topics and Flashcards tables - skipped if they already exist
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Topics (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Flashcards (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TopicId INTEGER NOT NULL,
                    FrontText TEXT NOT NULL,
                    BackText TEXT NOT NULL,
                
                    FrontSoundPath TEXT,
                    BackSoundPath TEXT,
                
                    CorrectAnswersCount INTEGER NOT NULL DEFAULT 0,
                    IncorrectAnswersCount INTEGER NOT NULL DEFAULT 0,
                
                    ColorArgb INTEGER NOT NULL,  -- argb format as integer
                    CreatedAt TEXT NOT NULL,
                    LastReviewedAt TEXT,
                    NextReviewAt TEXT,
                    FOREIGN KEY(TopicId) REFERENCES Topics(Id) ON DELETE CASCADE
                );

                """;
            command.ExecuteNonQuery();
        }
    }
}