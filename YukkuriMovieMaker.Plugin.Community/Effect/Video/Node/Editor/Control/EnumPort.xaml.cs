using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

    public static readonly DependencyProperty DefaultProperty =
        DependencyProperty.Register(
            nameof(Default),
            typeof(int),
            typeof(EnumPort),
            new PropertyMetadata(0));

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(
            nameof(Items),
            typeof(Type),
            typeof(EnumPort));

    public static readonly DependencyProperty IsEditableProperty =
        DependencyProperty.Register(
            nameof(IsEditable),
            typeof(bool),
            typeof(EnumPort),
            new PropertyMetadata(false));

    private bool _isUserInteraction;

    public EnumPort()
    {
        InitializeComponent();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Default
    {
        get => (int)GetValue(DefaultProperty);
        init
        {
            SetValue(DefaultProperty, value);
            SetValue(ValueProperty, value);
        }
    }

    public Type Items
    {
        get => (Type)GetValue(ItemsProperty);
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
        control.OnPropertyChanged(nameof(Value));
    }

    private void ComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isUserInteraction = true;
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUserInteraction) return;
        _isUserInteraction = false;

        BeginEditCommand?.Execute(null);
        EndEditCommand?.Execute(null);
    }
}