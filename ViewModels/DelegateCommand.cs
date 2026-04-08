using System;
using System.Windows.Input;

namespace TabletMidiPad.ViewModels;

/// <summary>
/// Lightweight <see cref="ICommand"/> implementation backed by delegates.
/// </summary>
internal sealed class DelegateCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Initializes a command with execute and optional can-execute delegates.
    /// </summary>
    /// <param name="execute">The action to run.</param>
    /// <param name="canExecute">Optional predicate controlling command availability.</param>
    public DelegateCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute();

    /// <summary>
    /// Raises <see cref="CanExecuteChanged"/> for binding refresh.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
