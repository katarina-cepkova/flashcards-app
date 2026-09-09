using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.App.Models
{
    public enum AppState
    {
        ClosedSet,
        CreatingSet,
        SelectingSet,
        OpenedSetView,
        OpenedSetEdit,
        LearningSession

    }
}
