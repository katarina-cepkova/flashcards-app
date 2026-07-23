using Microsoft.Data.Sqlite;

namespace Flashcards.Data.Database
{
    public class DatabaseInitializer
    {

       private readonly string _connectionString;

        public DatabaseInitializer(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void EnsureInitialized()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);  // will close after finishing the function
            connection.Open();  // creates flashcards.db database if it doesn't exist

            SqliteCommand command = connection.CreateCommand();
            // creating Topics and Flashcards tables - skipped if they already exist
            command.CommandText = """
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS Topics (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
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
                    FOREIGN KEY(TopicId) REFERENCES Topics(Id)
                );

                """;
            command.ExecuteNonQuery();
        }
    }
}
