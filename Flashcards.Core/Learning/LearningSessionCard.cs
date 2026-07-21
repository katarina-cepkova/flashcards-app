using Flashcards.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Core.Learning;

/// <summary>
/// Wraps a <see cref="Flashcard"/> with state that exists only for the
/// duration of a single learning session and is never persisted.
/// </summary>
public class LearningSessionCard
{
    public required Flashcard Flashcard { get; init; }
    public int SessionMissCount { get; set; }
    public int SessionCorrectCount { get; set; }
}
