namespace Flashcards.Core.Entities
{
    /// <summary>
    /// A single flashcard belonging to a <see cref="Topic"/>, with independently
    /// formattable front and back content.
    /// </summary>
    public sealed class Flashcard :IEquatable<Flashcard>
    {
        public long? Id { get; set; }
        public required long TopicId { get; set; }

        public string FrontText { get; set; } = "";
        public string BackText { get; set; } = "";

        // time-permitting extension
        public string? FrontSoundPath { get; set; }
        public string? BackSoundPath { get; set; }

        public int CorrectAnswersCount { get; set; } = 0;
        public int IncorrectAnswersCount { get; set; } = 0;

        // ARGB color value for the card background (.NET/WPF uses natively ARGB, not RGBA as web technologies)
        public int ColorArgb { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastReviewedAt { get; set; }
        public DateTime? NextReviewAt { get; set; } // time-permitting: scheduling

        public bool IsDeleted { get; set; } = false;


        /// <summary>Equality by <see cref="Id"/>; unsaved (null-id) flashcards are never equal.</summary>
        public bool Equals(Flashcard? other) => other is not null && Id is not null && other.Id == Id;


        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as Flashcard);


        /// <summary>Hash based on <see cref="Id"/>; constant for unsaved flashcards (safe, since they're never equal anyway).</summary>
        public override int GetHashCode() => Id?.GetHashCode() ?? 0;


        /// <summary>All-fields debug representation, not for end-user display.</summary>
        public override string ToString() =>
            $"Flashcard {{ Id={Id}, TopicId={TopicId}, " +
            $"FrontText=\"{FrontText}\", BackText=\"{BackText}\", " +
            $"FrontSoundPath={FrontSoundPath ?? "null"}, BackSoundPath={BackSoundPath ?? "null"}, " +
            $"CorrectAnswersCount={CorrectAnswersCount}, IncorrectAnswersCount={IncorrectAnswersCount}, " +
            $"ColorArgb={ColorArgb:X8}, CreatedAt={CreatedAt:O}, " +
            $"LastReviewedAt={LastReviewedAt?.ToString("O") ?? "null"}, " +
            $"NextReviewAt={NextReviewAt?.ToString("O") ?? "null"}, " +
            $"IsDeleted={IsDeleted} }}";

        /// <summary>
        /// Creates a new, not-yet-saved flashcard for <paramref name="topicId"/> with blank front/back
        /// text and the given background color, and every other field at its default value.
        /// </summary>
        /// <param name="topicId">The topic the new flashcard will belong to.</param>
        /// <param name="colorArgb">The card's initial background color, in ARGB format.</param>
        public static Flashcard CreateDefault(long topicId, int colorArgb)
        {
            return new Flashcard()
            {
                TopicId = topicId,
                ColorArgb = colorArgb,
            };
        }
    }
}
