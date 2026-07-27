using Microsoft.Data.Sqlite;

namespace Flashcards.Data
{
    /// <summary>
    /// Binds C# values as SQLite command parameters, converting types (dates, nulls)
    /// into the form SQLite expects.
    /// </summary>
    internal static class SqliteCommandExtensions
    {
        /// <summary>Binds a nullable string, using <see cref="DBNull"/> for <c>null</c>.</summary>
        public static void AddNullableString(this SqliteCommand command, string paramName, string? value)
        {
            command.Parameters.AddWithValue(paramName, (object?)value ?? DBNull.Value);
        }

        /// <summary>Binds a nullable date as an ISO 8601 string, using <see cref="DBNull"/> for <c>null</c>.</summary>
        public static void AddNullableDateTime(this SqliteCommand command, string paramName, DateTime? value)
        {
            command.Parameters.AddWithValue(paramName, (object?)value?.ToString("O") ?? DBNull.Value);
        }

        /// <summary>Binds a date as an ISO 8601 string.</summary>
        public static void AddDateTime(this SqliteCommand command, string paramName, DateTime value)
        {
            command.Parameters.AddWithValue(paramName, value.ToString("O"));
        }
    }
}