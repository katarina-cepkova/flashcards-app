using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Core.Entities
{
    /// <summary>
    /// A single flashcard belonging to a <see cref="Topic"/>, with independently
    /// formattable front and back content.
    /// </summary>
    public class Flashcard
    {
        public long Id { get; set; }
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
    }
}
