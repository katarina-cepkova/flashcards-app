using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Flashcards.Data.Database
{
    /// <summary>
    /// Reads values from a <see cref="SqliteDataReader"/>, converting SQLite's
    /// TEXT-based storage back into C# types (dates, nullable columns).
    /// </summary>
    internal static class SqliteDataReaderExtensions
    {
        /// <summary>Reads a nullable text column, returning <c>null</c> for <see cref="DBNull"/>.</summary>
        public static string? GetNullableString(this SqliteDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        /// <summary>Reads a nullable ISO 8601 date column, returning <c>null</c> for <see cref="DBNull"/>.</summary>
        public static DateTime? GetNullableDateTime(this SqliteDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);
        }

        /// <summary>Reads an ISO 8601 date column.</summary>
        public static DateTime GetDateTime(this SqliteDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);
        }
    }
}