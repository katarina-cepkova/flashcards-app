using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.Core.Learning
{
    public interface IRequeuePolicy
    {
        public bool ShouldRemove(LearningSessionCard card);

        public uint StepsAhead(LearningSessionCard card);
    }
}
