using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Core.Entities
{
    public class TopicListItem
    {
        public required Topic Topic { get; init; }
        public required int CardCount { get; init; }
    }
}
