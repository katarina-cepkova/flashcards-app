using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Data.Repositories
{
    internal struct TopicOrdinals
    {
        public int IdOrdinal;
        public int NameOrdinal;
        public int CreatedAtOrdinal;

        public static TopicOrdinals FromReader(SqliteDataReader reader) => new TopicOrdinals
        {
            IdOrdinal = reader.GetOrdinal("Id"),
            NameOrdinal = reader.GetOrdinal("Name"),
            CreatedAtOrdinal = reader.GetOrdinal("CreatedAt")
        };
    }
}
