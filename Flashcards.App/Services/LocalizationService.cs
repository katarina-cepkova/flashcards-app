using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Collections.ObjectModel;

namespace Flashcards.App.Services
{

    public class LocalizationService : ILocalizationService
    {
        public void SetLanguage(string cultureCode)
        {
            ResourceDictionary newDictionary = new ResourceDictionary
            {
                Source = new Uri($"Resources/Strings/Strings.{cultureCode}.xaml", UriKind.Relative)
            };

            // Application.Current -> returns current running app instance
            // .Resources -> root dictionary
            Collection<ResourceDictionary> mergedDictionary = Application.Current.Resources.MergedDictionaries;
            int indexToRemove = -1;
            for (int i = 0; i < mergedDictionary.Count; i++)
            {
                ResourceDictionary d = mergedDictionary[i];
                // OriginalString = exact path written in App.xaml
                if (d.Source != null && d.Source.OriginalString.Contains("Strings."))
                {
                    indexToRemove = i;
                    break;
                }
            }

            if (indexToRemove >= 0)
                mergedDictionary.RemoveAt(indexToRemove);

            mergedDictionary.Add(newDictionary);
        }
    }
}
