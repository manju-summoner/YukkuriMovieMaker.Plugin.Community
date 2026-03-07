using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

public partial class EnumPort
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(int),
            typeof(EnumPort),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(
            nameof(Items),
            typeof(List<string>),
            typeof(EnumPort),
            new PropertyMetadata(new List<string>()));

    public static readonly DependencyProperty IsEditableProperty =
        DependencyProperty.Register(
            nameof(IsEditable),
            typeof(bool),
            typeof(EnumPort),
            new PropertyMetadata(false));

    public EnumPort()
    {
        InitializeComponent();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public List<string> Items
    {
        get => (List<string>)GetValue(ItemsProperty);
        init => SetValue(ItemsProperty, value);
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        init => SetValue(IsEditableProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EnumPort)d;
        control.BeginEditCommand?.Execute(null);
        control.OnPropertyChanged(nameof(Value));
        control.EndEditCommand?.Execute(null);
    }
}