using Flashcards.Core.Entities;
using Flashcards.Core.Repositories;
using Flashcards.Data.Database;
using Flashcards.Data.Repositories;
using Microsoft.Data.Sqlite;


namespace Flashcards.Tests.Database
{
    public class SqliteTopicRepositoryTests : IAsyncLifetime
    {
        // Guid.NewGuid() -> generates a random 128-bit Globally Unique Identifier,
        // used here to avoid filename collisions between test runs
        private readonly string _connectionString =
            $"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared";

        private SqliteConnection _keeperConnection = null!;
        private SqliteTopicRepository _repository = null!;

        public async Task InitializeAsync()
        {
            // keeps in-memory DB alive, otherwise each test would open a new, empty one
            _keeperConnection = new SqliteConnection(_connectionString);
            await _keeperConnection.OpenAsync();

            DatabaseInitializer dbInit = new DatabaseInitializer(_connectionString);
            dbInit.EnsureInitialized();

            _repository = new SqliteTopicRepository(_connectionString);
        }

        public async Task DisposeAsync()
        {
            await _keeperConnection.DisposeAsync();
        }


        #region GetAllAsync tests
        [Fact]
        public async Task GetAllAsync_EmptyTopics_ReturnsEmptyIReadOnlyList()
        {
            // act
            IReadOnlyList<Topic> foundTopics = await _repository.GetAllAsync();

            // assert
            Assert.Empty(foundTopics);
        }


        [Fact]
        public async Task GetAllAsync_NonEmptyTopics_ReturnsTopics()
        {
            // arrange
            string topicAName = "Data Management";
            string topicBName = "Database Engines";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            Topic topicB = new Topic { Name = topicBName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);
            topicB.Id = await _repository.AddAsync(topicB);

            // act
            IReadOnlyList<Topic> topics = await _repository.GetAllAsync();

            // assert
            Assert.Equal(new List<Topic>() { topicA, topicB }, topics);
        }
        #endregion

