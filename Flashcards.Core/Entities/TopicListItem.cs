namespace Flashcards.Core.Entities
{
    /// <summary>
    /// A <see cref="Topic"/> paired with how many flashcards it has — used for the
    /// topic-selection list, which needs both without a separate round-trip per topic.
    /// </summary>
    public class TopicListItem
    {
        public required Topic Topic { get; init; }
        public required int CardCount { get; init; }
    }
}