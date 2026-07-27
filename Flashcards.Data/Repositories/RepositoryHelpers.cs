using Flashcards.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Data.Repositories
{
    internal static class RepositoryHelpers
    {
        /// <summary>
        /// Throws <see cref="EntityNotFoundException"/> if no row was affected by the last command.
        /// </summary>
        public static void EnsureRowsAffected(int rowsAffected, long id)
        {
            if (rowsAffected == 0)
            {
                throw new EntityNotFoundException($"Topic {id} does not exist.");
            }
        }
    }
}
