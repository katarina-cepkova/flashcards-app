namespace Flashcards.Core.Repositories
{
    /// <summary>
    /// Thrown when a repository operation references an entity that does not exist.
    /// </summary>
    public class EntityNotFoundException : Exception
    {
        public EntityNotFoundException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when a repository write references an entity that already exists in the repository.
    /// </summary>
    public class DuplicateEntityException : Exception
    {
        public DuplicateEntityException(string message) : base(message) { }
    }
}
