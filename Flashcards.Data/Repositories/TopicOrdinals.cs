using Microsoft.Data.Sqlite;

namespace Flashcards.Data.Repositories
{
    /// <summary>
    /// Caches the column ordinals of a <c>Topics</c> query's <see cref="SqliteDataReader"/>,
    /// so each row can be read back by resolved position instead of a per-row column name lookup.
    /// </summary>
    internal struct TopicOrdinals
    {
        public int IdOrdinal;
        public int NameOrdinal;
        public int CreatedAtOrdinal;

        /// <summary>
        /// Resolves every column's ordinal from <paramref name="reader"/>. Must be called once, before
        /// the read loop starts — a reader's column layout does not change between rows of the same query.
        /// </summary>
        public static TopicOrdinals FromReader(SqliteDataReader reader) => new TopicOrdinals
        {
            IdOrdinal = reader.GetOrdinal("Id"),
            NameOrdinal = reader.GetOrdinal("Name"),
            CreatedAtOrdinal = reader.GetOrdinal("CreatedAt")
        };
    }
}