namespace Flashcards.Data.Database;

public static class DatabaseLocation
{
    private const string FolderName = "FlashcardsDatabase";
    private const string FileName = "flashcards.db";

    public static string ConnectionString => $"Data Source={DbPath};Foreign Keys=True";

    public static string DbPath { get; } = BuildDbPath();

    private static string BuildDbPath()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            FolderName);

        Directory.CreateDirectory(folder);

        return Path.Combine(folder, FileName);
    }
}