using Flashcards.Core.Entities;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Flashcards.App.ViewModels
{
    internal partial class MainViewModel
    {
        #region Topic

        /// <summary>
        /// All topics currently known to the app, loaded once (on first Create/Open) and kept in
        /// sync locally on create/delete afterward — avoids a DB round-trip on every keystroke
        /// when checking for name collisions in IsTopicNameValid.
        /// </summary>
        public ObservableCollection<Topic> AvailableTopics { get; } = new();

        /// <summary>
        /// Topics with their flashcard counts, for the SelectingSet list — refreshed fresh from the
        /// repository each time SelectingSet is entered, since accuracy matters more here than for
        /// AvailableTopics's cached, keystroke-frequency use in create-set name-collision checks.
        /// </summary>
        public ObservableCollection<TopicListItem> TopicsForSelection { get; } = new();

        private bool _topicsLoaded;

        /// <summary>Loads AvailableTopics from the repository once; subsequent calls are a no-op.</summary>
        private async Task EnsureTopicsLoadedAsync()
        {
            if (_topicsLoaded) return;

            IReadOnlyList<Topic> topics = await _topicRepository.GetAllAsync();
            foreach (Topic topic in topics)
                AvailableTopics.Add(topic);
            _topicsLoaded = true;
        }

        /// <summary>
        /// Reloads TopicsForSelection and AvailableTopics from the repository — fresh each call, since
        /// this is used when entering SelectingSet (accuracy matters more here than avoiding a DB
        /// round-trip, unlike EnsureTopicsLoadedAsync's cache-once behavior for create-set's
        /// keystroke-frequency name-collision checks). Also marks AvailableTopics as loaded, so a
        /// subsequent EnsureTopicsLoadedAsync call (e.g. from EnterCreatingSetAsync) doesn't redundantly
        /// reload it.
        /// </summary>
        private async Task RefreshTopicsAsync()
        {
            IReadOnlyList<TopicListItem> results = await _topicRepository.GetAllWithCardCountsAsync();

            TopicsForSelection.Clear();
            AvailableTopics.Clear();

            foreach (var pair in results)
            {
                TopicsForSelection.Add(pair);
                AvailableTopics.Add(pair.Topic);
            }

            _topicsLoaded = true;
        }

        private Topic? _topic;

        /// <summary>The name of the currently open (or being created/renamed) flashcard set.</summary>
        public string TopicName
        {
            get => _topic?.Name ?? "";
            set
            {
                if (_topic is not null)
                {
                    _topic.Name = value;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RemainingCharactersText));
                    OnPropertyChanged(nameof(RemainingCharactersColor));
                    OnPropertyChanged(nameof(RemainingCharactersCount));
                    OnPropertyChanged(nameof(IsTopicNameValid));
                    OnPropertyChanged(nameof(TopicNameValidationMessage));
                }
            }
        }

        /// <summary>
        /// True when TopicName is non-empty and doesn't collide (case-insensitively, after
        /// trimming) with any other topic already loaded into AvailableTopics — checked entirely
        /// in memory, no DB round-trip per keystroke. The topic's own Id is excluded from the
        /// collision check, so renaming a topic back to its own current name (or leaving it
        /// unchanged) doesn't falsely flag as a collision. AvailableTopics entries are always
        /// already-trimmed (ConfirmTopicNameAsync trims before saving), so only TopicName itself
        /// needs trimming here.
        /// </summary>
        public bool IsTopicNameValid =>
            !string.IsNullOrWhiteSpace(TopicName) &&
            !AvailableTopics.Any(
                t => t.Id != _topic?.Id && 
                string.Equals(t.Name, TopicName.Trim(), StringComparison.OrdinalIgnoreCase)
            );

        /// <summary>
        /// Explains why TopicName is currently invalid — empty, or colliding with an existing topic
        /// — for display under TopicNameTextBox. Empty string when the name is valid (IsTopicNameValid).
        /// </summary>
        public string TopicNameValidationMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TopicName))
                    return (string)_resources["CreateSet_InvalidSetTopic_Message"];

                bool collides = AvailableTopics.Any(t => t.Id != _topic?.Id && string.Equals(t.Name, TopicName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (collides)
                    return ((string)_resources["CreateSet_AlreadyExistingTopic_Message"]).Replace("@", TopicName.Trim());

                return "";
            }
        }

        /// <summary>Maximum allowed length of a topic name.</summary>
        public int TopicMaxLength => 50;  // binding cannot have a static constant, use int instead

        /// <summary>How many more characters can still be typed into the topic name.</summary>
        public int RemainingCharactersCount => TopicMaxLength - TopicName.Length;

        /// <summary>Text representation of <see cref="RemainingCharactersCount"/>, for display.</summary>
        public string RemainingCharactersText => RemainingCharactersCount.ToString();

        /// <summary>Remaining-character threshold at or below which the label switches to the critical color.</summary>
        private int CriticalRemainingCharacterCount => 5;

        /// <summary>
        /// Remaining-character threshold at or below which the remaining-characters
        /// label should be shown to warn the user.
        /// </summary>
        public int RemainingCharactersWarningThreshold => 10;

        /// <summary>
        /// Text color for the remaining-characters label, escalating from the default
        /// color to a warning color and finally a critical color as the limit approaches.
        /// </summary>
        public Brush RemainingCharactersColor
        {
            get
            {
                if (RemainingCharactersCount <= CriticalRemainingCharacterCount)
                    return (Brush)_resources["CriticalBrush"];
                else if (RemainingCharactersCount <= RemainingCharactersWarningThreshold)
                    return (Brush)_resources["AlmostCriticalBrush"];
                return (Brush)_resources["TextOnDarkBrush"];
            }
        }

        #endregion

    }
}
