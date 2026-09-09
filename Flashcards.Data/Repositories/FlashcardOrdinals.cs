using Flashcards.Core.Entities;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Flashcards.Data.Repositories
{
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
