using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Views;

public partial class CustomFileSelector : UserControl, IPropertyEditorControl
{
    public static readonly DependencyProperty FilePathProperty =
        DependencyProperty.Register(
            nameof(FilePath),
            typeof(string),
            typeof(CustomFileSelector),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnFilePathChanged));

    public static readonly DependencyProperty ExtensionsProperty =
        DependencyProperty.Register(
            nameof(Extensions),
            typeof(string),
            typeof(CustomFileSelector),
            new PropertyMetadata(".grd,.png,.jpg,.bmp,.jpeg,.gif,.tiff"));

    public static readonly DependencyProperty FilterProperty =
        DependencyProperty.Register(
            nameof(Filter),
            typeof(string),
            typeof(CustomFileSelector),
            new PropertyMetadata("All Files|*.*"));

    private FileSelectorViewModel? _viewModel;

    public CustomFileSelector()
    {
        InitializeComponent();
    }

    public string FilePath
    {
        get => (string)GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    public string Extensions
    {
        get => (string)GetValue(ExtensionsProperty);
        set => SetValue(ExtensionsProperty, value);
    }

    public string Filter
    {
        get => (string)GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public void Initialize(string extensions, string filter, string? specialTooltip)
    {
        Extensions = extensions;
        Filter = filter;
        Tag = specialTooltip ?? string.Empty;

        _viewModel = new FileSelectorViewModel(extensions, filter);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;

        if (!string.IsNullOrWhiteSpace(FilePath))
            _viewModel.FilePath = FilePath;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileSelectorViewModel.FilePath)) return;
        if (_viewModel is null) return;

        BeginEdit?.Invoke(this, EventArgs.Empty);
        FilePath = _viewModel.FilePath;
        EndEdit?.Invoke(this, EventArgs.Empty);
    }

    private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CustomFileSelector selector) return;
        if (selector._viewModel is null) return;

        var newPath = e.NewValue as string ?? string.Empty;
        if (!string.Equals(selector._viewModel.FilePath, newPath, StringComparison.OrdinalIgnoreCase))
            selector._viewModel.FilePath = newPath;
    }
}
