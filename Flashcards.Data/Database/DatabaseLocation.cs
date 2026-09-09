namespace Flashcards.Data.Database
{
    /// <summary>
    /// Resolves the on-disk location of the application's SQLite database file and the connection
    /// string used to open it.
    /// </summary>
    public static class DatabaseLocation
    {
        private const string FolderName = "FlashcardsDatabase";
        private const string FileName = "flashcards.db";

        /// <summary>
        /// The connection string pointing at <see cref="DbPath"/>, with foreign key enforcement enabled.
        /// </summary>
        public static string ConnectionString => $"Data Source={DbPath};Foreign Keys=True";

        /// <summary>
        /// The absolute path to the application's SQLite database file, inside the current user's
        /// application data folder. The containing folder is created if it doesn't already exist.
        /// </summary>
        public static string DbPath { get; } = BuildDbPath();

        /// <summary>
        /// Builds <see cref="DbPath"/> and ensures its containing folder exists.
        /// </summary>
        private static string BuildDbPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                FolderName);

            Directory.CreateDirectory(folder);

            return Path.Combine(folder, FileName);
        }
    }
}