using Microsoft.Data.Sqlite;

namespace Flashcards.Data.Repositories
{
    /// <summary>
    /// Caches the column ordinals of a <c>Flashcards</c> query's <see cref="SqliteDataReader"/>,
    /// so each row can be read back by resolved position instead of a per-row column name lookup.
    /// </summary>
    internal struct FlashcardOrdinals
    {
        public int IdOrdinal;
        public int TopicIdOrdinal;
        public int FrontTextOrdinal;
        public int BackTextOrdinal;
        public int FrontSoundPathOrdinal;
        public int BackSoundPathOrdinal;
        public int CorrectAnswersCountOrdinal;
        public int IncorrectAnswersCountOrdinal;
        public int ColorArgbOrdinal;
        public int CreatedAtOrdinal;
        public int LastReviewedAtOrdinal;
        public int NextReviewAtOrdinal;

        /// <summary>
        /// Resolves every column's ordinal from <paramref name="reader"/>. Must be called once, before
        /// the read loop starts — a reader's column layout does not change between rows of the same query.
        /// </summary>
        public static FlashcardOrdinals FromReader(SqliteDataReader reader)
        {
            return new FlashcardOrdinals
            {
                IdOrdinal = reader.GetOrdinal("Id"),
                TopicIdOrdinal = reader.GetOrdinal("TopicId"),
                FrontTextOrdinal = reader.GetOrdinal("FrontText"),
                BackTextOrdinal = reader.GetOrdinal("BackText"),
                FrontSoundPathOrdinal = reader.GetOrdinal("FrontSoundPath"),
                BackSoundPathOrdinal = reader.GetOrdinal("BackSoundPath"),
                CorrectAnswersCountOrdinal = reader.GetOrdinal("CorrectAnswersCount"),
                IncorrectAnswersCountOrdinal = reader.GetOrdinal("IncorrectAnswersCount"),
                ColorArgbOrdinal = reader.GetOrdinal("ColorArgb"),
                CreatedAtOrdinal = reader.GetOrdinal("CreatedAt"),
                LastReviewedAtOrdinal = reader.GetOrdinal("LastReviewedAt"),
                NextReviewAtOrdinal = reader.GetOrdinal("NextReviewAt")
            };
        }
    }
}