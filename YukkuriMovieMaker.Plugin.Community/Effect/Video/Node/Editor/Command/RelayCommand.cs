using System.Diagnostics;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;

public class RelayCommand : ICommand
{
    private readonly Func<bool>? _canExecute;
    private readonly Action _execute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        if (_canExecute == null) return true;
        try
        {
            return _canExecute();
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[RelayCommand] CanExecute threw: {ex}");
            return false;
        }
    }

    public void Execute(object? parameter)
    {
        try
        {
            _execute();
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[RelayCommand] Execute threw: {ex}");
        }
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Func<T?, bool>? _canExecute;
    private readonly Action<T?> _execute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        if (_canExecute == null) return true;
        try
        {
            return _canExecute((T?)parameter);
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[RelayCommand<{typeof(T).Name}>] CanExecute threw: {ex}");
            return false;
        }
    }

    public void Execute(object? parameter)
    {
        try
        {
            _execute((T?)parameter);
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[RelayCommand<{typeof(T).Name}>] Execute threw: {ex}");
        }
    }
}