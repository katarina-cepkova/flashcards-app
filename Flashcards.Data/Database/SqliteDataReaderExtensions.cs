using Flashcards.Core.Entities;
using Flashcards.Data.Repositories;
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
        public static string? GetNullableString(this SqliteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        /// <summary>Reads a nullable ISO 8601 date column, returning <c>null</c> for <see cref="DBNull"/>.</summary>
        public static DateTime? GetNullableDateTime(this SqliteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);
        }

        /// <summary>Reads an ISO 8601 date column.</summary>
        public static DateTime GetDateTime(this SqliteDataReader reader, int ordinal)
        {
            return DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);
        }

        /// <remarks>
        /// Implemented as an extension on <see cref="SqliteDataReader"/> (alongside the other Sqlite helpers) rather than a
        /// member of <see cref="Topic"/>, since <see cref="Topic"/> is a persistence-agnostic entity in <c>Flashcards.Core</c>
        /// and must not depend on Sqlite types.
        /// </remarks>
        public static Topic ToTopic(this SqliteDataReader reader, TopicOrdinals ordinals)
        {
            return new Topic
            {
                Id = reader.GetInt64(ordinals.IdOrdinal),
                Name = reader.GetString(ordinals.NameOrdinal),
                CreatedAt = reader.GetDateTime(ordinals.CreatedAtOrdinal)
            };
        }


        /// <summary>
        /// Composes a <see cref="Flashcard"/> from the current row, using the given column ordinals.
        /// </summary>
        /// <remarks>
        /// Implemented as an extension on <see cref="SqliteDataReader"/> (alongside the other Sqlite helpers) rather
        /// than a member of <see cref="Flashcard"/>, since <see cref="Flashcard"/> is a persistence-agnostic
        /// entity in <c>Flashcards.Core</c> and must not depend on Sqlite types.
        /// </remarks>
        public static Flashcard ToFlashcard(this SqliteDataReader reader, FlashcardOrdinals ordinals)
        {
            return new Flashcard
            {
                Id = reader.GetInt64(ordinals.IdOrdinal),
                TopicId = reader.GetInt64(ordinals.TopicIdOrdinal),
                FrontText = reader.GetString(ordinals.FrontTextOrdinal),
                BackText = reader.GetString(ordinals.BackTextOrdinal),

                FrontSoundPath = reader.GetNullableString(ordinals.FrontSoundPathOrdinal),
                BackSoundPath = reader.GetNullableString(ordinals.BackSoundPathOrdinal),

                CorrectAnswersCount = reader.GetInt32(ordinals.CorrectAnswersCountOrdinal),
                IncorrectAnswersCount = reader.GetInt32(ordinals.IncorrectAnswersCountOrdinal),
                ColorArgb = reader.GetInt32(ordinals.ColorArgbOrdinal),

                CreatedAt = reader.GetDateTime(ordinals.CreatedAtOrdinal),
                LastReviewedAt = reader.GetNullableDateTime(ordinals.LastReviewedAtOrdinal),
                NextReviewAt = reader.GetNullableDateTime(ordinals.NextReviewAtOrdinal)
            };
        }
    }
}