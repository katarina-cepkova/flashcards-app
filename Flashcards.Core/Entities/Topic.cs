using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Core.Entities
{
    /// <summary>
    /// A named collection of flashcards (e.g. a subject or vocabulary set).
    /// </summary>
    public class Topic
    {
        public long Id { get; set; }
        public required string Name { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
