using System.Windows.Input;

namespace Flashcards.App.Commands
{
    /// <summary>
    /// Generic ICommand implementation that wraps a delegate, so individual commands 
    /// don't need their own ICommand class.
    /// </summary>
    /// <typeparam name="T">The type of the parameter passed to the wrapped delegate.</typeparam>
    internal class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;  // reference to the method that should be executed
        private readonly Func<T?, bool>? _canExecute;  // determines if executing should happen


        /// <summary>
        /// Creates a command that wraps the given delegates.
        /// </summary>
        /// <param name="execute">The action to run when the command is executed.</param>
        /// <param name="canExecute">
        /// Optional check deciding whether the command can currently execute. If omitted,
        /// the command is always considered executable.
        /// </param>
        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }


        /// <summary>
        /// Raised when the answer to <see cref="CanExecute"/> may have changed. Forwarded to
        /// WPF's <see cref="CommandManager.RequerySuggested"/> instead of a private backing
        /// event, so callers are notified automatically on common UI events without this
        /// class having to raise anything manually.
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            // when sb tries to register to my CanExecuteChanged, register it to CommandManager's event instead
            add => CommandManager.RequerySuggested += value;
            // when sb tries to unregister from my CanExecuteChanged, unregister it from CommandManager's event instead
            remove => CommandManager.RequerySuggested -= value;    
        }


        /// <summary>
        /// Determines whether the command can currently execute, by invoking the
        /// <c>canExecute</c> delegate supplied at construction with the given parameter.
        /// </summary>
        /// <param name="parameter">The parameter to pass to the <c>canExecute</c> delegate, cast to <typeparamref name="T"/>.</param>
        /// <returns><c>true</c> if no <c>canExecute</c> delegate was supplied, or if it returns <c>true</c>.</returns>

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;


        /// <summary>
        /// Runs the wrapped <c>execute</c> delegate with the given parameter.
        /// </summary>
        /// <param name="parameter">The parameter to pass to the <c>execute</c> delegate, cast to <typeparamref name="T"/>.</param>

        public void Execute(object? parameter) => _execute((T?)parameter);
    }

    /// <summary>
    /// Non-generic convenience wrapper over <see cref="RelayCommand{T}"/> for commands
    /// that don't need a parameter.
    /// </summary>
    internal class RelayCommand : RelayCommand<object>
    {
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : base(_ => execute(), canExecute is null ? null : _ => canExecute())
        {
        }
    }
}
