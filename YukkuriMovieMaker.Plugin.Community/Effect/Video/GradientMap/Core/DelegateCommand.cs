using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;

internal sealed class DelegateCommand(
    Action<object?> execute,
    Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    internal void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            execute(parameter);
    }
}
