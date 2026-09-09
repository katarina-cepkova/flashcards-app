using Flashcards.Data.Database;
using Flashcards.Data.Repositories;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Tests.Repositories
{
    public class SqliteFlashcardRepositoryTests :IAsyncLifetime
    {
        // Guid.NewGuid() -> generates a random 128-bit Globally Unique Identifier,
        // used here to avoid filename collisions between test runs
        private readonly string _connectionString =
            $"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared;Foreign Keys=True";

        private SqliteConnection _keeperConnection = null!;
        private SqliteFlashcardRepository _repository = null!;


        public async Task InitializeAsync()
        {
            // keeps in-memory DB alive, otherwise each test would open a new, empty one
            _keeperConnection = new SqliteConnection(_connectionString);
            await _keeperConnection.OpenAsync();

            DatabaseInitializer dbInit = new DatabaseInitializer(_connectionString);
            dbInit.EnsureInitialized();

            _repository = new SqliteFlashcardRepository(_connectionString);
        }


        public async Task DisposeAsync()
        {
            await _keeperConnection.DisposeAsync();
        }



    }
}
