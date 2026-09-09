using System.Windows.Input;

namespace Flashcards.App.Commands
{
    /// <summary>
    /// ICommand implementation that wraps an async delegate (e.g. a DB call), so commands
    /// backed by Task-returning methods don't need to be run as fire-and-forget async void
    /// directly in Execute. IsExecuting guards against re-entrancy — e.g. a rapid double-click
    /// firing the command again while the first call is still awaiting — by reporting
    /// CanExecute as false until the running call completes.
    /// </summary>
    /// <typeparam name="T">The type of the parameter passed to the wrapped delegate.</typeparam>
    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T?, Task> _execute;  // reference to the async method that should be executed
        private readonly Func<T?, bool>? _canExecute;  // determines if executing should happen
        private bool _isExecuting;  // true while a previous Execute call is still running


        /// <summary>
        /// Creates a command that wraps the given delegates.
        /// </summary>
        /// <param name="execute">The async action to run when the command is executed.</param>
        /// <param name="canExecute">
        /// Optional check deciding whether the command can currently execute. If omitted,
        /// the command is always considered executable (aside from the IsExecuting guard).
        /// </param>
        public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
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
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }


        /// <summary>
        /// Determines whether the command can currently execute — false while a previous call
        /// is still running, otherwise by invoking the <c>canExecute</c> delegate supplied at
        /// construction with the given parameter.
        /// </summary>
        /// <param name="parameter">The parameter to pass to the <c>canExecute</c> delegate, cast to <typeparamref name="T"/>.</param>
        /// <returns><c>false</c> while executing; otherwise <c>true</c> if no <c>canExecute</c> delegate was supplied, or if it returns <c>true</c>.</returns>
        public bool CanExecute(object? parameter) =>
            !_isExecuting && (_canExecute?.Invoke((T?)parameter) ?? true);


        /// <summary>
        /// Runs the wrapped async <c>execute</c> delegate with the given parameter, setting
        /// IsExecuting around the call so CanExecute reflects the in-progress state.
        /// ICommand.Execute is void, not Task, so this is unavoidably async void — exceptions
        /// from <c>execute</c> will propagate as unhandled unless it catches its own.
        /// </summary>
        /// <param name="parameter">The parameter to pass to the <c>execute</c> delegate, cast to <typeparamref name="T"/>.</param>
        public async void Execute(object? parameter)
        {
            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _execute((T?)parameter);
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// Non-generic convenience wrapper over <see cref="AsyncRelayCommand{T}"/> for commands
    /// that don't need a parameter.
    /// </summary>
    public class AsyncRelayCommand : AsyncRelayCommand<object>
    {
        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
            : base(_ => execute(), canExecute is null ? null : _ => canExecute())
        {
        }
    }
}