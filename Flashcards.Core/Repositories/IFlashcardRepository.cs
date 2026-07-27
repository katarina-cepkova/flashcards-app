using Flashcards.Core.Entities;
using Microsoft.Data.Sqlite;

namespace Flashcards.Core.Repositories
{
    /// <summary>
    /// Provides access to persisted <see cref="Flashcard"/> entities within a topic,
    /// and operations that create, update, or remove individual flashcards.
    /// </summary>
    public interface IFlashcardRepository
    {
        /// <summary>
        /// Retrieves all non-deleted flashcards belonging to a topic.
        /// </summary>
        /// <param name="topicId">The identifier of the topic whose flashcards are retrieved.</param>
        /// <returns>A read-only list of the topic's flashcards, in ascending order by flashcard ids.</returns>
        Task<IReadOnlyList<Flashcard>> GetByTopicIdAsync(long topicId);

        /// <summary>
        /// Retrieves a single flashcard by its identifier.
        /// </summary>
        /// <param name="id">The identifier of the flashcard to retrieve.</param>
        /// <returns>The matching <see cref="Flashcard"/>, or <c>null</c> if no flashcard with this id exists.</returns>
        Task<Flashcard?> GetByIdAsync(long id);

        /// <summary>
        /// Creates a new flashcard within a topic.
        /// </summary>
        /// <param name="transaction">
        /// The active transaction under which the insert is executed. The connection
        /// used is taken from <see cref="SqliteTransaction.Connection"/>; the caller
        /// (typically <see cref="SaveChangesAsync"/>) owns the transaction's lifetime.
        /// </param>
        /// <param name="flashcard">
        /// The flashcard to create. Its <see cref="Flashcard.Id"/> is ignored and assigned by the store;
        /// <see cref="Flashcard.TopicId"/> must reference an existing topic.
        /// </param>
        /// <returns>The identifier assigned to the newly created flashcard.</returns>
        Task<long> AddAsync(SqliteTransaction transaction, Flashcard flashcard);

        /// <summary>
        /// Persists changes to an existing flashcard's content, formatting, color,
        /// and review state.
        /// </summary>
        /// <param name="transaction">
        /// The active transaction under which the update is executed. The connection
        /// used is taken from <see cref="SqliteTransaction.Connection"/>; the caller
        /// (typically <see cref="SaveChangesAsync"/>) owns the transaction's lifetime.
        /// </param>
        /// <param name="flashcard">
        /// The flashcard with updated values. Its <see cref="Flashcard.Id"/> identifies
        /// which stored flashcard to update.
        /// </param>
        Task UpdateAsync(SqliteTransaction transaction, Flashcard flashcard);

        /// <summary>
        /// Marks a flashcard as deleted without removing its stored data.
        /// </summary>
        /// <param name="transaction">
        /// The active transaction under which the soft delete is executed. The connection
        /// used is taken from <see cref="SqliteTransaction.Connection"/>; the caller
        /// (typically <see cref="SaveChangesAsync"/>) owns the transaction's lifetime.
        /// </param>
        /// <param name="id">The identifier of the flashcard to delete.</param>
        /// <remarks>
        /// This is a soft delete: it sets <see cref="Flashcard.IsDeleted"/> rather than
        /// removing the row, so <see cref="GetByTopicAsync"/> excludes it afterwards
        /// while the data remains recoverable if needed.
        /// </remarks>
        Task DeleteAsync(SqliteTransaction transaction, long id);

        /// <summary>
        /// Persists a full set of in-progress changes to a topic's flashcards in a single
        /// atomic operation: new cards (no <see cref="Flashcard.Id"/>) are inserted,
        /// cards marked <see cref="Flashcard.IsDeleted"/> are soft-deleted, and the
        /// remaining cards are updated.
        /// </summary>
        /// <param name="flashcards">The full working set of flashcards from an edit session.</param>
        Task SaveChangesAsync(IReadOnlyList<Flashcard> flashcards);
    }
}