namespace Flashcards.Core.Entities
{
    /// <summary>
    /// A named collection of flashcards (e.g. a subject or vocabulary set).
    /// </summary>
    public sealed class Topic :IEquatable<Topic>
    {
        public long? Id { get; set; }
        public required string Name { get; set; }
        public DateTime CreatedAt { get; set; }


        /// <summary>Equality by <see cref="Id"/>; unsaved (null-id) topics are never equal.</summary>
        public bool Equals(Topic? other) => other is not null && other.Id is not null && other.Id == Id;


        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as Topic);


        /// <summary>
        /// Hash based on <see cref="Id"/>; constant for unsaved topics (safe, since they're never equal anyway).
        /// </summary>
        public override int GetHashCode() => Id?.GetHashCode() ?? 0;


        /// <summary>All-fields debug representation, not for end-user display.</summary>
        public override string ToString() => $"Topic {{ Id={Id}, Name=\"{Name}\", CreatedAt={CreatedAt:O} }}";

    }
}
