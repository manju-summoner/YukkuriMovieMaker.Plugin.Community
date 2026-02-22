using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public partial class GraphView
{
    private ConnectionAdorner? _connectionAdorner;

    public GraphView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var layer = AdornerLayer.GetAdornerLayer(RootGrid);
        if (layer != null)
        {
            _connectionAdorner = new ConnectionAdorner(RootGrid);
            layer.Add(_connectionAdorner);
            _connectionAdorner.DataContext = DataContext;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is GraphViewModel vm)
        {
            foreach (var node in vm.Nodes) node.PropertyChanged += Node_PropertyChanged;

            vm.Nodes.CollectionChanged += (_, args) =>
            {
                if (args.NewItems != null)
                    foreach (NodeViewModel n in args.NewItems)
                        n.PropertyChanged += Node_PropertyChanged;
            };
        }
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.X) ||
            e.PropertyName == nameof(NodeViewModel.Y))
            _connectionAdorner?.InvalidateVisual();
    }
}