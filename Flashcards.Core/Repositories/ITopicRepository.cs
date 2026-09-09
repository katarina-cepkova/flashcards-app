using Flashcards.Core.Entities;

namespace Flashcards.Core.Repositories
{
    /// <summary>
    /// Provides access to persisted <see cref="Topic"/> entities and operations
    /// that manage topics as a whole (creation, renaming, merging, deletion).
    /// </summary>
    public interface ITopicRepository
    {
        /// <summary>
        /// Retrieves all topics currently stored.
        /// </summary>
        /// <returns>
        /// A read-only list of all topics, in ascending order by topic ids.
        /// </returns>
        Task<IReadOnlyList<Topic>> GetAllAsync();

        /// <summary>
        /// Retrieves a single topic by its identifier.
        /// </summary>
        /// <param name="id">The identifier of the topic to retrieve.</param>
        /// <returns>The matching <see cref="Topic"/>, or <c>null</c> if no topic with this id exists.</returns>
        Task<Topic?> GetByIdAsync(long id);

        /// <summary>
        /// Retrieves a topic by its exact name. Used to detect name collisions
        /// before renaming or creating a topic.
        /// </summary>
        /// <param name="name">The topic name to search for.</param>
        /// <returns>The matching <see cref="Topic"/>, or <c>null</c> if no topic with this name exists.</returns>
        Task<Topic?> GetByNameAsync(string name);

        /// <summary>
        /// Creates a new topic.
        /// </summary>
        /// <param name="topic">The topic to create. Its <see cref="Topic.Id"/> is ignored and assigned by the store.</param>
        /// <returns>The identifier assigned to the newly created topic.</returns>
        Task<long> AddAsync(Topic topic);

        /// <summary>
        /// Moves all flashcards from <paramref name="sourceTopicId"/> into
        /// <paramref name="targetTopicId"/>, then deletes the now-empty source topic.
        /// This operation is irreversible.
        /// </summary>
        /// <param name="sourceTopicId">The topic whose flashcards are moved and which is deleted afterwards.</param>
        /// <param name="targetTopicId">The topic that receives the flashcards and continues to exist.</param>
        Task MergeAsync(long sourceTopicId, long targetTopicId);

        /// <summary>
        /// Renames an existing topic.
        /// </summary>
        /// <param name="id">The identifier of the topic to rename.</param>
        /// <param name="newName">The new name for the topic.</param>
        /// <remarks>
        /// Callers are expected to check <see cref="GetByNameAsync"/> beforehand if
        /// they need to detect and handle a name collision (e.g. by offering a merge)
        /// rather than relying on this method to do so.
        /// </remarks>
        Task RenameAsync(long id, string newName);

        /// <summary>
        /// Deletes a topic and its flashcards.
        /// </summary>
        /// <param name="id">The identifier of the topic to delete.</param>
        Task DeleteAsync(long id);
    }
}