using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ZEarth.App.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Synchronous command. Retains the WPF <see cref="CommandManager.RequerySuggested"/> wiring so that
    /// automatic enabling/disabling continues to work, while also allowing targeted
    /// <see cref="RaiseCanExecuteChanged"/> notifications from hot paths (RC-5).
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;
        private event EventHandler? _canExecuteChanged;

        public event EventHandler? CanExecuteChanged
        {
            add
            {
                _canExecuteChanged += value;
                CommandManager.RequerySuggested += value;
            }
            remove
            {
                _canExecuteChanged -= value;
                CommandManager.RequerySuggested -= value;
            }
        }

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);

        /// <summary>
        /// Targeted invalidation that notifies only this command's listeners. Unlike
        /// <c>CommandManager.InvalidateRequerySuggested</c> it does not trigger a global requery of
        /// every command in the application (RC-5).
        /// </summary>
        public void RaiseCanExecuteChanged() => _canExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Asynchronous command that prevents uncontrolled re-entry, reports exceptions explicitly and
    /// surfaces an <see cref="IsExecuting"/> flag so the view model can publish a non-blocking busy
    /// indicator (RC-3).
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Predicate<object?>? _canExecute;
        private readonly Action<Exception>? _onError;
        private readonly bool _allowConcurrent;
        private int _isExecuting;
        private event EventHandler? _canExecuteChanged;

        public AsyncRelayCommand(
            Func<Task> execute,
            Func<bool>? canExecute = null,
            Action<Exception>? onError = null,
            bool allowConcurrent = false)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null, onError, allowConcurrent)
        {
        }

        public AsyncRelayCommand(
            Func<object?, Task> execute,
            Predicate<object?>? canExecute = null,
            Action<Exception>? onError = null,
            bool allowConcurrent = false)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onError = onError;
            _allowConcurrent = allowConcurrent;
        }

        public event EventHandler? CanExecuteChanged
        {
            add
            {
                _canExecuteChanged += value;
                CommandManager.RequerySuggested += value;
            }
            remove
            {
                _canExecuteChanged -= value;
                CommandManager.RequerySuggested -= value;
            }
        }

        /// <summary>Raised when the executing state changes (used to drive the busy indicator).</summary>
        public event EventHandler? IsExecutingChanged;

        public bool IsExecuting => Volatile.Read(ref _isExecuting) == 1;

        public bool CanExecute(object? parameter)
        {
            if (!_allowConcurrent && IsExecuting) return false;
            return _canExecute == null || _canExecute(parameter);
        }

        public async void Execute(object? parameter)
        {
            // Re-entrancy guard: a second invocation while the first is still running is ignored.
            if (!_allowConcurrent && Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
            {
                return;
            }

            RaiseCanExecuteChanged();
            IsExecutingChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                await _execute(parameter).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // Never let an async command fault go unobserved; report it explicitly.
                _onError?.Invoke(ex);
            }
            finally
            {
                Interlocked.Exchange(ref _isExecuting, 0);
                IsExecutingChanged?.Invoke(this, EventArgs.Empty);
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() => _canExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
