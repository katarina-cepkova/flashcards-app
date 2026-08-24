using Markdig;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.App.Services
{
    internal static class MarkdownPipelineProvider
    {
        // UseEmphasisExtras also enables strikethrough (~~) parsing on top of basic bold/italic.
        // This same pipeline should be used in MarkdownToFlowDocumentConverter as well — otherwise
        // the toggle buttons could disagree with the preview about what's actually formatted.
        public static readonly MarkdownPipeline Pipeline =
            new MarkdownPipelineBuilder().UseEmphasisExtras().Build();
    }
}
