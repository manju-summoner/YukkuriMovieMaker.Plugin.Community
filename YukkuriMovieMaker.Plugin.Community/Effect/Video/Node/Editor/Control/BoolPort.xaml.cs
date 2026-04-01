using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

public partial class BoolPort
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(bool),
            typeof(BoolPort),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public BoolPort()
    {
        InitializeComponent();
        ToggleCommand = new RelayCommand(Toggle);
    }

    public ICommand ToggleCommand { get; }

    public bool Value
    {
        get => (bool)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public System.Windows.Media.Brush Brush
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    } = SystemColors.GrayTextBrush;

    private void Toggle()
    {
        BeginEditCommand?.Execute(null);
        Value = !Value;
        EndEditCommand?.Execute(null);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (BoolPort)d;
        control.Brush = (bool)e.NewValue ? SystemColors.HighlightBrush : SystemColors.GrayTextBrush;
    }
}