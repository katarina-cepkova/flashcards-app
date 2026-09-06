using Flashcards.Core.Repositories;

namespace Flashcards.Data.Repositories
{
    /// <summary>
    /// Small guard helpers shared by the SQLite repository implementations.
    /// </summary>
    internal static class RepositoryHelpers
    {
        /// <summary>
        /// Throws <see cref="EntityNotFoundException"/> if no row was affected by the last command.
        /// </summary>
        public static void EnsureRowsAffected(int rowsAffected, long id, string entityName)
        {
            if (rowsAffected == 0)
            {
                throw new EntityNotFoundException($"{entityName} {id} does not exist.");
            }
        }
    }
}