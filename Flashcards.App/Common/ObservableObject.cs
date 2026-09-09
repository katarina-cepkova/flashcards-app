using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flashcards.App.Common
{
    /// <summary>
    /// Base class providing INotifyPropertyChanged support so derived classes
    /// don't have to implement it themselves.
    /// </summary>
    internal abstract class ObservableObject : INotifyPropertyChanged
    {
        /// <summary>
        /// Raised whenever a property on this object changes, so that
        /// WPF bindings know to re-read the new value.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises <see cref="PropertyChanged"/> for the given property.
        /// </summary>
        /// <param name="propertyName">
        /// Name of the changed property. Automatically filled in with the
        /// calling member's name if omitted, via <see cref="CallerMemberNameAttribute"/>.
        /// </param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        // [CallerMemberName] auto-fills propertyName with the name of whatever
        // property/method called this, so callers usually don't pass it explicitly
        {
            // if nobody has subscribed to PropertyChanged yet, do nothing (avoids a null reference crash)
            // otherwise, notify all subscribers: "I (this) changed the property named propertyName"
            PropertyChanged?.Invoke(sender: this, new PropertyChangedEventArgs(propertyName));
        }
    }
}