        #region GetByIdAsync tests
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(10)]
        public async Task GetByIdAsync_EmptyRepository_ReturnsNull(long topicId)
        {
            Topic? topic = await _repository.GetByIdAsync(topicId);
            Assert.Null(topic);
        }


        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(10)]
        public async Task GetByIdAsync_NonExistingId_ReturnsNull(long topicId)
        {
            // arrange
            string topicName = ".NET";
            Topic topicA = new Topic { Name = topicName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            // act + assert
            Assert.Null(await _repository.GetByIdAsync(topicId));
        }


        [Fact]
        public async Task GetByIdAsync_OneTopicAndValidId_ReturnsTopic()
        {
            // arrange
            string topicName = ".NET";
            Topic topic = new Topic { Name = topicName, CreatedAt = DateTime.UtcNow };
            topic.Id = await _repository.AddAsync(topic);

            // act
            Topic? topicFromRepository = await _repository.GetByIdAsync(1);

            // assert
            Assert.Equal(topic, topicFromRepository);
        }


        [Fact]
        public async Task GetByIdAsync_TwoTopicsAndValidId_ReturnsTopic()
        {
            // arrange
            string topicAName = ".NET";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            string topicBName = "C#";
            Topic topicB = new Topic { Name = topicBName, CreatedAt = DateTime.UtcNow };
            topicB.Id = await _repository.AddAsync(topicB);

            // act
            Topic? topicFromRepository = await _repository.GetByIdAsync(2);

            // assert
            Assert.Equal(1, topicA.Id);
            Assert.Equal(topicB, topicFromRepository);
        }
        #endregion

        #region GetByNameAsync tests
        [Theory]
        [InlineData("Memory Management")]
        [InlineData("Garbage Collection")]
        [InlineData("Constructors")]
        [InlineData("LOH vs. SOH")]
        public async Task GetByNameAsync_EmptyRepository_ReturnsNull(string topicName)
        {
            Topic? topic = await _repository.GetByNameAsync(topicName);
            Assert.Null(topic);
        }


        [Theory]
        [InlineData("Memory Management")]
        [InlineData("Garbage Collection")]
        [InlineData("Constructors")]
        [InlineData("LOH vs. SOH")]
        public async Task GetByNameAsync_NonExistingName_ReturnsNull(string topicName)
        {
            // arrange
            string newTopicName = ".NET";
            Topic topicA = new Topic { Name = newTopicName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            // act + assert
            Assert.Null(await _repository.GetByNameAsync(topicName));
        }


        [Fact]
        public async Task GetByNameAsync_OneTopicAndValidName_ReturnsTopic()
        {
            // arrange
            string topicName = ".NET";
            Topic topic = new Topic { Name = topicName, CreatedAt = DateTime.UtcNow };
            topic.Id = await _repository.AddAsync(topic);

            // act
            Topic? topicFromRepository = await _repository.GetByNameAsync(".NET");

            // assert
            Assert.Equal(topic, topicFromRepository);
        }


        [Fact]
        public async Task GetByNameAsync_TwoTopicsAndValidName_ReturnsTopic()
        {
            // arrange
            string topicAName = ".NET";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            string topicBName = "C#";
            Topic topicB = new Topic { Name = topicBName, CreatedAt = DateTime.UtcNow };
            topicB.Id = await _repository.AddAsync(topicB);

            // act
            Topic? topicFromRepository = await _repository.GetByNameAsync("C#");

            // assert
            Assert.Equal(1, topicA.Id);
            Assert.Equal(topicB, topicFromRepository);
        }
        #endregion

        #region AddAsync tests

        [Fact]
        public async Task AddAsync_ConflictingNamesSameEntity_ThrowsException()
        {
            // arrange
            string topicAName = ".NET";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            // act + assert
            await Assert.ThrowsAsync<DuplicateEntityException>(() => _repository.AddAsync(topicA));
        }


        [Fact]
        public async Task AddAsync_ConflictingNamesDifferentEntities_ThrowsException()
        {
            // arrange
            string topicAName = ".NET";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            string topicBName = ".NET";
            Topic topicB = new Topic { Name = topicBName, CreatedAt = DateTime.UtcNow };

            // act + assert
            await Assert.ThrowsAsync<DuplicateEntityException>(() => _repository.AddAsync(topicB));
        }
        #endregion

        #region MergeAsync tests

        // TODO: after SqliteFlashcardRepository is implemented (Merge reassigns Flashcards.TopicId)

        #endregion


        #region RenameAsync tests
        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public async Task RenameAsync_InvalidIdWithEmptyTopics_ThrowsException(long id)
        {
            await Assert.ThrowsAsync<EntityNotFoundException>(() => _repository.RenameAsync(id, ".NET"));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(12)]
        public async Task RenameAsync_InvalidIdNonEmptyTopics_ThrowsException(long id)
        {
            // arrange
            string topicAName = ".NET";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            // act + assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => _repository.RenameAsync(id, ".NET"));
        }


        [Fact]
        public async Task RenameAsync_ValidIdInvalidName_ThrowsException()
        {
            // arrange
            string topicAName = ".NET";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            string topicBName = "C#";
            Topic topicB = new Topic { Name = topicBName, CreatedAt = DateTime.UtcNow };
            topicB.Id = await _repository.AddAsync(topicB);

            // act + assert
            Assert.NotNull(topicB.Id);
            await Assert.ThrowsAsync<DuplicateEntityException>(() => _repository.RenameAsync((long)topicB.Id, ".NET"));
        }


        [Fact]
        public async Task RenameAsync_ValidIdValidName_SilentlyPasses()
        {
            // arrange
            string topicAName = "F#";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            string topicBName = "C#";
            Topic topicB = new Topic { Name = topicBName, CreatedAt = DateTime.UtcNow };
            topicB.Id = await _repository.AddAsync(topicB);

            // act
            await _repository.RenameAsync(1, ".NET");

            // assert
            Assert.NotNull(topicA.Id);
            string? actualName = (await _repository.GetByIdAsync((long)topicA.Id))?.Name;
            Assert.NotNull(actualName);
            Assert.Equal(".NET", actualName);
        }

        #endregion


        #region DeleteAsync tests
        // TODO: after SqliteFlashcardRepository is implemented (Delete causes chain deletion of relevand Flashcards)

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public async Task DeleteAsync_InvalidIdWithEmptyTopics_ThrowsException(long id)
        {
            await Assert.ThrowsAsync<EntityNotFoundException>(() => _repository.DeleteAsync(id));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(12)]
        public async Task DeleteAsync_InvalidIdNonEmptyTopics_ThrowsException(long id)
        {
            // arrange
            string topicAName = ".NET";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            // act + assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => _repository.DeleteAsync(id));
        }


        [Fact]
        public async Task DeleteAsync_ValidId_SilentlyPasses()
        {
            // arrange
            string topicAName = ".NET";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            // act
            Assert.NotNull(topicA.Id);
            await _repository.DeleteAsync((long)topicA.Id);

            // assert
            Assert.Empty(await _repository.GetAllAsync());
        }

        [Fact]
        public async Task DeleteAsync_ValidIdMultipleTopicsInRepo_SilentlyPasses()
        {
            // arrange
            string topicAName = ".NET";
            Topic topicA = new Topic { Name = topicAName, CreatedAt = DateTime.UtcNow };
            topicA.Id = await _repository.AddAsync(topicA);

            string topicBName = "C#";
            Topic topicB = new Topic { Name = topicBName, CreatedAt = DateTime.UtcNow };
            topicB.Id = await _repository.AddAsync(topicB);

            // act
            Assert.NotNull(topicB.Id);
            await _repository.DeleteAsync((long)topicB.Id);

            // assert
            Assert.Null(await _repository.GetByIdAsync((long)topicB.Id));
        }

        #endregion


        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_ReturnsAddedTopic()
        {
            // arrange
            string topicName = "Database Systems";
            Topic topic = new Topic { Name = topicName, CreatedAt = DateTime.UtcNow };

            // act
            long id = await _repository.AddAsync(topic);
            Topic? result = await _repository.GetByIdAsync(id);

            // assert
            Assert.NotNull(result);
            Assert.Equal(topicName, result!.Name);
        }
    }
}