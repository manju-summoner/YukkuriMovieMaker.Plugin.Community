using System.Windows;

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
        control.BeginEditCommand?.Execute(null);
        control.OnPropertyChanged(nameof(Value));
        control.EndEditCommand?.Execute(null);
    }

    private static void OnDefaultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TextPort)d;
        if (string.IsNullOrEmpty(control.Value))
            control.Value = (string)e.NewValue;
    }
}