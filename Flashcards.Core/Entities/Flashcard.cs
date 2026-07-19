using System;
using System.Collections.Generic;
using System.Text;

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

        public int CorrectAnswersCount { get; set; }
        public int IncorrectAnswersCount { get; set; }

        // ARGB color value for the card background (.NET/WPF uses natively ARGB, not RGBA as web technologies)
        public int ColorArgb { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastReviewedAt { get; set; }
        public DateTime? NextReviewAt { get; set; } // time-permitting: scheduling

        public bool IsDeleted { get; set; }


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
    }
}
