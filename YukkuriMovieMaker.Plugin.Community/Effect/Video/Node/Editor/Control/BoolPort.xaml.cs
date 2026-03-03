using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

public partial class BoolPort : INotifyPropertyChanged
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
        DataContext = this;
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Toggle()
    {
        Value = !Value;
        Brush = Value ? SystemColors.HighlightBrush : SystemColors.GrayTextBrush;
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (BoolPort)d;
        control.OnPropertyChanged(nameof(Value));
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}