using Flashcards.Data.Database;
using Microsoft.Data.Sqlite;

namespace Flashcards.Tests.Data;

public class DatabaseInitializerTests : IDisposable
{
    private readonly List<string> _tempPaths = new();

    // creates a unique path per test so parallel test runs never share a db file
    private string CreateTempDbPath()
    {
        // Path.GetTempPath() -> returns pah do temp folder of the current user (e.g. C:\Users\User\AppData\Local\Temp\
        // Guid = Globally Unique Identifier - 128bit number - 
        string path = Path.Combine(Path.GetTempPath(), $"flashcards_test_{Guid.NewGuid()}", "flashcards.db");
        _tempPaths.Add(path);
        return path;
    }

    [Fact]
    public void EnsureInitialized_ParentFolderDoesNotExist_ThrowsSqliteException()
    {
        // arrange
        // the folder in dbPath is never created here — DatabaseInitializer
        // relies on the caller (DatabaseLocation) to ensure it exists first
        string dbPath = CreateTempDbPath();
        // Pooling=false: without it, SqliteConnection keeps the underlying file
        // handle alive in a pool after Dispose(), which can cause "file in use"
        // errors when the test cleanup tries to delete the temp file right after
        var initializer = new DatabaseInitializer($"Data Source={dbPath};Pooling=false");

        // act + assert
        Assert.Throws<SqliteException>(() => initializer.EnsureInitialized());
    }

    [Fact]
    public void EnsureInitialized_FolderExistsButNoDbFile_CreatesTablesSuccessfully()
    {
        // arrange
        string dbPath = CreateTempDbPath();
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);  // folder exists, but flashcards.db does not yet
        var initializer = new DatabaseInitializer($"Data Source={dbPath};Pooling=false");

        // act
        initializer.EnsureInitialized();

        // assert
        Assert.True(File.Exists(dbPath));
        AssertTablesAreUsable(dbPath);  // confirms both tables exist and accept real inserts, not just that the file was created
    }

    [Fact]
    public void EnsureInitialized_DbFileAlreadyInitialized_CanBeCalledAgainSafely()
    {
        // arrange
        string dbPath = CreateTempDbPath();
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var initializer = new DatabaseInitializer($"Data Source={dbPath};Pooling=false");
        initializer.EnsureInitialized();  // first call — creates everything

        // act
        initializer.EnsureInitialized();  // second call — relies on CREATE TABLE IF NOT EXISTS being a safe no-op

        // assert
        AssertTablesAreUsable(dbPath);
    }

    // opens its own short-lived connection to verify the schema is actually
    // usable (both tables exist, foreign key column accepts data), rather than
    // just checking that a .db file happens to exist on disk
    private static void AssertTablesAreUsable(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=false");
        connection.Open();

        var insertTopic = connection.CreateCommand();
        insertTopic.CommandText = "INSERT INTO Topics (Name, CreatedAt) VALUES ('Test', '2026-01-01T00:00:00Z')";
        insertTopic.ExecuteNonQuery();

        var insertFlashcard = connection.CreateCommand();
        insertFlashcard.CommandText = """
            INSERT INTO Flashcards (TopicId, FrontText, BackText, ColorArgb, CreatedAt)
            VALUES (1, 'front', 'back', 0, '2026-01-01T00:00:00Z')
            """;
        insertFlashcard.ExecuteNonQuery();

        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Flashcards";
        long count = (long)countCommand.ExecuteScalar()!;  // COUNT never returns null

        Assert.Equal(1, count);
    }

    // runs automatically after each test (xUnit creates a fresh instance per test)
    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                string? folder = Path.GetDirectoryName(path);
                if (folder is not null && Directory.Exists(folder))
                    Directory.Delete(folder);
            }
            catch (IOException)
            {
                // best-effort cleanup — occasional locked file from SQLite's
                // connection pool is not worth failing the test suite over
            }
        }
    }
}