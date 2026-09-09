using System;
using System.Collections.Generic;
using System.Text;

namespace Flashcards.App.Services
{
    /// <summary>
    /// Switches the application's active language by swapping the merged
    /// string resource dictionary at runtime.
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>
        /// Replaces the currently active language dictionary with the one
        /// matching the given culture code.
        /// </summary>
        /// <param name="cultureCode">Culture code matching a Strings.{cultureCode}.xaml file, e.g. "en-GB".</param>
        void SetLanguage(string cultureCode);
    }
}
