using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Views;

public partial class GrdIndexSelector : UserControl, IPropertyEditorControl
{
    public static readonly DependencyProperty FilePathProperty =
        DependencyProperty.Register(
            nameof(FilePath),
            typeof(string),
            typeof(GrdIndexSelector),
            new FrameworkPropertyMetadata(string.Empty, OnFilePathChanged));

    public static readonly DependencyProperty GradientIndexProperty =
        DependencyProperty.Register(
            nameof(GradientIndex),
            typeof(int),
            typeof(GrdIndexSelector),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnGradientIndexChanged));

    private GrdIndexSelectorViewModel? _viewModel;
    private GrdEffectPropertyBridge? _bridge;

    public GrdIndexSelector()
    {
        InitializeComponent();
    }

    public string FilePath
    {
        get => (string)GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    public int GradientIndex
    {
        get => (int)GetValue(GradientIndexProperty);
        set => SetValue(GradientIndexProperty, value);
    }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public void Initialize()
    {
        _viewModel = new GrdIndexSelectorViewModel();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;

        if (!string.IsNullOrWhiteSpace(FilePath))
            _viewModel.FilePath = FilePath;

        if (GradientIndex != 0)
            _viewModel.GradientIndex = GradientIndex;
    }

    internal void AttachBridge(GrdEffectPropertyBridge? bridge)
    {
        _bridge?.Dispose();
        _bridge = bridge;
    }

    internal void DetachBridge()
    {
        _bridge?.Dispose();
        _bridge = null;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel is null) return;

        if (e.PropertyName == nameof(GrdIndexSelectorViewModel.GradientIndex))
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            GradientIndex = _viewModel.GradientIndex;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }

    private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GrdIndexSelector selector || selector._viewModel is null) return;
        var path = e.NewValue as string ?? string.Empty;
        if (!string.Equals(selector._viewModel.FilePath, path, StringComparison.OrdinalIgnoreCase))
            selector._viewModel.FilePath = path;
    }

    private static void OnGradientIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GrdIndexSelector selector || selector._viewModel is null) return;
        var index = e.NewValue is int i ? i : 0;
        if (selector._viewModel.GradientIndex != index)
            selector._viewModel.GradientIndex = index;
    }
}
