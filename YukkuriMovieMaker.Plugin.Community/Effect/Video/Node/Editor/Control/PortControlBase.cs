using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

public abstract class PortControlBase : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty BeginEditCommandProperty =
        DependencyProperty.Register(
            nameof(BeginEditCommand),
            typeof(ICommand),
            typeof(PortControlBase),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EndEditCommandProperty =
        DependencyProperty.Register(
            nameof(EndEditCommand),
            typeof(ICommand),
            typeof(PortControlBase),
            new PropertyMetadata(null));

    public ICommand? BeginEditCommand
    {
        get => (ICommand)GetValue(BeginEditCommandProperty);
        set => SetValue(BeginEditCommandProperty, value);
    }

    public ICommand? EndEditCommand
    {
        get => (ICommand)GetValue(EndEditCommandProperty);
        set => SetValue(EndEditCommandProperty, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}