using System.Windows;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

public partial class TextPort
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(TextPort),
            new FrameworkPropertyMetadata(
                "",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty DefaultProperty =
        DependencyProperty.Register(
            nameof(Default),
            typeof(string),
            typeof(TextPort),
            new PropertyMetadata("", OnDefaultChanged));

    public TextPort()
    {
        InitializeComponent();
    }

    public string Default
    {
        get => (string)GetValue(DefaultProperty);
        init => SetValue(DefaultProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TextPort)d;
        control.OnPropertyChanged(nameof(Value));
    }

    private static void OnDefaultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TextPort)d;
        if (string.IsNullOrEmpty(control.Value))
            control.Value = (string)e.NewValue;
    }

    internal void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        BeginEditCommand?.Execute(null);
        EndEditCommand?.Execute(null);
    }

    internal void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            BeginEditCommand?.Execute(null);
            EndEditCommand?.Execute(null);
        }
    }
}